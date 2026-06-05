using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class PendingEventRepository : IPendingEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public PendingEventRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(pendingEvent => pendingEvent.PendingEventId == pendingEventId, ct);
    }

    public async Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(pendingEvent => pendingEvent.GcalEventId == gcalEventId, ct);
    }

    public async Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveUtc = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .Where(pendingEvent =>
                pendingEvent.IsAllDay == true &&
                pendingEvent.SourceSystem == "day_name" &&
                pendingEvent.StartDatetime.HasValue &&
                pendingEvent.StartDatetime.Value >= startUtc &&
                pendingEvent.StartDatetime.Value < endExclusiveUtc)
            .OrderByDescending(pendingEvent => pendingEvent.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = localStart.AddDays(1);
        var utcStart = localStart.ToUniversalTime();
        var utcEndExclusive = localEndExclusive.ToUniversalTime();

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .Where(pendingEvent =>
                pendingEvent.Summary != null &&
                pendingEvent.Summary.StartsWith("Sleep") &&
                pendingEvent.StartDatetime.HasValue &&
                pendingEvent.StartDatetime.Value >= utcStart &&
                pendingEvent.StartDatetime.Value < utcEndExclusive)
            .OrderByDescending(pendingEvent => pendingEvent.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pendingEvent);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        PendingEvent? existing = null;
        if (!string.IsNullOrWhiteSpace(pendingEvent.PendingEventId))
        {
            existing = await context.PendingEvents
                .SingleOrDefaultAsync(
                    storedPendingEvent => storedPendingEvent.PendingEventId == pendingEvent.PendingEventId,
                    ct);
        }

        if (existing is null && !string.IsNullOrWhiteSpace(pendingEvent.GcalEventId))
        {
            existing = await context.PendingEvents
                .SingleOrDefaultAsync(
                    storedPendingEvent => storedPendingEvent.GcalEventId == pendingEvent.GcalEventId,
                    ct);
        }

        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(pendingEvent.PendingEventId))
            {
                pendingEvent.PendingEventId = CreatePendingEventId();
            }

            context.PendingEvents.Add(pendingEvent);
        }
        else
        {
            existing.GcalEventId = pendingEvent.GcalEventId;
            existing.CalendarId = pendingEvent.CalendarId;
            existing.Summary = pendingEvent.Summary;
            existing.Description = pendingEvent.Description;
            existing.StartDatetime = pendingEvent.StartDatetime;
            existing.EndDatetime = pendingEvent.EndDatetime;
            existing.IsAllDay = pendingEvent.IsAllDay;
            existing.ColorId = pendingEvent.ColorId;
            existing.AppCreated = pendingEvent.AppCreated;
            existing.SourceSystem = pendingEvent.SourceSystem;
            existing.ReadyToPublish = pendingEvent.ReadyToPublish;
            existing.PublishAttemptedAt = pendingEvent.PublishAttemptedAt;
            existing.PublishError = pendingEvent.PublishError;
            existing.OperationType = pendingEvent.OperationType;
            existing.CreatedAt = pendingEvent.CreatedAt;
            existing.UpdatedAt = pendingEvent.UpdatedAt;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.PendingEvents
            .SingleOrDefaultAsync(
                storedPendingEvent => storedPendingEvent.PendingEventId == pendingEventId,
                ct);

        if (existing is null)
        {
            return;
        }

        context.PendingEvents.Remove(existing);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.PendingEvents
            .SingleOrDefaultAsync(
                storedPendingEvent => storedPendingEvent.GcalEventId == gcalEventId,
                ct);

        if (existing is null)
        {
            return;
        }

        context.PendingEvents.Remove(existing);
        await context.SaveChangesAsync(ct);
    }

    private static string CreatePendingEventId()
    {
        return $"pending_{Guid.NewGuid():N}";
    }
}

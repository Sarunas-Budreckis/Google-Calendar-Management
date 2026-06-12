using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class EventRepository : IEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public EventRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Event?> GetByEventIdAsync(string eventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId, ct);
    }

    public async Task<Event?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.GcalEventId == gcalEventId, ct);
    }

    public async Task<IList<Event>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rangeStartUtc = ToLocalDayBoundaryUtc(from);
        var rangeEndExclusiveUtc = ToLocalDayBoundaryUtc(to.AddDays(1));

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.Events
            .AsNoTracking()
            .Where(e => (!e.IsDeleted || e.HasUnpublishedChanges) &&
                        e.StartDatetime.HasValue &&
                        e.StartDatetime < rangeEndExclusiveUtc &&
                        (e.EndDatetime ?? e.StartDatetime.Value) >= rangeStartUtc)
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.Summary)
            .ToListAsync(ct);
    }

    public async Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        var earliestEvent = await context.Events
            .AsNoTracking()
            .Where(e => e.Lifecycle != "candidate" && !e.IsDeleted && e.StartDatetime.HasValue)
            .OrderBy(e => e.StartDatetime)
            .Select(e => new { Start = e.StartDatetime, e.EndDatetime, e.IsAllDay })
            .FirstOrDefaultAsync(ct);

        if (earliestEvent is null || !earliestEvent.Start.HasValue)
        {
            return null;
        }

        var latestEvent = await context.Events
            .AsNoTracking()
            .Where(e => e.Lifecycle != "candidate" && !e.IsDeleted && e.StartDatetime.HasValue)
            .OrderByDescending(e => e.EndDatetime ?? e.StartDatetime)
            .Select(e => new { Start = e.StartDatetime, e.EndDatetime, e.IsAllDay })
            .FirstOrDefaultAsync(ct);

        if (latestEvent is null || !latestEvent.Start.HasValue)
        {
            return null;
        }

        var from = DateOnly.FromDateTime(NormalizeUtc(earliestEvent.Start.Value).Date);
        var to = GetInclusiveEndDate(latestEvent.Start.Value, latestEvent.EndDatetime, latestEvent.IsAllDay);
        return (from, to);
    }

    public async Task UpsertAsync(Event ev, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ev.EventId);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Events.SingleOrDefaultAsync(e => e.EventId == ev.EventId, ct);
        if (existing is null)
        {
            context.Events.Add(ev);
        }
        else
        {
            CopyMutableFields(ev, existing);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteByEventIdAsync(string eventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Events.SingleOrDefaultAsync(e => e.EventId == eventId, ct);
        if (existing is not null)
        {
            context.DeletedEvents.Add(CreateDeletedEventSnapshot(existing, DateTime.UtcNow, "user"));
            context.Events.Remove(existing);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task UpdateLifecycleAsync(string eventId, string lifecycle, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lifecycle);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Events.SingleOrDefaultAsync(e => e.EventId == eventId, ct);
        if (existing is not null)
        {
            existing.Lifecycle = lifecycle;
            existing.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> RevertToLastSyncedAsync(string eventId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var ev = await context.Events
            .Include(e => e.Versions)
            .SingleOrDefaultAsync(e => e.EventId == eventId, ct);
        if (ev is null) return false;

        var lastVersion = ev.Versions.OrderByDescending(v => v.VersionId).FirstOrDefault();
        if (lastVersion is not null)
        {
            ev.Summary = lastVersion.Summary;
            ev.Description = lastVersion.Description;
            ev.StartDatetime = lastVersion.StartDatetime;
            ev.EndDatetime = lastVersion.EndDatetime;
            ev.IsAllDay = lastVersion.IsAllDay;
            ev.ColorId = lastVersion.ColorId;
        }
        ev.HasUnpublishedChanges = false;
        ev.IsDeleted = false;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Event?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
    {
        var dayStartUtc = ToLocalDayBoundaryUtc(date);
        var dayEndUtc = ToLocalDayBoundaryUtc(date.AddDays(1));

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.Events
            .AsNoTracking()
            .Where(e => e.SourceSystem == "day_name" &&
                        !e.IsDeleted &&
                        e.StartDatetime >= dayStartUtc &&
                        e.StartDatetime < dayEndUtc)
            .OrderBy(e => e.StartDatetime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Event?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var dayStartUtc = ToLocalDayBoundaryUtc(date);
        var dayEndUtc = ToLocalDayBoundaryUtc(date.AddDays(1));

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.Events
            .AsNoTracking()
            .Where(e => (e.SourceSystem == "toggl" || e.SourceSystem == "sleep") &&
                        !e.IsDeleted &&
                        e.Summary != null &&
                        e.Summary.Contains("Sleep") &&
                        e.StartDatetime.HasValue &&
                        e.StartDatetime < dayEndUtc &&
                        (e.EndDatetime ?? e.StartDatetime.Value) >= dayStartUtc)
            .OrderBy(e => e.StartDatetime)
            .FirstOrDefaultAsync(ct);
    }

    private static void CopyMutableFields(Event source, Event target)
    {
        target.GcalEventId = source.GcalEventId;
        target.CalendarId = source.CalendarId;
        target.Summary = source.Summary;
        target.Description = source.Description;
        target.StartDatetime = source.StartDatetime;
        target.EndDatetime = source.EndDatetime;
        target.IsAllDay = source.IsAllDay;
        target.ColorId = source.ColorId;
        target.Lifecycle = source.Lifecycle;
        target.Publish = source.Publish;
        target.HasUnpublishedChanges = source.HasUnpublishedChanges;
        target.IsDeleted = source.IsDeleted;
        target.SourceSystem = source.SourceSystem;
        target.RecurringEventId = source.RecurringEventId;
        target.IsRecurringInstance = source.IsRecurringInstance;
        target.GcalEtag = source.GcalEtag;
        target.GcalUpdatedAt = source.GcalUpdatedAt;
        target.LastSyncedAt = source.LastSyncedAt;
        target.AppLastModifiedAt = source.AppLastModifiedAt;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
    }

    private static DateTime ToLocalDayBoundaryUtc(DateOnly date)
    {
        return date
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Local)
            .ToUniversalTime();
    }

    private static DeletedEvent CreateDeletedEventSnapshot(Event existingEvent, DateTime deletedAt, string deletionSource)
    {
        return new DeletedEvent
        {
            EventId = existingEvent.EventId,
            GcalEventId = existingEvent.GcalEventId ?? existingEvent.EventId,
            CalendarId = existingEvent.CalendarId,
            Summary = existingEvent.Summary,
            Description = existingEvent.Description,
            StartDatetime = existingEvent.StartDatetime,
            EndDatetime = existingEvent.EndDatetime,
            IsAllDay = existingEvent.IsAllDay,
            ColorId = existingEvent.ColorId,
            GcalEtag = existingEvent.GcalEtag,
            RecurringEventId = existingEvent.RecurringEventId,
            IsRecurringInstance = existingEvent.IsRecurringInstance,
            AppCreated = existingEvent.SourceSystem == "manual" || existingEvent.Publish == "local_only",
            SourceSystem = existingEvent.SourceSystem,
            DeletedAt = deletedAt,
            DeletionSource = deletionSource,
            OriginalCreatedAt = existingEvent.CreatedAt,
            OriginalUpdatedAt = existingEvent.UpdatedAt
        };
    }

    private static DateOnly GetInclusiveEndDate(DateTime startUtc, DateTime? endUtc, bool? isAllDay)
    {
        var normalizedStartUtc = NormalizeUtc(startUtc);
        var normalizedEndUtc = NormalizeUtc(endUtc ?? startUtc);
        var endDate = DateOnly.FromDateTime(normalizedEndUtc.Date);

        if (isAllDay == true && normalizedEndUtc > normalizedStartUtc)
        {
            return endDate.AddDays(-1);
        }

        return endDate;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}

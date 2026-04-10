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

    public async Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(pendingEvent => pendingEvent.GcalEventId == gcalEventId, ct);
    }

    public async Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pendingEvent);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var existing = await context.PendingEvents
            .SingleOrDefaultAsync(
                storedPendingEvent => storedPendingEvent.GcalEventId == pendingEvent.GcalEventId,
                ct);

        if (existing is null)
        {
            if (pendingEvent.Id == Guid.Empty)
            {
                pendingEvent.Id = Guid.NewGuid();
            }

            context.PendingEvents.Add(pendingEvent);
        }
        else
        {
            existing.Summary = pendingEvent.Summary;
            existing.Description = pendingEvent.Description;
            existing.StartDatetime = pendingEvent.StartDatetime;
            existing.EndDatetime = pendingEvent.EndDatetime;
            existing.ColorId = pendingEvent.ColorId;
            existing.CreatedAt = pendingEvent.CreatedAt;
            existing.UpdatedAt = pendingEvent.UpdatedAt;
        }

        await context.SaveChangesAsync(ct);
    }
}

using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class GcalEventRepository : IGcalEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public GcalEventRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rangeStartUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.GcalEvents
            .AsNoTracking()
            .Where(gcalEvent =>
                !gcalEvent.IsDeleted &&
                gcalEvent.StartDatetime.HasValue &&
                gcalEvent.StartDatetime.Value < rangeEndExclusiveUtc &&
                (gcalEvent.EndDatetime ?? gcalEvent.StartDatetime.Value) >= rangeStartUtc)
            .OrderBy(gcalEvent => gcalEvent.StartDatetime ?? gcalEvent.EndDatetime)
            .ThenBy(gcalEvent => gcalEvent.Summary)
            .ToListAsync(ct);
    }

    public async Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.GcalEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                gcalEvent => !gcalEvent.IsDeleted && gcalEvent.GcalEventId == gcalEventId,
                ct);
    }
}

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

    public async Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        var earliestEvent = await context.GcalEvents
            .AsNoTracking()
            .Where(gcalEvent => !gcalEvent.IsDeleted && gcalEvent.StartDatetime.HasValue)
            .OrderBy(gcalEvent => gcalEvent.StartDatetime)
            .Select(gcalEvent => new
            {
                Start = gcalEvent.StartDatetime,
                gcalEvent.EndDatetime,
                gcalEvent.IsAllDay
            })
            .FirstOrDefaultAsync(ct);

        if (earliestEvent is null || !earliestEvent.Start.HasValue)
        {
            return null;
        }

        var latestEvent = await context.GcalEvents
            .AsNoTracking()
            .Where(gcalEvent => !gcalEvent.IsDeleted && gcalEvent.StartDatetime.HasValue)
            .OrderByDescending(gcalEvent => gcalEvent.EndDatetime ?? gcalEvent.StartDatetime)
            .Select(gcalEvent => new
            {
                Start = gcalEvent.StartDatetime,
                gcalEvent.EndDatetime,
                gcalEvent.IsAllDay
            })
            .FirstOrDefaultAsync(ct);

        if (latestEvent is null || !latestEvent.Start.HasValue)
        {
            return null;
        }

        var from = DateOnly.FromDateTime(NormalizeUtc(earliestEvent.Start.Value).Date);
        var to = GetInclusiveEndDate(latestEvent.Start.Value, latestEvent.EndDatetime, latestEvent.IsAllDay);
        return (from, to);
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

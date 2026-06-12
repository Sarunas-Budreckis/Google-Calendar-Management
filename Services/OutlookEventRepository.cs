using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class OutlookEventRepository : IOutlookEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public OutlookEventRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<OutlookEvent>> GetEventsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = ToUtcRange(date, date);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.OutlookEvents
            .AsNoTracking()
            .Where(e => e.StartDatetime >= utcStart && e.StartDatetime < utcEnd)
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.OutlookEventId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetEventCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = ToUtcRange(from, to);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var timestamps = await context.OutlookEvents
            .AsNoTracking()
            .Where(e => !e.IsSuppressed && e.StartDatetime >= utcStart && e.StartDatetime < utcEnd)
            .Select(e => e.StartDatetime)
            .ToListAsync(ct);

        return timestamps
            .GroupBy(t => DateOnly.FromDateTime(NormalizeUtc(t).ToLocalTime()))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<bool> SetSuppressedAsync(string outlookEventId, bool suppressed, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var ev = await context.OutlookEvents.SingleOrDefaultAsync(e => e.OutlookEventId == outlookEventId, ct);
        if (ev is null)
        {
            return false;
        }

        ev.IsSuppressed = suppressed;
        await context.SaveChangesAsync(ct);
        return true;
    }

    private static (DateTime utcStart, DateTime utcEnd) ToUtcRange(DateOnly from, DateOnly to)
    {
        var localStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEnd = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return (localStart.ToUniversalTime(), localEnd.ToUniversalTime());
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

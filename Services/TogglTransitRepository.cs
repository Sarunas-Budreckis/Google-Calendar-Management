using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglTransitRepository : ITogglTransitRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public TogglTransitRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TogglEntry>> GetTransitEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var (utcStart, utcEndExclusive) = LocalDayToUtcRange(date);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.TogglEntries
            .AsNoTracking()
            .Where(e => e.TogglDataType == TogglDataType.TogglTransit
                        && e.StartTime >= utcStart
                        && e.StartTime < utcEndExclusive)
            .OrderBy(e => e.StartTime)
            .ThenBy(e => e.TogglId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetTransitEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (utcStart, utcEndExclusive) = LocalDayRangeToUtcRange(from, to);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var starts = await context.TogglEntries
            .AsNoTracking()
            .Where(e => e.TogglDataType == TogglDataType.TogglTransit
                        && e.StartTime >= utcStart
                        && e.StartTime < utcEndExclusive)
            .Select(e => e.StartTime)
            .ToListAsync(ct);

        return starts
            .GroupBy(start => DateOnly.FromDateTime(NormalizeUtc(start).ToLocalTime()))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private static (DateTime UtcStart, DateTime UtcEndExclusive) LocalDayToUtcRange(DateOnly date)
    {
        var localStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return (localStart.ToUniversalTime(), localStart.AddDays(1).ToUniversalTime());
    }

    private static (DateTime UtcStart, DateTime UtcEndExclusive) LocalDayRangeToUtcRange(DateOnly from, DateOnly to)
    {
        var localStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return (localStart.ToUniversalTime(), localEndExclusive.ToUniversalTime());
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

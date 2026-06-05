using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneRepository : ITogglPhoneRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public TogglPhoneRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TogglEntry>> GetPhoneEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = localStart.AddDays(1);
        var utcStart = localStart.ToUniversalTime();
        var utcEndExclusive = localEndExclusive.ToUniversalTime();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.TogglEntries
            .AsNoTracking()
            .Where(entry =>
                entry.TogglDataType == TogglDataType.TogglPhone &&
                entry.StartTime >= utcStart &&
                entry.StartTime < utcEndExclusive)
            .OrderBy(entry => entry.StartTime)
            .ThenBy(entry => entry.TogglId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetPhoneEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var utcStart = localStart.ToUniversalTime();
        var utcEndExclusive = localEndExclusive.ToUniversalTime();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var entries = await context.TogglEntries
            .AsNoTracking()
            .Where(entry =>
                entry.TogglDataType == TogglDataType.TogglPhone &&
                entry.StartTime >= utcStart &&
                entry.StartTime < utcEndExclusive)
            .Select(entry => entry.StartTime)
            .ToListAsync(ct);

        return entries
            .GroupBy(start => DateOnly.FromDateTime(NormalizeUtc(start).ToLocalTime()))
            .Where(group => group.Key >= from && group.Key <= to)
            .ToDictionary(group => group.Key, group => group.Count());
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

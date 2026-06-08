using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class CallLogRepository : ICallLogRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public CallLogRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<CallLogEntry>> GetEntriesForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = localStart.AddDays(1);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.CallLogEntries
            .AsNoTracking()
            .Where(e => e.Date >= localStart && e.Date < localEndExclusive)
            .OrderBy(e => e.Date)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var localEndExclusive = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var dates = await context.CallLogEntries
            .AsNoTracking()
            .Where(e => e.Date >= localStart && e.Date < localEndExclusive)
            .Select(e => e.Date)
            .ToListAsync(ct);

        return dates
            .GroupBy(d => DateOnly.FromDateTime(d))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
    }

}

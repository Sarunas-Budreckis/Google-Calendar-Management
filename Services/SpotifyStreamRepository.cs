using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class SpotifyStreamRepository : ISpotifyStreamRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public SpotifyStreamRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<SpotifyStream>> GetStreamsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = ToUtcRange(date, date);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.SpotifyStreams
            .AsNoTracking()
            .Where(s => s.PlayedAt >= utcStart && s.PlayedAt < utcEnd)
            .OrderBy(s => s.PlayedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetStreamCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (utcStart, utcEnd) = ToUtcRange(from, to);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var timestamps = await context.SpotifyStreams
            .AsNoTracking()
            .Where(s => s.PlayedAt >= utcStart && s.PlayedAt < utcEnd)
            .Select(s => s.PlayedAt)
            .ToListAsync(ct);

        return timestamps
            .GroupBy(t => DateOnly.FromDateTime(NormalizeUtc(t).ToLocalTime()))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
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

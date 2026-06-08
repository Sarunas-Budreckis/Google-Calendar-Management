using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class Civ5SessionRepository : ICiv5SessionRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public Civ5SessionRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Civ5SessionPoint>> GetPointsForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var localStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        return await ctx.Civ5SessionPoints
            .AsNoTracking()
            .Where(p => p.FileModifiedAt >= utcStart && p.FileModifiedAt < utcEnd)
            .OrderBy(p => p.FileModifiedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, int>> GetPointCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var utcStart = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var utcEnd = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var timestamps = await ctx.Civ5SessionPoints
            .AsNoTracking()
            .Where(p => p.FileModifiedAt >= utcStart && p.FileModifiedAt < utcEnd)
            .Select(p => p.FileModifiedAt)
            .ToListAsync(ct);

        return timestamps
            .GroupBy(ts => DateOnly.FromDateTime(NormalizeUtc(ts).ToLocalTime()))
            .Where(g => g.Key >= from && g.Key <= to)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<HashSet<(DateTime FileModifiedAt, string GameMode)>> GetExistingDedupKeysAsync(
        IReadOnlyList<DateTime> candidates, CancellationToken ct = default)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var min = candidates.Min();
        var max = candidates.Max();

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await ctx.Civ5SessionPoints
            .AsNoTracking()
            .Where(p => p.FileModifiedAt >= min && p.FileModifiedAt <= max)
            .Select(p => new { p.FileModifiedAt, p.GameMode })
            .ToListAsync(ct);

        return existing.Select(p => (p.FileModifiedAt, p.GameMode)).ToHashSet();
    }

    public async Task<int> InsertPointsAsync(IReadOnlyList<Civ5SessionPoint> points, CancellationToken ct = default)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var inserted = 0;
        foreach (var point in points)
        {
            inserted += await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT OR IGNORE INTO civ5_data (
                    scanned_at,
                    file_modified_at,
                    game_mode,
                    linked_event_id,
                    linked_event_type)
                VALUES (
                    {point.ScannedAt},
                    {point.FileModifiedAt},
                    {point.GameMode},
                    {point.LinkedEventId},
                    {point.LinkedEventType})
                """, ct);
        }

        return inserted;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

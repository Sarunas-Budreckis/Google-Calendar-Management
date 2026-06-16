using GoogleCalendarManagement.Constants;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services.DataLinking;

public sealed class ComfyUIClumpBlockProvider : IClumpBlockProvider
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly EightFifteenRuleService _eightFifteenRule;

    public ComfyUIClumpBlockProvider(
        IDbContextFactory<CalendarDbContext> contextFactory,
        EightFifteenRuleService eightFifteenRule)
    {
        _contextFactory = contextFactory;
        _eightFifteenRule = eightFifteenRule;
    }

    public string SourceKey => SourceKeys.ComfyUI;

    public async Task<IReadOnlyList<ClumpBlockResult>> GetClumpsAndBlocksAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var dataPoints = await ctx.DataPoints
            .AsNoTracking()
            .Where(dp => dp.SourceKey == SourceKey && dp.StartUtc >= fromUtc && dp.StartUtc < toUtc)
            .ToListAsync(ct);

        if (dataPoints.Count == 0)
        {
            return [];
        }

        var keyedDataPoints = new List<(DataPoint DataPoint, int SourceId)>();
        foreach (var dataPoint in dataPoints)
        {
            if (int.TryParse(dataPoint.SourceRef, out var sourceId))
            {
                keyedDataPoints.Add((dataPoint, sourceId));
            }
        }

        if (keyedDataPoints.Count == 0)
        {
            return [];
        }

        var ids = keyedDataPoints.Select(dp => dp.SourceId).ToHashSet();
        var rawPoints = await ctx.ComfyUIScanPoints
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);

        var rawById = rawPoints.ToDictionary(p => p.Id);
        var resolved = new List<(DataPoint DataPoint, ComfyUIScanPoint Raw)>();
        foreach (var keyedDataPoint in keyedDataPoints)
        {
            if (rawById.TryGetValue(keyedDataPoint.SourceId, out var rawPoint))
            {
                resolved.Add((keyedDataPoint.DataPoint, rawPoint));
            }
        }

        var scanPoints = resolved.Select(x => x.Raw).ToList();
        var windows = ComfyUISessionCoalescer.CoalesceIntoWindows(scanPoints);

        var results = new List<ClumpBlockResult>();
        foreach (var window in windows)
        {
            var windowDps = resolved
                .Where(x => x.Raw.Timestamp >= window.WindowStart
                         && x.Raw.Timestamp <= window.WindowEnd)
                .Select(x => new ClumpDataPoint(
                    x.DataPoint.DataPointId,
                    x.DataPoint.SourceKey,
                    x.DataPoint.SourceRef,
                    x.DataPoint.StartUtc,
                    x.DataPoint.EndUtc))
                .ToList();

            var clump = new Clump(windowDps, window.WindowStart, window.WindowEnd);
            var blocks = _eightFifteenRule.ApplyRule(window.WindowStart, window.WindowEnd)
                .Select(r => new Block(r.Start, r.End))
                .ToList();

            results.Add(new ClumpBlockResult(clump, blocks));
        }

        return results;
    }
}

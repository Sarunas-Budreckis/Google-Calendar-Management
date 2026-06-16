using GoogleCalendarManagement.Constants;
using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services.DataLinking;

public sealed class PhoneClumpBlockProvider : IClumpBlockProvider
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly TogglSlidingWindowService _slidingWindowService;
    private readonly EightFifteenRuleService _eightFifteenRule;

    public PhoneClumpBlockProvider(
        IDbContextFactory<CalendarDbContext> contextFactory,
        TogglSlidingWindowService slidingWindowService,
        EightFifteenRuleService eightFifteenRule)
    {
        _contextFactory = contextFactory;
        _slidingWindowService = slidingWindowService;
        _eightFifteenRule = eightFifteenRule;
    }

    public string SourceKey => SourceKeys.TogglPhone;

    public async Task<IReadOnlyList<ClumpBlockResult>> GetClumpsAndBlocksAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var dataPoints = await ctx.DataPoints
            .AsNoTracking()
            .Where(dp => dp.SourceKey == SourceKey
                && ((dp.EndUtc > fromUtc && dp.StartUtc < toUtc)
                    || (dp.StartUtc == dp.EndUtc && dp.StartUtc >= fromUtc && dp.StartUtc < toUtc)))
            .ToListAsync(ct);

        if (dataPoints.Count == 0)
        {
            return [];
        }

        var entries = dataPoints
            .Select(dp => new TogglSlidingWindowService.SlidingWindowEntry(dp.StartUtc, dp.EndUtc))
            .ToList();

        var windows = _slidingWindowService.ComputeWindows(
            entries,
            gapThreshold: TimeSpan.FromMinutes(ImportThresholds.PhoneCoalesceGapMinutes),
            qualityThreshold: ImportThresholds.PhoneCoalesceQualityThreshold,
            minWindowDuration: TimeSpan.FromMinutes(ImportThresholds.PhoneCoalesceMinWindowDurationMinutes));

        var results = new List<ClumpBlockResult>();
        foreach (var window in windows)
        {
            var windowDps = dataPoints
                .Where(dp => (dp.EndUtc > window.WindowStartUtc && dp.StartUtc < window.WindowEndUtc)
                    || (dp.StartUtc == dp.EndUtc
                        && dp.StartUtc >= window.WindowStartUtc
                        && dp.StartUtc <= window.WindowEndUtc))
                .Select(dp => new ClumpDataPoint(
                    dp.DataPointId,
                    dp.SourceKey,
                    dp.SourceRef,
                    dp.StartUtc,
                    dp.EndUtc))
                .ToList();

            var clump = new Clump(windowDps, window.WindowStartUtc, window.WindowEndUtc);
            var blocks = _eightFifteenRule.ApplyRule(window.WindowStartUtc, window.WindowEndUtc)
                .Select(r => new Block(r.Start, r.End))
                .ToList();

            results.Add(new ClumpBlockResult(clump, blocks));
        }

        return results;
    }
}

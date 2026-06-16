using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services.DataLinking;

public sealed class TrivialClumpBlockProvider : IClumpBlockProvider
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly EightFifteenRuleService _eightFifteenRule;

    public TrivialClumpBlockProvider(
        string sourceKey,
        IDbContextFactory<CalendarDbContext> contextFactory,
        EightFifteenRuleService eightFifteenRule)
    {
        SourceKey = sourceKey;
        _contextFactory = contextFactory;
        _eightFifteenRule = eightFifteenRule;
    }

    public string SourceKey { get; }

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

        var results = new List<ClumpBlockResult>(dataPoints.Count);
        foreach (var dp in dataPoints)
        {
            var clumpPoint = new ClumpDataPoint(dp.DataPointId, dp.SourceKey, dp.SourceRef, dp.StartUtc, dp.EndUtc);
            var clump = new Clump([clumpPoint], dp.StartUtc, dp.EndUtc);
            var blocks = _eightFifteenRule.ApplyRule(dp.StartUtc, dp.EndUtc)
                .Select(r => new Block(r.Start, r.End))
                .ToList();

            results.Add(new ClumpBlockResult(clump, blocks));
        }

        return results;
    }
}

using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// Auto-links a Spotify stream datapoint to an approved event when EXACTLY one approved event's
/// time range overlaps the datapoint's <c>[start_utc, end_utc]</c> (Story 8.15). Zero or two-plus
/// overlapping events → no-op (the datapoint stays unlinked, or keeps any pre-existing auto link
/// until the engine's reversal pass cleans it up when the linked event moves/deletes/un-approves).
///
/// Pure: it only reads approved events to decide and NEVER writes — the engine applies the proposed
/// ops atomically. Reversal is the engine's responsibility, so this rule never proposes "removal":
/// when the previously-covering event is gone, the rule simply re-evaluates to zero-cover (no op),
/// and the engine deletes the now-stale auto link during its reversal pass.
/// </summary>
public sealed class SpotifyAutoLinkRule : ILinkRule
{
    /// <summary>Stable rule id stamped onto <c>link.rule_id</c>.</summary>
    public const string Id = "spotify_auto_link";

    private const string LifecycleApproved = "approved";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public SpotifyAutoLinkRule(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public string RuleId => Id;

    public async Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(
        RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct)
    {
        var datapoints = eligible
            .Where(dp => dp.SourceKey == SpotifyImportService.SourceKey)
            .ToList();
        if (datapoints.Count == 0)
        {
            return Array.Empty<RuleProposedOp>();
        }

        // One query for all approved events overlapping the eligible window; per-datapoint coverage
        // is then computed in memory. Timings come straight from data_point (the 8.9 projector owns
        // the start/end math) — this rule never recalculates them.
        var windowStart = datapoints.Min(dp => dp.StartUtc);
        var windowEnd = datapoints.Max(dp => dp.EndUtc);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var approved = await context.Events.AsNoTracking()
            .Where(e => e.Lifecycle == LifecycleApproved
                && !e.IsDeleted
                && e.StartDatetime != null
                && e.StartDatetime < windowEnd
                && (e.EndDatetime ?? e.StartDatetime) > windowStart)
            .Select(e => new { e.EventId, e.StartDatetime, e.EndDatetime })
            .ToListAsync(ct);

        var ops = new List<RuleProposedOp>();
        foreach (var dp in datapoints)
        {
            string? coverEventId = null;
            var coverCount = 0;
            foreach (var e in approved)
            {
                var start = e.StartDatetime!.Value;
                var end = e.EndDatetime ?? start;
                // Overlap: ev.start < dp.end AND ev.end > dp.start.
                if (start < dp.EndUtc && end > dp.StartUtc)
                {
                    coverEventId = e.EventId;
                    if (++coverCount > 1)
                    {
                        break;
                    }
                }
            }

            // Single cover → auto-link. Zero or 2+ → propose nothing.
            if (coverCount == 1)
            {
                ops.Add(RuleProposedOp.Link(dp.DataPointId, coverEventId!, RuleId));
            }
        }

        return ops;
    }
}

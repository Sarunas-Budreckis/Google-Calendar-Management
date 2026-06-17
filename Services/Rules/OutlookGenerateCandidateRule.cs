using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// Resolves Outlook datapoints against their underlying <c>OutlookEvent</c> (Story 8.15):
/// <list type="bullet">
///   <item>not suppressed → propose <see cref="ProposedOpKind.GenerateCandidate"/> (the engine mints
///   a translucent <c>candidate</c> event with <c>source_system = "outlook"</c> and links it);</item>
///   <item>suppressed → propose <see cref="ProposedOpKind.Ignore"/> (datapoint kept, no event).</item>
/// </list>
/// Idempotent: a datapoint already in the matching state set by a prior run is left untouched, and a
/// flipped <c>IsSuppressed</c> produces the linked↔ignored transition op. Pure read-only proposal:
/// the engine performs all writes.
/// </summary>
public sealed class OutlookGenerateCandidateRule : ILinkRule
{
    /// <summary>Stable rule id stamped onto <c>link.rule_id</c>.</summary>
    public const string Id = "outlook_generate_candidate";

    private const string OriginAutoRule = "auto_rule";
    private const string StateLinked = "linked";
    private const string StateIgnored = "ignored";
    private const string NoSubjectSummary = "(No subject)";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public OutlookGenerateCandidateRule(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public string RuleId => Id;

    public async Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(
        RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct)
    {
        var datapoints = eligible
            .Where(dp => dp.SourceKey == OutlookImportService.SourceKey)
            .ToList();
        if (datapoints.Count == 0)
        {
            return Array.Empty<RuleProposedOp>();
        }

        var ids = datapoints.Select(dp => dp.DataPointId).ToList();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // EligibleDataPoint carries no source_ref, so resolve each datapoint's OutlookEventId here.
        var refByDataPoint = await context.DataPoints.AsNoTracking()
            .Where(dp => ids.Contains(dp.DataPointId))
            .ToDictionaryAsync(dp => dp.DataPointId, dp => dp.SourceRef, ct);

        var sourceRefs = refByDataPoint.Values.Distinct().ToList();
        var outlookEvents = await context.OutlookEvents.AsNoTracking()
            .Where(e => sourceRefs.Contains(e.OutlookEventId))
            .ToDictionaryAsync(e => e.OutlookEventId, ct);

        // Current link state per datapoint (auto_rule or absent — the engine excludes manual rows
        // from the eligible set, so reading state here is enough to stay idempotent and flip on toggle).
        var linkByDataPoint = await context.Links.AsNoTracking()
            .Where(l => ids.Contains(l.DataPointId))
            .ToDictionaryAsync(l => l.DataPointId, ct);

        var ops = new List<RuleProposedOp>();
        foreach (var dp in datapoints)
        {
            if (!refByDataPoint.TryGetValue(dp.DataPointId, out var sourceRef) ||
                !outlookEvents.TryGetValue(sourceRef, out var oe))
            {
                // Raw Outlook event no longer resolvable — leave the datapoint for reconciliation.
                continue;
            }

            linkByDataPoint.TryGetValue(dp.DataPointId, out var link);
            if (link is { Origin: OriginAutoRule, RuleId: not null } && link.RuleId != RuleId)
            {
                // Only this rule may revise rows it previously authored.
                continue;
            }

            var isLinked = link is { State: StateLinked } && link.RuleId == RuleId;
            var isIgnored = link is { State: StateIgnored } && link.RuleId == RuleId;

            if (oe.IsSuppressed)
            {
                if (isIgnored)
                {
                    continue; // already ignored by a prior run — idempotent no-op
                }

                // Unlinked → ignore; previously linked (suppression just toggled on) → flip to ignore.
                ops.Add(RuleProposedOp.Ignore(dp.DataPointId, RuleId));
            }
            else
            {
                var summary = string.IsNullOrWhiteSpace(oe.Subject) ? NoSubjectSummary : oe.Subject;
                if (isLinked && link?.EventId is not null &&
                    await CandidateMatchesAsync(context, link.EventId, summary, oe.StartDatetime, oe.EndDatetime, ct))
                {
                    continue; // already linked to its candidate — idempotent no-op
                }

                // Unlinked → generate; previously ignored → re-generate; stale linked candidate → refresh.
                ops.Add(RuleProposedOp.GenerateCandidate(
                    dp.DataPointId, RuleId, summary, oe.StartDatetime, oe.EndDatetime,
                    OutlookImportService.SourceKey));
            }
        }

        return ops;
    }

    private static async Task<bool> CandidateMatchesAsync(
        CalendarDbContext context,
        string eventId,
        string summary,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        var ev = await context.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => new { e.Lifecycle, e.SourceSystem, e.Summary, e.StartDatetime, e.EndDatetime })
            .SingleOrDefaultAsync(ct);

        return ev is not null &&
            ev.Lifecycle == "candidate" &&
            ev.SourceSystem == OutlookImportService.SourceKey &&
            ev.Summary == summary &&
            ev.StartDatetime == startUtc &&
            ev.EndDatetime == endUtc;
    }
}

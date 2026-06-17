using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// The rule pipeline (Story 8.14). Holds all integrity invariants — manual-sacred, deterministic,
/// idempotent, atomic, auditable, reversible — independent of the concrete rules (Story 8.15+).
/// All <c>link</c>-table writes go through <see cref="ILinkService"/> so undo semantics live in one
/// place; the engine never mutates the <c>link</c> table directly.
/// </summary>
public sealed class RuleEngineService : IRuleEngineService
{
    private const string OriginAutoRule = "auto_rule";
    private const string LifecycleCandidate = "candidate";
    private const string StateLinked = "linked";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IEventRepository _eventRepository;
    private readonly IEventIdentityService _eventIdentity;
    private readonly ILinkService _linkService;
    private readonly IReadOnlyList<ILinkRule> _rules;
    private readonly ILogger<RuleEngineService> _logger;

    public RuleEngineService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        IEventRepository eventRepository,
        IEventIdentityService eventIdentity,
        ILinkService linkService,
        IEnumerable<ILinkRule> rules,
        ILogger<RuleEngineService> logger)
    {
        _contextFactory = contextFactory;
        _eventRepository = eventRepository;
        _eventIdentity = eventIdentity;
        _linkService = linkService;
        _rules = rules.ToList();
        _logger = logger;
    }

    public async Task RunPipelineAsync(RuleScope scope, CancellationToken ct = default)
    {
        var eligible = await BuildEligibleListAsync(scope, ct);
        if (eligible.Count == 0)
        {
            return;
        }

        // Rules run sequentially in registration order so results are deterministic.
        var ruleOutputs = new List<(ILinkRule Rule, IReadOnlyList<RuleProposedOp> Ops)>(_rules.Count);
        foreach (var rule in _rules)
        {
            var ops = await rule.ProposeOpsAsync(scope, eligible, ct) ?? Array.Empty<RuleProposedOp>();
            ruleOutputs.Add((rule, ops));
        }

        var finalOps = AggregateProposals(ruleOutputs);
        var applied = await ApplyOpsAsync(finalOps, ct);

        _logger.LogInformation(
            "Rule pipeline applied {Count} op(s) over {Eligible} eligible datapoint(s) for scope {From}–{To} (source {Source})",
            applied, eligible.Count, scope.FromDate, scope.ToDate, scope.SourceKeyFilter ?? "*");
    }

    public async Task ReverseAndRerunAsync(string eventId, CancellationToken ct = default)
    {
        var ev = await _eventRepository.GetByEventIdAsync(eventId, ct);
        if (ev is null)
        {
            _logger.LogWarning("ReverseAndRerunAsync: event {EventId} not found; skipping reversal", eventId);
            return;
        }

        if (ev.StartDatetime is null)
        {
            _logger.LogWarning("ReverseAndRerunAsync: event {EventId} has no start time; skipping reversal", eventId);
            return;
        }

        var startUtc = ev.StartDatetime.Value;
        var endUtc = ev.EndDatetime ?? ev.StartDatetime.Value;
        await ReverseRangeAndRerunAsync(startUtc, endUtc, ct);
    }

    public async Task ReverseRangeAndRerunAsync(DateTime oldStartUtc, DateTime oldEndUtc, CancellationToken ct = default)
    {
        List<int> overlappingIds;
        await using (var context = await _contextFactory.CreateDbContextAsync(ct))
        {
            overlappingIds = await context.DataPoints.AsNoTracking()
                .Where(dp => dp.StartUtc < oldEndUtc && dp.EndUtc >= oldStartUtc)
                .Select(dp => dp.DataPointId)
                .ToListAsync(ct);
        }

        if (overlappingIds.Count > 0)
        {
            var deleted = await _linkService.DeleteAutoLinksForDataPointsAsync(overlappingIds, ct);
            await CleanupOrphanedCandidatesAsync(deleted, ct);
        }

        var fromDate = ToLocalDate(oldStartUtc);
        var toDate = ToLocalDate(oldEndUtc);
        if (toDate < fromDate)
        {
            toDate = fromDate;
        }

        await RunPipelineAsync(new RuleScope(fromDate, toDate), ct);
    }

    public async Task RunForImportAsync(string sourceKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);

        DateTime minUtc;
        DateTime maxUtc;
        await using (var context = await _contextFactory.CreateDbContextAsync(ct))
        {
            var query = context.DataPoints.AsNoTracking().Where(dp => dp.SourceKey == sourceKey);
            if (!await query.AnyAsync(ct))
            {
                return;
            }

            minUtc = await query.MinAsync(dp => dp.StartUtc, ct);
            maxUtc = await query.MaxAsync(dp => dp.EndUtc, ct);
        }

        await RunPipelineAsync(new RuleScope(ToLocalDate(minUtc), ToLocalDate(maxUtc), sourceKey), ct);
    }

    public async Task RunForEventApproveAsync(string eventId, CancellationToken ct = default)
    {
        var ev = await _eventRepository.GetByEventIdAsync(eventId, ct);
        if (ev?.StartDatetime is null)
        {
            return;
        }

        var fromDate = ToLocalDate(ev.StartDatetime.Value);
        var toDate = ToLocalDate(ev.EndDatetime ?? ev.StartDatetime.Value);
        if (toDate < fromDate)
        {
            toDate = fromDate;
        }

        await RunPipelineAsync(new RuleScope(fromDate, toDate), ct);
    }

    public async Task RunForEventEditTimeAsync(string eventId, DateTime oldStartUtc, DateTime oldEndUtc, CancellationToken ct = default)
    {
        // Clean + re-evaluate the position the event vacated, then evaluate the new position
        // without deleting already-correct auto links that overlap the moved event.
        await ReverseRangeAndRerunAsync(oldStartUtc, oldEndUtc, ct);
        await RunForEventApproveAsync(eventId, ct);
    }

    public Task RunForEventDeleteAsync(string eventId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default) =>
        // The event row is already gone — reverse over the range supplied by the caller.
        ReverseRangeAndRerunAsync(startUtc, endUtc, ct);

    // --- Internals ---

    private async Task<IReadOnlyList<EligibleDataPoint>> BuildEligibleListAsync(RuleScope scope, CancellationToken ct)
    {
        var startUtc = ToLocalDayStartUtc(scope.FromDate);
        var endExclusiveUtc = ToLocalDayStartUtc(scope.ToDate.AddDays(1));

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var query = context.DataPoints.AsNoTracking()
            .Where(dp => dp.StartUtc < endExclusiveUtc && dp.EndUtc >= startUtc);

        if (!string.IsNullOrWhiteSpace(scope.SourceKeyFilter))
        {
            query = query.Where(dp => dp.SourceKey == scope.SourceKeyFilter);
        }

        // A unique index guarantees ≤1 link per datapoint, so FirstOrDefault yields its origin or null.
        var rows = await query
            .OrderBy(dp => dp.StartUtc)
            .ThenBy(dp => dp.DataPointId)
            .Select(dp => new
            {
                dp.DataPointId,
                dp.SourceKey,
                dp.StartUtc,
                dp.EndUtc,
                Origin = dp.Links.Select(l => l.Origin).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Origin == null || r.Origin == OriginAutoRule)
            .Select(r => new EligibleDataPoint(r.DataPointId, r.SourceKey, r.StartUtc, r.EndUtc))
            .ToList();
    }

    private static IReadOnlyList<RuleProposedOp> AggregateProposals(
        IReadOnlyList<(ILinkRule Rule, IReadOnlyList<RuleProposedOp> Ops)> ruleOutputs)
    {
        // First rule wins: once a datapoint is claimed, later rules' ops for it are discarded.
        var claimed = new HashSet<int>();
        var final = new List<RuleProposedOp>();
        foreach (var (rule, ops) in ruleOutputs)
        {
            foreach (var op in ops)
            {
                if (op.RuleId != rule.RuleId)
                {
                    throw new InvalidOperationException(
                        $"Rule '{rule.RuleId}' proposed an op stamped with rule_id '{op.RuleId}'.");
                }

                if (claimed.Add(op.DataPointId))
                {
                    final.Add(op);
                }
            }
        }

        return final;
    }

    private async Task<int> ApplyOpsAsync(IReadOnlyList<RuleProposedOp> ops, CancellationToken ct)
    {
        if (ops.Count == 0)
        {
            return 0;
        }

        var writes = new List<AutoLinkWrite>(ops.Count);
        var candidateIdsToNotify = new List<string>();
        var displacedLinks = new List<Link>();

        foreach (var op in ops)
        {
            switch (op.Kind)
            {
                case ProposedOpKind.Link:
                    TrackDisplacedAutoLink(displacedLinks, await _linkService.GetLinkAsync(op.DataPointId, ct), op.EventId);
                    writes.Add(new AutoLinkWrite(op.DataPointId, op.EventId, Ignore: false, op.RuleId));
                    break;

                case ProposedOpKind.Ignore:
                    TrackDisplacedAutoLink(displacedLinks, await _linkService.GetLinkAsync(op.DataPointId, ct), replacementEventId: null);
                    writes.Add(new AutoLinkWrite(op.DataPointId, null, Ignore: true, op.RuleId));
                    break;

                case ProposedOpKind.GenerateCandidate:
                    ValidateGenerateCandidateOp(op);

                    var existing = await _linkService.GetLinkAsync(op.DataPointId, ct);
                    if (existing is { Origin: OriginAutoRule, State: StateLinked, RuleId: var ruleId, EventId: not null }
                        && ruleId == op.RuleId)
                    {
                        var refreshed = CreateCandidateEvent(op, existing.EventId);
                        candidateIdsToNotify.Add(refreshed.EventId);
                        writes.Add(new AutoLinkWrite(op.DataPointId, existing.EventId, Ignore: false, op.RuleId, refreshed));
                        break;
                    }

                    TrackDisplacedAutoLink(displacedLinks, existing, replacementEventId: null);
                    var generated = CreateCandidateEvent(op);
                    candidateIdsToNotify.Add(generated.EventId);
                    writes.Add(new AutoLinkWrite(op.DataPointId, generated.EventId, Ignore: false, op.RuleId, generated));
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported rule op kind: {op.Kind}.");
            }
        }

        await _linkService.WriteAutoBatchAsync(writes, ct);
        await CleanupOrphanedCandidatesAsync(displacedLinks, ct);

        foreach (var id in candidateIdsToNotify.Distinct())
        {
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(id));
        }

        return writes.Count;
    }

    private Event CreateCandidateEvent(RuleProposedOp op, string? eventId = null)
    {
        var now = DateTime.UtcNow;
        return new Event
        {
            EventId = eventId ?? _eventIdentity.MintEventId(),
            CalendarId = "primary",
            Summary = op.GeneratedEventSummary,
            StartDatetime = op.GeneratedEventStart,
            EndDatetime = op.GeneratedEventEnd,
            Lifecycle = LifecycleCandidate,
            Publish = "local_only",
            SourceSystem = op.GeneratedEventSourceSystem ?? OriginAutoRule,
            HasUnpublishedChanges = false,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void TrackDisplacedAutoLink(List<Link> displacedLinks, Link? existing, string? replacementEventId)
    {
        if (existing is not { Origin: OriginAutoRule, State: StateLinked, EventId: not null })
        {
            return;
        }

        if (existing.EventId == replacementEventId)
        {
            return;
        }

        displacedLinks.Add(existing);
    }

    private async Task CleanupOrphanedCandidatesAsync(IReadOnlyList<Link> deletedLinks, CancellationToken ct)
    {
        var candidateLinks = deletedLinks
            .Where(l => l.State == StateLinked && !string.IsNullOrEmpty(l.EventId))
            .GroupBy(l => l.EventId!)
            .Select(g => g.First())
            .ToList();
        if (candidateLinks.Count == 0)
        {
            return;
        }

        var orphanedIds = new List<string>();
        await using (var context = await _contextFactory.CreateDbContextAsync(ct))
        {
            foreach (var link in candidateLinks)
            {
                var eventId = link.EventId!;
                var ev = await context.Events.SingleOrDefaultAsync(e => e.EventId == eventId, ct);
                if (ev is null || ev.Lifecycle != LifecycleCandidate || !IsEngineGeneratedCandidate(link, ev))
                {
                    continue;
                }

                // Only delete an auto-generated candidate once nothing links to it any more.
                var stillReferenced = await context.Links.AnyAsync(l => l.EventId == eventId, ct);
                if (stillReferenced)
                {
                    continue;
                }

                context.Events.Remove(ev);
                orphanedIds.Add(eventId);
            }

            if (orphanedIds.Count > 0)
            {
                await context.SaveChangesAsync(ct);
            }
        }

        foreach (var id in orphanedIds)
        {
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(id));
        }
    }

    private static bool IsEngineGeneratedCandidate(Link sourceLink, Event ev) =>
        ev.SourceSystem == OriginAutoRule ||
        (sourceLink.RuleId == OutlookGenerateCandidateRule.Id &&
            ev.SourceSystem == OutlookImportService.SourceKey);

    private static DateTime ToLocalDayStartUtc(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();

    private static DateOnly ToLocalDate(DateTime utc)
    {
        var asUtc = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };
        return DateOnly.FromDateTime(asUtc.ToLocalTime());
    }

    private static void ValidateGenerateCandidateOp(RuleProposedOp op)
    {
        if (string.IsNullOrWhiteSpace(op.GeneratedEventSummary) ||
            op.GeneratedEventStart is null ||
            op.GeneratedEventEnd is null)
        {
            throw new InvalidOperationException(
                "A GenerateCandidate op requires a summary, generated start, and generated end.");
        }
    }
}

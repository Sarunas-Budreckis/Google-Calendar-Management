using GoogleCalendarManagement.Services.Rules;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// The ordered, atomic rule pipeline (Story 8.14). Runs every registered <see cref="ILinkRule"/>
/// over the eligible datapoints in a scope, applies the proposed link / ignore / generate-candidate
/// ops atomically, and reverses only its own <c>auto_rule</c> state when events move, are deleted,
/// or are un-approved. Manual links are never touched; runs are deterministic and idempotent.
///
/// Triggers are invoked directly by callers (import services, event lifecycle paths) — see the
/// <c>RunFor*</c> methods. Pipeline order is date-range invariant and independent of import order.
/// </summary>
public interface IRuleEngineService
{
    // --- Core ---

    /// <summary>Runs all rules over eligible datapoints in <paramref name="scope"/> and applies the ops atomically.</summary>
    Task RunPipelineAsync(RuleScope scope, CancellationToken ct = default);

    /// <summary>
    /// Removes all <c>auto_rule</c> link rows for datapoints overlapping the event's time range, then
    /// re-runs the pipeline for that range. Loads the event by id to find its extent.
    /// </summary>
    Task ReverseAndRerunAsync(string eventId, CancellationToken ct = default);

    /// <summary>
    /// Reversal variant that takes an explicit UTC range (used when the event row is already gone or
    /// already moved — e.g. delete and time-change), then re-runs the pipeline for that range.
    /// </summary>
    Task ReverseRangeAndRerunAsync(DateTime oldStartUtc, DateTime oldEndUtc, CancellationToken ct = default);

    // --- Direct triggers ---

    /// <summary>Post-import: runs the pipeline over the imported source's datapoints (Story 8.15 wires this).</summary>
    Task RunForImportAsync(string sourceKey, CancellationToken ct = default);

    /// <summary>Event approved: runs the pipeline over the event's time range.</summary>
    Task RunForEventApproveAsync(string eventId, CancellationToken ct = default);

    /// <summary>Event time changed: reverses the old range and re-evaluates both old and new positions.</summary>
    Task RunForEventEditTimeAsync(string eventId, DateTime oldStartUtc, DateTime oldEndUtc, CancellationToken ct = default);

    /// <summary>Event deleted / un-approved: reverses the auto state over the supplied range.</summary>
    Task RunForEventDeleteAsync(string eventId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
}

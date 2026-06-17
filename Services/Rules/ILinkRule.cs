namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// A hardcoded automation that proposes link / ignore / generate-candidate operations over the
/// eligible datapoints in a scope (Story 8.14). Rules are pure: <see cref="ProposeOpsAsync"/> must
/// NOT write to the database — it only returns proposed ops, which the engine applies atomically.
/// Concrete rules (e.g. Spotify auto-link, Outlook generate-candidate) land in Story 8.15.
/// </summary>
public interface ILinkRule
{
    /// <summary>Stable identifier stamped onto <c>link.rule_id</c> (e.g. <c>"spotify_auto_link"</c>).</summary>
    string RuleId { get; }

    /// <summary>
    /// Propose operations for the supplied eligible datapoints. Receives only the eligible subset
    /// for the scope (never manual rows). Must be deterministic and side-effect free.
    /// </summary>
    Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(
        RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct);
}

namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// A single operation a rule proposes for one datapoint (Story 8.14). Rules return these
/// from <see cref="ILinkRule.ProposeOpsAsync"/> — they NEVER write to the database themselves;
/// the <c>RuleEngineService</c> applies the ops atomically.
/// </summary>
/// <param name="Kind">Link / Ignore / GenerateCandidate.</param>
/// <param name="DataPointId">The datapoint this op resolves.</param>
/// <param name="RuleId">The rule that produced this op (stamped onto <c>link.rule_id</c>).</param>
/// <param name="EventId">Target event for <see cref="ProposedOpKind.Link"/>; null for Ignore and for
/// GenerateCandidate (the engine mints the event).</param>
/// <param name="GeneratedEventSummary">Title for a GenerateCandidate event.</param>
/// <param name="GeneratedEventStart">UTC start for a GenerateCandidate event.</param>
/// <param name="GeneratedEventEnd">UTC end for a GenerateCandidate event.</param>
/// <param name="GeneratedEventSourceSystem">Origin stamped onto a generated event's
/// <c>source_system</c>. Defaults to <c>"auto_rule"</c> when null; concrete rules (Story 8.15,
/// e.g. Outlook) may supply their own source key.</param>
public readonly record struct RuleProposedOp(
    ProposedOpKind Kind,
    int DataPointId,
    string RuleId,
    string? EventId = null,
    string? GeneratedEventSummary = null,
    DateTime? GeneratedEventStart = null,
    DateTime? GeneratedEventEnd = null,
    string? GeneratedEventSourceSystem = null)
{
    /// <summary>Propose linking <paramref name="dataPointId"/> to <paramref name="eventId"/>.</summary>
    public static RuleProposedOp Link(int dataPointId, string eventId, string ruleId) =>
        new(ProposedOpKind.Link, dataPointId, ruleId, EventId: eventId);

    /// <summary>Propose ignoring <paramref name="dataPointId"/> (no event).</summary>
    public static RuleProposedOp Ignore(int dataPointId, string ruleId) =>
        new(ProposedOpKind.Ignore, dataPointId, ruleId);

    /// <summary>Propose generating a candidate event and linking <paramref name="dataPointId"/> to it.</summary>
    public static RuleProposedOp GenerateCandidate(
        int dataPointId, string ruleId, string summary,
        DateTime startUtc, DateTime endUtc, string? sourceSystem = null) =>
        new(ProposedOpKind.GenerateCandidate, dataPointId, ruleId,
            GeneratedEventSummary: summary,
            GeneratedEventStart: startUtc,
            GeneratedEventEnd: endUtc,
            GeneratedEventSourceSystem: sourceSystem);
}

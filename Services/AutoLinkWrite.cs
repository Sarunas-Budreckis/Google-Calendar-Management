using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// One auto-rule link/ignore write requested by the rule engine (Story 8.14). A batch of these is
/// applied by <see cref="ILinkService.WriteAutoBatchAsync"/> in a single transaction under one
/// shared <c>action_group_id</c>, so an entire pipeline run can be undone as one step.
/// </summary>
/// <param name="DataPointId">Datapoint to resolve.</param>
/// <param name="EventId">Target event when linking; ignored when <paramref name="Ignore"/> is true.</param>
/// <param name="Ignore">True to write <c>state = 'ignored'</c> (event_id null); false to link.</param>
/// <param name="RuleId">The rule that produced this write (required for auto_rule rows).</param>
/// <param name="GeneratedEvent">Unsaved candidate event to insert atomically with this link, if any.</param>
public readonly record struct AutoLinkWrite(
    int DataPointId,
    string? EventId,
    bool Ignore,
    string RuleId,
    Event? GeneratedEvent = null);

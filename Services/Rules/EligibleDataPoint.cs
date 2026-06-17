namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// A datapoint a rule is allowed to act on within a scope (Story 8.14). Eligible means the
/// datapoint is either unlinked (no <c>link</c> row) or carries an <c>origin = 'auto_rule'</c>
/// link. Manually-resolved datapoints (<c>origin = 'manual'</c>) are NEVER surfaced to rules —
/// manual decisions are sacred.
/// </summary>
public sealed record EligibleDataPoint(int DataPointId, string SourceKey, DateTime StartUtc, DateTime EndUtc);

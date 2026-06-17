namespace GoogleCalendarManagement.Services.Rules;

/// <summary>
/// The time (and optionally source) window a rule pipeline run operates over (Story 8.14).
/// Dates are inclusive local calendar days; the engine converts them to UTC day boundaries when
/// querying datapoints. Pipeline output is date-range invariant — the same scope always yields the
/// same proposed ops regardless of when (or how) it was triggered.
/// </summary>
/// <param name="FromDate">Inclusive first local day in scope.</param>
/// <param name="ToDate">Inclusive last local day in scope.</param>
/// <param name="SourceKeyFilter">When set, only datapoints of this <c>source_key</c> are considered.</param>
public sealed record RuleScope(DateOnly FromDate, DateOnly ToDate, string? SourceKeyFilter = null);

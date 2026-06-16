namespace GoogleCalendarManagement.Models;

public sealed record CoverageResult(int Total, int Covered, CoverageLevel Level);

public enum CoverageLevel { Full, Partial, None }

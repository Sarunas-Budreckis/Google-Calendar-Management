using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public interface ICoverageService
{
    Task<CoverageResult> GetDateSourceCoverageAsync(DateOnly date, string sourceKey, CancellationToken ct = default);
    Task<CoverageResult> GetDayCoverageAsync(DateOnly date, CancellationToken ct = default);
    Task<CoverageResult> GetEventCoverageAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
}

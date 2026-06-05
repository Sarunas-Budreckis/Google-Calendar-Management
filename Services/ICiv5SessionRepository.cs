using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ICiv5SessionRepository
{
    Task<IReadOnlyList<Civ5SessionPoint>> GetPointsForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetPointCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<HashSet<(DateTime FileModifiedAt, string GameMode)>> GetExistingDedupKeysAsync(IReadOnlyList<DateTime> candidates, CancellationToken ct = default);
    Task InsertPointsAsync(IReadOnlyList<Civ5SessionPoint> points, CancellationToken ct = default);
}

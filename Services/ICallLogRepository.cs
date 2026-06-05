using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ICallLogRepository
{
    Task<IReadOnlyList<CallLogEntry>> GetEntriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<HashSet<(DateTime Date, string? Number, int DurationSeconds)>> GetExistingDedupKeysAsync(CancellationToken ct = default);
}

using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ITogglSleepRepository
{
    Task<IReadOnlyList<TogglEntry>> GetSleepEntriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetSleepEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

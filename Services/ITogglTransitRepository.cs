using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ITogglTransitRepository
{
    Task<IReadOnlyList<TogglEntry>> GetTransitEntriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetTransitEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

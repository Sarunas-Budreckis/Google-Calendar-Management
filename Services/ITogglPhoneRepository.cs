using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ITogglPhoneRepository
{
    Task<IReadOnlyList<TogglEntry>> GetPhoneEntriesForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetPhoneEntryCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

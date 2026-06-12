using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IOutlookEventRepository
{
    Task<IReadOnlyList<OutlookEvent>> GetEventsForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetEventCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<bool> SetSuppressedAsync(string outlookEventId, bool suppressed, CancellationToken ct = default);
}

using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public interface IEventPickerService
{
    Task<EventPickerResult> GetCandidatesAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        string? searchText,
        CancellationToken ct = default);
}

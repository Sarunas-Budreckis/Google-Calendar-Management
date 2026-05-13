namespace GoogleCalendarManagement.Services;

public interface ITogglApiClient
{
    Task<bool> TestConnectionAsync(string apiToken, CancellationToken ct = default);

    Task<IReadOnlyList<TogglTimeEntryDto>> GetTimeEntriesAsync(
        string apiToken,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default);
}

namespace GoogleCalendarManagement.Services;

public interface IGoogleCalendarService
{
    Task<OperationResult<OAuthStatus>> AuthenticateAsync(CancellationToken ct = default);

    Task<OperationResult<bool>> IsAuthenticatedAsync();

    Task RevokeAndClearTokensAsync();

    Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchAllEventsAsync(
        string calendarId,
        DateTime start,
        DateTime end,
        IProgress<int>? progress = null,
        CancellationToken ct = default);

    Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchIncrementalEventsAsync(
        string calendarId,
        string syncToken,
        CancellationToken ct = default);
}

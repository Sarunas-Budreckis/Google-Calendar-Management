namespace GoogleCalendarManagement.Services;

public interface ISyncManager
{
    Task<SyncResult> SyncAsync(
        string calendarId = "primary",
        DateTime? rangeStart = null,
        DateTime? rangeEnd = null,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default);
}

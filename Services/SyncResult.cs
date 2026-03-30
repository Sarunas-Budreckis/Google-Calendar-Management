namespace GoogleCalendarManagement.Services;

public sealed record SyncResult(
    bool Success,
    int EventsAdded,
    int EventsUpdated,
    int EventsDeleted,
    string? NewSyncToken,
    string? ErrorMessage,
    bool WasCancelled = false);

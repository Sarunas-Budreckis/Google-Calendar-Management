namespace GoogleCalendarManagement.Services;

public sealed record SyncProgress(
    int PagesFetched,
    int EventsProcessed,
    string StatusMessage);

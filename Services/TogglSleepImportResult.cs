namespace GoogleCalendarManagement.Services;

public sealed record TogglSleepImportResult(
    bool Success,
    int RecordsFetched,
    string? ErrorMessage);

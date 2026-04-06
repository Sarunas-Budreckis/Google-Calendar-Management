namespace GoogleCalendarManagement.Services;

public sealed record ExportResult(
    bool Success,
    bool WasCancelled,
    int ExportedEventCount,
    string? FileName,
    string? ErrorMessage);

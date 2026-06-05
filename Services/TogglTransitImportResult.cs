namespace GoogleCalendarManagement.Services;

public sealed record TogglTransitImportResult(bool Success, int RecordsFetched, string? ErrorMessage);

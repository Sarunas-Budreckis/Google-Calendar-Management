namespace GoogleCalendarManagement.Services;

public sealed record SpotifyImportResult(bool Success, int RecordsFetched, string? ErrorMessage);

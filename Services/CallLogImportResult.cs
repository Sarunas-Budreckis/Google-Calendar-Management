namespace GoogleCalendarManagement.Services;

public sealed record CallLogImportResult(bool Success, int NewRecordsInserted, int DuplicatesSkipped, string? ErrorMessage);

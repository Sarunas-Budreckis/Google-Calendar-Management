namespace GoogleCalendarManagement.Services;

public record TogglCsvImportResult(bool Success, int Inserted, int Skipped, int Malformed, string? ErrorMessage);

public interface ITogglCsvImportService
{
    Task<TogglCsvImportResult> ImportFromStreamAsync(Stream stream, CancellationToken ct = default);
}

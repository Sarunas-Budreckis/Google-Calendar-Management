namespace GoogleCalendarManagement.Services;

public interface IIcsExportService
{
    Task<(DateOnly From, DateOnly To)?> GetStoredEventRangeAsync(CancellationToken ct = default);

    Task<ExportResult> ExportToFileAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

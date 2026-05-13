namespace GoogleCalendarManagement.Services;

public interface ITogglSleepImportService
{
    Task<TogglSleepImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}

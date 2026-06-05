namespace GoogleCalendarManagement.Services;

public interface ITogglTransitImportService
{
    Task<TogglTransitImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}

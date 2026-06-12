namespace GoogleCalendarManagement.Services;

public interface IOutlookImportService
{
    Task<OutlookImportResult> ImportAsync(string accessToken, DateOnly start, DateOnly end, CancellationToken ct = default);
}

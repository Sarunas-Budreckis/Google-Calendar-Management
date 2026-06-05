namespace GoogleCalendarManagement.Services;

public interface ICallLogImportService
{
    Task<CallLogImportResult> ImportFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default);
}

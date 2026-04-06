using Windows.Storage;

namespace GoogleCalendarManagement.Services;

public interface IIcsImportService
{
    Task<ImportResult> ImportFromFileAsync(StorageFile file, CancellationToken ct = default);
}

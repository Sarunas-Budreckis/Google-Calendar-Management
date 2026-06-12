namespace GoogleCalendarManagement.Services;

public interface IDataSourceImportHandler
{
    string SourceKey { get; }

    bool IsApiFetch => false;

    Task TriggerImportAsync(CancellationToken ct = default);
}

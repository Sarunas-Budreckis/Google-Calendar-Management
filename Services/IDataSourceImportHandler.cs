namespace GoogleCalendarManagement.Services;

public interface IDataSourceImportHandler
{
    string SourceKey { get; }

    Task TriggerImportAsync(CancellationToken ct = default);
}

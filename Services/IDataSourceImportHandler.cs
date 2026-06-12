namespace GoogleCalendarManagement.Services;

public interface IDataSourceImportHandler
{
    string SourceKey { get; }

    bool IsApiFetch => false;

    Task TriggerImportAsync(CancellationToken ct = default);

    // Returns this handler's data-point projector.
    // Default: null. Concrete handlers must override in Story 8.9.
    IDataPointProjector? GetProjector() => null;
}

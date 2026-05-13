using System.Collections.Concurrent;

namespace GoogleCalendarManagement.Services;

public sealed class DataSourceImportHandlerRegistry
{
    private readonly ConcurrentDictionary<string, IDataSourceImportHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    public void Register(IDataSourceImportHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (string.IsNullOrWhiteSpace(handler.SourceKey))
        {
            throw new ArgumentException("Import handlers must provide a source key.", nameof(handler));
        }

        _handlers[handler.SourceKey] = handler;
    }

    public IReadOnlyCollection<IDataSourceImportHandler> GetHandlers()
    {
        return _handlers.Values.ToList();
    }

    public bool HasHandler(string sourceKey)
    {
        return !string.IsNullOrWhiteSpace(sourceKey) && _handlers.ContainsKey(sourceKey);
    }

    public IDataSourceImportHandler? GetHandler(string sourceKey)
    {
        return string.IsNullOrWhiteSpace(sourceKey)
            ? null
            : _handlers.GetValueOrDefault(sourceKey);
    }
}

using System.Collections.Concurrent;

namespace GoogleCalendarManagement.Services;

public sealed class DataSourceImportHandlerRegistry
{
    private readonly ConcurrentDictionary<string, IDataSourceImportHandler> _handlers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, IDataSourceImportHandler> _csvHandlers =
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

    public void RegisterCsvHandler(string sourceKey, IDataSourceImportHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        _csvHandlers[sourceKey] = handler;
    }

    public IReadOnlyCollection<IDataSourceImportHandler> GetHandlers()
    {
        return _handlers.Values.ToList();
    }

    public bool HasHandler(string sourceKey)
    {
        if (string.Equals(sourceKey, "comfyui", StringComparison.OrdinalIgnoreCase))
        {
            sourceKey = ComfyUIFolderScannerService.SourceKey;
        }

        return !string.IsNullOrWhiteSpace(sourceKey) && _handlers.ContainsKey(sourceKey);
    }

    public IDataSourceImportHandler? GetHandler(string sourceKey)
    {
        if (string.Equals(sourceKey, "comfyui", StringComparison.OrdinalIgnoreCase))
        {
            sourceKey = ComfyUIFolderScannerService.SourceKey;
        }

        return string.IsNullOrWhiteSpace(sourceKey)
            ? null
            : _handlers.GetValueOrDefault(sourceKey);
    }

    public bool IsApiFetch(string sourceKey)
    {
        var handler = GetHandler(sourceKey);
        return handler?.IsApiFetch ?? false;
    }

    public bool HasCsvHandler(string sourceKey)
    {
        return !string.IsNullOrWhiteSpace(sourceKey) && _csvHandlers.ContainsKey(sourceKey);
    }

    public IDataSourceImportHandler? GetCsvHandler(string sourceKey)
    {
        return string.IsNullOrWhiteSpace(sourceKey)
            ? null
            : _csvHandlers.GetValueOrDefault(sourceKey);
    }
}

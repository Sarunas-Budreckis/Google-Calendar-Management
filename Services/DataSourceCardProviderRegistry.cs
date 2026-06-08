namespace GoogleCalendarManagement.Services;

public sealed class DataSourceCardProviderRegistry
{
    private readonly Dictionary<string, IDataSourceCardProvider> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IDataSourceCardProvider> Providers => _providers.Values;

    public void Register(IDataSourceCardProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.SourceKey))
        {
            throw new ArgumentException("Provider source key must be non-empty.", nameof(provider));
        }

        _providers[provider.SourceKey] = provider;
    }

    public IDataSourceCardProvider? GetProvider(string sourceKey)
    {
        if (string.Equals(sourceKey, "comfyui", StringComparison.OrdinalIgnoreCase))
        {
            sourceKey = ComfyUIFolderScannerService.SourceKey;
        }

        return _providers.TryGetValue(sourceKey, out var provider) ? provider : null;
    }
}

namespace GoogleCalendarManagement.Services.DataLinking;

public sealed class ClumpBlockProviderRegistry : IClumpBlockProviderRegistry
{
    private readonly Dictionary<string, IClumpBlockProvider> _byKey;

    public ClumpBlockProviderRegistry(IEnumerable<IClumpBlockProvider> providers)
    {
        _byKey = providers.ToDictionary(p => p.SourceKey, StringComparer.Ordinal);
        AllProviders = _byKey.Values.ToList();
    }

    public IClumpBlockProvider? GetProvider(string sourceKey) =>
        _byKey.TryGetValue(sourceKey, out var p) ? p : null;

    public IReadOnlyList<IClumpBlockProvider> AllProviders { get; }
}

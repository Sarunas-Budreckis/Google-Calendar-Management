using System.Collections.Concurrent;

namespace GoogleCalendarManagement.Services;

public class SourcePointerResolverRegistry : ISourcePointerResolverRegistry
{
    private readonly ConcurrentDictionary<string, ISourcePointerResolver> _resolvers = new();

    public void Register(ISourcePointerResolver resolver)
    {
        if (!_resolvers.TryAdd(resolver.SourceKey, resolver))
            throw new InvalidOperationException($"A resolver for source key '{resolver.SourceKey}' is already registered.");
    }

    public ISourcePointerResolver? GetResolver(string sourceKey)
    {
        return _resolvers.TryGetValue(sourceKey, out var resolver) ? resolver : null;
    }

    public async Task<string?> ResolveDisplayAsync(string sourceKey, string sourceRef, CancellationToken ct = default)
    {
        var resolver = GetResolver(sourceKey);
        if (resolver is null)
        {
            return null;
        }

        return await resolver.ResolveDisplayAsync(sourceRef, ct);
    }
}

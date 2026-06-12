namespace GoogleCalendarManagement.Services;

public interface ISourcePointerResolverRegistry
{
    void Register(ISourcePointerResolver resolver);
    ISourcePointerResolver? GetResolver(string sourceKey);
    Task<string?> ResolveDisplayAsync(string sourceKey, string sourceRef, CancellationToken ct = default);
}

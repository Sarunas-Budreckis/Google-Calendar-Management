namespace GoogleCalendarManagement.Services;

public interface ISourcePointerResolver
{
    string SourceKey { get; }
    Task<bool> ExistsAsync(string sourceRef, CancellationToken ct = default);
    Task<string?> ResolveDisplayAsync(string sourceRef, CancellationToken ct = default);
}

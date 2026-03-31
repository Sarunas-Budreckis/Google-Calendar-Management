namespace GoogleCalendarManagement.Services;

public interface ISystemStateRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Writes all key/value pairs atomically in a single transaction.</summary>
    Task SetManyAsync(IReadOnlyDictionary<string, string> pairs, CancellationToken ct = default);
}

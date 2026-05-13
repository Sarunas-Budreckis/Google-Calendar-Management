namespace GoogleCalendarManagement.Services;

public interface IConfigRepository
{
    Task<string?> GetConfigValueAsync(string key, CancellationToken ct = default);

    Task SetConfigValueAsync(
        string key,
        string? value,
        string? configType = null,
        string? description = null,
        bool encrypt = false,
        CancellationToken ct = default);
}

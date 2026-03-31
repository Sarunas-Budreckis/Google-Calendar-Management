namespace GoogleCalendarManagement.Services;

public interface ISystemStateRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);
}

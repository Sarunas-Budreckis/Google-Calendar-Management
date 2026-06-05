namespace GoogleCalendarManagement.Services;

public interface IStatsFmApiClient
{
    Task<string> TestConnectionAsync(string bearerToken, CancellationToken ct = default);
    Task<IReadOnlyList<StatsFmStreamItemDto>> GetStreamsAsync(string bearerToken, DateOnly start, DateOnly end, CancellationToken ct = default);
}

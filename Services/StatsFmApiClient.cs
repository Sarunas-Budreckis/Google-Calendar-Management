using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace GoogleCalendarManagement.Services;

public sealed class StatsFmApiClient : IStatsFmApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    private string? _cachedToken;
    private string? _cachedUserId;

    public StatsFmApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> TestConnectionAsync(string bearerToken, CancellationToken ct = default)
    {
        using var response = await SendWithRateLimitRetryAsync(
            () => CreateRequest(HttpMethod.Get, "api/v1/users/me", bearerToken), ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new StatsFmApiException(CreateFailureMessage(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<StatsFmMeDto>(stream, JsonOptions, ct);
        var userId = dto?.Item?.Id ?? throw new StatsFmApiException("stats.fm /users/me did not return a user ID.");

        _cachedToken = bearerToken;
        _cachedUserId = userId;

        return userId;
    }

    public async Task<IReadOnlyList<StatsFmStreamItemDto>> GetStreamsAsync(
        string bearerToken,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default)
    {
        var userId = await GetOrFetchUserIdAsync(bearerToken, ct);
        var afterUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var beforeUtc = DateTime.SpecifyKind(end.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
        var after = afterUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        var before = beforeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        const int limit = 500;
        var result = new List<StatsFmStreamItemDto>();
        string? cursor = null;

        while (true)
        {
            var url = $"api/v1/users/{Uri.EscapeDataString(userId)}/streams?after={Uri.EscapeDataString(after)}&before={Uri.EscapeDataString(before)}&limit={limit}";
            if (cursor is not null)
            {
                url += $"&before={Uri.EscapeDataString(cursor)}";
            }

            using var response = await SendWithRateLimitRetryAsync(
                () => CreateRequest(HttpMethod.Get, url, bearerToken), ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new StatsFmApiException(CreateFailureMessage(response));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var page = await JsonSerializer.DeserializeAsync<StatsFmStreamsResponseDto>(stream, JsonOptions, ct);
            var items = page?.Items ?? [];

            result.AddRange(items);

            if (items.Count < limit)
            {
                break;
            }

            // cursor pagination: fetch earlier streams by using the earliest endTime as new 'before'
            var earliest = items.MinBy(i => i.EndTime)?.EndTime;
            if (earliest is null || earliest == cursor)
            {
                break;
            }

            cursor = earliest;
        }

        return result;
    }

    private async Task<string> GetOrFetchUserIdAsync(string bearerToken, CancellationToken ct)
    {
        if (_cachedToken == bearerToken && _cachedUserId is not null)
        {
            return _cachedUserId;
        }

        return await TestConnectionAsync(bearerToken, ct);
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        using var firstRequest = requestFactory();
        var response = await _httpClient.SendAsync(firstRequest, ct);
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var retryDelay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
        response.Dispose();

        await Task.Delay(retryDelay, ct);
        using var retryRequest = requestFactory();
        return await _httpClient.SendAsync(retryRequest, ct);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string bearerToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return request;
    }

    private static string CreateFailureMessage(HttpResponseMessage response) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "stats.fm API rejected the configured token.",
            HttpStatusCode.Forbidden => "stats.fm API denied access for the configured token.",
            HttpStatusCode.TooManyRequests => "stats.fm API rate limit exceeded. Try again later.",
            _ => $"stats.fm API request failed with status {(int)response.StatusCode} {response.ReasonPhrase}."
        };
}

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class StatsFmApiClient : IStatsFmApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ILogger<StatsFmApiClient> _logger;

    private string? _cachedToken;
    private string? _cachedUserId;

    public StatsFmApiClient(HttpClient httpClient, ILogger<StatsFmApiClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StatsFmApiClient>.Instance;
    }

    public async Task<string> TestConnectionAsync(string bearerToken, CancellationToken ct = default)
    {
        var normalizedToken = NormalizeBearerToken(bearerToken);
        using var response = await SendWithRateLimitRetryAsync(
            () => CreateRequest(HttpMethod.Get, "api/v1/users/me", normalizedToken), ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new StatsFmApiException(CreateFailureMessage(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<StatsFmMeDto>(stream, JsonOptions, ct);
        var userId = dto?.Item?.Id ?? throw new StatsFmApiException("stats.fm /users/me did not return a user ID.");

        _cachedToken = normalizedToken;
        _cachedUserId = userId;

        return userId;
    }

    public async Task<IReadOnlyList<StatsFmStreamItemDto>> GetStreamsAsync(
        string bearerToken,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default)
    {
        var normalizedToken = NormalizeBearerToken(bearerToken);
        var userId = await GetOrFetchUserIdAsync(normalizedToken, ct);
        var after = ToUnixMilliseconds(start.ToDateTime(TimeOnly.MinValue));
        var before = ToUnixMilliseconds(end.AddDays(1).ToDateTime(TimeOnly.MinValue));

        _logger.LogInformation(
            "Fetching stats.fm streams for user {UserId} from {StartDate} through {EndDate} using Unix millisecond range {After}-{Before}.",
            userId, start, end, after, before);

        const int limit = 500;
        var result = new List<StatsFmStreamItemDto>();
        long? cursor = null;
        var pageNumber = 0;

        while (true)
        {
            var beforeParam = cursor ?? before;
            var url = $"api/v1/users/{Uri.EscapeDataString(userId)}/streams?after={after}&before={beforeParam}&limit={limit}";

            using var response = await SendWithRateLimitRetryAsync(
                () => CreateRequest(HttpMethod.Get, url, normalizedToken), ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new StatsFmApiException(CreateFailureMessage(response));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var page = await JsonSerializer.DeserializeAsync<StatsFmStreamsResponseDto>(stream, JsonOptions, ct);
            var items = page?.Items ?? [];

            pageNumber++;
            _logger.LogInformation(
                "stats.fm streams page {PageNumber} returned {Count} item(s). Before cursor: {Before}.",
                pageNumber, items.Count, beforeParam);

            result.AddRange(items);

            if (items.Count < limit)
            {
                break;
            }

            // cursor pagination: fetch earlier streams by using the earliest endTime as new 'before'
            var earliest = items.MinBy(i => i.EndTime)?.EndTime;
            if (!TryParseEndTime(earliest, out var earliestOffset))
            {
                _logger.LogWarning(
                    "Stopping stats.fm pagination because earliest endTime could not be parsed. Value: {EndTime}",
                    earliest);
                break;
            }

            var nextCursor = earliestOffset.AddMilliseconds(-1).ToUnixTimeMilliseconds();
            if (nextCursor == cursor)
            {
                break;
            }

            cursor = nextCursor;
            await Task.Delay(200, ct); // conservative inter-page throttle
        }

        var enriched = await HydrateFlatStreamTracksAsync(result, normalizedToken, ct);
        _logger.LogInformation("Fetched {Count} stats.fm stream item(s) after metadata hydration.", enriched.Count);
        return enriched;
    }

    private async Task<string> GetOrFetchUserIdAsync(string bearerToken, CancellationToken ct)
    {
        var normalizedToken = NormalizeBearerToken(bearerToken);
        if (_cachedToken == normalizedToken && _cachedUserId is not null)
        {
            return _cachedUserId;
        }

        return await TestConnectionAsync(normalizedToken, ct);
    }

    private async Task<IReadOnlyList<StatsFmStreamItemDto>> HydrateFlatStreamTracksAsync(
        IReadOnlyList<StatsFmStreamItemDto> items, string bearerToken, CancellationToken ct)
    {
        var trackIds = items
            .Where(i => i.Track is null && i.TrackId.HasValue)
            .Select(i => i.TrackId!.Value)
            .Distinct()
            .ToArray();

        if (trackIds.Length == 0)
        {
            return items;
        }

        var tracksById = new Dictionary<int, StatsFmStreamTrackDto>();
        foreach (var chunk in trackIds.Chunk(50))
        {
            var ids = string.Join(",", chunk);
            using var response = await SendWithRateLimitRetryAsync(
                () => CreateRequest(HttpMethod.Get, $"api/v1/tracks?ids={Uri.EscapeDataString(ids)}", bearerToken), ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "stats.fm track metadata lookup failed with status {StatusCode}. Falling back to flat stream track names.",
                    (int)response.StatusCode);
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var page = await JsonSerializer.DeserializeAsync<StatsFmTracksResponseDto>(stream, JsonOptions, ct);
            foreach (var track in page?.Items ?? [])
            {
                if (track.Id.HasValue)
                {
                    tracksById[track.Id.Value] = track;
                }
            }
        }

        _logger.LogInformation(
            "Hydrated {HydratedCount} of {RequestedCount} stats.fm track metadata record(s).",
            tracksById.Count, trackIds.Length);

        return items
            .Select(i => i.Track is not null || i.TrackId is null || !tracksById.TryGetValue(i.TrackId.Value, out var track)
                ? i
                : i with { Track = track })
            .ToArray();
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        const int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            var response = await _httpClient.SendAsync(request, ct);
            if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt >= maxRetries)
            {
                return response;
            }

            var waitTime = response.Headers.RetryAfter?.Delta is { } d && d > delay ? d : delay;
            response.Dispose();
            await Task.Delay(waitTime, ct);
            delay += delay; // 1s → 2s → 4s
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string bearerToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", NormalizeBearerToken(bearerToken));
        return request;
    }

    private static long ToUnixMilliseconds(DateTime localDateTime)
    {
        var local = DateTime.SpecifyKind(localDateTime, DateTimeKind.Local);
        return new DateTimeOffset(local).ToUnixTimeMilliseconds();
    }

    private static bool TryParseEndTime(string? endTime, out DateTimeOffset value)
    {
        return DateTimeOffset.TryParse(
            endTime,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
    }

    private static string NormalizeBearerToken(string bearerToken)
    {
        const string prefix = "Bearer ";
        var trimmed = bearerToken.Trim();
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..].Trim()
            : trimmed;
    }

    private static string CreateFailureMessage(HttpResponseMessage response) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "stats.fm API rejected the configured token.",
            HttpStatusCode.Forbidden => "stats.fm API denied access for the configured token.",
            HttpStatusCode.TooManyRequests => "stats.fm API rate limit exceeded after retries. Try again later.",
            _ => $"stats.fm API request failed with status {(int)response.StatusCode} {response.ReasonPhrase}."
        };
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace GoogleCalendarManagement.Services;

public sealed class TogglApiClient : ITogglApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    private string? _cachedWorkspaceToken;
    private int _cachedWorkspaceId;

    public TogglApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TestConnectionAsync(string apiToken, CancellationToken ct = default)
    {
        using var response = await SendWithRateLimitRetryAsync(
            () => CreateGetRequest("api/v9/me", apiToken), ct);

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        throw new TogglApiException(CreateFailureMessage(response));
    }

    public async Task<IReadOnlyList<TogglTimeEntryDto>> GetTimeEntriesAsync(
        string apiToken,
        DateOnly start,
        DateOnly end,
        CancellationToken ct = default)
    {
        var workspaceId = await GetOrFetchWorkspaceIdAsync(apiToken, ct);
        var inclusiveEnd = end.AddDays(1);
        var reportsUrl = $"https://api.track.toggl.com/reports/api/v3/workspace/{workspaceId}/search/time_entries";

        var result = new List<TogglTimeEntryDto>();
        int? firstRowNumber = null;

        while (true)
        {
            var requestBody = new TogglReportsSearchRequestDto(
                StartDate: start.ToString("yyyy-MM-dd"),
                EndDate: inclusiveEnd.ToString("yyyy-MM-dd"),
                OrderBy: "date",
                OrderDir: "ASC",
                FirstRowNumber: firstRowNumber);

            using var response = await SendWithRateLimitRetryAsync(
                () =>
                {
                    var req = CreateGetRequest(reportsUrl, apiToken);
                    req.Method = HttpMethod.Post;
                    req.Content = JsonContent.Create(requestBody, options: JsonOptions);
                    return req;
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new TogglApiException(CreateFailureMessage(response));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var page = await JsonSerializer.DeserializeAsync<List<TogglReportsTimeEntryDto>>(stream, JsonOptions, ct) ?? [];
            result.AddRange(page.Select(MapToDto));

            if (response.Headers.TryGetValues("X-Next-Row-Number", out var headerValues))
            {
                var headerValue = headerValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(headerValue) && int.TryParse(headerValue, out var nextRow))
                {
                    firstRowNumber = nextRow;
                    continue;
                }
            }

            break;
        }

        return result;
    }

    private async Task<int> GetOrFetchWorkspaceIdAsync(string apiToken, CancellationToken ct)
    {
        if (_cachedWorkspaceToken == apiToken && _cachedWorkspaceId != 0)
        {
            return _cachedWorkspaceId;
        }

        using var response = await SendWithRateLimitRetryAsync(
            () => CreateGetRequest("api/v9/me", apiToken), ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new TogglApiException("Failed to fetch Toggl workspace ID: " + CreateFailureMessage(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!doc.RootElement.TryGetProperty("default_workspace_id", out var wsProp) || wsProp.ValueKind != JsonValueKind.Number)
        {
            throw new TogglApiException("Toggl /me response did not include a default_workspace_id.");
        }

        var workspaceId = wsProp.GetInt32();
        if (workspaceId == 0)
        {
            throw new TogglApiException("Toggl /me returned a zero workspace ID.");
        }

        _cachedWorkspaceToken = apiToken;
        _cachedWorkspaceId = workspaceId;
        return workspaceId;
    }

    private static TogglTimeEntryDto MapToDto(TogglReportsTimeEntryDto r) =>
        new(
            Id: r.Id,
            Description: r.Description,
            Start: r.Start,
            Stop: r.Stop,
            Duration: (int)(r.Dur / 1000),
            ProjectId: r.Pid,
            ProjectName: r.Project,
            Tags: r.Tags);

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

    private static HttpRequestMessage CreateGetRequest(string url, string apiToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiToken}:api_token"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static string CreateFailureMessage(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Toggl API rejected the configured token.",
            HttpStatusCode.Forbidden => "Toggl API denied access for the configured token.",
            HttpStatusCode.TooManyRequests => "Toggl API rate limit was exceeded. Try again later.",
            _ => $"Toggl API request failed with status {(int)response.StatusCode} {response.ReasonPhrase}."
        };
    }
}

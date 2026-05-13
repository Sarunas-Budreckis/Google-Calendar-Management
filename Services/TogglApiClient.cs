using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GoogleCalendarManagement.Services;

public sealed class TogglApiClient : ITogglApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public TogglApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TestConnectionAsync(string apiToken, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "me", apiToken);
        using var response = await SendWithRateLimitRetryAsync(request, ct);
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
        var inclusiveEnd = end.AddDays(1);
        var relativeUrl =
            $"me/time_entries?start_date={Uri.EscapeDataString(start.ToString("yyyy-MM-dd"))}&end_date={Uri.EscapeDataString(inclusiveEnd.ToString("yyyy-MM-dd"))}";
        using var request = CreateRequest(HttpMethod.Get, relativeUrl, apiToken);
        using var response = await SendWithRateLimitRetryAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new TogglApiException(CreateFailureMessage(response));
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<List<TogglTimeEntryDto>>(stream, JsonOptions, ct) ?? [];
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.TooManyRequests)
        {
            return response;
        }

        var retryDelay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
        response.Dispose();

        await Task.Delay(retryDelay, ct);
        using var retryRequest = CloneRequest(request);
        return await _httpClient.SendAsync(retryRequest, ct);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, string apiToken)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiToken}:api_token"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
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

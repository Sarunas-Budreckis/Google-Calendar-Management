using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoogleCalendarManagement.Services;

public sealed class GraphApiClient : IGraphApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public GraphApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<GraphEventDto>> GetCalendarViewAsync(
        string accessToken,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        // calendarView end is exclusive; add one day so endDate is included
        var start = startDate.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss");
        var end = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-ddTHH:mm:ss");
        var url = $"v1.0/me/calendarView?startDateTime={start}&endDateTime={end}" +
                  "&$top=500&$select=id,subject,start,end,isAllDay,organizer,location,bodyPreview,type,seriesMasterId";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new GraphApiException($"Graph API returned {(int)response.StatusCode}: {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var page = JsonSerializer.Deserialize<GraphCalendarViewPage>(json, JsonOptions)
                   ?? throw new GraphApiException("Unexpected null response from Graph API.");

        return page.Value ?? [];
    }

    private sealed class GraphCalendarViewPage
    {
        [JsonPropertyName("value")]
        public List<GraphEventDto>? Value { get; set; }
    }
}

public sealed class GraphApiException : Exception
{
    public GraphApiException(string message) : base(message) { }
}

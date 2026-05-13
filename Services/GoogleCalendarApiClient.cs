using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Google.Apis.Json;

namespace GoogleCalendarManagement.Services;

public interface IGoogleCalendarApiClientFactory
{
    Task<IGoogleCalendarApiClient> CreateAsync(UserCredential credential, CancellationToken ct = default);
}

public interface IGoogleCalendarApiClient
{
    Task<GoogleCalendarEventPage> ListEventsAsync(
        string calendarId,
        DateTime startUtc,
        DateTime endUtc,
        string? pageToken,
        CancellationToken ct = default);

    Task<Event> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default);

    Task<Event> InsertEventAsync(
        string calendarId,
        Event eventPayload,
        CancellationToken ct = default);

    Task<Event> UpdateEventAsync(
        string calendarId,
        string eventId,
        Event eventPayload,
        string? ifMatchEtag,
        CancellationToken ct = default);

    Task DeleteEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default);
}

public sealed record GoogleCalendarEventPage(
    IReadOnlyList<Event> Items,
    string? NextPageToken,
    string? NextSyncToken);

public sealed class GoogleCalendarApiClientFactory : IGoogleCalendarApiClientFactory
{
    public Task<IGoogleCalendarApiClient> CreateAsync(UserCredential credential, CancellationToken ct = default)
    {
        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Google Calendar Management"
        });

        return Task.FromResult<IGoogleCalendarApiClient>(new GoogleCalendarApiClient(service));
    }
}

public sealed class GoogleCalendarApiClient : IGoogleCalendarApiClient
{
    private readonly CalendarService _calendarService;

    public GoogleCalendarApiClient(CalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    public async Task<GoogleCalendarEventPage> ListEventsAsync(
        string calendarId,
        DateTime startUtc,
        DateTime endUtc,
        string? pageToken,
        CancellationToken ct = default)
    {
        var request = _calendarService.Events.List(calendarId);
        request.TimeMinDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc));
        request.TimeMaxDateTimeOffset = new DateTimeOffset(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc));
        request.SingleEvents = true;
        request.ShowDeleted = true;
        request.MaxResults = 250;
        request.PageToken = pageToken;

        var response = await request.ExecuteAsync(ct);
        return new GoogleCalendarEventPage(
            response.Items?.ToList() ?? [],
            response.NextPageToken,
            response.NextSyncToken);
    }

    public async Task<Event> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default)
    {
        var request = _calendarService.Events.Get(calendarId, eventId);
        return await request.ExecuteAsync(ct);
    }

    public async Task<Event> InsertEventAsync(
        string calendarId,
        Event eventPayload,
        CancellationToken ct = default)
    {
        var request = _calendarService.Events.Insert(eventPayload, calendarId);
        return await request.ExecuteAsync(ct);
    }

    public async Task<Event> UpdateEventAsync(
        string calendarId,
        string eventId,
        Event eventPayload,
        string? ifMatchEtag,
        CancellationToken ct = default)
    {
        var requestUri = new Uri(
            $"https://www.googleapis.com/calendar/v3/calendars/{Uri.EscapeDataString(calendarId)}/events/{Uri.EscapeDataString(eventId)}");

        var serializer = NewtonsoftJsonSerializer.Instance;
        using var request = new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = new StringContent(
                serializer.Serialize(eventPayload),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(ifMatchEtag))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatchEtag);
        }

        var response = await _calendarService.HttpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new GoogleApiException(
                "calendar",
                "Conditional update failed.")
            {
                HttpStatusCode = HttpStatusCode.PreconditionFailed
            };
        }

        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        var updatedEvent = serializer.Deserialize<Event>(responseBody);
        if (updatedEvent is null)
        {
            throw new InvalidOperationException("Google Calendar update returned an empty response.");
        }

        return updatedEvent;
    }

    public async Task DeleteEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default)
    {
        var request = _calendarService.Events.Delete(calendarId, eventId);
        await request.ExecuteAsync(ct);
    }
}

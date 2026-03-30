using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

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
}

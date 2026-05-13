using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class GoogleCalendarService : IGoogleCalendarService
{
    private static readonly string[] Scopes = [CalendarService.Scope.Calendar];
    private readonly ITokenStorageService _tokenStorage;
    private readonly IGoogleAuthorizationBroker _authorizationBroker;
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly GoogleCalendarOptions _options;
    private readonly IGoogleCalendarApiClientFactory _apiClientFactory;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        ITokenStorageService tokenStorage,
        IGoogleAuthorizationBroker authorizationBroker,
        IDbContextFactory<CalendarDbContext> contextFactory,
        GoogleCalendarOptions options,
        ILogger<GoogleCalendarService> logger)
        : this(
            tokenStorage,
            authorizationBroker,
            contextFactory,
            options,
            new GoogleCalendarApiClientFactory(),
            logger)
    {
    }

    public GoogleCalendarService(
        ITokenStorageService tokenStorage,
        IGoogleAuthorizationBroker authorizationBroker,
        IDbContextFactory<CalendarDbContext> contextFactory,
        GoogleCalendarOptions options,
        IGoogleCalendarApiClientFactory apiClientFactory,
        ILogger<GoogleCalendarService> logger)
    {
        _tokenStorage = tokenStorage;
        _authorizationBroker = authorizationBroker;
        _contextFactory = contextFactory;
        _options = options;
        _apiClientFactory = apiClientFactory;
        _logger = logger;
    }

    public async Task<OperationResult<OAuthStatus>> AuthenticateAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_options.ClientSecretPath))
        {
            return OperationResult<OAuthStatus>.Failure(
                "client_secret.json not found. See README for setup instructions.");
        }

        _logger.LogInformation("Google Calendar auth initiated");

        try
        {
            var secrets = GoogleClientSecrets.FromFile(_options.ClientSecretPath).Secrets;
            var credential = await _authorizationBroker.AuthorizeAsync(secrets, Scopes, ct);
            await _tokenStorage.StoreTokenAsync(credential.Token);
            await WriteAuditLogAsync("gcal_auth", success: true, errorMessage: null);

            var accountEmail = ExtractEmailAddress(credential.Token.IdToken) ?? "unknown";
            _logger.LogInformation("Google Calendar auth succeeded for {AccountEmail}", accountEmail);

            return OperationResult<OAuthStatus>.Ok(OAuthStatus.Authenticated);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<OAuthStatus>.Failure("Authentication cancelled by user.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar auth failed: {Error}", ex.Message);
            return OperationResult<OAuthStatus>.Failure("Unable to reach Google. Check internet connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar auth failed: {Error}", ex.Message);
            return OperationResult<OAuthStatus>.Failure(
                "Unable to connect Google Calendar. Check your credentials and try again.");
        }
    }

    public async Task<OperationResult<bool>> IsAuthenticatedAsync()
    {
        try
        {
            var token = await _tokenStorage.LoadTokenAsync();
            if (token is null)
            {
                return OperationResult<bool>.Ok(false);
            }

            var hasRefreshToken = !string.IsNullOrWhiteSpace(token.RefreshToken);
            var hasValidAccessToken = !token.IsStale;
            return OperationResult<bool>.Ok(hasRefreshToken || hasValidAccessToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar authentication status check failed.");
            return OperationResult<bool>.Failure("Unable to determine Google Calendar connection status.");
        }
    }

    public async Task RevokeAndClearTokensAsync()
    {
        var token = await _tokenStorage.LoadTokenAsync();

        try
        {
            if (token is not null && File.Exists(_options.ClientSecretPath))
            {
                var credential = CreateUserCredential(token);
                await credential.RevokeTokenAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke Google Calendar token before clearing local credentials.");
        }
        finally
        {
            await _tokenStorage.ClearTokenAsync();
            await WriteAuditLogAsync("gcal_revoke", success: true, errorMessage: null);
            _logger.LogInformation("Google Calendar tokens cleared.");
        }
    }

    public Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchAllEventsAsync(
        string calendarId,
        DateTime start,
        DateTime end,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
        => FetchAllEventsInternalAsync(calendarId, start, end, progress, ct);

    public Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchIncrementalEventsAsync(
        string calendarId,
        string syncToken,
        CancellationToken ct = default)
    {
        return Task.FromResult(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(
            "Incremental sync is not implemented yet."));
    }

    public async Task<OperationResult<GcalEventDto>> GetEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default)
    {
        var apiClientResult = await CreateAuthenticatedApiClientAsync(ct);
        if (!apiClientResult.Success)
        {
            return OperationResult<GcalEventDto>.Failure(apiClientResult.ErrorMessage!);
        }

        try
        {
            var googleEvent = await apiClientResult.ApiClient!.GetEventAsync(calendarId, eventId, ct);
            return OperationResult<GcalEventDto>.Ok(MapEvent(calendarId, googleEvent));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar get event failed due to network error.");
            return OperationResult<GcalEventDto>.Failure("Unable to reach Google. Check internet connection.");
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "Google Calendar API returned an error while getting event {EventId}.", eventId);
            return OperationResult<GcalEventDto>.Failure(GetFriendlyWriteApiErrorMessage(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar get event failed unexpectedly for {EventId}.", eventId);
            return OperationResult<GcalEventDto>.Failure("Unable to load the latest Google Calendar event.");
        }
    }

    public async Task<GoogleCalendarWriteResult> InsertEventAsync(
        GoogleCalendarWriteRequest request,
        CancellationToken ct = default)
    {
        var apiClientResult = await CreateAuthenticatedApiClientAsync(ct);
        if (!apiClientResult.Success)
        {
            return GoogleCalendarWriteResult.Failure(apiClientResult.ErrorMessage!);
        }

        try
        {
            var googleEvent = await apiClientResult.ApiClient!.InsertEventAsync(
                request.CalendarId,
                BuildEventPayload(request),
                ct);
            return GoogleCalendarWriteResult.Ok(MapEvent(request.CalendarId, googleEvent));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar insert failed due to network error.");
            return GoogleCalendarWriteResult.Failure(
                "Unable to reach Google. Check internet connection.",
                GoogleCalendarWriteFailureKind.Network,
                ex.ToString());
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "Google Calendar API returned an error while inserting an event.");
            return GoogleCalendarWriteResult.Failure(
                GetFriendlyWriteApiErrorMessage(ex),
                MapWriteFailureKind(ex),
                ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar insert failed unexpectedly.");
            return GoogleCalendarWriteResult.Failure(
                "Unable to publish the event to Google Calendar.",
                errorDetails: ex.ToString());
        }
    }

    public async Task<GoogleCalendarWriteResult> UpdateEventAsync(
        string eventId,
        GoogleCalendarWriteRequest request,
        string? ifMatchEtag,
        CancellationToken ct = default)
    {
        var apiClientResult = await CreateAuthenticatedApiClientAsync(ct);
        if (!apiClientResult.Success)
        {
            return GoogleCalendarWriteResult.Failure(apiClientResult.ErrorMessage!);
        }

        try
        {
            var googleEvent = await apiClientResult.ApiClient!.UpdateEventAsync(
                request.CalendarId,
                eventId,
                BuildEventPayload(request),
                ifMatchEtag,
                ct);
            return GoogleCalendarWriteResult.Ok(MapEvent(request.CalendarId, googleEvent));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar update failed due to network error for {EventId}.", eventId);
            return GoogleCalendarWriteResult.Failure(
                "Unable to reach Google. Check internet connection.",
                GoogleCalendarWriteFailureKind.Network,
                ex.ToString());
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "Google Calendar API returned an error while updating event {EventId}.", eventId);
            return GoogleCalendarWriteResult.Failure(
                GetFriendlyWriteApiErrorMessage(ex),
                MapWriteFailureKind(ex),
                ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar update failed unexpectedly for {EventId}.", eventId);
            return GoogleCalendarWriteResult.Failure(
                "Unable to publish the event to Google Calendar.",
                errorDetails: ex.ToString());
        }
    }

    public async Task<GoogleCalendarDeleteResult> DeleteEventAsync(
        string calendarId,
        string eventId,
        CancellationToken ct = default)
    {
        var apiClientResult = await CreateAuthenticatedApiClientAsync(ct);
        if (!apiClientResult.Success)
        {
            return GoogleCalendarDeleteResult.Failure(apiClientResult.ErrorMessage!);
        }

        try
        {
            await apiClientResult.ApiClient!.DeleteEventAsync(calendarId, eventId, ct);
            return GoogleCalendarDeleteResult.Ok();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar delete failed due to network error for {EventId}.", eventId);
            return GoogleCalendarDeleteResult.Failure(
                "Unable to reach Google. Check internet connection.",
                ex.ToString());
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "Google Calendar API returned an error while deleting event {EventId}.", eventId);
            return GoogleCalendarDeleteResult.Failure(
                GetFriendlyWriteApiErrorMessage(ex),
                ex.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar delete failed unexpectedly for {EventId}.", eventId);
            return GoogleCalendarDeleteResult.Failure(
                "Unable to delete the event from Google Calendar.",
                ex.ToString());
        }
    }

    private UserCredential CreateUserCredential(TokenResponse tokenResponse)
    {
        var clientSecrets = GoogleClientSecrets.FromFile(_options.ClientSecretPath).Secrets;
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = clientSecrets,
            Scopes = Scopes,
            DataStore = new NullDataStore()
        });

        return _authorizationBroker.CreateUserCredential(flow, "user", tokenResponse);
    }

    private async Task<AuthenticatedApiClientResult> CreateAuthenticatedApiClientAsync(CancellationToken ct)
    {
        if (!File.Exists(_options.ClientSecretPath))
        {
            return AuthenticatedApiClientResult.Failure(
                "client_secret.json not found. See README for setup instructions.");
        }

        var token = await _tokenStorage.LoadTokenAsync();
        if (token is null)
        {
            return AuthenticatedApiClientResult.Failure(
                "Google Calendar is not connected. Connect your account and try again.");
        }

        var credential = CreateUserCredential(token);
        var apiClient = await _apiClientFactory.CreateAsync(credential, ct);
        return AuthenticatedApiClientResult.Ok(apiClient);
    }

    private async Task WriteAuditLogAsync(string operationType, bool success, string? errorMessage)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            OperationType = operationType,
            UserAction = true,
            Success = success,
            ErrorMessage = errorMessage
        });

        await context.SaveChangesAsync();
    }

    private async Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchAllEventsInternalAsync(
        string calendarId,
        DateTime start,
        DateTime end,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        if (!File.Exists(_options.ClientSecretPath))
        {
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(
                "client_secret.json not found. See README for setup instructions.");
        }

        var token = await _tokenStorage.LoadTokenAsync();
        if (token is null)
        {
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(
                "Google Calendar is not connected. Connect your account and try again.");
        }

        var mappedEvents = new FetchAllEventsResultList();
        string? nextPageToken = null;
        string? nextSyncToken = null;
        var pagesFetched = 0;

        try
        {
            var credential = CreateUserCredential(token);
            var apiClient = await _apiClientFactory.CreateAsync(credential, ct);

            do
            {
                ct.ThrowIfCancellationRequested();

                var page = await apiClient.ListEventsAsync(
                    calendarId,
                    DateTime.SpecifyKind(start, DateTimeKind.Utc),
                    DateTime.SpecifyKind(end, DateTimeKind.Utc),
                    nextPageToken,
                    ct);

                pagesFetched++;
                progress?.Report(pagesFetched);
                mappedEvents.AddRange(page.Items.Select(item => MapEvent(calendarId, item)));

                nextPageToken = page.NextPageToken;
                nextSyncToken = page.NextSyncToken ?? nextSyncToken;
            }
            while (!string.IsNullOrWhiteSpace(nextPageToken));

            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((mappedEvents, nextSyncToken));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Google Calendar fetch cancelled by user after {PageCount} page(s).", pagesFetched);
            var partialEvents = new FetchAllEventsResultList
            {
                WasCancelled = true
            };
            partialEvents.AddRange(mappedEvents);
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                partialEvents,
                null));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Calendar fetch failed due to network error.");
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(
                "Unable to reach Google. Check internet connection.");
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "Google Calendar API returned an error while fetching events.");
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(GetFriendlyApiErrorMessage(ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Calendar fetch failed unexpectedly.");
            return OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Failure(
                "Unable to fetch events from Google Calendar.");
        }
    }

    private static GcalEventDto MapEvent(string calendarId, Event googleEvent)
    {
        var isAllDay = !string.IsNullOrWhiteSpace(googleEvent.Start?.Date);
        var (startUtc, endUtc) = GetEventBoundsUtc(googleEvent, isAllDay);

        return new GcalEventDto(
            googleEvent.Id ?? string.Empty,
            calendarId,
            googleEvent.Summary,
            googleEvent.Description,
            startUtc,
            endUtc,
            isAllDay,
            googleEvent.ColorId,
            googleEvent.ETag,
            googleEvent.UpdatedDateTimeOffset?.UtcDateTime,
            string.Equals(googleEvent.Status, "cancelled", StringComparison.OrdinalIgnoreCase),
            googleEvent.RecurringEventId,
            !string.IsNullOrWhiteSpace(googleEvent.RecurringEventId));
    }

    private static (DateTime? StartUtc, DateTime? EndUtc) GetEventBoundsUtc(Event googleEvent, bool isAllDay)
    {
        if (isAllDay)
        {
            var startDate = ParseAllDayDate(googleEvent.Start?.Date);
            var endDate = ParseAllDayDate(googleEvent.End?.Date);

            if (startDate is null)
            {
                return (null, null);
            }

            return (startDate, endDate ?? startDate.Value.AddDays(1));
        }

        return (
            googleEvent.Start?.DateTimeDateTimeOffset?.UtcDateTime,
            googleEvent.End?.DateTimeDateTimeOffset?.UtcDateTime);
    }

    private static DateTime? ParseAllDayDate(string? rawDate)
    {
        if (!DateOnly.TryParse(rawDate, out var parsedDate))
        {
            return null;
        }

        return parsedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    }

    private static string GetFriendlyApiErrorMessage(GoogleApiException ex)
    {
        if (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            return "Session expired. Please reconnect Google Calendar.";
        }

        if (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
            ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Calendar quota exceeded. Try again later.";
        }

        return "Unable to fetch events from Google Calendar.";
    }

    private static string GetFriendlyWriteApiErrorMessage(GoogleApiException ex)
    {
        if (ex.HttpStatusCode == HttpStatusCode.Unauthorized)
        {
            return "Session expired. Please reconnect Google Calendar.";
        }

        if (ex.HttpStatusCode == HttpStatusCode.PreconditionFailed)
        {
            return "The Google Calendar event changed before this publish completed.";
        }

        if (ex.HttpStatusCode == HttpStatusCode.Forbidden &&
            ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Calendar quota exceeded. Try again later.";
        }

        return "Unable to write the event to Google Calendar.";
    }

    private static GoogleCalendarWriteFailureKind MapWriteFailureKind(GoogleApiException ex)
    {
        return ex.HttpStatusCode switch
        {
            HttpStatusCode.Unauthorized => GoogleCalendarWriteFailureKind.Authentication,
            HttpStatusCode.PreconditionFailed => GoogleCalendarWriteFailureKind.PreconditionFailed,
            _ => GoogleCalendarWriteFailureKind.Unknown
        };
    }

    private static Event BuildEventPayload(GoogleCalendarWriteRequest request)
    {
        var eventPayload = new Event
        {
            Summary = request.Summary,
            Description = request.Description,
            ColorId = request.ColorId
        };

        if (request.IsAllDay)
        {
            var startDate = NormalizeAllDayDate(request.StartDateTimeUtc);
            var endDateExclusive = NormalizeAllDayEndDate(request.StartDateTimeUtc, request.EndDateTimeUtc);
            eventPayload.Start = new EventDateTime { Date = startDate.ToString("yyyy-MM-dd") };
            eventPayload.End = new EventDateTime { Date = endDateExclusive.ToString("yyyy-MM-dd") };
            return eventPayload;
        }

        eventPayload.Start = new EventDateTime
        {
            DateTimeDateTimeOffset = ToUtcOffset(request.StartDateTimeUtc)
        };
        eventPayload.End = new EventDateTime
        {
            DateTimeDateTimeOffset = ToUtcOffset(request.EndDateTimeUtc ?? request.StartDateTimeUtc)
        };
        return eventPayload;
    }

    private static DateOnly NormalizeAllDayDate(DateTime? utcValue)
    {
        var effectiveUtc = DateTime.SpecifyKind(utcValue ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
        return DateOnly.FromDateTime(effectiveUtc.Date);
    }

    private static DateOnly NormalizeAllDayEndDate(DateTime? startUtc, DateTime? endUtc)
    {
        var normalizedStart = NormalizeAllDayDate(startUtc);
        var normalizedEnd = endUtc.HasValue
            ? DateOnly.FromDateTime(DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc).Date)
            : normalizedStart.AddDays(1);

        return normalizedEnd <= normalizedStart
            ? normalizedStart.AddDays(1)
            : normalizedEnd;
    }

    private static DateTimeOffset? ToUtcOffset(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalizedUtc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(normalizedUtc);
    }

    private static string? ExtractEmailAddress(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        var segments = idToken.Split('.');
        if (segments.Length < 2)
        {
            return null;
        }

        var payload = segments[1]
            .Replace('-', '+')
            .Replace('_', '/');

        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("email", out var emailElement)
                ? emailElement.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed record AuthenticatedApiClientResult(
        bool Success,
        IGoogleCalendarApiClient? ApiClient,
        string? ErrorMessage)
    {
        public static AuthenticatedApiClientResult Ok(IGoogleCalendarApiClient apiClient)
            => new(true, apiClient, null);

        public static AuthenticatedApiClientResult Failure(string message)
            => new(false, null, message);
    }
}

using FluentAssertions;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3.Data;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit;

public sealed class GoogleCalendarServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public GoogleCalendarServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
    }

    [Fact]
    public async Task FetchAllEventsAsync_ConsumesAllPages_ReportsProgress_AndMapsEvents()
    {
        var tokenStorage = new Mock<ITokenStorageService>();
        tokenStorage.Setup(mock => mock.LoadTokenAsync()).ReturnsAsync(CreateTokenResponse());

        var apiClient = new Mock<IGoogleCalendarApiClient>();
        apiClient.SetupSequence(mock => mock.ListEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleCalendarEventPage(
                [
                    new Event
                    {
                        Id = "timed-1",
                        Summary = "Timed event",
                        Description = "Timed description",
                        ColorId = "11",
                        ETag = "\"etag-1\"",
                        Status = "confirmed",
                        UpdatedDateTimeOffset = new DateTimeOffset(2026, 01, 10, 12, 30, 00, TimeSpan.Zero),
                        Start = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 15, 09, 00, 00, TimeSpan.FromHours(-6))
                        },
                        End = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 15, 10, 00, 00, TimeSpan.FromHours(-6))
                        }
                    },
                    new Event
                    {
                        Id = "all-day-1",
                        Summary = "All day event",
                        Status = "confirmed",
                        Start = new EventDateTime { Date = "2026-01-20" },
                        End = new EventDateTime { Date = "2026-01-21" }
                    }
                ],
                "page-2",
                null))
            .ReturnsAsync(new GoogleCalendarEventPage(
                [
                    new Event
                    {
                        Id = "recurring-1",
                        Summary = "Recurring instance",
                        Status = "confirmed",
                        RecurringEventId = "series-123",
                        Start = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 22, 14, 00, 00, TimeSpan.FromHours(1))
                        },
                        End = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 22, 15, 00, 00, TimeSpan.FromHours(1))
                        }
                    }
                ],
                "page-3",
                null))
            .ReturnsAsync(new GoogleCalendarEventPage(
                [
                    new Event
                    {
                        Id = "cancelled-1",
                        Summary = "Cancelled event",
                        Status = "cancelled",
                        Start = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 23, 08, 00, 00, TimeSpan.Zero)
                        },
                        End = new EventDateTime
                        {
                            DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 23, 09, 00, 00, TimeSpan.Zero)
                        }
                    }
                ],
                null,
                "sync-token-123"));

        var factory = new Mock<IGoogleCalendarApiClientFactory>();
        factory.Setup(mock => mock.CreateAsync(It.IsAny<Google.Apis.Auth.OAuth2.UserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiClient.Object);

        var service = CreateService(tokenStorage.Object, factory.Object);
        var progressValues = new List<int>();
        var progress = new CallbackProgress<int>(value => progressValues.Add(value));

        var result = await service.FetchAllEventsAsync(
            "primary",
            new DateTime(2025, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc),
            progress);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.SyncToken.Should().Be("sync-token-123");
        result.Data.Events.Should().HaveCount(4);
        progressValues.Should().Equal(1, 2, 3);

        var timedEvent = result.Data.Events.Single(evt => evt.GcalEventId == "timed-1");
        timedEvent.StartDateTimeUtc.Should().Be(new DateTime(2026, 01, 15, 15, 00, 00, DateTimeKind.Utc));
        timedEvent.EndDateTimeUtc.Should().Be(new DateTime(2026, 01, 15, 16, 00, 00, DateTimeKind.Utc));
        timedEvent.IsAllDay.Should().BeFalse();
        timedEvent.IsDeleted.Should().BeFalse();
        timedEvent.ColorId.Should().Be("11");

        var allDayEvent = result.Data.Events.Single(evt => evt.GcalEventId == "all-day-1");
        allDayEvent.IsAllDay.Should().BeTrue();
        allDayEvent.StartDateTimeUtc.Should().Be(new DateTime(2026, 01, 20, 0, 0, 0, DateTimeKind.Utc));
        allDayEvent.EndDateTimeUtc.Should().Be(new DateTime(2026, 01, 21, 0, 0, 0, DateTimeKind.Utc));

        var recurringEvent = result.Data.Events.Single(evt => evt.GcalEventId == "recurring-1");
        recurringEvent.IsRecurringInstance.Should().BeTrue();
        recurringEvent.RecurringEventId.Should().Be("series-123");

        var deletedEvent = result.Data.Events.Single(evt => evt.GcalEventId == "cancelled-1");
        deletedEvent.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task FetchAllEventsAsync_WhenCancelledAfterACompletedPage_ReturnsPartialEvents()
    {
        var tokenStorage = new Mock<ITokenStorageService>();
        tokenStorage.Setup(mock => mock.LoadTokenAsync()).ReturnsAsync(CreateTokenResponse());

        using var cts = new CancellationTokenSource();
        var apiClient = new Mock<IGoogleCalendarApiClient>();
        apiClient.SetupSequence(mock => mock.ListEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return new GoogleCalendarEventPage(
                    [
                        new Event
                        {
                            Id = "first-page-event",
                            Summary = "Only first page persisted",
                            Status = "confirmed",
                            Start = new EventDateTime
                            {
                                DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 15, 09, 00, 00, TimeSpan.Zero)
                            },
                            End = new EventDateTime
                            {
                                DateTimeDateTimeOffset = new DateTimeOffset(2026, 01, 15, 10, 00, 00, TimeSpan.Zero)
                            }
                        }
                    ],
                    "page-2",
                    null);
            });

        var factory = new Mock<IGoogleCalendarApiClientFactory>();
        factory.Setup(mock => mock.CreateAsync(It.IsAny<Google.Apis.Auth.OAuth2.UserCredential>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiClient.Object);

        var service = CreateService(tokenStorage.Object, factory.Object);

        var result = await service.FetchAllEventsAsync(
            "primary",
            new DateTime(2025, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc),
            ct: cts.Token);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Events.Should().ContainSingle();
        result.Data.Events.Single().GcalEventId.Should().Be("first-page-event");
        result.Data.SyncToken.Should().BeNull();
    }

    [Fact]
    public async Task FetchAllEventsAsync_WhenNoStoredTokenExists_ReturnsFriendlyFailure()
    {
        var tokenStorage = new Mock<ITokenStorageService>();
        tokenStorage.Setup(mock => mock.LoadTokenAsync()).ReturnsAsync((TokenResponse?)null);

        var service = CreateService(tokenStorage.Object, Mock.Of<IGoogleCalendarApiClientFactory>());

        var result = await service.FetchAllEventsAsync(
            "primary",
            new DateTime(2025, 07, 01, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("connect");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private GoogleCalendarService CreateService(
        ITokenStorageService tokenStorage,
        IGoogleCalendarApiClientFactory apiClientFactory)
    {
        return new GoogleCalendarService(
            tokenStorage,
            Mock.Of<IGoogleAuthorizationBroker>(),
            _contextFactory,
            CreateGoogleOptionsWithClientSecret(),
            apiClientFactory,
            NullLogger<GoogleCalendarService>.Instance);
    }

    private static TokenResponse CreateTokenResponse()
    {
        return new TokenResponse
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresInSeconds = 3600,
            IssuedUtc = DateTime.UtcNow
        };
    }

    private static GoogleCalendarOptions CreateGoogleOptionsWithClientSecret()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gcm-fetch-{Guid.NewGuid():N}");
        var options = new GoogleCalendarOptions(root);
        Directory.CreateDirectory(options.CredentialsDirectoryPath);
        File.WriteAllText(
            options.ClientSecretPath,
            """
            {
              "installed": {
                "client_id": "test-client-id.apps.googleusercontent.com",
                "project_id": "test-project",
                "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                "token_uri": "https://oauth2.googleapis.com/token",
                "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
                "client_secret": "test-client-secret",
                "redirect_uris": [ "http://localhost" ]
              }
            }
            """);
        return options;
    }

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public CallbackProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value)
        {
            _callback(value);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext()
        {
            return new CalendarDbContext(_options);
        }

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}

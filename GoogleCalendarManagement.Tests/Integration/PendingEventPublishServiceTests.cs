using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class PendingEventPublishServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly FixedTimeProvider _timeProvider = new(new DateTimeOffset(2026, 04, 27, 12, 0, 0, TimeSpan.Zero));

    public PendingEventPublishServiceTests()
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
    public async Task PublishAsync_NewDraft_InsertsLiveEventAndRemovesPendingRow()
    {
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-1",
            CalendarId = "primary",
            Summary = "Draft event",
            Description = "Draft description",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "sage",
            AppCreated = true,
            SourceSystem = "manual",
            CreatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var googleService = new StubGoogleCalendarService
        {
            InsertHandler = request =>
            {
                request.ColorId.Should().Be("2");
                return Task.FromResult(GoogleCalendarWriteResult.Ok(new GcalEventDto(
                    "evt-new",
                    request.CalendarId,
                    request.Summary,
                    request.Description,
                    request.StartDateTimeUtc,
                    request.EndDateTimeUtc,
                    request.IsAllDay,
                    request.ColorId,
                    "\"etag-new\"",
                    new DateTime(2026, 04, 27, 11, 30, 0, DateTimeKind.Utc),
                    false,
                    null,
                    false)));
            }
        };

        var service = CreateSut(googleService);

        var result = await service.PublishAsync(["pending-1"]);

        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.PendingEvents.CountAsync()).Should().Be(0);
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);

        var liveEvent = await context.GcalEvents.SingleAsync(item => item.GcalEventId == "evt-new");
        liveEvent.Summary.Should().Be("Draft event");
        liveEvent.AppCreated.Should().BeTrue();
        liveEvent.AppPublished.Should().BeTrue();
        liveEvent.AppPublishedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        liveEvent.SourceSystem.Should().Be("manual");
    }

    [Fact]
    public async Task PublishAsync_ExistingEdit_WritesVersionSnapshotBeforeOverwrite()
    {
        await SeedLiveEventAsync(new GcalEvent
        {
            GcalEventId = "evt-1",
            CalendarId = "primary",
            Summary = "Original title",
            Description = "Original description",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "1",
            GcalEtag = "\"etag-old\"",
            GcalUpdatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            AppPublished = true,
            AppPublishedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc)
        });
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-edit-1",
            GcalEventId = "evt-1",
            CalendarId = "primary",
            Summary = "Updated title",
            Description = "Updated description",
            StartDatetime = new DateTime(2026, 04, 27, 17, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 18, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "purple",
            AppCreated = false,
            SourceSystem = "google-overlay",
            CreatedAt = new DateTime(2026, 04, 27, 10, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var googleService = new StubGoogleCalendarService
        {
            UpdateHandler = (_, request, ifMatch) =>
            {
                ifMatch.Should().Be("\"etag-old\"");
                request.ColorId.Should().Be("9");
                return Task.FromResult(GoogleCalendarWriteResult.Ok(new GcalEventDto(
                    "evt-1",
                    request.CalendarId,
                    request.Summary,
                    request.Description,
                    request.StartDateTimeUtc,
                    request.EndDateTimeUtc,
                    request.IsAllDay,
                    request.ColorId,
                    "\"etag-new\"",
                    new DateTime(2026, 04, 27, 11, 30, 0, DateTimeKind.Utc),
                    false,
                    null,
                    false)));
            }
        };

        var service = CreateSut(googleService);

        var result = await service.PublishAsync(["pending-edit-1"]);

        result.SuccessCount.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.PendingEvents.CountAsync()).Should().Be(0);

        var version = await context.GcalEventVersions.SingleAsync(item => item.GcalEventId == "evt-1");
        version.GcalEtag.Should().Be("\"etag-old\"");
        version.ChangedBy.Should().Be("manual_publish");
        version.ChangeReason.Should().Be("updated");

        var liveEvent = await context.GcalEvents.SingleAsync(item => item.GcalEventId == "evt-1");
        liveEvent.Summary.Should().Be("Updated title");
        liveEvent.GcalEtag.Should().Be("\"etag-new\"");
        liveEvent.AppPublished.Should().BeTrue();
        liveEvent.AppPublishedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task PublishAsync_Failure_PreservesPendingRowAndStoresPublishError()
    {
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-fail-1",
            CalendarId = "primary",
            Summary = "Draft event",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "azure",
            AppCreated = true,
            SourceSystem = "manual",
            CreatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var service = CreateSut(new StubGoogleCalendarService
        {
            InsertHandler = _ => Task.FromResult(
                GoogleCalendarWriteResult.Failure("Quota exceeded."))
        });

        var result = await service.PublishAsync(["pending-fail-1"]);

        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var pendingEvent = await context.PendingEvents.SingleAsync(item => item.PendingEventId == "pending-fail-1");
        pendingEvent.PublishError.Should().Be("Quota exceeded.");
        pendingEvent.PublishAttemptedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        (await context.GcalEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_WhenGoogleWriteResponseOmitsColor_PreservesPendingColor()
    {
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-color-1",
            CalendarId = "primary",
            Summary = "Color draft",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "lavender",
            AppCreated = true,
            SourceSystem = "manual",
            CreatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var service = CreateSut(new StubGoogleCalendarService
        {
            InsertHandler = request =>
            {
                request.ColorId.Should().Be("1");
                return Task.FromResult(GoogleCalendarWriteResult.Ok(new GcalEventDto(
                    "evt-color-1",
                    request.CalendarId,
                    request.Summary,
                    request.Description,
                    request.StartDateTimeUtc,
                    request.EndDateTimeUtc,
                    request.IsAllDay,
                    null,
                    "\"etag-color\"",
                    new DateTime(2026, 04, 27, 11, 30, 0, DateTimeKind.Utc),
                    false,
                    null,
                    false)));
            }
        });

        var result = await service.PublishAsync(["pending-color-1"]);

        result.SuccessCount.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.GcalEvents.SingleAsync(item => item.GcalEventId == "evt-color-1");
        liveEvent.ColorId.Should().Be("lavender");
    }

    [Fact]
    public async Task PublishAsync_MixedBatch_PersistsSuccessfulItemsWithoutRollingBackFailures()
    {
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-ok",
            CalendarId = "primary",
            Summary = "Success draft",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            ColorId = "azure",
            AppCreated = true,
            SourceSystem = "manual",
            CreatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-bad",
            CalendarId = "primary",
            Summary = "Fail draft",
            StartDatetime = new DateTime(2026, 04, 27, 17, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 18, 0, 0, DateTimeKind.Utc),
            ColorId = "grey",
            AppCreated = true,
            SourceSystem = "manual",
            CreatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var service = CreateSut(new StubGoogleCalendarService
        {
            InsertHandler = request =>
            {
                if (request.Summary == "Fail draft")
                {
                    return Task.FromResult(GoogleCalendarWriteResult.Failure("Network down."));
                }

                return Task.FromResult(GoogleCalendarWriteResult.Ok(new GcalEventDto(
                    "evt-success",
                    request.CalendarId,
                    request.Summary,
                    request.Description,
                    request.StartDateTimeUtc,
                    request.EndDateTimeUtc,
                    request.IsAllDay,
                    request.ColorId,
                    "\"etag-success\"",
                    new DateTime(2026, 04, 27, 11, 30, 0, DateTimeKind.Utc),
                    false,
                    null,
                    false)));
            }
        });

        var result = await service.PublishAsync(["pending-ok", "pending-bad"]);

        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.GcalEvents.CountAsync()).Should().Be(1);
        (await context.PendingEvents.CountAsync()).Should().Be(1);
        var failedPending = await context.PendingEvents.SingleAsync();
        failedPending.PendingEventId.Should().Be("pending-bad");
        failedPending.PublishError.Should().Be("Network down.");
    }

    [Fact]
    public async Task PublishAsync_WhenUpdateGets412AndLocalPendingIsNewer_RetriesOnceWithFreshEtag()
    {
        await SeedLiveEventAsync(new GcalEvent
        {
            GcalEventId = "evt-conflict",
            CalendarId = "primary",
            Summary = "Original title",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            ColorId = "1",
            GcalEtag = "\"etag-old\"",
            GcalUpdatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            AppPublished = true,
            AppPublishedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc)
        });
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-conflict",
            GcalEventId = "evt-conflict",
            CalendarId = "primary",
            Summary = "Locally newer",
            StartDatetime = new DateTime(2026, 04, 27, 17, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 18, 0, 0, DateTimeKind.Utc),
            ColorId = "orange",
            AppCreated = false,
            SourceSystem = "google-overlay",
            CreatedAt = new DateTime(2026, 04, 27, 10, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 12, 0, 0, DateTimeKind.Utc)
        });

        var updateCalls = new List<string?>();
        var service = CreateSut(new StubGoogleCalendarService
        {
            UpdateHandler = (_, request, ifMatch) =>
            {
                updateCalls.Add(ifMatch);
                return Task.FromResult(updateCalls.Count == 1
                    ? GoogleCalendarWriteResult.Failure(
                        "The Google Calendar event changed before this publish completed.",
                        GoogleCalendarWriteFailureKind.PreconditionFailed)
                    : GoogleCalendarWriteResult.Ok(new GcalEventDto(
                        "evt-conflict",
                        request.CalendarId,
                        request.Summary,
                        request.Description,
                        request.StartDateTimeUtc,
                        request.EndDateTimeUtc,
                        request.IsAllDay,
                        request.ColorId,
                        "\"etag-fresh\"",
                        new DateTime(2026, 04, 27, 12, 5, 0, DateTimeKind.Utc),
                        false,
                        null,
                        false)));
            },
            GetHandler = (_, _, _) => Task.FromResult(OperationResult<GcalEventDto>.Ok(new GcalEventDto(
                "evt-conflict",
                "primary",
                "Google newer",
                null,
                new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
                false,
                "6",
                "\"etag-fresh\"",
                new DateTime(2026, 04, 27, 12, 1, 0, DateTimeKind.Utc),
                false,
                null,
                false)))
        });

        var result = await service.PublishAsync(["pending-conflict"]);

        result.SuccessCount.Should().Be(1);
        updateCalls.Should().Equal("\"etag-old\"", "\"etag-fresh\"");
    }

    [Fact]
    public async Task PublishAsync_WhenUpdateGets412AndGoogleVersionIsNewer_LeavesPendingRow()
    {
        await SeedLiveEventAsync(new GcalEvent
        {
            GcalEventId = "evt-conflict-loss",
            CalendarId = "primary",
            Summary = "Original title",
            StartDatetime = new DateTime(2026, 04, 27, 15, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 16, 0, 0, DateTimeKind.Utc),
            ColorId = "1",
            GcalEtag = "\"etag-old\"",
            GcalUpdatedAt = new DateTime(2026, 04, 27, 12, 0, 0, DateTimeKind.Utc),
            AppPublished = true,
            AppPublishedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 10, 0, 0, DateTimeKind.Utc)
        });
        await SeedPendingEventAsync(new PendingEvent
        {
            PendingEventId = "pending-conflict-loss",
            GcalEventId = "evt-conflict-loss",
            CalendarId = "primary",
            Summary = "Locally older",
            StartDatetime = new DateTime(2026, 04, 27, 17, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 27, 18, 0, 0, DateTimeKind.Utc),
            ColorId = "orange",
            AppCreated = false,
            SourceSystem = "google-overlay",
            CreatedAt = new DateTime(2026, 04, 27, 10, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 27, 11, 0, 0, DateTimeKind.Utc)
        });

        var service = CreateSut(new StubGoogleCalendarService
        {
            UpdateHandler = (_, _, _) => Task.FromResult(
                GoogleCalendarWriteResult.Failure(
                    "The Google Calendar event changed before this publish completed.",
                    GoogleCalendarWriteFailureKind.PreconditionFailed))
        });

        var result = await service.PublishAsync(["pending-conflict-loss"]);

        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var pendingEvent = await context.PendingEvents.SingleAsync(item => item.PendingEventId == "pending-conflict-loss");
        pendingEvent.PublishError.Should().Contain("newer");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private PendingEventPublishService CreateSut(StubGoogleCalendarService googleCalendarService)
    {
        return new PendingEventPublishService(
            _contextFactory,
            googleCalendarService,
            new ColorMappingService(),
            _timeProvider,
            NullLogger<PendingEventPublishService>.Instance);
    }

    private async Task SeedPendingEventAsync(PendingEvent pendingEvent)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.PendingEvents.Add(pendingEvent);
        await context.SaveChangesAsync();
    }

    private async Task SeedLiveEventAsync(GcalEvent gcalEvent)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.Add(gcalEvent);
        await context.SaveChangesAsync();
    }

    private sealed class StubGoogleCalendarService : IGoogleCalendarService
    {
        public Func<string, string, CancellationToken, Task<OperationResult<GcalEventDto>>>? GetHandler { get; set; }
        public Func<GoogleCalendarWriteRequest, Task<GoogleCalendarWriteResult>>? InsertHandler { get; set; }
        public Func<string, GoogleCalendarWriteRequest, string?, Task<GoogleCalendarWriteResult>>? UpdateHandler { get; set; }

        public Task<OperationResult<OAuthStatus>> AuthenticateAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<OperationResult<bool>> IsAuthenticatedAsync() => throw new NotImplementedException();

        public Task RevokeAndClearTokensAsync() => throw new NotImplementedException();

        public Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchAllEventsAsync(
            string calendarId,
            DateTime start,
            DateTime end,
            IProgress<int>? progress = null,
            CancellationToken ct = default) => throw new NotImplementedException();

        public Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>> FetchIncrementalEventsAsync(
            string calendarId,
            string syncToken,
            CancellationToken ct = default) => throw new NotImplementedException();

        public Task<OperationResult<GcalEventDto>> GetEventAsync(
            string calendarId,
            string eventId,
            CancellationToken ct = default)
        {
            return GetHandler?.Invoke(calendarId, eventId, ct) ??
                Task.FromResult(OperationResult<GcalEventDto>.Failure("No get handler configured."));
        }

        public Task<GoogleCalendarWriteResult> InsertEventAsync(
            GoogleCalendarWriteRequest request,
            CancellationToken ct = default)
        {
            return InsertHandler?.Invoke(request) ??
                Task.FromResult(GoogleCalendarWriteResult.Failure("No insert handler configured."));
        }

        public Task<GoogleCalendarWriteResult> UpdateEventAsync(
            string eventId,
            GoogleCalendarWriteRequest request,
            string? ifMatchEtag,
            CancellationToken ct = default)
        {
            return UpdateHandler?.Invoke(eventId, request, ifMatchEtag) ??
                Task.FromResult(GoogleCalendarWriteResult.Failure("No update handler configured."));
        }

        public Task<GoogleCalendarDeleteResult> DeleteEventAsync(
            string calendarId,
            string eventId,
            CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
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

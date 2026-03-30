using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class GoogleCalendarSyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public GoogleCalendarSyncTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();
        context.Dispose();

        _contextFactory = new TestDbContextFactory(options);
    }

    [Fact]
    public async Task SyncAsync_FullSync_InsertsEvents_WritesRefreshMetadata_AndAuditLog()
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                new List<GcalEventDto>
                {
                    CreateEvent("event-1", "First event"),
                    CreateEvent("event-2", "Second event")
                },
                "sync-token-123")));

        var syncManager = new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            NullLogger<SyncManager>.Instance);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.WasCancelled.Should().BeFalse();
        result.EventsAdded.Should().Be(2);
        result.NewSyncToken.Should().Be("sync-token-123");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var events = await context.GcalEvents.OrderBy(evt => evt.GcalEventId).ToListAsync();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(evt => evt.LastSyncedAt.HasValue);

        var refresh = await context.DataSourceRefreshes.SingleAsync();
        refresh.SourceName.Should().Be("gcal");
        refresh.RecordsFetched.Should().Be(2);
        refresh.Success.Should().BeTrue();
        refresh.SyncToken.Should().Be("sync-token-123");

        var auditLog = await context.AuditLogs.SingleAsync(log => log.OperationType == "gcal_sync");
        auditLog.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_Resync_UpdatesExistingRows_WithoutDuplicating_AndMarksCancelledEventsDeleted()
    {
        var originalTimestamp = new DateTime(2026, 01, 01, 8, 0, 0, DateTimeKind.Utc);

        await using (var seedContext = await _contextFactory.CreateDbContextAsync())
        {
            seedContext.GcalEvents.AddRange(
                new GcalEvent
                {
                    GcalEventId = "event-1",
                    CalendarId = "primary",
                    Summary = "Original summary",
                    StartDatetime = originalTimestamp,
                    EndDatetime = originalTimestamp.AddHours(1),
                    CreatedAt = originalTimestamp,
                    UpdatedAt = originalTimestamp
                },
                new GcalEvent
                {
                    GcalEventId = "event-2",
                    CalendarId = "primary",
                    Summary = "Will be cancelled",
                    StartDatetime = originalTimestamp.AddDays(1),
                    EndDatetime = originalTimestamp.AddDays(1).AddHours(1),
                    CreatedAt = originalTimestamp,
                    UpdatedAt = originalTimestamp
                });
            await seedContext.SaveChangesAsync();
        }

        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                new List<GcalEventDto>
                {
                    CreateEvent("event-1", "Updated summary"),
                    CreateEvent("event-2", "Will be cancelled", isDeleted: true)
                },
                "sync-token-456")));

        var syncManager = new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            NullLogger<SyncManager>.Instance);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsAdded.Should().Be(0);
        result.EventsUpdated.Should().Be(1);
        result.EventsDeleted.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var events = await context.GcalEvents.OrderBy(evt => evt.GcalEventId).ToListAsync();
        events.Should().HaveCount(2);
        events.Single(evt => evt.GcalEventId == "event-1").Summary.Should().Be("Updated summary");
        events.Single(evt => evt.GcalEventId == "event-2").IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_UpdatedEvent_CreatesSnapshotBeforeOverwrite()
    {
        var seedTimestamp = new DateTime(2026, 01, 10, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new GcalEvent
        {
            GcalEventId = "event-1",
            CalendarId = "primary",
            Summary = "Original summary",
            Description = "Original description",
            StartDatetime = seedTimestamp,
            EndDatetime = seedTimestamp.AddHours(1),
            IsAllDay = false,
            ColorId = "2",
            GcalEtag = "\"etag-original\"",
            GcalUpdatedAt = seedTimestamp,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-1",
                "Updated summary",
                description: "Updated description",
                start: seedTimestamp.AddDays(1),
                end: seedTimestamp.AddDays(1).AddHours(2),
                colorId: "5",
                etag: "\"etag-updated\"",
                updatedAt: seedTimestamp.AddDays(1))
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsUpdated.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "event-1");
        var versions = await context.GcalEventVersions
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.VersionId)
            .ToListAsync();

        versions.Should().ContainSingle();
        versions[0].GcalEventId.Should().Be("event-1");
        versions[0].Summary.Should().Be("Original summary");
        versions[0].Description.Should().Be("Original description");
        versions[0].StartDatetime.Should().Be(seedTimestamp);
        versions[0].EndDatetime.Should().Be(seedTimestamp.AddHours(1));
        versions[0].ColorId.Should().Be("2");
        versions[0].GcalEtag.Should().Be("\"etag-original\"");
        versions[0].ChangedBy.Should().Be("gcal_sync");
        versions[0].ChangeReason.Should().Be("updated");
        versions[0].CreatedAt.Should().BeOnOrBefore(liveEvent.UpdatedAt);

        liveEvent.Summary.Should().Be("Updated summary");
        liveEvent.Description.Should().Be("Updated description");
        liveEvent.StartDatetime.Should().Be(seedTimestamp.AddDays(1));
        liveEvent.EndDatetime.Should().Be(seedTimestamp.AddDays(1).AddHours(2));
        liveEvent.ColorId.Should().Be("5");
        liveEvent.GcalEtag.Should().Be("\"etag-updated\"");
    }

    [Fact]
    public async Task SyncAsync_DeletedEvent_CreatesSnapshotBeforeSoftDelete()
    {
        var seedTimestamp = new DateTime(2026, 01, 11, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new GcalEvent
        {
            GcalEventId = "event-1",
            CalendarId = "primary",
            Summary = "Existing summary",
            Description = "Existing description",
            StartDatetime = seedTimestamp,
            EndDatetime = seedTimestamp.AddHours(1),
            IsAllDay = false,
            ColorId = "3",
            GcalEtag = "\"etag-existing\"",
            GcalUpdatedAt = seedTimestamp,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-1",
                "Existing summary",
                description: "Existing description",
                start: seedTimestamp,
                end: seedTimestamp.AddHours(1),
                colorId: "3",
                etag: "\"etag-deleted\"",
                updatedAt: seedTimestamp.AddDays(1),
                isDeleted: true)
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsDeleted.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "event-1");
        var version = await context.GcalEventVersions.SingleAsync(evt => evt.GcalEventId == "event-1");

        version.Summary.Should().Be("Existing summary");
        version.Description.Should().Be("Existing description");
        version.ColorId.Should().Be("3");
        version.GcalEtag.Should().Be("\"etag-existing\"");
        version.ChangedBy.Should().Be("gcal_sync");
        version.ChangeReason.Should().Be("deleted");
        version.CreatedAt.Should().BeOnOrBefore(liveEvent.UpdatedAt);

        liveEvent.IsDeleted.Should().BeTrue();
        liveEvent.GcalUpdatedAt.Should().Be(seedTimestamp.AddDays(1));
    }

    [Fact]
    public async Task SyncAsync_NewEvent_DoesNotCreateVersionRow()
    {
        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent("event-1", "First event")
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsAdded.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.GcalEvents.CountAsync()).Should().Be(1);
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_UnchangedEvent_DoesNotCreateVersionRow()
    {
        var eventTimestamp = new DateTime(2026, 01, 12, 9, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new GcalEvent
        {
            GcalEventId = "event-1",
            CalendarId = "primary",
            Summary = "Same summary",
            Description = "Same description",
            StartDatetime = eventTimestamp,
            EndDatetime = eventTimestamp.AddHours(1),
            IsAllDay = false,
            ColorId = "5",
            GcalEtag = "\"event-1-etag\"",
            GcalUpdatedAt = new DateTime(2026, 01, 14, 18, 0, 0, DateTimeKind.Utc),
            LastSyncedAt = eventTimestamp.AddDays(-1),
            CreatedAt = eventTimestamp.AddDays(-1),
            UpdatedAt = eventTimestamp.AddDays(-1)
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-1",
                "Same summary",
                description: "Same description",
                start: eventTimestamp,
                end: eventTimestamp.AddHours(1))
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsUpdated.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "event-1");

        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
        liveEvent.LastSyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncAsync_RepeatedUpdates_AppendHistoryWithoutMutatingOldRows()
    {
        var seedTimestamp = new DateTime(2026, 01, 13, 9, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new GcalEvent
        {
            GcalEventId = "event-1",
            CalendarId = "primary",
            Summary = "Version A",
            Description = "Description A",
            StartDatetime = seedTimestamp,
            EndDatetime = seedTimestamp.AddHours(1),
            IsAllDay = false,
            ColorId = "1",
            GcalEtag = "\"etag-a\"",
            GcalUpdatedAt = seedTimestamp,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var firstSyncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-1",
                "Version B",
                description: "Description B",
                start: seedTimestamp.AddDays(1),
                end: seedTimestamp.AddDays(1).AddHours(1),
                colorId: "2",
                etag: "\"etag-b\"",
                updatedAt: seedTimestamp.AddDays(1))
        });

        await firstSyncManager.SyncAsync("primary");

        int firstSnapshotId;
        await using (var afterFirstSync = await _contextFactory.CreateDbContextAsync())
        {
            var firstSnapshot = await afterFirstSync.GcalEventVersions.SingleAsync();
            firstSnapshotId = firstSnapshot.VersionId;
            firstSnapshot.Summary.Should().Be("Version A");
            firstSnapshot.ChangeReason.Should().Be("updated");
        }

        var secondSyncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-1",
                "Version C",
                description: "Description C",
                start: seedTimestamp.AddDays(2),
                end: seedTimestamp.AddDays(2).AddHours(1),
                colorId: "3",
                etag: "\"etag-c\"",
                updatedAt: seedTimestamp.AddDays(2))
        });

        await secondSyncManager.SyncAsync("primary");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "event-1");
        var versions = await context.GcalEventVersions
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.VersionId)
            .ToListAsync();

        versions.Should().HaveCount(2);
        versions[0].Summary.Should().Be("Version B");
        versions[0].Description.Should().Be("Description B");
        versions[1].Summary.Should().Be("Version A");
        versions[1].Description.Should().Be("Description A");
        versions.Single(version => version.VersionId == firstSnapshotId).Summary.Should().Be("Version A");

        liveEvent.Summary.Should().Be("Version C");
        liveEvent.Description.Should().Be("Description C");
        liveEvent.ColorId.Should().Be("3");
        liveEvent.GcalEtag.Should().Be("\"etag-c\"");
    }

    [Fact]
    public async Task SyncAsync_EmptyCalendar_ReturnsSuccessAndWritesRefreshMetadata()
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                new List<GcalEventDto>(),
                "empty-sync-token")));

        var syncManager = new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            NullLogger<SyncManager>.Instance);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsAdded.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.Should().BeEmpty();

        var refresh = await context.DataSourceRefreshes.SingleAsync();
        refresh.Success.Should().BeTrue();
        refresh.RecordsFetched.Should().Be(0);
        refresh.SyncToken.Should().Be("empty-sync-token");
    }

    [Fact]
    public async Task SyncAsync_WhenCancelledDuringPersistence_PreservesAlreadyWrittenRows_AndExitsCleanly()
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                new List<GcalEventDto>
                {
                    CreateEvent("event-1", "First"),
                    CreateEvent("event-2", "Second"),
                    CreateEvent("event-3", "Third")
                },
                "sync-token-789")));

        var syncManager = new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            NullLogger<SyncManager>.Instance);

        using var cts = new CancellationTokenSource();
        var progress = new CallbackProgress<SyncProgress>(value =>
        {
            if (value.EventsProcessed == 1)
            {
                cts.Cancel();
            }
        });

        var result = await syncManager.SyncAsync("primary", progress: progress, ct: cts.Token);

        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();

        await using var context = await _contextFactory.CreateDbContextAsync();
        var storedEvents = await context.GcalEvents.OrderBy(evt => evt.GcalEventId).ToListAsync();
        storedEvents.Should().ContainSingle(evt => evt.GcalEventId == "event-1");

        var refresh = await context.DataSourceRefreshes.SingleAsync();
        refresh.Success.Should().BeFalse();
        refresh.SyncToken.Should().BeNull();
    }

    [Fact]
    public async Task SyncAsync_WhenDatabaseIsLocked_ReturnsFriendlyFailureInsteadOfThrowing()
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                new List<GcalEventDto>
                {
                    CreateEvent("event-1", "Locked write")
                },
                "sync-token-locked")));

        var lockingFactory = new LockingDbContextFactory(_contextFactory.Options);
        var syncManager = new SyncManager(
            googleCalendarService.Object,
            lockingFactory,
            NullLogger<SyncManager>.Instance);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeFalse();
        result.ErrorMessage.Should().Contain("busy or locked");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private SyncManager CreateSyncManager(IEnumerable<GcalEventDto> events, string? syncToken = "sync-token")
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(mock => mock.FetchAllEventsAsync(
                "primary",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<IProgress<int>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>.Ok((
                events.ToList(),
                syncToken)));

        return new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            NullLogger<SyncManager>.Instance);
    }

    private async Task SeedEventAsync(GcalEvent gcalEvent)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.Add(gcalEvent);
        await context.SaveChangesAsync();
    }

    private static GcalEventDto CreateEvent(
        string eventId,
        string summary,
        string? description = null,
        DateTime? start = null,
        DateTime? end = null,
        string? colorId = "5",
        string? etag = null,
        DateTime? updatedAt = null,
        bool isDeleted = false)
    {
        return new GcalEventDto(
            eventId,
            "primary",
            summary,
            description ?? $"{summary} description",
            start ?? new DateTime(2026, 01, 15, 9, 0, 0, DateTimeKind.Utc),
            end ?? new DateTime(2026, 01, 15, 10, 0, 0, DateTimeKind.Utc),
            false,
            colorId,
            etag ?? $"\"{eventId}-etag\"",
            updatedAt ?? new DateTime(2026, 01, 14, 18, 0, 0, DateTimeKind.Utc),
            isDeleted,
            null,
            false);
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

        public DbContextOptions<CalendarDbContext> Options => _options;
    }

    private sealed class LockingDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public LockingDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext()
        {
            return new LockingCalendarDbContext(_options);
        }

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class LockingCalendarDbContext : CalendarDbContext
    {
        public LockingCalendarDbContext(DbContextOptions<CalendarDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            throw new DbUpdateException(
                "Simulated database lock.",
                new InvalidOperationException("SQLite Error 5: 'database is locked'."));
        }
    }
}

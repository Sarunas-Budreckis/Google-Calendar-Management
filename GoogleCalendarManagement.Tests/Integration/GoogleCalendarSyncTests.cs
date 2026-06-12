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

        var syncManager = CreateSyncManager(googleCalendarService);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.WasCancelled.Should().BeFalse();
        result.EventsAdded.Should().Be(2);
        result.NewSyncToken.Should().Be("sync-token-123");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var events = await context.Events.OrderBy(evt => evt.GcalEventId).ToListAsync();
        events.Should().HaveCount(2);
        events.Should().OnlyContain(evt => evt.LastSyncedAt.HasValue);
        events.Should().OnlyContain(evt => evt.EventId != "");
        events.Should().OnlyContain(evt => evt.Lifecycle == "approved" && evt.Publish == "published");

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

        await SeedEventAsync(
            NewEvent("evt-1", "event-1", "Original summary", originalTimestamp, originalTimestamp.AddHours(1)),
            NewEvent("evt-2", "event-2", "Will be cancelled", originalTimestamp.AddDays(1), originalTimestamp.AddDays(1).AddHours(1)));

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

        var syncManager = CreateSyncManager(googleCalendarService);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsAdded.Should().Be(0);
        result.EventsUpdated.Should().Be(1);
        result.EventsDeleted.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var events = await context.Events.OrderBy(evt => evt.GcalEventId).ToListAsync();
        events.Should().HaveCount(2);
        events.Single(evt => evt.GcalEventId == "event-1").Summary.Should().Be("Updated summary");
        events.Single(evt => evt.GcalEventId == "event-2").IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_UpdatedEvent_CreatesSnapshotBeforeOverwrite()
    {
        var seedTimestamp = new DateTime(2026, 01, 10, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-update-1",
            GcalEventId = "event-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
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
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-1");
        var versions = await context.GcalEventVersions
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.VersionId)
            .ToListAsync();

        versions.Should().ContainSingle();
        versions[0].EventId.Should().Be("evt-update-1");
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

        await SeedEventAsync(new Event
        {
            EventId = "evt-delete-1",
            GcalEventId = "event-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
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
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-1");
        var version = await context.GcalEventVersions.SingleAsync(evt => evt.EventId == "evt-delete-1");
        var tombstone = await context.DeletedEvents.SingleAsync(evt => evt.EventId == "evt-delete-1");

        version.Summary.Should().Be("Existing summary");
        version.Description.Should().Be("Existing description");
        version.ColorId.Should().Be("3");
        version.GcalEtag.Should().Be("\"etag-existing\"");
        version.ChangedBy.Should().Be("gcal_sync");
        version.ChangeReason.Should().Be("deleted");
        version.CreatedAt.Should().BeOnOrBefore(liveEvent.UpdatedAt);
        tombstone.GcalEventId.Should().Be("event-1");
        tombstone.Summary.Should().Be("Existing summary");
        tombstone.DeletionSource.Should().Be("gcal_sync");

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
        (await context.Events.CountAsync()).Should().Be(1);
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_UnchangedEvent_DoesNotCreateVersionRow()
    {
        var eventTimestamp = new DateTime(2026, 01, 12, 9, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-unchanged-1",
            GcalEventId = "event-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
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
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-1");

        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
        liveEvent.LastSyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncAsync_RepeatedUpdates_AppendHistoryWithoutMutatingOldRows()
    {
        var seedTimestamp = new DateTime(2026, 01, 13, 9, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-history-1",
            GcalEventId = "event-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
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
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-1");
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

        var syncManager = CreateSyncManager(googleCalendarService);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsAdded.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Events.Should().BeEmpty();

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

        var syncManager = CreateSyncManager(googleCalendarService);

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
        var storedEvents = await context.Events.OrderBy(evt => evt.GcalEventId).ToListAsync();
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
            new EventIdentityService(new EventRepository(_contextFactory)),
            NullLogger<SyncManager>.Instance);

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeFalse();
        result.ErrorMessage.Should().Contain("busy or locked");
    }

    [Fact]
    public async Task SyncAsync_UpdatedRecurringEvent_SnapshotCopiesRecurringMetadataAndGcalUpdatedAt()
    {
        var seedTimestamp = new DateTime(2026, 02, 01, 8, 0, 0, DateTimeKind.Utc);
        const string recurringParentId = "recurring-parent-1";

        await SeedEventAsync(new Event
        {
            EventId = "evt-recurring-1",
            GcalEventId = "event-recurring-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = "Recurring instance v1",
            GcalEtag = "\"etag-v1\"",
            GcalUpdatedAt = seedTimestamp,
            RecurringEventId = recurringParentId,
            IsRecurringInstance = true,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-recurring-1",
                "Recurring instance v2",
                etag: "\"etag-v2\"",
                updatedAt: seedTimestamp.AddDays(1),
                recurringEventId: recurringParentId,
                isRecurringInstance: true)
        });

        await syncManager.SyncAsync("primary");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var version = await context.GcalEventVersions.SingleAsync(v => v.EventId == "evt-recurring-1");

        version.GcalUpdatedAt.Should().Be(seedTimestamp);
        version.RecurringEventId.Should().Be(recurringParentId);
        version.IsRecurringInstance.Should().BeTrue();
        version.ChangeReason.Should().Be("updated");
    }

    [Fact]
    public async Task SyncAsync_SameEtagButChangedRecurringMetadata_StillCreatesSnapshotBeforeOverwrite()
    {
        var seedTimestamp = new DateTime(2026, 02, 01, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-recurring-2",
            GcalEventId = "event-recurring-2",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = "Recurring instance v1",
            GcalEtag = "\"etag-stable\"",
            GcalUpdatedAt = seedTimestamp,
            RecurringEventId = null,
            IsRecurringInstance = false,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-recurring-2",
                "Recurring instance v1",
                etag: "\"etag-stable\"",
                updatedAt: seedTimestamp.AddDays(1),
                recurringEventId: "recurring-parent-2",
                isRecurringInstance: true)
        });

        await syncManager.SyncAsync("primary");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(v => v.GcalEventId == "event-recurring-2");
        var version = await context.GcalEventVersions.SingleAsync(v => v.EventId == "evt-recurring-2");

        version.GcalUpdatedAt.Should().Be(seedTimestamp);
        version.RecurringEventId.Should().BeNull();
        version.IsRecurringInstance.Should().BeFalse();

        liveEvent.GcalUpdatedAt.Should().Be(seedTimestamp.AddDays(1));
        liveEvent.RecurringEventId.Should().Be("recurring-parent-2");
        liveEvent.IsRecurringInstance.Should().BeTrue();
    }

    [Fact]
    public async Task SyncAsync_Overwrite_DoesNotResetOwnershipMetadata()
    {
        var seedTimestamp = new DateTime(2026, 02, 02, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-owned-1",
            GcalEventId = "event-owned-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = "App-owned event",
            GcalEtag = "\"etag-original\"",
            SourceSystem = "manual",
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-owned-1",
                "App-owned event",
                etag: "\"etag-updated\"",
                updatedAt: seedTimestamp.AddDays(1))
        });

        await syncManager.SyncAsync("primary");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-owned-1");

        liveEvent.EventId.Should().Be("evt-owned-1");
        liveEvent.SourceSystem.Should().Be("manual");
        liveEvent.Lifecycle.Should().Be("approved");
        liveEvent.Publish.Should().Be("published");
    }

    [Fact]
    public async Task SyncAsync_Delete_DoesNotResetOwnershipMetadata()
    {
        var seedTimestamp = new DateTime(2026, 02, 03, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-owned-2",
            GcalEventId = "event-owned-2",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = "App-owned event to be deleted",
            GcalEtag = "\"etag-original\"",
            SourceSystem = "manual",
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-owned-2",
                "App-owned event to be deleted",
                etag: "\"etag-deleted\"",
                updatedAt: seedTimestamp.AddDays(1),
                isDeleted: true)
        });

        await syncManager.SyncAsync("primary");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-owned-2");

        liveEvent.IsDeleted.Should().BeTrue();
        liveEvent.EventId.Should().Be("evt-owned-2");
        liveEvent.SourceSystem.Should().Be("manual");
    }

    [Fact]
    public async Task SyncAsync_WhenGoogleOmitsColorId_PreservesExistingLocalColor()
    {
        var seedTimestamp = new DateTime(2026, 02, 04, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-color-preserve",
            GcalEventId = "event-color-preserve",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = "Original summary",
            StartDatetime = seedTimestamp,
            EndDatetime = seedTimestamp.AddHours(1),
            ColorId = "lavender",
            GcalEtag = "\"etag-original\"",
            GcalUpdatedAt = seedTimestamp,
            SourceSystem = "manual",
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-color-preserve",
                "Updated summary",
                start: seedTimestamp,
                end: seedTimestamp.AddHours(2),
                colorId: null,
                etag: "\"etag-updated\"",
                updatedAt: seedTimestamp.AddDays(1))
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-color-preserve");
        liveEvent.ColorId.Should().Be("lavender");
    }

    [Fact]
    public async Task SyncAsync_UpdatedEvent_WithLocalUnpublishedEdits_DoesNotClobberLocalFields()
    {
        var seedTimestamp = new DateTime(2026, 03, 01, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-dirty-1",
            GcalEventId = "event-dirty-1",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            HasUnpublishedChanges = true,
            Summary = "Local edit",
            Description = "Local description",
            StartDatetime = seedTimestamp,
            EndDatetime = seedTimestamp.AddHours(1),
            ColorId = "5",
            GcalEtag = "\"etag-old\"",
            GcalUpdatedAt = seedTimestamp,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-dirty-1",
                "GCal update",
                description: "GCal description",
                start: seedTimestamp.AddDays(1),
                end: seedTimestamp.AddDays(1).AddHours(2),
                colorId: "9",
                etag: "\"etag-new\"",
                updatedAt: seedTimestamp.AddDays(1))
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsUpdated.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-dirty-1");

        // User-facing fields preserved.
        liveEvent.Summary.Should().Be("Local edit");
        liveEvent.Description.Should().Be("Local description");
        liveEvent.StartDatetime.Should().Be(seedTimestamp);
        liveEvent.EndDatetime.Should().Be(seedTimestamp.AddHours(1));
        liveEvent.ColorId.Should().Be("5");
        liveEvent.HasUnpublishedChanges.Should().BeTrue();

        // Sync metadata refreshed.
        liveEvent.GcalEtag.Should().Be("\"etag-new\"");
        liveEvent.GcalUpdatedAt.Should().Be(seedTimestamp.AddDays(1));
        liveEvent.LastSyncedAt.Should().NotBeNull();

        // No snapshot written because nothing was overwritten.
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SyncAsync_DeletedEvent_WithLocalUnpublishedEdits_AppliesDelete()
    {
        var seedTimestamp = new DateTime(2026, 03, 02, 8, 0, 0, DateTimeKind.Utc);

        await SeedEventAsync(new Event
        {
            EventId = "evt-dirty-delete",
            GcalEventId = "event-dirty-delete",
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            HasUnpublishedChanges = true,
            Summary = "Local edit pending",
            GcalEtag = "\"etag-old\"",
            GcalUpdatedAt = seedTimestamp,
            CreatedAt = seedTimestamp,
            UpdatedAt = seedTimestamp
        });

        var syncManager = CreateSyncManager(new[]
        {
            CreateEvent(
                "event-dirty-delete",
                "Local edit pending",
                etag: "\"etag-deleted\"",
                updatedAt: seedTimestamp.AddDays(1),
                isDeleted: true)
        });

        var result = await syncManager.SyncAsync("primary");

        result.Success.Should().BeTrue();
        result.EventsDeleted.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await context.Events.SingleAsync(evt => evt.GcalEventId == "event-dirty-delete");
        var version = await context.GcalEventVersions.SingleAsync(v => v.EventId == "evt-dirty-delete");
        var tombstone = await context.DeletedEvents.SingleAsync(v => v.EventId == "evt-dirty-delete");

        liveEvent.IsDeleted.Should().BeTrue();
        version.ChangeReason.Should().Be("deleted");
        tombstone.Summary.Should().Be("Local edit pending");
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

        return CreateSyncManager(googleCalendarService);
    }

    private SyncManager CreateSyncManager(Mock<IGoogleCalendarService> googleCalendarService)
    {
        return new SyncManager(
            googleCalendarService.Object,
            _contextFactory,
            new EventIdentityService(new EventRepository(_contextFactory)),
            NullLogger<SyncManager>.Instance);
    }

    private async Task SeedEventAsync(params Event[] events)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Events.AddRange(events);
        await context.SaveChangesAsync();
    }

    private static Event NewEvent(string eventId, string gcalEventId, string summary, DateTime start, DateTime end)
    {
        return new Event
        {
            EventId = eventId,
            GcalEventId = gcalEventId,
            CalendarId = "primary",
            Lifecycle = "approved",
            Publish = "published",
            Summary = summary,
            StartDatetime = start,
            EndDatetime = end,
            CreatedAt = start,
            UpdatedAt = start
        };
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
        bool isDeleted = false,
        string? recurringEventId = null,
        bool isRecurringInstance = false)
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
            recurringEventId,
            isRecurringInstance);
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

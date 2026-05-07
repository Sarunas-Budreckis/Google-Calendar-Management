using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class PendingEventRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public PendingEventRepositoryTests()
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
    public async Task UpsertAsync_CreatesThenUpdatesPendingEvent()
    {
        await using (var seedContext = await _contextFactory.CreateDbContextAsync())
        {
            seedContext.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-1",
                CalendarId = "primary",
                Summary = "Original",
                StartDatetime = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
                ColorId = "1",
                CreatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc)
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new PendingEventRepository(_contextFactory);
        var created = new PendingEvent
        {
            PendingEventId = "pending_evt_1",
            GcalEventId = "evt-1",
            CalendarId = "primary",
            Summary = "Draft title",
            Description = "Draft description",
            StartDatetime = new DateTime(2026, 04, 04, 11, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 04, 12, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "1",
            AppCreated = false,
            SourceSystem = "google-overlay",
            ReadyToPublish = false,
            CreatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc)
        };

        await repository.UpsertAsync(created);

        var updated = new PendingEvent
        {
            PendingEventId = created.PendingEventId,
            GcalEventId = created.GcalEventId,
            CalendarId = created.CalendarId,
            Summary = "Updated title",
            Description = "Updated description",
            StartDatetime = created.StartDatetime,
            EndDatetime = created.EndDatetime,
            IsAllDay = created.IsAllDay,
            ColorId = created.ColorId,
            AppCreated = created.AppCreated,
            SourceSystem = created.SourceSystem,
            ReadyToPublish = created.ReadyToPublish,
            CreatedAt = created.CreatedAt,
            UpdatedAt = new DateTime(2026, 04, 04, 8, 45, 0, DateTimeKind.Utc)
        };

        await repository.UpsertAsync(updated);

        var stored = await repository.GetByGcalEventIdAsync("evt-1");

        stored.Should().NotBeNull();
        stored!.Summary.Should().Be("Updated title");
        stored.Description.Should().Be("Updated description");
        stored.CreatedAt.Should().Be(created.CreatedAt);
        stored.UpdatedAt.Should().Be(updated.UpdatedAt);
    }

    [Fact]
    public async Task DeleteByGcalEventIdAsync_RemovesPendingEvent()
    {
        await using (var seedContext = await _contextFactory.CreateDbContextAsync())
        {
            seedContext.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-2",
                CalendarId = "primary",
                Summary = "Original",
                StartDatetime = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
                ColorId = "1",
                CreatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc)
            });
            seedContext.PendingEvents.Add(new PendingEvent
            {
                PendingEventId = "pending_evt_2",
                GcalEventId = "evt-2",
                CalendarId = "primary",
                Summary = "Draft",
                StartDatetime = new DateTime(2026, 04, 04, 11, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 04, 12, 0, 0, DateTimeKind.Utc),
                IsAllDay = false,
                ColorId = "1",
                AppCreated = false,
                SourceSystem = "google-overlay",
                ReadyToPublish = false,
                CreatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc)
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new PendingEventRepository(_contextFactory);

        await repository.DeleteByGcalEventIdAsync("evt-2");

        var stored = await repository.GetByGcalEventIdAsync("evt-2");
        stored.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsDraftWhenSchemaDoesNotDefineStoreDefaults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE pending_event (
                    pending_event_id TEXT NOT NULL CONSTRAINT PK_pending_event PRIMARY KEY,
                    gcal_event_id TEXT NULL,
                    calendar_id TEXT NOT NULL,
                    summary TEXT NULL,
                    description TEXT NULL,
                    start_datetime TEXT NULL,
                    end_datetime TEXT NULL,
                    is_all_day INTEGER NULL,
                    color_id TEXT NULL,
                    app_created INTEGER NOT NULL,
                    source_system TEXT NULL,
                    ready_to_publish INTEGER NOT NULL,
                    publish_attempted_at TEXT NULL,
                    publish_error TEXT NULL,
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );
                CREATE UNIQUE INDEX idx_pending_event_gcal_event_id ON pending_event (gcal_event_id);
                CREATE INDEX idx_pending_event_date ON pending_event (start_datetime, end_datetime);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestDbContextFactory(options);
        var repository = new PendingEventRepository(factory);
        var createdAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc);

        await repository.UpsertAsync(new PendingEvent
        {
            PendingEventId = "pending_manual_1",
            CalendarId = "primary",
            Summary = "Draft title",
            Description = "Draft description",
            StartDatetime = new DateTime(2026, 04, 04, 11, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 04, 12, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "azure",
            AppCreated = true,
            SourceSystem = "manual",
            ReadyToPublish = false,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });

        var stored = await repository.GetByPendingEventIdAsync("pending_manual_1");

        stored.Should().NotBeNull();
        stored!.AppCreated.Should().BeTrue();
        stored.ReadyToPublish.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertAsync_ColorChange_UpdatesPendingColorWithoutTouchingLiveEventOrVersionHistory()
    {
        await using (var seedContext = await _contextFactory.CreateDbContextAsync())
        {
            seedContext.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-3",
                CalendarId = "primary",
                Summary = "Original",
                StartDatetime = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
                ColorId = "1",
                CreatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc)
            });
            await seedContext.SaveChangesAsync();
        }

        var repository = new PendingEventRepository(_contextFactory);
        await repository.UpsertAsync(new PendingEvent
        {
            PendingEventId = "pending_evt_3",
            GcalEventId = "evt-3",
            CalendarId = "primary",
            Summary = "Original",
            StartDatetime = new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "purple",
            AppCreated = false,
            SourceSystem = "google-overlay",
            ReadyToPublish = false,
            CreatedAt = new DateTime(2026, 04, 04, 8, 30, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 04, 8, 45, 0, DateTimeKind.Utc)
        });

        var stored = await repository.GetByGcalEventIdAsync("evt-3");
        stored.Should().NotBeNull();
        stored!.ColorId.Should().Be("purple");

        await using var verificationContext = await _contextFactory.CreateDbContextAsync();
        var liveEvent = await verificationContext.GcalEvents.SingleAsync(item => item.GcalEventId == "evt-3");
        liveEvent.ColorId.Should().Be("1");
        verificationContext.GcalEventVersions.Should().BeEmpty();
    }

    public void Dispose()
    {
        _connection.Dispose();
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

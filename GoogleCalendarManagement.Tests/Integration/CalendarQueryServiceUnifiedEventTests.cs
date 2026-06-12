using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class CalendarQueryServiceUnifiedEventTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public CalendarQueryServiceUnifiedEventTests()
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
    public async Task GetEventsForRangeAsync_ExcludesSoftDeletedUnifiedEvents()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Events.AddRange(
                CreateEvent("evt-visible", "Visible event"),
                CreateEvent("evt-deleted", "Deleted event", isDeleted: true));
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        var events = await service.GetEventsForRangeAsync(
            new DateOnly(2026, 04, 05),
            new DateOnly(2026, 04, 05));

        events.Select(e => e.EventId).Should().ContainSingle().Which.Should().Be("evt-visible");
    }

    [Fact]
    public async Task GetEventByIdAsync_ReturnsNullForSoftDeletedUnifiedEvent()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Events.Add(CreateEvent("evt-deleted", "Deleted event", isDeleted: true));
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        var ev = await service.GetEventByIdAsync("evt-deleted");

        ev.Should().BeNull();
    }

    [Fact]
    public async Task GetEventsForRangeAsync_DerivesSourceKindOpacityAndStatusFromLifecycleAndPublish()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.Events.AddRange(
                CreateEvent("evt-candidate", "Candidate", lifecycle: "candidate", publish: "local_only"),
                CreateEvent("evt-pending", "Pending", lifecycle: "approved", publish: "local_only"),
                CreateEvent("evt-google", "Google", lifecycle: "approved", publish: "published"),
                CreateEvent("evt-dirty-google", "Dirty Google", lifecycle: "approved", publish: "published", hasUnpublishedChanges: true));
            await context.SaveChangesAsync();
        }

        var service = CreateService();

        var events = await service.GetEventsForRangeAsync(
            new DateOnly(2026, 04, 05),
            new DateOnly(2026, 04, 05));

        events.Should().Contain(e => e.EventId == "evt-candidate" &&
            e.SourceKind == CalendarEventSourceKind.Candidate &&
            e.Opacity == 0.6 &&
            e.StatusLabel == "Candidate event");
        events.Should().Contain(e => e.EventId == "evt-pending" &&
            e.SourceKind == CalendarEventSourceKind.Pending &&
            e.Opacity == 0.6 &&
            e.StatusLabel == "Not yet published to Google Calendar");
        events.Should().Contain(e => e.EventId == "evt-google" &&
            e.SourceKind == CalendarEventSourceKind.Google &&
            e.Opacity == 1.0 &&
            e.StatusLabel == "");
        events.Should().Contain(e => e.EventId == "evt-dirty-google" &&
            e.SourceKind == CalendarEventSourceKind.Google &&
            e.Opacity == 0.6 &&
            e.StatusLabel == "Local changes, pending push to GCal");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private CalendarQueryService CreateService() =>
        new(new EventRepository(_contextFactory), new ColorMappingService());

    private static Event CreateEvent(
        string eventId,
        string summary,
        bool isDeleted = false,
        string lifecycle = "approved",
        string publish = "published",
        bool hasUnpublishedChanges = false)
    {
        var now = new DateTime(2026, 04, 05, 8, 0, 0, DateTimeKind.Utc);
        return new Event
        {
            EventId = eventId,
            GcalEventId = $"gcal-{eventId}",
            CalendarId = "primary",
            Summary = summary,
            StartDatetime = new DateTime(2026, 04, 05, 9, 0, 0, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 05, 10, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "azure",
            Lifecycle = lifecycle,
            Publish = publish,
            HasUnpublishedChanges = hasUnpublishedChanges,
            IsDeleted = isDeleted,
            CreatedAt = now,
            UpdatedAt = now
        };
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

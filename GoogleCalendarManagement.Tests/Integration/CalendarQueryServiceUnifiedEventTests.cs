using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
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

        var service = new CalendarQueryService(_contextFactory, new ColorMappingService());

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

        var service = new CalendarQueryService(_contextFactory, new ColorMappingService());

        var ev = await service.GetEventByIdAsync("evt-deleted");

        ev.Should().BeNull();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static Event CreateEvent(string eventId, string summary, bool isDeleted = false)
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
            Lifecycle = "approved",
            Publish = "published",
            HasUnpublishedChanges = false,
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

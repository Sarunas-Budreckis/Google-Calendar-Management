using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class CalendarQueryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public CalendarQueryServiceTests()
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
    public async Task GetEventsForRangeAsync_ReturnsOnlyEventsInRequestedRange()
    {
        await SeedMonthEventsAsync(totalEvents: 50, deletedEvery: 0);
        var repository = new GcalEventRepository(_contextFactory);
        var service = new CalendarQueryService(repository, new ColorMappingService());

        var events = await service.GetEventsForRangeAsync(
            new DateOnly(2026, 01, 10),
            new DateOnly(2026, 01, 20));

        // Seed places events on days (index % 28) + 1 for index 0..49.
        // Days 10–20 (inclusive, 11 days) appear at indices 9–19 and 37–47 → 2 events each = 22 total.
        const int expectedCount = 22;
        events.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task GetEventsForRangeAsync_ExcludesSoftDeletedEvents()
    {
        await SeedMonthEventsAsync(totalEvents: 12, deletedEvery: 3);
        var repository = new GcalEventRepository(_contextFactory);
        var service = new CalendarQueryService(repository, new ColorMappingService());

        var events = await service.GetEventsForRangeAsync(
            new DateOnly(2026, 01, 01),
            new DateOnly(2026, 01, 31));

        events.Should().HaveCount(8);
        events.Should().NotContain(evt => evt.GcalEventId.EndsWith("-deleted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetEventsForRangeAsync_MapsNullSummaryToEmptyString()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "event-null-summary",
                CalendarId = "primary",
                Summary = null,
                StartDatetime = new DateTime(2026, 01, 15, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 01, 15, 10, 0, 0, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 01, 15, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 01, 15, 8, 0, 0, DateTimeKind.Utc)
            });
            await context.SaveChangesAsync();
        }

        var repository = new GcalEventRepository(_contextFactory);
        var service = new CalendarQueryService(repository, new ColorMappingService());

        var events = await service.GetEventsForRangeAsync(
            new DateOnly(2026, 01, 01),
            new DateOnly(2026, 01, 31));

        events.Should().ContainSingle();
        events[0].Title.Should().BeEmpty();
        events[0].ColorHex.Should().Be("#0088CC");
    }

    [Fact]
    public async Task GetEventsForRangeAsync_DeletedAllDayEvent_DoesNotCreateYearViewBars()
    {
        var date = new DateOnly(2026, 01, 18);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "deleted-all-day",
                CalendarId = "primary",
                Summary = "Deleted All Day",
                StartDatetime = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                EndDatetime = date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                IsAllDay = true,
                IsDeleted = true,
                CreatedAt = new DateTime(2026, 01, 18, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 01, 18, 8, 0, 0, DateTimeKind.Utc)
            });
            await context.SaveChangesAsync();
        }

        var repository = new GcalEventRepository(_contextFactory);
        var service = new CalendarQueryService(repository, new ColorMappingService());

        var events = await service.GetEventsForRangeAsync(date, date);
        var projection = YearViewDayProjectionBuilder.Build([date], events, new Dictionary<DateOnly, SyncStatus>());

        events.Should().BeEmpty();
        projection.DayLookup[date].SingleDayAllDayBar.HasContent.Should().BeFalse();
        projection.DayLookup[date].MultiDayAllDayBar.HasContent.Should().BeFalse();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private async Task SeedMonthEventsAsync(int totalEvents, int deletedEvery)
    {
        var events = new List<GcalEvent>();
        var createdAt = new DateTime(2026, 01, 01, 8, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < totalEvents; index++)
        {
            var day = (index % 28) + 1;
            var isDeleted = deletedEvery > 0 && (index + 1) % deletedEvery == 0;
            var id = isDeleted ? $"event-{index + 1}-deleted" : $"event-{index + 1}";

            events.Add(new GcalEvent
            {
                GcalEventId = id,
                CalendarId = "primary",
                Summary = $"Event {index + 1}",
                StartDatetime = new DateTime(2026, 01, day, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 01, day, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = isDeleted,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.AddRange(events);
        await context.SaveChangesAsync();
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

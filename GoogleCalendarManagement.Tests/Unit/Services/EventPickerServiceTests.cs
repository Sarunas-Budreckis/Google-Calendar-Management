using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EventEntity = GoogleCalendarManagement.Data.Entities.Event;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class EventPickerServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly EventRepository _eventRepository;
    private readonly ColorMappingService _colorService;

    // Fixed reference time: 2026-06-16T09:00:00 UTC
    private static readonly DateTimeOffset RangeStart = new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    public EventPickerServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        _eventRepository = new EventRepository(_contextFactory);
        _colorService = new ColorMappingService();
    }

    public void Dispose() => _connection.Dispose();

    private IEventPickerService CreateService() => new EventPickerService(_eventRepository, _colorService);

    [Fact]
    public async Task ConcurrentEvent_AppearsInConcurrentEvents_NotOtherEvents()
    {
        await SeedEventAsync("evt-concurrent", "Concurrent Event",
            start: new DateTime(2026, 6, 16, 8, 30, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 16, 9, 30, 0, DateTimeKind.Utc),
            lifecycle: "approved");

        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, null);

        result.ConcurrentEvents.Should().ContainSingle(e => e.EventId == "evt-concurrent");
        result.OtherEvents.Select(e => e.EventId).Should().NotContain("evt-concurrent");
    }

    [Fact]
    public async Task OtherEvents_SortedByMidpointProximity_NearerFirst()
    {
        // evt-far: midpoint is 7 days before range (farther)
        await SeedEventAsync("evt-far", "Far Event",
            start: new DateTime(2026, 6, 9, 8, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc),
            lifecycle: "approved");

        // evt-near: midpoint is 1 day before range (closer)
        await SeedEventAsync("evt-near", "Near Event",
            start: new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            lifecycle: "approved");

        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, null);

        result.OtherEvents.Should().HaveCount(2);
        result.OtherEvents[0].EventId.Should().Be("evt-near");
        result.OtherEvents[1].EventId.Should().Be("evt-far");
    }

    [Fact]
    public async Task LongRange_IncludesEventsNearRangeEndWindow()
    {
        var longRangeEnd = RangeStart.AddMonths(6);
        await SeedEventAsync("evt-near-range-end", "Near Range End",
            start: longRangeEnd.AddDays(1).UtcDateTime,
            end: longRangeEnd.AddDays(1).AddHours(1).UtcDateTime,
            lifecycle: "approved");

        var result = await CreateService().GetCandidatesAsync(RangeStart, longRangeEnd, null);

        result.OtherEvents.Should().ContainSingle(e => e.EventId == "evt-near-range-end");
    }

    [Fact]
    public async Task ApprovedEvent_WithNullEndDatetime_UsesStartAsEnd()
    {
        var start = new DateTime(2026, 6, 16, 9, 30, 0, DateTimeKind.Utc);
        await SeedEventAsync("evt-point", "Point Event",
            start: start,
            end: null,
            lifecycle: "approved");

        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, null);

        var item = result.ConcurrentEvents.Should().ContainSingle(e => e.EventId == "evt-point").Subject;
        item.EndLocal.Should().Be(item.StartLocal);
    }

    [Fact]
    public async Task RangeEndBeforeRangeStart_Throws()
    {
        var act = () => CreateService().GetCandidatesAsync(RangeEnd, RangeStart, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("rangeEnd");
    }

    [Fact]
    public async Task CandidateEvents_ExcludedFromBothLists()
    {
        await SeedEventAsync("evt-candidate", "Candidate Event",
            start: new DateTime(2026, 6, 16, 9, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc),
            lifecycle: "candidate");

        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, null);

        result.ConcurrentEvents.Select(e => e.EventId).Should().NotContain("evt-candidate");
        result.OtherEvents.Select(e => e.EventId).Should().NotContain("evt-candidate");
    }

    [Fact]
    public async Task SearchText_FiltersEventsCaseInsensitively()
    {
        await SeedEventAsync("evt-foo", "Foo Activity",
            start: new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
            lifecycle: "approved");

        await SeedEventAsync("evt-bar", "Bar Activity",
            start: new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc),
            lifecycle: "approved");

        // Case-insensitive search for "foo"
        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, "FOO");

        result.OtherEvents.Should().ContainSingle(e => e.EventId == "evt-foo");
        result.OtherEvents.Select(e => e.EventId).Should().NotContain("evt-bar");
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsBothListsEmpty()
    {
        var result = await CreateService().GetCandidatesAsync(RangeStart, RangeEnd, null);

        result.ConcurrentEvents.Should().BeEmpty();
        result.OtherEvents.Should().BeEmpty();
    }

    private async Task SeedEventAsync(
        string eventId,
        string summary,
        DateTime start,
        DateTime? end,
        string lifecycle = "approved")
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _eventRepository.UpsertAsync(new EventEntity
        {
            EventId = eventId,
            CalendarId = "primary",
            Summary = summary,
            StartDatetime = start,
            EndDatetime = end,
            IsAllDay = false,
            Lifecycle = lifecycle,
            Publish = "published",
            HasUnpublishedChanges = false,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}

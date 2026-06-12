using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using EventEntity = GoogleCalendarManagement.Data.Entities.Event;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class EventRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public EventRepositoryTests()
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
    public async Task UpsertAsync_CreatesEventAndRetrievesByEventId()
    {
        var repository = new EventRepository(_contextFactory);
        var ev = CreateEvent("evt-local-1", "gcal-1");

        await repository.UpsertAsync(ev, CancellationToken.None);

        var stored = await repository.GetByEventIdAsync("evt-local-1", CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.EventId.Should().Be("evt-local-1");
        stored.GcalEventId.Should().Be("gcal-1");
        stored.Summary.Should().Be("Test event");
    }

    [Fact]
    public async Task GetByGcalEventIdAsync_RetrievesStableEvent()
    {
        var repository = new EventRepository(_contextFactory);
        await repository.UpsertAsync(CreateEvent("evt-local-2", "gcal-2"), CancellationToken.None);

        var stored = await repository.GetByGcalEventIdAsync("gcal-2", CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.EventId.Should().Be("evt-local-2");
    }

    [Fact]
    public async Task EventIdentityService_MintsDistinctIdsAndResolvesByEitherKey()
    {
        var repository = new EventRepository(_contextFactory);
        var identityService = new EventIdentityService(repository);
        await repository.UpsertAsync(CreateEvent("evt-local-3", "gcal-3"), CancellationToken.None);

        var first = identityService.MintEventId();
        var second = identityService.MintEventId();
        var resolvedByEventId = await identityService.ResolveEventIdAsync("evt-local-3", null, CancellationToken.None);
        var resolvedByGcalId = await identityService.ResolveEventIdAsync(null, "gcal-3", CancellationToken.None);

        first.Should().NotBeNullOrWhiteSpace();
        first.Should().HaveLength(32);
        first.Should().NotBe(second);
        resolvedByEventId.Should().Be("evt-local-3");
        resolvedByGcalId.Should().Be("evt-local-3");
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsOverlappingApprovedNonDeletedEvents()
    {
        var repository = new EventRepository(_contextFactory);
        await repository.UpsertAsync(CreateEvent(
            "evt-overlap",
            "gcal-overlap",
            startUtc: new DateTime(2026, 04, 05, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 04, 05, 10, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        await repository.UpsertAsync(CreateEvent(
            "evt-outside",
            "gcal-outside",
            startUtc: new DateTime(2026, 04, 07, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 04, 07, 10, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        await repository.UpsertAsync(CreateEvent(
            "evt-candidate",
            "gcal-candidate",
            startUtc: new DateTime(2026, 04, 05, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 04, 05, 10, 0, 0, DateTimeKind.Utc),
            lifecycle: "candidate"), CancellationToken.None);
        await repository.UpsertAsync(CreateEvent(
            "evt-deleted",
            "gcal-deleted",
            startUtc: new DateTime(2026, 04, 05, 9, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 04, 05, 10, 0, 0, DateTimeKind.Utc),
            isDeleted: true), CancellationToken.None);

        var events = await repository.GetByDateRangeAsync(
            new DateOnly(2026, 04, 05),
            new DateOnly(2026, 04, 05),
            CancellationToken.None);

        events.Select(e => e.EventId).Should().Contain(["evt-overlap", "evt-candidate"]);
        events.Select(e => e.EventId).Should().NotContain(["evt-outside", "evt-deleted"]);
    }

    [Fact]
    public async Task DeleteByEventIdAsync_RemovesEvent()
    {
        var repository = new EventRepository(_contextFactory);
        await repository.UpsertAsync(CreateEvent("evt-delete", "gcal-delete"), CancellationToken.None);

        await repository.DeleteByEventIdAsync("evt-delete", CancellationToken.None);

        var stored = await repository.GetByEventIdAsync("evt-delete", CancellationToken.None);
        stored.Should().BeNull();

        await using var context = await _contextFactory.CreateDbContextAsync();
        var tombstone = await context.DeletedEvents.SingleAsync();
        tombstone.EventId.Should().Be("evt-delete");
        tombstone.GcalEventId.Should().Be("gcal-delete");
        tombstone.DeletionSource.Should().Be("user");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static EventEntity CreateEvent(
        string eventId,
        string? gcalEventId,
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        string lifecycle = "approved",
        bool isDeleted = false)
    {
        var now = new DateTime(2026, 04, 04, 8, 0, 0, DateTimeKind.Utc);
        return new EventEntity
        {
            EventId = eventId,
            GcalEventId = gcalEventId,
            CalendarId = "primary",
            Summary = "Test event",
            Description = "Test description",
            StartDatetime = startUtc ?? new DateTime(2026, 04, 04, 9, 0, 0, DateTimeKind.Utc),
            EndDatetime = endUtc ?? new DateTime(2026, 04, 04, 10, 0, 0, DateTimeKind.Utc),
            IsAllDay = false,
            ColorId = "azure",
            Lifecycle = lifecycle,
            Publish = "published",
            HasUnpublishedChanges = false,
            IsDeleted = isDeleted,
            SourceSystem = "manual",
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

using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Unit;

public sealed class SyncStatusServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public SyncStatusServiceTests()
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

    // ── GetSyncStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SyncStatusService_DateWithNonDeletedEvent_ReturnsSynced()
    {
        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-1",
                CalendarId = "primary",
                StartDatetime = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 15));

        result[new DateOnly(2026, 3, 15)].Should().Be(SyncStatus.Synced);
    }

    [Fact]
    public async Task SyncStatusService_DateWithOnlyDeletedEvents_ReturnsNotSynced()
    {
        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-deleted",
                CalendarId = "primary",
                StartDatetime = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc),
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 15));

        result[new DateOnly(2026, 3, 15)].Should().Be(SyncStatus.NotSynced);
    }

    [Fact]
    public async Task SyncStatusService_MultiDayTimedEvent_MarksEachCoveredDate()
    {
        // Timed event spanning 3 days
        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-multiday",
                CalendarId = "primary",
                StartDatetime = new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 3, 12, 2, 0, 0, DateTimeKind.Utc),
                IsAllDay = false,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 13));

        result[new DateOnly(2026, 3, 9)].Should().Be(SyncStatus.NotSynced);
        result[new DateOnly(2026, 3, 10)].Should().Be(SyncStatus.Synced);
        result[new DateOnly(2026, 3, 11)].Should().Be(SyncStatus.Synced);
        result[new DateOnly(2026, 3, 12)].Should().Be(SyncStatus.Synced);
        result[new DateOnly(2026, 3, 13)].Should().Be(SyncStatus.NotSynced);
    }

    [Fact]
    public async Task SyncStatusService_AllDayEventWithMidnightExclusiveEnd_MarksCorrectDates()
    {
        // Single-day all-day event: StartDatetime = 2026-03-15 00:00, EndDatetime = 2026-03-16 00:00 (exclusive)
        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.GcalEvents.Add(new GcalEvent
            {
                GcalEventId = "evt-allday",
                CalendarId = "primary",
                StartDatetime = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc),
                IsAllDay = true,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 3, 14), new DateOnly(2026, 3, 17));

        result[new DateOnly(2026, 3, 14)].Should().Be(SyncStatus.NotSynced);
        result[new DateOnly(2026, 3, 15)].Should().Be(SyncStatus.Synced, "the single all-day event covers only March 15");
        result[new DateOnly(2026, 3, 16)].Should().Be(SyncStatus.NotSynced, "exclusive end date must not be marked synced");
        result[new DateOnly(2026, 3, 17)].Should().Be(SyncStatus.NotSynced);
    }

    [Fact]
    public async Task SyncStatusService_EmptyDatabase_ReturnsAllNotSynced()
    {
        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 5));

        result.Should().HaveCount(5);
        result.Values.Should().AllBeEquivalentTo(SyncStatus.NotSynced);
    }

    [Fact]
    public async Task SyncStatusService_RangeContainsEveryDate()
    {
        var sut = CreateSut();
        var result = await sut.GetSyncStatusAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        result.Should().HaveCount(31);
        for (var d = new DateOnly(2026, 1, 1); d <= new DateOnly(2026, 1, 31); d = d.AddDays(1))
        {
            result.Should().ContainKey(d);
        }
    }

    // ── GetLastSyncTimeAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task SyncStatusService_GetLastSyncTime_IgnoresFailedOrNonGcalRows()
    {
        var expectedTime = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.DataSourceRefreshes.AddRange(
                new DataSourceRefresh
                {
                    SourceName = "gcal",
                    Success = true,
                    LastRefreshedAt = expectedTime,
                    RecordsFetched = 10
                },
                new DataSourceRefresh
                {
                    SourceName = "gcal",
                    Success = false,                         // failed — must be ignored
                    LastRefreshedAt = expectedTime.AddHours(1),
                    ErrorMessage = "auth error"
                },
                new DataSourceRefresh
                {
                    SourceName = "other_source",             // wrong source — must be ignored
                    Success = true,
                    LastRefreshedAt = expectedTime.AddHours(2)
                }
            );
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetLastSyncTimeAsync();

        result.Should().Be(expectedTime);
    }

    [Fact]
    public async Task SyncStatusService_GetLastSyncTime_ReturnsNullWhenNoSuccessfulRows()
    {
        await using (var ctx = _contextFactory.CreateDbContext())
        {
            ctx.DataSourceRefreshes.Add(new DataSourceRefresh
            {
                SourceName = "gcal",
                Success = false,
                LastRefreshedAt = DateTime.UtcNow,
                ErrorMessage = "network error"
            });
            await ctx.SaveChangesAsync();
        }

        var sut = CreateSut();
        var result = await sut.GetLastSyncTimeAsync();

        result.Should().BeNull();
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    [Fact]
    public void FormatLastSyncTooltip_NullTime_ReturnsNoSyncOnRecord()
    {
        var result = MainViewModel.FormatLastSyncTooltip(null);
        result.Should().Be("No sync on record");
    }

    [Fact]
    public void FormatRelativeLastSyncLabel_JustNow_ReturnsJustNow()
    {
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Local);
        var result = MainViewModel.FormatRelativeLastSyncLabel(now.AddSeconds(-30), now);
        result.Should().Be("Last synced just now");
    }

    [Fact]
    public void FormatRelativeLastSyncLabel_45Minutes_ReturnsMinutesAgo()
    {
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Local);
        var result = MainViewModel.FormatRelativeLastSyncLabel(now.AddMinutes(-45), now);
        result.Should().Be("Last synced 45 minutes ago");
    }

    [Fact]
    public void FormatRelativeLastSyncLabel_3Hours_ReturnsHoursAgo()
    {
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Local);
        var result = MainViewModel.FormatRelativeLastSyncLabel(now.AddHours(-3), now);
        result.Should().Be("Last synced 3 hours ago");
    }

    [Fact]
    public void FormatRelativeLastSyncLabel_1Hour_ReturnsSingular()
    {
        var now = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Local);
        var result = MainViewModel.FormatRelativeLastSyncLabel(now.AddHours(-1), now);
        result.Should().Be("Last synced 1 hour ago");
    }

    [Fact]
    public void FormatLastSyncTooltip_ExactTime_ReturnsLocalTimestamp()
    {
        var result = MainViewModel.FormatLastSyncTooltip(new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc));

        result.Should().Contain("2026");
        result.Should().Contain("Last synced");
    }

    public void Dispose() => _connection.Dispose();

    private SyncStatusService CreateSut() =>
        new SyncStatusService(_contextFactory, NullLogger<SyncStatusService>.Instance);

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new CalendarDbContext(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new CalendarDbContext(_options));
    }
}

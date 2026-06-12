using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class DataPointRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public DataPointRepositoryTests()
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
    public async Task DataPoint_InsertAndRetrieveById_RoundTripsAllFields()
    {
        var dataPoint = new DataPoint
        {
            SourceKey = "spotify_stream",
            SourceRef = "2026-06-12T13:00:00.0000000Z|Track",
            StartUtc = new DateTime(2026, 06, 12, 13, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2026, 06, 12, 13, 3, 33, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 06, 12, 14, 0, 0, DateTimeKind.Utc)
        };

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.DataPoints.Add(dataPoint);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = await _contextFactory.CreateDbContextAsync();
        var stored = await verifyContext.DataPoints.SingleAsync(e => e.DataPointId == dataPoint.DataPointId);

        stored.SourceKey.Should().Be(dataPoint.SourceKey);
        stored.SourceRef.Should().Be(dataPoint.SourceRef);
        stored.StartUtc.Should().Be(dataPoint.StartUtc);
        stored.EndUtc.Should().Be(dataPoint.EndUtc);
        stored.CreatedAt.Should().Be(dataPoint.CreatedAt);
    }

    [Fact]
    public async Task DataPoint_QueryByStartUtcRange_ReturnsOnlyMatchingRows()
    {
        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.DataPoints.AddRange(
                CreateDataPoint("early", new DateTime(2026, 06, 12, 07, 0, 0, DateTimeKind.Utc)),
                CreateDataPoint("inside-1", new DateTime(2026, 06, 12, 09, 0, 0, DateTimeKind.Utc)),
                CreateDataPoint("inside-2", new DateTime(2026, 06, 12, 10, 0, 0, DateTimeKind.Utc)),
                CreateDataPoint("late", new DateTime(2026, 06, 12, 13, 0, 0, DateTimeKind.Utc)));
            await context.SaveChangesAsync();
        }

        await using var verifyContext = await _contextFactory.CreateDbContextAsync();
        var rows = await verifyContext.DataPoints
            .Where(e => e.StartUtc >= new DateTime(2026, 06, 12, 08, 0, 0, DateTimeKind.Utc)
                && e.StartUtc < new DateTime(2026, 06, 12, 12, 0, 0, DateTimeKind.Utc))
            .OrderBy(e => e.StartUtc)
            .ToListAsync();

        rows.Select(e => e.SourceRef).Should().Equal(["inside-1", "inside-2"]);
    }

    [Fact]
    public async Task DataPoint_InstantDataPoint_RoundTripsWhenStartEqualsEnd()
    {
        var instant = new DateTime(2026, 06, 12, 11, 30, 0, DateTimeKind.Utc);
        var dataPoint = CreateDataPoint("instant", instant, instant);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.DataPoints.Add(dataPoint);
            await context.SaveChangesAsync();
        }

        await using var verifyContext = await _contextFactory.CreateDbContextAsync();
        var stored = await verifyContext.DataPoints.SingleAsync(e => e.DataPointId == dataPoint.DataPointId);

        stored.StartUtc.Should().Be(instant);
        stored.EndUtc.Should().Be(instant);
    }

    [Fact]
    public void SourcePointerResolverRegistry_GetResolver_ReturnsNullForUnknownKey()
    {
        var registry = new SourcePointerResolverRegistry();

        var resolver = registry.GetResolver("unknown_key");

        resolver.Should().BeNull();
    }

    [Fact]
    public async Task SourcePointerResolverRegistry_ResolveDisplayAsync_ReturnsNullForUnknownKey()
    {
        var registry = new SourcePointerResolverRegistry();

        var display = await registry.ResolveDisplayAsync("unknown_key", "ref", CancellationToken.None);

        display.Should().BeNull();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static DataPoint CreateDataPoint(
        string sourceRef,
        DateTime startUtc,
        DateTime? endUtc = null)
    {
        return new DataPoint
        {
            SourceKey = "toggl_entry",
            SourceRef = sourceRef,
            StartUtc = startUtc,
            EndUtc = endUtc ?? startUtc.AddMinutes(30),
            CreatedAt = new DateTime(2026, 06, 12, 14, 0, 0, DateTimeKind.Utc)
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

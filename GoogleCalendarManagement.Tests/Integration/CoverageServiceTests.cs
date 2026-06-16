using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class CoverageServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CalendarDbContext> _options;
    private readonly TestDbContextFactory _contextFactory;

    public CoverageServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(_options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(_options);
    }

    [Fact]
    public async Task GetDateSourceCoverage_ZeroDatapoints_ReturnsFull()
    {
        var service = new CoverageService(_contextFactory);

        var result = await service.GetDateSourceCoverageAsync(new DateOnly(2026, 6, 1), "toggl_sleep");

        result.Level.Should().Be(CoverageLevel.Full);
        result.Total.Should().Be(0);
        result.Covered.Should().Be(0);
    }

    [Fact]
    public async Task GetDateSourceCoverage_AllCovered_ReturnsFull()
    {
        var date = new DateOnly(2026, 6, 1);
        var start1 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var start2 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO data_point (data_point_id, source_key, source_ref, start_utc, end_utc, created_at)
            VALUES (1, 'toggl_sleep', 'ref1', @s1, @e1, @now), (2, 'toggl_sleep', 'ref2', @s2, @e2, @now)",
            ("@s1", start1.ToString("O")), ("@e1", start1.AddHours(1).ToString("O")),
            ("@s2", start2.ToString("O")), ("@e2", start2.AddHours(1).ToString("O")),
            ("@now", DateTime.UtcNow.ToString("O")));

        await CreateLinkTableAsync(_connection);
        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO link (link_id, data_point_id, state, origin, created_at, updated_at)
            VALUES (1, 1, 'linked', 'manual', @now, @now), (2, 2, 'linked', 'manual', @now, @now)",
            ("@now", DateTime.UtcNow.ToString("O")));

        var service = new CoverageService(_contextFactory);
        var result = await service.GetDateSourceCoverageAsync(date, "toggl_sleep");

        result.Level.Should().Be(CoverageLevel.Full);
        result.Total.Should().Be(2);
        result.Covered.Should().Be(2);
    }

    [Fact]
    public async Task GetDateSourceCoverage_EfSeededDataPoint_UsesProviderDateTimeFormat()
    {
        var date = new DateOnly(2026, 6, 1);
        await using (var context = new CalendarDbContext(_options))
        {
            context.DataPoints.Add(new DataPoint
            {
                SourceKey = "toggl_sleep",
                SourceRef = "ef-ref",
                StartUtc = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var service = new CoverageService(_contextFactory);
        var result = await service.GetDateSourceCoverageAsync(date, "toggl_sleep");

        result.Total.Should().Be(1);
        result.Covered.Should().Be(0);
        result.Level.Should().Be(CoverageLevel.None);
    }

    [Fact]
    public async Task GetDateSourceCoverage_DuplicateLinks_CountsDistinctDataPoints()
    {
        var date = new DateOnly(2026, 6, 1);
        var start = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO data_point (data_point_id, source_key, source_ref, start_utc, end_utc, created_at)
            VALUES (1, 'toggl_sleep', 'ref1', @s, @e, @now)",
            ("@s", start.ToString("O")), ("@e", start.AddHours(1).ToString("O")),
            ("@now", DateTime.UtcNow.ToString("O")));

        await CreateLinkTableAsync(_connection);
        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO link (link_id, data_point_id, state, origin, created_at, updated_at)
            VALUES (1, 1, 'linked', 'manual', @now, @now), (2, 1, 'linked', 'manual', @now, @now)",
            ("@now", DateTime.UtcNow.ToString("O")));

        var service = new CoverageService(_contextFactory);
        var result = await service.GetDateSourceCoverageAsync(date, "toggl_sleep");

        result.Total.Should().Be(1);
        result.Covered.Should().Be(1);
        result.Level.Should().Be(CoverageLevel.Full);
    }

    [Fact]
    public async Task GetDateSourceCoverage_PartialCoverage_ReturnsPartial()
    {
        var date = new DateOnly(2026, 6, 1);
        var start1 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var start2 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var start3 = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO data_point (data_point_id, source_key, source_ref, start_utc, end_utc, created_at)
            VALUES (1, 'toggl_sleep', 'ref1', @s1, @e1, @now),
                   (2, 'toggl_sleep', 'ref2', @s2, @e2, @now),
                   (3, 'toggl_sleep', 'ref3', @s3, @e3, @now)",
            ("@s1", start1.ToString("O")), ("@e1", start1.AddHours(1).ToString("O")),
            ("@s2", start2.ToString("O")), ("@e2", start2.AddHours(1).ToString("O")),
            ("@s3", start3.ToString("O")), ("@e3", start3.AddHours(1).ToString("O")),
            ("@now", DateTime.UtcNow.ToString("O")));

        await CreateLinkTableAsync(_connection);
        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO link (link_id, data_point_id, state, origin, created_at, updated_at)
            VALUES (1, 1, 'linked', 'manual', @now, @now)",
            ("@now", DateTime.UtcNow.ToString("O")));

        var service = new CoverageService(_contextFactory);
        var result = await service.GetDateSourceCoverageAsync(date, "toggl_sleep");

        result.Level.Should().Be(CoverageLevel.Partial);
        result.Total.Should().Be(3);
        result.Covered.Should().Be(1);
    }

    [Fact]
    public async Task GetDateSourceCoverage_NoCoverage_ReturnsNone()
    {
        var date = new DateOnly(2026, 6, 1);
        var start1 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var start2 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        await ExecuteNonQueryAsync(_connection, @"
            INSERT INTO data_point (data_point_id, source_key, source_ref, start_utc, end_utc, created_at)
            VALUES (1, 'toggl_sleep', 'ref1', @s1, @e1, @now), (2, 'toggl_sleep', 'ref2', @s2, @e2, @now)",
            ("@s1", start1.ToString("O")), ("@e1", start1.AddHours(1).ToString("O")),
            ("@s2", start2.ToString("O")), ("@e2", start2.AddHours(1).ToString("O")),
            ("@now", DateTime.UtcNow.ToString("O")));

        await CreateLinkTableAsync(_connection);
        // No link rows inserted

        var service = new CoverageService(_contextFactory);
        var result = await service.GetDateSourceCoverageAsync(date, "toggl_sleep");

        result.Level.Should().Be(CoverageLevel.None);
        result.Total.Should().Be(2);
        result.Covered.Should().Be(0);
    }

    [Fact]
    public async Task GetDateSourceCoverage_LinkTableMissing_GracefulFallback()
    {
        // Use a separate in-memory connection that has data_point but not link
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();

        await ExecuteNonQueryAsync(conn, @"
            CREATE TABLE data_point (
                data_point_id INTEGER PRIMARY KEY,
                source_key TEXT NOT NULL,
                source_ref TEXT NOT NULL,
                start_utc TEXT NOT NULL,
                end_utc TEXT NOT NULL,
                created_at TEXT NOT NULL
            )");

        var start = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        await ExecuteNonQueryAsync(conn, @"
            INSERT INTO data_point (data_point_id, source_key, source_ref, start_utc, end_utc, created_at)
            VALUES (1, 'toggl_sleep', 'ref1', @s, @e, @now)",
            ("@s", start.ToString("O")), ("@e", start.AddHours(1).ToString("O")),
            ("@now", DateTime.UtcNow.ToString("O")));

        var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(conn).Options;
        var factory = new TestDbContextFactory(options);
        var service = new CoverageService(factory);

        var act = async () => await service.GetDateSourceCoverageAsync(new DateOnly(2026, 6, 1), "toggl_sleep");

        await act.Should().NotThrowAsync();
        var result = await service.GetDateSourceCoverageAsync(new DateOnly(2026, 6, 1), "toggl_sleep");
        result.Total.Should().BeGreaterThan(0);
        result.Covered.Should().Be(0);
        result.Level.Should().Be(CoverageLevel.None);
    }

    [Fact]
    public async Task GetDayCoverage_DataPointTableMissing_ReturnsFullZero()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();

        var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(conn).Options;
        var factory = new TestDbContextFactory(options);
        var service = new CoverageService(factory);

        var result = await service.GetDayCoverageAsync(new DateOnly(2026, 6, 1));

        result.Total.Should().Be(0);
        result.Covered.Should().Be(0);
        result.Level.Should().Be(CoverageLevel.Full);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static async Task CreateLinkTableAsync(SqliteConnection conn)
    {
        await ExecuteNonQueryAsync(conn, @"
            CREATE TABLE IF NOT EXISTS link (
                link_id INTEGER PRIMARY KEY,
                data_point_id INTEGER NOT NULL,
                event_id TEXT NULL,
                state TEXT NOT NULL,
                origin TEXT NOT NULL,
                rule_id TEXT NULL,
                action_group_id TEXT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            )");
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        await cmd.ExecuteNonQueryAsync();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CalendarDbContext(_options));
    }
}

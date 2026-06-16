using FluentAssertions;
using GoogleCalendarManagement.Constants;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.Services.DataLinking;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit.Services.DataLinking;

public sealed class Civ5ClumpBlockProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly EightFifteenRuleService _eightFifteenRule = new();

    public Civ5ClumpBlockProviderTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new CalendarDbContext(options);
        ctx.Database.EnsureCreated();
        _contextFactory = new TestDbContextFactory(options);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void SourceKey_MatchesCiv5ProjectorSourceKey()
    {
        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);

        provider.SourceKey.Should().Be(Civ5SaveScannerService.SourceKey);
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_EmptyRange_ReturnsEmpty()
    {
        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);

        var result = await provider.GetClumpsAndBlocksAsync(from, to);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_TwoPointsWithinGap_YieldsOneClump()
    {
        var t0 = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(20);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var p0 = new Civ5SessionPoint { FileModifiedAt = t0, GameMode = "single", ScannedAt = DateTime.UtcNow };
        var p1 = new Civ5SessionPoint { FileModifiedAt = t1, GameMode = "single", ScannedAt = DateTime.UtcNow };
        ctx.Civ5SessionPoints.AddRange(p0, p1);
        await ctx.SaveChangesAsync();

        ctx.DataPoints.AddRange(
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p0.Id.ToString(), StartUtc = t0, EndUtc = t0, CreatedAt = DateTime.UtcNow },
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p1.Id.ToString(), StartUtc = t1, EndUtc = t1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t1.AddMinutes(1));

        result.Should().HaveCount(1);
        result[0].Clump.DataPoints.Should().HaveCount(2);
        result[0].Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_TwoPointsBeyondGap_YieldsTwoClumps()
    {
        var t0 = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(60); // beyond 30-min gap

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var p0 = new Civ5SessionPoint { FileModifiedAt = t0, GameMode = "single", ScannedAt = DateTime.UtcNow };
        var p1 = new Civ5SessionPoint { FileModifiedAt = t1, GameMode = "single", ScannedAt = DateTime.UtcNow };
        ctx.Civ5SessionPoints.AddRange(p0, p1);
        await ctx.SaveChangesAsync();

        ctx.DataPoints.AddRange(
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p0.Id.ToString(), StartUtc = t0, EndUtc = t0, CreatedAt = DateTime.UtcNow },
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p1.Id.ToString(), StartUtc = t1, EndUtc = t1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t1.AddMinutes(1));

        result.Should().HaveCount(2);
        result[0].Clump.DataPoints.Should().HaveCount(1);
        result[1].Clump.DataPoints.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_InvalidSourceRef_SkipsBadDataPoint()
    {
        var t0 = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.Add(new DataPoint
        {
            SourceKey = SourceKeys.Civ5,
            SourceRef = "not-a-row-id",
            StartUtc = t0,
            EndUtc = t0,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddMinutes(1));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_Blocks_MatchEightFifteenOutput()
    {
        var t0 = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(20);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var p0 = new Civ5SessionPoint { FileModifiedAt = t0, GameMode = "single", ScannedAt = DateTime.UtcNow };
        var p1 = new Civ5SessionPoint { FileModifiedAt = t1, GameMode = "single", ScannedAt = DateTime.UtcNow };
        ctx.Civ5SessionPoints.AddRange(p0, p1);
        await ctx.SaveChangesAsync();

        ctx.DataPoints.AddRange(
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p0.Id.ToString(), StartUtc = t0, EndUtc = t0, CreatedAt = DateTime.UtcNow },
            new DataPoint { SourceKey = SourceKeys.Civ5, SourceRef = p1.Id.ToString(), StartUtc = t1, EndUtc = t1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var provider = new Civ5ClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t1.AddMinutes(1));

        var expected = _eightFifteenRule.ApplyRule(t0, t1);
        result[0].Blocks.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            result[0].Blocks[i].BlockStartUtc.Should().Be(expected[i].Start);
            result[0].Blocks[i].BlockEndUtc.Should().Be(expected[i].End);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new CalendarDbContext(_options));
    }
}

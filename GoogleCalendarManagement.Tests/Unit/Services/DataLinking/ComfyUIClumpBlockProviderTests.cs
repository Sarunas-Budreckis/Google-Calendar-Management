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

public sealed class ComfyUIClumpBlockProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly EightFifteenRuleService _eightFifteenRule = new();

    public ComfyUIClumpBlockProviderTests()
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
    public void SourceKey_MatchesComfyUIProjectorSourceKey()
    {
        var provider = new ComfyUIClumpBlockProvider(_contextFactory, _eightFifteenRule);

        provider.SourceKey.Should().Be(ComfyUIFolderScannerService.SourceKey);
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_EmptyRange_ReturnsEmpty()
    {
        var provider = new ComfyUIClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = await provider.GetClumpsAndBlocksAsync(from, from.AddDays(1));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_TwoPointsWithinGap_YieldsOneClump()
    {
        var t0 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(10); // within 15-min gap

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var p0 = new ComfyUIScanPoint { Timestamp = t0, EventType = "run", ScannedAt = DateTime.UtcNow };
        var p1 = new ComfyUIScanPoint { Timestamp = t1, EventType = "run", ScannedAt = DateTime.UtcNow };
        ctx.ComfyUIScanPoints.AddRange(p0, p1);
        await ctx.SaveChangesAsync();

        ctx.DataPoints.AddRange(
            new DataPoint { SourceKey = SourceKeys.ComfyUI, SourceRef = p0.Id.ToString(), StartUtc = t0, EndUtc = t0, CreatedAt = DateTime.UtcNow },
            new DataPoint { SourceKey = SourceKeys.ComfyUI, SourceRef = p1.Id.ToString(), StartUtc = t1, EndUtc = t1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var provider = new ComfyUIClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t1.AddMinutes(1));

        result.Should().HaveCount(1);
        result[0].Clump.DataPoints.Should().HaveCount(2);
        result[0].Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_TwoPointsBeyondGap_YieldsTwoClumps()
    {
        var t0 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(30); // beyond 15-min gap

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var p0 = new ComfyUIScanPoint { Timestamp = t0, EventType = "run", ScannedAt = DateTime.UtcNow };
        var p1 = new ComfyUIScanPoint { Timestamp = t1, EventType = "run", ScannedAt = DateTime.UtcNow };
        ctx.ComfyUIScanPoints.AddRange(p0, p1);
        await ctx.SaveChangesAsync();

        ctx.DataPoints.AddRange(
            new DataPoint { SourceKey = SourceKeys.ComfyUI, SourceRef = p0.Id.ToString(), StartUtc = t0, EndUtc = t0, CreatedAt = DateTime.UtcNow },
            new DataPoint { SourceKey = SourceKeys.ComfyUI, SourceRef = p1.Id.ToString(), StartUtc = t1, EndUtc = t1, CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var provider = new ComfyUIClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t1.AddMinutes(1));

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_InvalidSourceRef_SkipsBadDataPoint()
    {
        var t0 = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.Add(new DataPoint
        {
            SourceKey = SourceKeys.ComfyUI,
            SourceRef = "not-a-row-id",
            StartUtc = t0,
            EndUtc = t0,
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var provider = new ComfyUIClumpBlockProvider(_contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddMinutes(1));

        result.Should().BeEmpty();
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

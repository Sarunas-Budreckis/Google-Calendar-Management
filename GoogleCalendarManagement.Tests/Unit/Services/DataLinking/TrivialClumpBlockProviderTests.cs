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

public sealed class TrivialClumpBlockProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly EightFifteenRuleService _eightFifteenRule = new();

    public TrivialClumpBlockProviderTests()
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
    public async Task GetClumpsAndBlocksAsync_ThreeDataPoints_YieldsThreeClumps()
    {
        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint(SourceKeys.CallLog, "1", t0, t0.AddMinutes(5)),
            MakePoint(SourceKeys.CallLog, "2", t0.AddHours(1), t0.AddHours(1).AddMinutes(10)),
            MakePoint(SourceKeys.CallLog, "3", t0.AddHours(2), t0.AddHours(2).AddMinutes(3)));
        await ctx.SaveChangesAsync();

        var provider = new TrivialClumpBlockProvider(SourceKeys.CallLog, _contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddHours(3));

        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r =>
        {
            r.Clump.DataPoints.Should().HaveCount(1);
            r.Blocks.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_EachClumpHasCorrectDataPoint()
    {
        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint(SourceKeys.Toggl, "entry-1", t0, t0.AddMinutes(30)),
            MakePoint(SourceKeys.Toggl, "entry-2", t0.AddHours(1), t0.AddHours(1).AddMinutes(45)));
        await ctx.SaveChangesAsync();

        var provider = new TrivialClumpBlockProvider(SourceKeys.Toggl, _contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddHours(2));

        result.Should().HaveCount(2);
        result.Select(r => r.Clump.DataPoints[0].SourceRef)
            .Should().BeEquivalentTo("entry-1", "entry-2");
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_BlocksMatchEightFifteenOutput()
    {
        var start = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = start.AddMinutes(35);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.Add(MakePoint(SourceKeys.CallLog, "c1", start, end));
        await ctx.SaveChangesAsync();

        var provider = new TrivialClumpBlockProvider(SourceKeys.CallLog, _contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(start, end.AddMinutes(1));

        var expected = _eightFifteenRule.ApplyRule(start, end);
        result.Should().HaveCount(1);
        result[0].Blocks.Should().HaveCount(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            result[0].Blocks[i].BlockStartUtc.Should().Be(expected[i].Start);
            result[0].Blocks[i].BlockEndUtc.Should().Be(expected[i].End);
        }
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_OnlyMatchesCorrectSourceKey()
    {
        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint(SourceKeys.CallLog, "call-1", t0, t0.AddMinutes(5)),
            MakePoint(SourceKeys.Toggl, "toggl-1", t0.AddHours(1), t0.AddHours(1).AddMinutes(30)));
        await ctx.SaveChangesAsync();

        var provider = new TrivialClumpBlockProvider(SourceKeys.CallLog, _contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddHours(3));

        result.Should().HaveCount(1);
        result[0].Clump.DataPoints[0].SourceRef.Should().Be("call-1");
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_DurationPointOverlappingRange_IsIncluded()
    {
        var from = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint(SourceKeys.CallLog, "overlap", from.AddMinutes(-5), from.AddMinutes(5)),
            MakePoint(SourceKeys.CallLog, "ended-before", from.AddMinutes(-10), from));
        await ctx.SaveChangesAsync();

        var provider = new TrivialClumpBlockProvider(SourceKeys.CallLog, _contextFactory, _eightFifteenRule);
        var result = await provider.GetClumpsAndBlocksAsync(from, from.AddHours(1));

        result.Should().ContainSingle();
        result[0].Clump.DataPoints[0].SourceRef.Should().Be("overlap");
    }

    private static DataPoint MakePoint(string sourceKey, string sourceRef, DateTime start, DateTime end) =>
        new() { SourceKey = sourceKey, SourceRef = sourceRef, StartUtc = start, EndUtc = end, CreatedAt = DateTime.UtcNow };

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new CalendarDbContext(_options));
    }
}

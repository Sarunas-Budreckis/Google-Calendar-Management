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

public sealed class PhoneClumpBlockProviderTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly TogglSlidingWindowService _slidingWindowService = new();
    private readonly EightFifteenRuleService _eightFifteenRule = new();

    public PhoneClumpBlockProviderTests()
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
    public async Task GetClumpsAndBlocksAsync_PointsWithinGap_YieldsOneClump()
    {
        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint("p1", t0, t0.AddMinutes(10)),
            MakePoint("p2", t0.AddMinutes(12), t0.AddMinutes(20)));
        await ctx.SaveChangesAsync();

        var provider = CreateProvider();
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddHours(1));

        result.Should().ContainSingle();
        result[0].Clump.DataPoints.Should().HaveCount(2);
        result[0].Blocks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_DurationPointOverlappingRange_IsIncluded()
    {
        var from = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.Add(MakePoint("overlap", from.AddMinutes(-5), from.AddMinutes(5)));
        await ctx.SaveChangesAsync();

        var provider = CreateProvider();
        var result = await provider.GetClumpsAndBlocksAsync(from, from.AddHours(1));

        result.Should().ContainSingle();
        result[0].Clump.DataPoints[0].SourceRef.Should().Be("overlap");
    }

    [Fact]
    public async Task GetClumpsAndBlocksAsync_ZeroDurationPointAtWindowEnd_RemainsInClump()
    {
        var t0 = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var windowEnd = t0.AddMinutes(10);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        ctx.DataPoints.AddRange(
            MakePoint("duration", t0, windowEnd),
            MakePoint("instant-at-end", windowEnd, windowEnd));
        await ctx.SaveChangesAsync();

        var provider = CreateProvider();
        var result = await provider.GetClumpsAndBlocksAsync(t0, t0.AddHours(1));

        result.Should().ContainSingle();
        result[0].Clump.DataPoints.Select(dp => dp.SourceRef)
            .Should().BeEquivalentTo("duration", "instant-at-end");
    }

    private PhoneClumpBlockProvider CreateProvider() =>
        new(_contextFactory, _slidingWindowService, _eightFifteenRule);

    private static DataPoint MakePoint(string sourceRef, DateTime start, DateTime end) =>
        new()
        {
            SourceKey = SourceKeys.TogglPhone,
            SourceRef = sourceRef,
            StartUtc = start,
            EndUtc = end,
            CreatedAt = DateTime.UtcNow
        };

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new CalendarDbContext(_options));
    }
}

using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class DataPointReconciliationSweepServiceTests : IDisposable
{
    private const string SourceKey = "test_source";

    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<IDataPointProjectorRegistry> _registry = new();

    public DataPointReconciliationSweepServiceTests()
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
    public async Task RunPostImportAsync_InsertsOrphanedDataPoints()
    {
        var projector = CreateProjector(SourceKey, TwoSpecs(SourceKey));
        RegisterSingle(projector);
        var service = CreateService();

        await service.RunPostImportAsync(SourceKey);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var rows = await ctx.DataPoints.Where(dp => dp.SourceKey == SourceKey).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.SourceRef).Should().BeEquivalentTo("ref-1", "ref-2");
    }

    [Fact]
    public async Task RunPostImportAsync_IsIdempotent()
    {
        var projector = CreateProjector(SourceKey, TwoSpecs(SourceKey));
        RegisterSingle(projector);
        var service = CreateService();

        await service.RunPostImportAsync(SourceKey);
        await service.RunPostImportAsync(SourceKey);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var count = await ctx.DataPoints.CountAsync(dp => dp.SourceKey == SourceKey);
        count.Should().Be(2);
    }

    [Fact]
    public async Task RunPostImportAsync_UpdatesExistingDataPointTimes()
    {
        var start = new DateTime(2026, 06, 12, 8, 0, 0, DateTimeKind.Utc);
        await using (var seedCtx = await _contextFactory.CreateDbContextAsync())
        {
            seedCtx.DataPoints.Add(new DataPoint
            {
                SourceKey = SourceKey,
                SourceRef = "ref-1",
                StartUtc = start,
                EndUtc = start.AddHours(1),
                CreatedAt = start
            });
            await seedCtx.SaveChangesAsync();
        }

        var updatedStart = start.AddHours(2);
        var projector = CreateProjector(
            SourceKey,
            [new DataPointSpec(SourceKey, "ref-1", updatedStart, updatedStart.AddHours(1))]);
        RegisterSingle(projector);
        var service = CreateService();

        await service.RunPostImportAsync(SourceKey);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var row = await ctx.DataPoints.SingleAsync(dp => dp.SourceKey == SourceKey && dp.SourceRef == "ref-1");
        row.StartUtc.Should().Be(updatedStart);
        row.EndUtc.Should().Be(updatedStart.AddHours(1));
    }

    [Fact]
    public async Task RunPostImportAsync_UnknownSourceKey_DoesNotThrow()
    {
        _registry.Setup(r => r.GetProjector("unregistered")).Returns((IDataPointProjector?)null);
        var service = CreateService();

        var act = async () => await service.RunPostImportAsync("unregistered");

        await act.Should().NotThrowAsync();
        await using var ctx = await _contextFactory.CreateDbContextAsync();
        (await ctx.DataPoints.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task RunStartupDriftCheckAsync_CallsProjectorForEveryRegisteredSource()
    {
        var projectorA = CreateProjector("source_a", TwoSpecs("source_a"));
        var projectorB = CreateProjector("source_b", TwoSpecs("source_b"));
        var projectorC = CreateProjector("source_c", TwoSpecs("source_c"));

        _registry.Setup(r => r.GetAllProjectors())
            .Returns(new[] { projectorA.Object, projectorB.Object, projectorC.Object });
        _registry.Setup(r => r.GetProjector("source_a")).Returns(projectorA.Object);
        _registry.Setup(r => r.GetProjector("source_b")).Returns(projectorB.Object);
        _registry.Setup(r => r.GetProjector("source_c")).Returns(projectorC.Object);
        var service = CreateService();

        await service.RunStartupDriftCheckAsync(CancellationToken.None);

        projectorA.Verify(p => p.GetOrphanedSpecsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()), Times.Once);
        projectorB.Verify(p => p.GetOrphanedSpecsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()), Times.Once);
        projectorC.Verify(p => p.GetOrphanedSpecsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RebuildRegistryForSourceAsync_DeletesExistingThenReinserts()
    {
        await using (var seedCtx = await _contextFactory.CreateDbContextAsync())
        {
            seedCtx.DataPoints.AddRange(
                NewDataPoint(SourceKey, "old-1"),
                NewDataPoint(SourceKey, "old-2"),
                NewDataPoint(SourceKey, "old-3"));
            await seedCtx.SaveChangesAsync();
        }

        var projector = CreateProjector(SourceKey, TwoSpecs(SourceKey));
        RegisterSingle(projector);
        var service = CreateService();

        await service.RebuildRegistryForSourceAsync(SourceKey);

        await using var ctx = await _contextFactory.CreateDbContextAsync();
        var rows = await ctx.DataPoints.Where(dp => dp.SourceKey == SourceKey).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Select(r => r.SourceRef).Should().BeEquivalentTo("ref-1", "ref-2");
    }

    private DataPointReconciliationSweepService CreateService()
    {
        return new DataPointReconciliationSweepService(
            _registry.Object,
            _contextFactory,
            TimeProvider.System,
            NullLogger<DataPointReconciliationSweepService>.Instance);
    }

    private void RegisterSingle(Mock<IDataPointProjector> projector)
    {
        var key = projector.Object.SourceKey;
        _registry.Setup(r => r.GetProjector(key)).Returns(projector.Object);
        _registry.Setup(r => r.GetAllProjectors()).Returns(new[] { projector.Object });
    }

    private static Mock<IDataPointProjector> CreateProjector(string sourceKey, IReadOnlyList<DataPointSpec> specs)
    {
        var projector = new Mock<IDataPointProjector>();
        projector.Setup(p => p.SourceKey).Returns(sourceKey);
        projector.Setup(p => p.GetOrphanedSpecsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(specs);
        projector.Setup(p => p.GetAllRawSourceRefsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(specs.Select(s => s.SourceRef).ToList());
        return projector;
    }

    private static IReadOnlyList<DataPointSpec> TwoSpecs(string sourceKey)
    {
        var start = new DateTime(2026, 06, 12, 8, 0, 0, DateTimeKind.Utc);
        return new List<DataPointSpec>
        {
            new(sourceKey, "ref-1", start, start.AddHours(1)),
            new(sourceKey, "ref-2", start.AddHours(1), start.AddHours(2))
        };
    }

    private static DataPoint NewDataPoint(string sourceKey, string sourceRef) => new()
    {
        SourceKey = sourceKey,
        SourceRef = sourceRef,
        StartUtc = new DateTime(2026, 06, 11, 8, 0, 0, DateTimeKind.Utc),
        EndUtc = new DateTime(2026, 06, 11, 9, 0, 0, DateTimeKind.Utc),
        CreatedAt = new DateTime(2026, 06, 11, 10, 0, 0, DateTimeKind.Utc)
    };

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new CalendarDbContext(_options));
        }
    }
}

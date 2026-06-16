using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class DataPointProjectionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<CalendarDbContext> _options;
    private readonly TestDbContextFactory _contextFactory;

    public DataPointProjectionTests()
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
    public async Task TogglSleepProjector_ProjectsOnlySleepRowsWithExpectedSpec()
    {
        var start = new DateTime(2026, 6, 12, 3, 0, 0, DateTimeKind.Utc);
        await using (var ctx = new CalendarDbContext(_options))
        {
            ctx.TogglEntries.AddRange(
                new TogglEntry
                {
                    TogglId = 101,
                    StartTime = start,
                    EndTime = start.AddHours(7),
                    TogglDataType = TogglDataType.TogglSleep,
                    CreatedAt = start
                },
                new TogglEntry
                {
                    TogglId = 102,
                    StartTime = start.AddHours(9),
                    EndTime = start.AddHours(10),
                    TogglDataType = TogglDataType.TogglTransit,
                    CreatedAt = start
                });
            await ctx.SaveChangesAsync();
        }

        await using var assertCtx = new CalendarDbContext(_options);
        var specs = await new TogglSleepProjector().GetOrphanedSpecsAsync(assertCtx);

        specs.Should().ContainSingle();
        specs[0].Should().Be(new DataPointSpec(
            TogglSleepImportService.SourceKey,
            "101",
            start,
            start.AddHours(7)));
    }

    [Fact]
    public async Task SpotifyProjector_UsesNaturalKeyAndComputesPlayedExtent()
    {
        var playedAt = new DateTime(2026, 6, 12, 14, 32, 0, DateTimeKind.Utc);
        var naturalKey = $"{playedAt:yyyy-MM-ddTHH:mm:ss.fffffff}|Neon Genesis";
        await using (var ctx = new CalendarDbContext(_options))
        {
            ctx.SpotifyStreams.Add(new SpotifyStream
            {
                PlayedAt = playedAt,
                TrackName = "Neon Genesis",
                ArtistName = "Artist",
                DurationMs = 240000,
                MsPlayed = 180000,
                NaturalKey = naturalKey
            });
            await ctx.SaveChangesAsync();
        }

        await using var assertCtx = new CalendarDbContext(_options);
        var specs = await new SpotifyProjector().GetOrphanedSpecsAsync(assertCtx);

        specs.Should().ContainSingle();
        specs[0].Should().Be(new DataPointSpec(
            SpotifyImportService.SourceKey,
            naturalKey,
            playedAt.AddMinutes(-3),
            playedAt));
    }

    [Fact]
    public async Task ReconciliationSweep_IsIdempotentAcrossAllStorySources()
    {
        await SeedOneRowPerSourceAsync();
        var registry = new DataPointProjectorRegistry();
        RegisterStoryProjectors(registry);
        var sweep = new DataPointReconciliationSweepService(
            registry,
            _contextFactory,
            TimeProvider.System,
            NullLogger<DataPointReconciliationSweepService>.Instance);

        await sweep.RunStartupDriftCheckAsync();
        await sweep.RunStartupDriftCheckAsync();

        await using var ctx = new CalendarDbContext(_options);
        var counts = await ctx.DataPoints
            .GroupBy(dp => dp.SourceKey)
            .Select(g => new { SourceKey = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SourceKey, x => x.Count);

        counts.Should().Contain(new Dictionary<string, int>
        {
            [TogglSleepImportService.SourceKey] = 1,
            [TogglTransitImportService.SourceKey] = 1,
            [TogglPhoneCardProvider.SourceKey] = 1,
            [CallLogImportService.SourceKey] = 1,
            [Civ5SaveScannerService.SourceKey] = 1,
            [ComfyUIFolderScannerService.SourceKey] = 1,
            [SpotifyImportService.SourceKey] = 1,
            [OutlookImportService.SourceKey] = 1,
            [MapsTimelineImportHandler.SourceKey] = 1
        });
    }

    [Fact]
    public void HandlerProjectors_ReturnMatchingSourceKeys()
    {
        new TogglSleepImportHandler(null!, null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(TogglSleepImportService.SourceKey);
        new TogglTransitImportHandler(null!, null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(TogglTransitImportService.SourceKey);
        new TogglPhoneImportHandler(null!, null!).GetProjector()!.SourceKey
            .Should().Be(TogglPhoneCardProvider.SourceKey);
        new CallLogImportHandler(null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(CallLogImportService.SourceKey);
        new Civ5ImportHandler(null!, null!).GetProjector()!.SourceKey
            .Should().Be(Civ5SaveScannerService.SourceKey);
        new ComfyUIImportHandler(null!, null!, null!, TimeProvider.System).GetProjector()!.SourceKey
            .Should().Be(ComfyUIFolderScannerService.SourceKey);
        new SpotifyImportHandler(null!, null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(SpotifyImportService.SourceKey);
        new OutlookImportHandler(null!, null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(OutlookImportService.SourceKey);
        ((IDataSourceImportHandler)new MapsTimelineImportHandler(null!, null!, null!, null!, null!, null!))
            .GetProjector()!.SourceKey.Should().Be(MapsTimelineImportHandler.SourceKey);
        new TogglCsvImportHandler(null!, null!, null!).GetProjector()!.SourceKey
            .Should().Be(TogglSleepImportService.SourceKey);
    }

    private async Task SeedOneRowPerSourceAsync()
    {
        var start = new DateTime(2026, 6, 12, 8, 0, 0, DateTimeKind.Utc);
        await using var ctx = new CalendarDbContext(_options);

        ctx.TogglEntries.AddRange(
            new TogglEntry
            {
                TogglId = 201,
                StartTime = start,
                EndTime = start.AddHours(1),
                TogglDataType = TogglDataType.TogglSleep,
                CreatedAt = start
            },
            new TogglEntry
            {
                TogglId = 202,
                StartTime = start.AddHours(2),
                EndTime = start.AddHours(3),
                TogglDataType = TogglDataType.TogglTransit,
                CreatedAt = start
            },
            new TogglEntry
            {
                TogglId = 203,
                StartTime = start.AddHours(4),
                EndTime = null,
                TogglDataType = TogglDataType.TogglPhone,
                CreatedAt = start
            });
        var callImport = new CallLogImport
        {
            ImportedAt = start,
            FileName = "calls.csv",
            RecordCount = 1,
            DateMin = DateOnly.FromDateTime(start),
            DateMax = DateOnly.FromDateTime(start)
        };
        ctx.CallLogImports.Add(callImport);
        ctx.CallLogEntries.Add(new CallLogEntry
        {
            Import = callImport,
            CallType = "Outgoing",
            Date = start.AddHours(5),
            DurationSeconds = 60,
            Service = "Phone"
        });
        ctx.Civ5SessionPoints.Add(new Civ5SessionPoint
        {
            ScannedAt = start.AddHours(6),
            FileModifiedAt = start.AddHours(6).AddMinutes(15),
            GameMode = "single"
        });
        ctx.ComfyUIScanPoints.Add(new ComfyUIScanPoint
        {
            ScannedAt = start.AddHours(7),
            Timestamp = start.AddHours(7).AddMinutes(15),
            EventType = "created"
        });
        var playedAt = start.AddHours(8);
        ctx.SpotifyStreams.Add(new SpotifyStream
        {
            PlayedAt = playedAt,
            TrackName = "Song",
            ArtistName = "Artist",
            DurationMs = 120000,
            MsPlayed = 0,
            NaturalKey = $"{playedAt:yyyy-MM-ddTHH:mm:ss.fffffff}|Song"
        });
        ctx.OutlookEvents.Add(new OutlookEvent
        {
            OutlookEventId = "outlook-1",
            Subject = "Work",
            StartDatetime = start.AddHours(9),
            EndDatetime = start.AddHours(10),
            LastSyncedAt = start
        });
        ctx.MapsTimelineRaws.Add(new MapsTimelineRaw
        {
            ImportedAt = start.AddHours(11),
            FileName = "Timeline.json",
            FileSizeBytes = 42,
            CoveredDateMin = DateOnly.FromDateTime(start.Date),
            CoveredDateMax = DateOnly.FromDateTime(start.Date.AddDays(1)),
            RawJson = "{}"
        });

        await ctx.SaveChangesAsync();
    }

    private static void RegisterStoryProjectors(DataPointProjectorRegistry registry)
    {
        registry.Register(new TogglSleepProjector());
        registry.Register(new TogglTransitProjector());
        registry.Register(new TogglPhoneProjector());
        registry.Register(new CallLogProjector());
        registry.Register(new Civ5Projector());
        registry.Register(new ComfyUIProjector());
        registry.Register(new SpotifyProjector());
        registry.Register(new OutlookProjector());
        registry.Register(new MapsTimelineProjector());
    }

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

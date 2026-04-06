using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class IcsExportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly string _tempDirectory;

    public IcsExportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"gcm-ics-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ExportToFileAsync_WritesOnlyIntersectingNonDeletedEvents()
    {
        await SeedEventsAsync();
        var exportPath = Path.Combine(_tempDirectory, "calendar-export-2026-04-05.ics");
        var service = new IcsExportService(
            new GcalEventRepository(_contextFactory),
            new StubIcsFileSavePickerService(exportPath),
            NullLogger<IcsExportService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 12, 00, 00, TimeSpan.Zero)));

        var result = await service.ExportToFileAsync(new DateOnly(2026, 04, 01), new DateOnly(2026, 04, 30));

        result.Success.Should().BeTrue();
        result.WasCancelled.Should().BeFalse();
        result.ExportedEventCount.Should().Be(2);
        result.FileName.Should().Be("calendar-export-2026-04-05.ics");
        File.Exists(exportPath).Should().BeTrue();

        var ics = await File.ReadAllTextAsync(exportPath);
        ics.Should().Contain("BEGIN:VCALENDAR");
        ics.Should().Contain("UID:evt-in-range");
        ics.Should().Contain("UID:evt-all-day");
        ics.Should().NotContain("UID:evt-deleted");
        ics.Should().NotContain("UID:evt-outside");
        ics.Should().NotContain("UID:evt-boundary");
    }

    [Fact]
    public async Task GetStoredEventRangeAsync_ReturnsEarliestStartAndLatestInclusiveEnd()
    {
        await SeedEventsAsync();
        var service = new IcsExportService(
            new GcalEventRepository(_contextFactory),
            new StubIcsFileSavePickerService(null),
            NullLogger<IcsExportService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 12, 00, 00, TimeSpan.Zero)));

        var range = await service.GetStoredEventRangeAsync();

        range.Should().Be((new DateOnly(2026, 03, 31), new DateOnly(2026, 05, 01)));
    }

    [Fact]
    public async Task ExportToFileAsync_NoEventsFound_DoesNotWriteFile()
    {
        var exportPath = Path.Combine(_tempDirectory, "empty.ics");
        var service = new IcsExportService(
            new GcalEventRepository(_contextFactory),
            new StubIcsFileSavePickerService(exportPath),
            NullLogger<IcsExportService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 12, 00, 00, TimeSpan.Zero)));

        var result = await service.ExportToFileAsync(new DateOnly(2026, 04, 01), new DateOnly(2026, 04, 30));

        result.Success.Should().BeTrue();
        result.ExportedEventCount.Should().Be(0);
        File.Exists(exportPath).Should().BeFalse();
    }

    [Fact]
    public async Task ExportToFileAsync_CancelledPicker_ReturnsCancelledResult()
    {
        var service = new IcsExportService(
            new GcalEventRepository(_contextFactory),
            new StubIcsFileSavePickerService(null),
            NullLogger<IcsExportService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 12, 00, 00, TimeSpan.Zero)));

        var result = await service.ExportToFileAsync(new DateOnly(2026, 04, 01), new DateOnly(2026, 04, 30));

        result.Success.Should().BeFalse();
        result.WasCancelled.Should().BeTrue();
        result.ExportedEventCount.Should().Be(0);
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private async Task SeedEventsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.AddRange(
            new GcalEvent
            {
                GcalEventId = "evt-in-range",
                CalendarId = "primary",
                Summary = "In Range",
                StartDatetime = new DateTime(2026, 04, 05, 15, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 05, 16, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 02, 10, 00, 00, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 04, 02, 10, 00, 00, DateTimeKind.Utc)
            },
            new GcalEvent
            {
                GcalEventId = "evt-all-day",
                CalendarId = "primary",
                Summary = "All Day",
                IsAllDay = true,
                StartDatetime = new DateTime(2026, 04, 10, 00, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 11, 00, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 03, 10, 00, 00, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 04, 03, 10, 00, 00, DateTimeKind.Utc)
            },
            new GcalEvent
            {
                GcalEventId = "evt-deleted",
                CalendarId = "primary",
                Summary = "Deleted",
                IsDeleted = true,
                StartDatetime = new DateTime(2026, 04, 12, 09, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 12, 10, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc)
            },
            new GcalEvent
            {
                GcalEventId = "evt-outside",
                CalendarId = "primary",
                Summary = "Outside",
                StartDatetime = new DateTime(2026, 05, 01, 09, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 05, 01, 10, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc)
            },
            new GcalEvent
            {
                GcalEventId = "evt-boundary",
                CalendarId = "primary",
                Summary = "Boundary",
                StartDatetime = new DateTime(2026, 03, 31, 23, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 01, 00, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc),
                CreatedAt = new DateTime(2026, 04, 04, 10, 00, 00, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();
    }

    private sealed class StubIcsFileSavePickerService : IIcsFileSavePickerService
    {
        private readonly string? _path;

        public StubIcsFileSavePickerService(string? path)
        {
            _path = path;
        }

        public Task<string?> PickSavePathAsync(string suggestedFileName)
        {
            return Task.FromResult(_path);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
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

using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Storage;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class IcsImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly string _tempDirectory;

    public IcsImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"gcm-ics-import-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ImportFromFileAsync_InsertsNewEvents_UsingSafeDefaults()
    {
        var importFile = await CreateStorageFileAsync(
            "insert.ics",
            """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:new-evt-1
SUMMARY:Imported Event
DESCRIPTION:Imported from backup
DTSTART:20260405T143000Z
DTEND:20260405T154500Z
END:VEVENT
END:VCALENDAR
""");

        var service = CreateService(new DateTimeOffset(2026, 04, 05, 18, 00, 00, TimeSpan.Zero));

        var result = await service.ImportFromFileAsync(importFile);

        result.Success.Should().BeTrue();
        result.ImportedEventCount.Should().Be(1);
        result.NewEventCount.Should().Be(1);
        result.UpdatedEventCount.Should().Be(0);
        result.SkippedEventCount.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var imported = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "new-evt-1");
        imported.CalendarId.Should().Be("primary");
        imported.Summary.Should().Be("Imported Event");
        imported.Description.Should().Be("Imported from backup");
        imported.StartDatetime.Should().Be(new DateTime(2026, 04, 05, 14, 30, 00, DateTimeKind.Utc));
        imported.EndDatetime.Should().Be(new DateTime(2026, 04, 05, 15, 45, 00, DateTimeKind.Utc));
        imported.IsAllDay.Should().BeFalse();
        imported.IsDeleted.Should().BeFalse();
        imported.AppPublished.Should().BeFalse();
        imported.ColorId.Should().Be("azure");
        imported.CreatedAt.Should().Be(new DateTime(2026, 04, 05, 18, 00, 00, DateTimeKind.Utc));
        imported.UpdatedAt.Should().Be(new DateTime(2026, 04, 05, 18, 00, 00, DateTimeKind.Utc));
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportFromFileAsync_UpdatesExistingEvent_WritesVersionHistory_AndLeavesUnrelatedRowsUntouched()
    {
        await SeedExistingEventsAsync();

        var importFile = await CreateStorageFileAsync(
            "update.ics",
            """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:existing-evt
SUMMARY:Updated Summary
DESCRIPTION:Updated description
DTSTART:20260406T080000Z
DTEND:20260406T093000Z
END:VEVENT
BEGIN:VEVENT
UID:new-evt-2
SUMMARY:Brand New
DTSTART;VALUE=DATE:20260410
DTEND;VALUE=DATE:20260411
END:VEVENT
END:VCALENDAR
""");

        var service = CreateService(new DateTimeOffset(2026, 04, 07, 09, 15, 00, TimeSpan.Zero));

        var result = await service.ImportFromFileAsync(importFile);

        result.Success.Should().BeTrue();
        result.ImportedEventCount.Should().Be(2);
        result.NewEventCount.Should().Be(1);
        result.UpdatedEventCount.Should().Be(1);
        result.SkippedEventCount.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var updated = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "existing-evt");
        updated.Summary.Should().Be("Updated Summary");
        updated.Description.Should().Be("Updated description");
        updated.StartDatetime.Should().Be(new DateTime(2026, 04, 06, 08, 00, 00, DateTimeKind.Utc));
        updated.EndDatetime.Should().Be(new DateTime(2026, 04, 06, 09, 30, 00, DateTimeKind.Utc));
        updated.IsAllDay.Should().BeFalse();
        updated.ColorId.Should().Be("banana");
        updated.AppPublished.Should().BeTrue();
        updated.CreatedAt.Should().Be(new DateTime(2026, 04, 01, 09, 00, 00, DateTimeKind.Utc));
        updated.UpdatedAt.Should().Be(new DateTime(2026, 04, 07, 09, 15, 00, DateTimeKind.Utc));

        var version = await context.GcalEventVersions.SingleAsync(evt => evt.GcalEventId == "existing-evt");
        version.ChangedBy.Should().Be("ics_import");
        version.ChangeReason.Should().Be("imported");
        version.Summary.Should().Be("Original Summary");
        version.Description.Should().Be("Original description");
        version.StartDatetime.Should().Be(new DateTime(2026, 04, 05, 08, 00, 00, DateTimeKind.Utc));
        version.EndDatetime.Should().Be(new DateTime(2026, 04, 05, 09, 00, 00, DateTimeKind.Utc));
        version.ColorId.Should().Be("banana");

        var untouched = await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "untouched-evt");
        untouched.Summary.Should().Be("Do Not Touch");
        untouched.UpdatedAt.Should().Be(new DateTime(2026, 04, 02, 10, 00, 00, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ImportFromFileAsync_InvalidCalendar_MakesNoDatabaseChanges()
    {
        await SeedExistingEventsAsync();
        var importFile = await CreateStorageFileAsync(
            "invalid.ics",
            """
BEGIN:VEVENT
UID:bad-evt
SUMMARY:Invalid
DTSTART:20260405T143000Z
DTEND:20260405T154500Z
END:VEVENT
""");

        var service = CreateService(new DateTimeOffset(2026, 04, 07, 09, 15, 00, TimeSpan.Zero));

        var result = await service.ImportFromFileAsync(importFile);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.GcalEvents.CountAsync()).Should().Be(2);
        (await context.GcalEventVersions.CountAsync()).Should().Be(0);
        (await context.GcalEvents.SingleAsync(evt => evt.GcalEventId == "existing-evt")).Summary.Should().Be("Original Summary");
    }

    public void Dispose()
    {
        _connection.Dispose();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private IcsImportService CreateService(DateTimeOffset utcNow)
    {
        return new IcsImportService(
            _contextFactory,
            NullLogger<IcsImportService>.Instance,
            new FixedTimeProvider(utcNow));
    }

    private async Task<StorageFile> CreateStorageFileAsync(string fileName, string content)
    {
        var path = Path.Combine(_tempDirectory, fileName);
        await File.WriteAllTextAsync(path, content.ReplaceLineEndings("\r\n"));
        return await StorageFile.GetFileFromPathAsync(path);
    }

    private async Task SeedExistingEventsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.GcalEvents.AddRange(
            new GcalEvent
            {
                GcalEventId = "existing-evt",
                CalendarId = "primary",
                Summary = "Original Summary",
                Description = "Original description",
                StartDatetime = new DateTime(2026, 04, 05, 08, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 05, 09, 00, 00, DateTimeKind.Utc),
                IsAllDay = false,
                ColorId = "banana",
                AppPublished = true,
                IsDeleted = false,
                CreatedAt = new DateTime(2026, 04, 01, 09, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 01, 09, 00, 00, DateTimeKind.Utc)
            },
            new GcalEvent
            {
                GcalEventId = "untouched-evt",
                CalendarId = "primary",
                Summary = "Do Not Touch",
                Description = "Existing row outside import set",
                StartDatetime = new DateTime(2026, 04, 08, 12, 00, 00, DateTimeKind.Utc),
                EndDatetime = new DateTime(2026, 04, 08, 13, 00, 00, DateTimeKind.Utc),
                IsAllDay = false,
                ColorId = "grape",
                CreatedAt = new DateTime(2026, 04, 02, 10, 00, 00, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 04, 02, 10, 00, 00, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();
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

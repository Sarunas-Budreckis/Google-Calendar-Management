using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class CallLogImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly RecordingRecipient _recipient = new();

    public CallLogImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);

        WeakReferenceMessenger.Default.Register<DataSourceImportCompletedMessage>(_recipient, (_, message) =>
        {
            _recipient.Messages.Add(message);
        });
    }

    [Fact]
    public async Task ImportFromStreamAsync_ParsesAllColumnsCorrectly()
    {
        var csv = BuildCsv(
            ("Incoming", "01/15/2026 09:30:00", "00:12:00", "+1234567890", "Mom", "Home", "iPhone"));
        var service = CreateService();

        var result = await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");

        result.Success.Should().BeTrue();
        result.NewRecordsInserted.Should().Be(1);
        result.DuplicatesSkipped.Should().Be(0);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.CallLogEntries.SingleAsync();
        entry.CallType.Should().Be("Incoming");
        entry.DurationSeconds.Should().Be(720);
        entry.Number.Should().Be("+1234567890");
        entry.Contact.Should().Be("Mom");
        entry.Location.Should().Be("Home");
        entry.Service.Should().Be("iPhone");
    }

    [Fact]
    public async Task ImportFromStreamAsync_SkipsDuplicates_SameDateNumberDuration()
    {
        var row = ("Outgoing", "01/15/2026 10:00:00", "00:05:00", "+9999999", "Dad", (string?)null, "iPhone");
        var csv = BuildCsv(row);
        var service = CreateService();

        var firstResult = await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");
        var secondResult = await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");

        firstResult.NewRecordsInserted.Should().Be(1);
        firstResult.DuplicatesSkipped.Should().Be(0);
        secondResult.NewRecordsInserted.Should().Be(0);
        secondResult.DuplicatesSkipped.Should().Be(1);

        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.CallLogEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ImportFromStreamAsync_CreatesCallLogImportRecord()
    {
        var csv = BuildCsv(
            ("Incoming", "03/01/2026 08:00:00", "00:20:00", "+111", "Alice", null, "iPhone"),
            ("Outgoing", "03/05/2026 15:30:00", "00:10:00", "+222", "Bob", null, "FaceTime"));
        var service = CreateService();

        await service.ImportFromStreamAsync(ToCsvStream(csv), "march_calls.csv");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var import = await context.CallLogImports.SingleAsync();
        import.FileName.Should().Be("march_calls.csv");
        import.RecordCount.Should().Be(2);
        import.DateMin.Should().Be(new DateOnly(2026, 3, 1));
        import.DateMax.Should().Be(new DateOnly(2026, 3, 5));
    }

    [Fact]
    public async Task ImportFromStreamAsync_WritesDataSourceImportLog()
    {
        var csv = BuildCsv(("Incoming", "01/15/2026 09:00:00", "00:11:00", "+100", "Test", null, "iPhone"));
        var service = CreateService();

        await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var log = await context.DataSourceImportLogs.Include(l => l.DataSource).SingleAsync();
        log.DataSource.SourceKey.Should().Be("call_log");
        log.Success.Should().BeTrue();
        log.RecordsFetched.Should().Be(1);
        log.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ImportFromStreamAsync_PublishesImportCompletedMessage()
    {
        var csv = BuildCsv(("Incoming", "01/15/2026 09:00:00", "00:11:00", "+100", "Test", null, "iPhone"));
        var service = CreateService();

        await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");

        _recipient.Messages.Should().ContainSingle();
        _recipient.Messages[0].SourceKey.Should().Be("call_log");
        _recipient.Messages[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task ImportFromStreamAsync_EmptyCsv_ReturnsSuccessWithZeroRecords()
    {
        var csv = "Call type,Date,Duration,Number,Contact,Location,Service\n";
        var service = CreateService();

        var result = await service.ImportFromStreamAsync(ToCsvStream(csv), "empty.csv");

        result.Success.Should().BeTrue();
        result.NewRecordsInserted.Should().Be(0);
        result.DuplicatesSkipped.Should().Be(0);
    }

    [Fact]
    public async Task ImportFromStreamAsync_NullableFieldsAreNullWhenEmpty()
    {
        var csv = BuildCsv(("Missed", "01/20/2026 14:00:00", "00:00:05", "", "", "", "iPhone"));
        var service = CreateService();

        await service.ImportFromStreamAsync(ToCsvStream(csv), "calls.csv");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var entry = await context.CallLogEntries.SingleAsync();
        entry.Number.Should().BeNull();
        entry.Contact.Should().BeNull();
        entry.Location.Should().BeNull();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(_recipient);
        _connection.Dispose();
    }

    private CallLogImportService CreateService() => new(_contextFactory);

    private static string BuildCsv(params (string CallType, string Date, string Duration, string? Number, string? Contact, string? Location, string Service)[] rows)
    {
        var lines = new List<string>
        {
            "Call type,Date,Duration,Number,Contact,Location,Service"
        };
        foreach (var (callType, date, duration, number, contact, location, service) in rows)
        {
            lines.Add($"{callType},{date},{duration},{number ?? ""},{contact ?? ""},{location ?? ""},{service}");
        }

        return string.Join("\n", lines);
    }

    private static Stream ToCsvStream(string csv)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream, leaveOpen: true);
        writer.Write(csv);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(new CalendarDbContext(_options));
    }

    private sealed class RecordingRecipient
    {
        public List<DataSourceImportCompletedMessage> Messages { get; } = [];
    }
}

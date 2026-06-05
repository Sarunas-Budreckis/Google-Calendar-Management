using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class TogglSleepImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<ITogglApiClient> _apiClient = new();
    private readonly Mock<IConfigRepository> _configRepository = new();
    private readonly RecordingRecipient _recipient = new();

    public TogglSleepImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
        _configRepository
            .Setup(mock => mock.GetConfigValueAsync("toggl_api_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        WeakReferenceMessenger.Default.Register<DataSourceImportCompletedMessage>(_recipient, (_, message) =>
        {
            _recipient.Messages.Add(message);
        });
    }

    [Fact]
    public async Task ImportAsync_WhenDescriptionContainsSleep_StoresEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync("test-token", DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "Deep sleep")]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(1);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.TogglEntries.SingleAsync();
        stored.TogglId.Should().Be(42);
        stored.Description.Should().Be("Deep sleep");
        stored.VisibleAsEvent.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_WhenDescriptionDoesNotContainSleep_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "Work session")]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenEntryIsRunning_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep", duration: -12345)]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenSleepEntryStartIsMissing_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep", start: null)]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenSleepEntryStartIsInvalid_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep", start: "not-a-date")]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WritesImportLogOnSuccess()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep")]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var log = await context.DataSourceImportLogs.Include(log => log.DataSource).SingleAsync();
        log.DataSource.SourceKey.Should().Be("toggl_sleep");
        log.CoveredStartDate.Should().Be(DateOnly.Parse("2026-05-01"));
        log.CoveredEndDate.Should().Be(DateOnly.Parse("2026-05-02"));
        log.RecordsFetched.Should().Be(1);
        log.Success.Should().BeTrue();
        log.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ImportAsync_WritesImportLogOnFailure()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TogglApiException("Toggl rejected the token."));
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Toggl rejected the token");
        await using var context = await _contextFactory.CreateDbContextAsync();
        var log = await context.DataSourceImportLogs.Include(log => log.DataSource).SingleAsync();
        log.DataSource.SourceKey.Should().Be("toggl_sleep");
        log.Success.Should().BeFalse();
        log.RecordsFetched.Should().Be(0);
        log.ErrorMessage.Should().Contain("Toggl rejected the token");
    }

    [Fact]
    public async Task ImportAsync_PublishesDataSourceImportCompletedMessage()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep")]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        _recipient.Messages.Should().ContainSingle();
        _recipient.Messages[0].SourceKey.Should().Be("toggl_sleep");
        _recipient.Messages[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_UpsertsByTogglId_NoDuplicates()
    {
        _apiClient
            .SetupSequence(mock => mock.GetTimeEntriesAsync(It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, "sleep", duration: 3600)])
            .ReturnsAsync([CreateEntry(42, "sleep updated", duration: 7200)]);
        var service = CreateService();

        await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));
        await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.TogglEntries.SingleAsync();
        stored.Description.Should().Be("sleep updated");
        stored.DurationSeconds.Should().Be(7200);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(_recipient);
        _connection.Dispose();
    }

    private TogglSleepImportService CreateService()
    {
        return new TogglSleepImportService(
            _contextFactory,
            _configRepository.Object,
            _apiClient.Object,
            TimeProvider.System);
    }

    private static TogglTimeEntryDto CreateEntry(
        long id,
        string description,
        int duration = 3600,
        string? start = "2026-05-01T22:00:00Z")
    {
        return new TogglTimeEntryDto(
            Id: id,
            Description: description,
            Start: start,
            Stop: "2026-05-01T23:00:00Z",
            Duration: duration,
            ProjectId: null,
            ProjectName: "Health",
            Tags: ["night"]);
    }

    private sealed class RecordingRecipient
    {
        public List<DataSourceImportCompletedMessage> Messages { get; } = [];
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

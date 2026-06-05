using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class TogglTransitImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;
    private readonly Mock<ITogglApiClient> _apiClient = new();
    private readonly Mock<IConfigRepository> _configRepository = new();

    public TogglTransitImportServiceTests()
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
    }

    [Fact]
    public async Task ImportAsync_WhenProjectIsTransit_StoresTransitEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42)]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(1);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var stored = await context.TogglEntries.SingleAsync();
        stored.TogglId.Should().Be(42);
        stored.ProjectName.Should().Be("Transit");
        stored.TogglDataType.Should().Be(TogglDataType.TogglTransit);
    }

    [Fact]
    public async Task ImportAsync_WhenTransitEntryStartIsMissing_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, start: null)]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ImportAsync_WhenTransitEntryStartIsInvalid_SkipsEntry()
    {
        _apiClient
            .Setup(mock => mock.GetTimeEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateEntry(42, start: "not-a-date")]);
        var service = CreateService();

        var result = await service.ImportAsync(DateOnly.Parse("2026-05-01"), DateOnly.Parse("2026-05-02"));

        result.Success.Should().BeTrue();
        result.RecordsFetched.Should().Be(0);
        await using var context = await _contextFactory.CreateDbContextAsync();
        (await context.TogglEntries.CountAsync()).Should().Be(0);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private TogglTransitImportService CreateService()
    {
        return new TogglTransitImportService(
            _contextFactory,
            _configRepository.Object,
            _apiClient.Object,
            TimeProvider.System);
    }

    private static TogglTimeEntryDto CreateEntry(
        long id,
        int duration = 3600,
        string? start = "2026-05-01T22:00:00Z")
    {
        return new TogglTimeEntryDto(
            Id: id,
            Description: "Driving",
            Start: start,
            Stop: "2026-05-01T23:00:00Z",
            Duration: duration,
            ProjectId: null,
            ProjectName: "Transit",
            Tags: null);
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

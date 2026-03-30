using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public SettingsViewModelTests()
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
    public async Task InitializeAsync_LoadsMostRecentSuccessfulLastSyncFromDatabase()
    {
        var latestSuccessfulSync = new DateTime(2026, 03, 30, 12, 45, 00, DateTimeKind.Utc);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            context.DataSourceRefreshes.AddRange(
                new DataSourceRefresh
                {
                    SourceName = "gcal",
                    Success = true,
                    LastRefreshedAt = latestSuccessfulSync.AddHours(-2)
                },
                new DataSourceRefresh
                {
                    SourceName = "gcal",
                    Success = false,
                    LastRefreshedAt = latestSuccessfulSync.AddHours(1)
                },
                new DataSourceRefresh
                {
                    SourceName = "gcal",
                    Success = true,
                    LastRefreshedAt = latestSuccessfulSync
                });

            await context.SaveChangesAsync();
        }

        var viewModel = CreateViewModel(_contextFactory, isAuthenticated: true);

        await viewModel.InitializeAsync();

        viewModel.LastSyncText.Should().Be(latestSuccessfulSync.ToLocalTime().ToString("g"));
    }

    [Fact]
    public async Task InitializeAsync_WhenLastSyncReadFails_SetsUnavailable()
    {
        var failingFactory = new ThrowingDbContextFactory();
        var viewModel = CreateViewModel(failingFactory, isAuthenticated: false);

        await viewModel.InitializeAsync();

        viewModel.LastSyncText.Should().Be("Unavailable");
    }

    [Fact]
    public async Task SyncWithGoogleCalendarAsync_WhenSyncThrows_ShowsFriendlyErrorWithoutLeavingViewModelSyncing()
    {
        var syncManager = new Mock<ISyncManager>();
        syncManager
            .Setup(manager => manager.SyncAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IProgress<SyncProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated sync failure."));

        var dialogService = new Mock<IContentDialogService>();
        var viewModel = CreateViewModel(
            _contextFactory,
            isAuthenticated: true,
            syncManager: syncManager.Object,
            dialogService: dialogService.Object);

        await viewModel.InitializeAsync();
        await viewModel.SyncWithGoogleCalendarCommand.ExecuteAsync(null);

        viewModel.IsSyncing.Should().BeFalse();
        viewModel.SyncStatusText.Should().Be("Unable to sync Google Calendar.");
        dialogService.Verify(
            service => service.ShowErrorAsync(
                "Google Calendar Sync",
                "Unable to sync Google Calendar. Check the log for details and try again."),
            Times.Once);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private static SettingsViewModel CreateViewModel(
        IDbContextFactory<CalendarDbContext> contextFactory,
        bool isAuthenticated,
        ISyncManager? syncManager = null,
        IContentDialogService? dialogService = null)
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(service => service.IsAuthenticatedAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(isAuthenticated));

        return new SettingsViewModel(
            googleCalendarService.Object,
            dialogService ?? Mock.Of<IContentDialogService>(),
            syncManager ?? Mock.Of<ISyncManager>(),
            contextFactory,
            NullLogger<SettingsViewModel>.Instance);
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

    private sealed class ThrowingDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        public CalendarDbContext CreateDbContext()
        {
            throw new InvalidOperationException("Simulated database access failure.");
        }

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated database access failure.");
        }
    }
}

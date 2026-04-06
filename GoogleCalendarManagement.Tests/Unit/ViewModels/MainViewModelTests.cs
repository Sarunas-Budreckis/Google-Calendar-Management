using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Windows.Storage;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class MainViewModelTests : IDisposable
{
    public MainViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task SwitchViewModeCommand_MonthView_LoadsWholeMonth_AndUpdatesBreadcrumb()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Month);

        queryService.LastFrom.Should().Be(new DateOnly(2026, 01, 01));
        queryService.LastTo.Should().Be(new DateOnly(2026, 01, 31));
        viewModel.BreadcrumbLabel.Should().Be("January 2026");
    }

    [Fact]
    public async Task SwitchViewModeCommand_WeekView_StartsOnMonday()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 03, 26)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Week);

        queryService.LastFrom.Should().Be(new DateOnly(2026, 03, 23));
        queryService.LastTo.Should().Be(new DateOnly(2026, 03, 29));
        viewModel.BreadcrumbLabel.Should().Be("Mar 23\u201329, 2026");
    }

    [Fact]
    public async Task NavigatePreviousCommand_MonthView_WrapsJanuaryToPreviousDecember()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.NavigatePreviousCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2025, 12, 15));
        queryService.LastFrom.Should().Be(new DateOnly(2025, 12, 01));
        queryService.LastTo.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public async Task NavigateTodayCommand_UsesTimeProviderDate()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Day, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 04, 02, 12, 0, 0, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        await viewModel.NavigateTodayCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 04, 02));
        queryService.LastFrom.Should().Be(new DateOnly(2026, 04, 02));
        queryService.LastTo.Should().Be(new DateOnly(2026, 04, 02));
    }

    [Fact]
    public async Task InitializeAsync_LoadsSavedYearState_AndBuildsYearBreadcrumb()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 06, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();

        viewModel.CurrentViewMode.Should().Be(ViewMode.Year);
        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 06, 15));
        viewModel.BreadcrumbLabel.Should().Be("2026");
        queryService.RequestedRanges.Should().Contain((new DateOnly(2026, 01, 01), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public async Task JumpToDateCommand_SetsCurrentDateAndLoadsCorrectRange()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 01, 01)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.JumpToDateCommand.ExecuteAsync(new DateOnly(2026, 06, 15));

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 06, 15));
        viewModel.CurrentViewMode.Should().Be(ViewMode.Month);
        queryService.LastFrom.Should().Be(new DateOnly(2026, 06, 01));
        queryService.LastTo.Should().Be(new DateOnly(2026, 06, 30));
    }

    [Fact]
    public async Task NavigateNextCommand_YearView_AdvancesOneYear_AndUpdatesBreadcrumb()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 06, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.NavigateNextCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2027, 06, 15));
        viewModel.BreadcrumbLabel.Should().Be("2027");
        queryService.RequestedRanges.Should().Contain((new DateOnly(2027, 01, 01), new DateOnly(2027, 12, 31)));
    }

    [Fact]
    public async Task YearViewNavigation_ReusesPreloadedAdjacentYears()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 06, 15)));
        var syncStatusService = new StubSyncStatusService();
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            syncStatusService: syncStatusService);

        await viewModel.InitializeAsync();
        await queryService.WaitForCallsAsync(11);
        await syncStatusService.WaitForCallsAsync(11);

        queryService.CallsByRange[(new DateOnly(2021, 01, 01), new DateOnly(2021, 12, 31))].Should().Be(1);
        queryService.CallsByRange[(new DateOnly(2031, 01, 01), new DateOnly(2031, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2021, 01, 01), new DateOnly(2021, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2031, 01, 01), new DateOnly(2031, 12, 31))].Should().Be(1);

        await viewModel.NavigateNextCommand.ExecuteAsync(null);
        await viewModel.NavigatePreviousCommand.ExecuteAsync(null);

        queryService.CallsByRange[(new DateOnly(2026, 01, 01), new DateOnly(2026, 12, 31))].Should().Be(1);
        queryService.CallsByRange[(new DateOnly(2027, 01, 01), new DateOnly(2027, 12, 31))].Should().Be(1);
        queryService.CallsByRange[(new DateOnly(2032, 01, 01), new DateOnly(2032, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2026, 01, 01), new DateOnly(2026, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2027, 01, 01), new DateOnly(2027, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2032, 01, 01), new DateOnly(2032, 12, 31))].Should().Be(1);
        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 06, 15));
        viewModel.BreadcrumbLabel.Should().Be("2026");
    }

    [Fact]
    public async Task InitializeAsync_YearView_PreloadsThreeYearsOnEachSide_AndPinsActualCurrentYear()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2034, 06, 15)));
        var syncStatusService = new StubSyncStatusService();
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            syncStatusService: syncStatusService,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 04, 02, 12, 0, 0, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        await queryService.WaitForCallsAsync(12);
        await syncStatusService.WaitForCallsAsync(12);

        foreach (var year in Enumerable.Range(2029, 11))
        {
            queryService.CallsByRange[(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31))].Should().Be(1);
            syncStatusService.CallsByRange[(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31))].Should().Be(1);
        }

        queryService.CallsByRange[(new DateOnly(2026, 01, 01), new DateOnly(2026, 12, 31))].Should().Be(1);
        syncStatusService.CallsByRange[(new DateOnly(2026, 01, 01), new DateOnly(2026, 12, 31))].Should().Be(1);
    }

    [Fact]
    public async Task NavigateNextCommand_WeekView_AdvancesSevenDays_AndUpdatesBreadcrumb()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Week, new DateOnly(2026, 03, 26)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.NavigateNextCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 04, 02));
        viewModel.BreadcrumbLabel.Should().Be("Mar 30\u2013Apr 5, 2026");
        queryService.LastFrom.Should().Be(new DateOnly(2026, 03, 30));
        queryService.LastTo.Should().Be(new DateOnly(2026, 04, 05));
    }

    [Fact]
    public async Task NavigateNextCommand_DayView_AdvancesOneDay_AndUpdatesBreadcrumb()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Day, new DateOnly(2026, 03, 26)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.NavigateNextCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 03, 27));
        viewModel.BreadcrumbLabel.Should().Be("Friday, 27 March 2026");
        queryService.LastFrom.Should().Be(new DateOnly(2026, 03, 27));
        queryService.LastTo.Should().Be(new DateOnly(2026, 03, 27));
    }

    [Fact]
    public async Task NavigateTodayCommand_PreservesActiveViewMode()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Week, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 04, 02, 12, 0, 0, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        await viewModel.NavigateTodayCommand.ExecuteAsync(null);

        viewModel.CurrentViewMode.Should().Be(ViewMode.Week);
        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 04, 02));
    }

    [Fact]
    public async Task InitializeAsync_WithoutSuccessfulSync_ShowsNeverSyncedLabel()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            syncStatusService: new StubSyncStatusService());

        await viewModel.InitializeAsync();

        viewModel.LastSyncLabel.Should().Be("Never synced - click to sync");
        viewModel.LastSyncTooltip.Should().Be("No sync on record");
    }

    [Fact]
    public void FormatRelativeLastSyncLabel_UsesRelativeAndDateFormats()
    {
        using var cultureScope = new CultureScope("en-US");
        var now = new DateTime(2026, 04, 05, 12, 00, 00, DateTimeKind.Local);

        MainViewModel.FormatRelativeLastSyncLabel(now.AddSeconds(-20), now)
            .Should().Be("Last synced just now");
        MainViewModel.FormatRelativeLastSyncLabel(now.AddMinutes(-7), now)
            .Should().Be("Last synced 7 minutes ago");
        MainViewModel.FormatRelativeLastSyncLabel(now.AddHours(-3), now)
            .Should().Be("Last synced 3 hours ago");
        MainViewModel.FormatRelativeLastSyncLabel(now.AddDays(-2), now)
            .Should().Be("Last synced on Friday, April 3");
    }

    [Fact]
    public void FormatLastSyncTooltip_UsesExactLocalTimeAndTimezone()
    {
        using var cultureScope = new CultureScope("en-US");
        var lastSyncUtc = new DateTime(2026, 04, 05, 15, 30, 00, DateTimeKind.Utc);

        var tooltip = MainViewModel.FormatLastSyncTooltip(lastSyncUtc);

        tooltip.Should().NotBeNullOrWhiteSpace();
        tooltip.Should().NotBe("No sync on record");
        tooltip.Should().Contain("2026");
        tooltip.Should().Contain("30");
    }

    [Fact]
    public async Task RequestSyncFlyout_UsesCurrentVisiblePeriod()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 12, 00, 00, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        viewModel.RequestSyncFlyout();

        DateOnly.FromDateTime(viewModel.SelectedSyncFromDate.Date).Should().Be(new DateOnly(2026, 04, 01));
        DateOnly.FromDateTime(viewModel.SelectedSyncToDate.Date).Should().Be(new DateOnly(2026, 04, 30));
        viewModel.SyncFlyoutOpenRequestId.Should().Be(1);
    }

    [Fact]
    public async Task RequestSyncFlyoutForVisibleRange_PrefillsCurrentViewDates()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Week, new DateOnly(2026, 04, 09)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        viewModel.RequestSyncFlyoutForVisibleRange();

        DateOnly.FromDateTime(viewModel.SelectedSyncFromDate.Date).Should().Be(new DateOnly(2026, 04, 06));
        DateOnly.FromDateTime(viewModel.SelectedSyncToDate.Date).Should().Be(new DateOnly(2026, 04, 12));
    }

    [Fact]
    public async Task GetExportDateRangeDefaultsAsync_UsesStoredEventBoundsWhenAvailable()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var exportService = new Mock<IIcsExportService>();
        exportService
            .Setup(service => service.GetStoredEventRangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((new DateOnly(2026, 02, 10), new DateOnly(2026, 08, 22)));

        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            exportService: exportService.Object);

        await viewModel.InitializeAsync();
        var range = await viewModel.GetExportDateRangeDefaultsAsync();

        range.Should().Be((new DateOnly(2026, 02, 10), new DateOnly(2026, 08, 22)));
    }

    [Fact]
    public async Task GetExportDateRangeDefaultsAsync_FallsBackToVisibleRangeWhenNoStoredEventsExist()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Week, new DateOnly(2026, 04, 09)));
        var exportService = new Mock<IIcsExportService>();
        exportService
            .Setup(service => service.GetStoredEventRangeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(((DateOnly From, DateOnly To)?)null);

        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            exportService: exportService.Object);

        await viewModel.InitializeAsync();
        var range = await viewModel.GetExportDateRangeDefaultsAsync();

        range.Should().Be((new DateOnly(2026, 04, 06), new DateOnly(2026, 04, 12)));
    }

    [Fact]
    public async Task SelectedSyncDates_InvalidRange_ShowsValidationAndDisablesConfirm()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        viewModel.SelectedSyncFromDate = new DateTimeOffset(2026, 04, 10, 0, 0, 0, TimeSpan.Zero);
        viewModel.SelectedSyncToDate = new DateTimeOffset(2026, 04, 09, 0, 0, 0, TimeSpan.Zero);

        viewModel.SyncValidationText.Should().Be("Start date must be before end date");
        viewModel.CanConfirmSync.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmSyncAsync_Success_RefreshesStateImmediately()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var syncManager = new Mock<ISyncManager>();
        syncManager
            .Setup(manager => manager.SyncAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IProgress<SyncProgress>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncResult(true, 3, 1, 0, "token", null));

        var syncStatusService = new StubSyncStatusService
        {
            LastSyncTime = new DateTime(2026, 04, 05, 13, 15, 00, DateTimeKind.Utc)
        };

        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            syncStatusService: syncStatusService,
            syncManager: syncManager.Object,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 04, 05, 13, 16, 00, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        viewModel.RequestSyncFlyout();

        await viewModel.ConfirmSyncAsync();

        syncManager.Verify(manager => manager.SyncAsync(
            "primary",
            new DateTime(2026, 04, 01, 0, 0, 0),
            new DateTime(2026, 05, 01, 0, 0, 0),
            null,
            It.IsAny<CancellationToken>()),
            Times.Once);
        viewModel.IsSyncing.Should().BeFalse();
        viewModel.LastSyncLabel.Should().Be("Last synced 1 minute ago");
        viewModel.LastSyncTooltip.Should().NotBe("No sync on record");
        queryService.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task SyncCompletedMessage_RefreshesTopBarState()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var syncStatusService = new StubSyncStatusService();
        var viewModel = CreateViewModel(queryService, navigationStateService, syncStatusService: syncStatusService);

        await viewModel.InitializeAsync();
        syncStatusService.LastSyncTime = new DateTime(2026, 04, 05, 13, 00, 00, DateTimeKind.Utc);

        WeakReferenceMessenger.Default.Send(new SyncCompletedMessage());
        await queryService.WaitForCallsAsync(2);

        viewModel.LastSyncLabel.Should().NotBe("Never synced - click to sync");
        queryService.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ImportFromIcsAsync_Success_ShowsSummaryNotification_AndRefreshesVisibleRange()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var importService = new Mock<IIcsImportService>();
        importService
            .Setup(service => service.ImportFromFileAsync(It.IsAny<StorageFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult(
                Success: true,
                ImportedEventCount: 3,
                NewEventCount: 2,
                UpdatedEventCount: 1,
                SkippedInvalidEventCount: 1,
                SkippedRecurringEventCount: 0,
                ErrorMessage: null));

        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            importService: importService.Object);

        await viewModel.InitializeAsync();
        var file = await CreateTempStorageFileAsync("import-success.ics");

        await viewModel.ImportFromIcsAsync(file);
        await queryService.WaitForCallsAsync(2);

        viewModel.NotificationSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success);
        viewModel.NotificationMessage.Should().Be("Imported 3 events (2 new, 1 updated, 1 skipped as invalid).");
        viewModel.IsNotificationOpen.Should().BeTrue();
        queryService.CallCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ImportFromIcsAsync_Failure_ShowsErrorNotification_WithoutRefresh()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 04, 05)));
        var importService = new Mock<IIcsImportService>();
        importService
            .Setup(service => service.ImportFromFileAsync(It.IsAny<StorageFile>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportResult(
                Success: false,
                ImportedEventCount: 0,
                NewEventCount: 0,
                UpdatedEventCount: 0,
                SkippedInvalidEventCount: 0,
                SkippedRecurringEventCount: 0,
                ErrorMessage: "The selected file is not a valid ICS calendar."));

        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            importService: importService.Object);

        await viewModel.InitializeAsync();
        var initialCallCount = queryService.CallCount;
        var file = await CreateTempStorageFileAsync("import-failure.ics");

        await viewModel.ImportFromIcsAsync(file);

        viewModel.NotificationSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
        viewModel.NotificationMessage.Should().Be("The selected file is not a valid ICS calendar.");
        queryService.CallCount.Should().Be(initialCallCount);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    private static MainViewModel CreateViewModel(
        RecordingCalendarQueryService queryService,
        StubNavigationStateService navigationStateService,
        StubSyncStatusService? syncStatusService = null,
        ISyncManager? syncManager = null,
        IContentDialogService? dialogService = null,
        IIcsExportService? exportService = null,
        IIcsImportService? importService = null,
        TimeProvider? timeProvider = null)
    {
        return new MainViewModel(
            queryService,
            navigationStateService,
            syncStatusService ?? new StubSyncStatusService(),
            syncManager ?? Mock.Of<ISyncManager>(),
            dialogService ?? Mock.Of<IContentDialogService>(),
            exportService ?? Mock.Of<IIcsExportService>(),
            importService ?? Mock.Of<IIcsImportService>(),
            NullLogger<MainViewModel>.Instance,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 03, 30, 12, 0, 0, TimeSpan.Zero)));
    }

    private static async Task<StorageFile> CreateTempStorageFileAsync(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "gcm-mainvm-tests");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n");
        return await StorageFile.GetFileFromPathAsync(path);
    }

    private sealed class StubSyncStatusService : ISyncStatusService
    {
        private readonly List<TaskCompletionSource> _waiters = [];

        public DateTime? LastSyncTime { get; set; }

        public int CallCount { get; private set; }

        public Dictionary<(DateOnly From, DateOnly To), int> CallsByRange { get; } = [];

        public Task<Dictionary<DateOnly, SyncStatus>> GetSyncStatusAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            CallCount++;
            var key = (from, to);
            CallsByRange[key] = CallsByRange.TryGetValue(key, out var existingCount) ? existingCount + 1 : 1;

            foreach (var waiter in _waiters.ToArray())
            {
                waiter.TrySetResult();
            }

            return Task.FromResult(new Dictionary<DateOnly, SyncStatus>());
        }

        public Task<DateTime?> GetLastSyncTimeAsync(CancellationToken ct = default)
            => Task.FromResult(LastSyncTime);

        public async Task WaitForCallsAsync(int expectedCallCount)
        {
            if (CallCount >= expectedCallCount)
            {
                return;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(waiter);

            while (CallCount < expectedCallCount)
            {
                await waiter.Task;
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(waiter);
            }
        }
    }

    private sealed class RecordingCalendarQueryService : ICalendarQueryService
    {
        private readonly List<TaskCompletionSource> _waiters = [];

        public DateOnly? LastFrom { get; private set; }
        public DateOnly? LastTo { get; private set; }
        public int CallCount { get; private set; }
        public List<(DateOnly From, DateOnly To)> RequestedRanges { get; } = [];
        public Dictionary<(DateOnly From, DateOnly To), int> CallsByRange { get; } = [];

        public Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default)
        {
            return Task.FromResult<CalendarEventDisplayModel?>(null);
        }

        public Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            LastFrom = from;
            LastTo = to;
            CallCount++;
            RequestedRanges.Add((from, to));
            var key = (from, to);
            CallsByRange[key] = CallsByRange.TryGetValue(key, out var existingCount) ? existingCount + 1 : 1;

            foreach (var waiter in _waiters.ToArray())
            {
                waiter.TrySetResult();
            }

            return Task.FromResult<IList<CalendarEventDisplayModel>>([]);
        }

        public async Task WaitForCallsAsync(int expectedCallCount)
        {
            if (CallCount >= expectedCallCount)
            {
                return;
            }

            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(waiter);

            while (CallCount < expectedCallCount)
            {
                await waiter.Task;
                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(waiter);
            }
        }
    }

    private sealed class StubNavigationStateService : INavigationStateService
    {
        private NavigationState _state;

        public StubNavigationStateService(NavigationState state)
        {
            _state = state;
        }

        public Task<NavigationState> LoadAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_state);
        }

        public Task SaveAsync(NavigationState state, CancellationToken ct = default)
        {
            _state = state;
            return Task.CompletedTask;
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

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}

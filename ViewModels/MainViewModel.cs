using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICalendarQueryService _calendarQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly ISyncStatusService _syncStatusService;
    private readonly ISyncManager _syncManager;
    private readonly IContentDialogService _dialogService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private Task? _initializationTask;
    private CancellationTokenSource _refreshCts = new();
    private ViewMode _currentViewMode;
    private DateOnly _currentDate;
    private string _breadcrumbLabel = string.Empty;
    private IList<CalendarEventDisplayModel> _currentEvents = [];
    private bool _isLoading;
    private IReadOnlyDictionary<DateOnly, SyncStatus> _syncStatusMap = new Dictionary<DateOnly, SyncStatus>();
    private DateTime? _lastSyncTime;
    private DateTimeOffset _selectedSyncFromDate;
    private DateTimeOffset _selectedSyncToDate;
    private string _syncValidationText = string.Empty;
    private bool _isSyncing;
    private int _syncFlyoutOpenRequestId;

    public MainViewModel(
        ICalendarQueryService calendarQueryService,
        INavigationStateService navigationStateService,
        ISyncStatusService syncStatusService,
        ISyncManager syncManager,
        IContentDialogService dialogService,
        ILogger<MainViewModel> logger,
        TimeProvider? timeProvider = null)
    {
        _calendarQueryService = calendarQueryService;
        _navigationStateService = navigationStateService;
        _syncStatusService = syncStatusService;
        _syncManager = syncManager;
        _dialogService = dialogService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        SwitchViewModeCommand = new AsyncRelayCommand<ViewMode>(SwitchViewModeAsync);
        NavigatePreviousCommand = new AsyncRelayCommand(NavigatePreviousAsync);
        NavigateNextCommand = new AsyncRelayCommand(NavigateNextAsync);
        NavigateTodayCommand = new AsyncRelayCommand(NavigateTodayAsync);
        JumpToDateCommand = new AsyncRelayCommand<DateOnly>(JumpToDateAsync);
        RefreshStatusCommand = new AsyncRelayCommand(RefreshStatusAsync);

        var (defaultFrom, defaultTo) = GetDefaultSyncRange();
        _selectedSyncFromDate = ToLocalDateOffset(defaultFrom);
        _selectedSyncToDate = ToLocalDateOffset(defaultTo);
        UpdateSyncValidation();

        WeakReferenceMessenger.Default.Register<MainViewModel, SyncCompletedMessage>(
            this,
            static (recipient, _) => recipient.OnSyncCompleted());
    }

    public ViewMode CurrentViewMode
    {
        get => _currentViewMode;
        private set => SetProperty(ref _currentViewMode, value);
    }

    public DateOnly CurrentDate
    {
        get => _currentDate;
        private set => SetProperty(ref _currentDate, value);
    }

    public string BreadcrumbLabel
    {
        get => _breadcrumbLabel;
        private set => SetProperty(ref _breadcrumbLabel, value);
    }

    public IList<CalendarEventDisplayModel> CurrentEvents
    {
        get => _currentEvents;
        private set
        {
            if (SetProperty(ref _currentEvents, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool ShowEmptyState => !IsLoading && CurrentEvents.Count == 0;

    public Visibility EmptyStateVisibility => ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;

    public IReadOnlyDictionary<DateOnly, SyncStatus> SyncStatusMap => _syncStatusMap;

    public string LastSyncLabel => FormatRelativeLastSyncLabel(_lastSyncTime, _timeProvider.GetLocalNow().DateTime);

    public string LastSyncTooltip => FormatLastSyncTooltip(_lastSyncTime);

    public DateTimeOffset SelectedSyncFromDate
    {
        get => _selectedSyncFromDate;
        set
        {
            if (SetProperty(ref _selectedSyncFromDate, value))
            {
                UpdateSyncValidation();
            }
        }
    }

    public DateTimeOffset SelectedSyncToDate
    {
        get => _selectedSyncToDate;
        set
        {
            if (SetProperty(ref _selectedSyncToDate, value))
            {
                UpdateSyncValidation();
            }
        }
    }

    public string SyncValidationText
    {
        get => _syncValidationText;
        private set
        {
            if (SetProperty(ref _syncValidationText, value))
            {
                OnPropertyChanged(nameof(SyncValidationVisibility));
                OnPropertyChanged(nameof(CanConfirmSync));
            }
        }
    }

    public Visibility SyncValidationVisibility =>
        string.IsNullOrWhiteSpace(SyncValidationText) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (SetProperty(ref _isSyncing, value))
            {
                OnPropertyChanged(nameof(SyncButtonText));
                OnPropertyChanged(nameof(SyncButtonProgressVisibility));
                OnPropertyChanged(nameof(CanConfirmSync));
            }
        }
    }

    public string SyncButtonText => IsSyncing ? "Syncing..." : "Sync";

    public Visibility SyncButtonProgressVisibility => IsSyncing ? Visibility.Visible : Visibility.Collapsed;

    public bool CanConfirmSync => !IsSyncing && string.IsNullOrWhiteSpace(SyncValidationText);

    public int SyncFlyoutOpenRequestId
    {
        get => _syncFlyoutOpenRequestId;
        private set => SetProperty(ref _syncFlyoutOpenRequestId, value);
    }

    public IAsyncRelayCommand<ViewMode> SwitchViewModeCommand { get; }

    public IAsyncRelayCommand NavigatePreviousCommand { get; }

    public IAsyncRelayCommand NavigateNextCommand { get; }

    public IAsyncRelayCommand NavigateTodayCommand { get; }

    public IAsyncRelayCommand<DateOnly> JumpToDateCommand { get; }

    public IAsyncRelayCommand RefreshStatusCommand { get; }

    public Task InitializeAsync()
    {
        return _initializationTask ??= InitializeCoreAsync();
    }

    /// <summary>Navigates to a specific date and view mode in a single operation, triggering only one data refresh.</summary>
    public async Task NavigateToAsync(DateOnly date, ViewMode mode)
    {
        CurrentDate = date;
        CurrentViewMode = mode;
        await RefreshAsync();
    }

    public void RequestSyncFlyout()
    {
        if (IsSyncing)
        {
            return;
        }

        var (from, to) = GetVisibleSyncRange();
        ApplySyncRange(from, to);
        SyncFlyoutOpenRequestId++;
    }

    public void RequestSyncFlyoutForVisibleRange()
    {
        RequestSyncFlyout();
    }

    public void RefreshRelativeSyncPresentation()
    {
        OnPropertyChanged(nameof(LastSyncLabel));
    }

    public async Task ConfirmSyncAsync()
    {
        if (!CanConfirmSync)
        {
            return;
        }

        IsSyncing = true;

        try
        {
            var startDate = DateOnly.FromDateTime(SelectedSyncFromDate.Date).ToDateTime(TimeOnly.MinValue);
            var endDateExclusive = DateOnly.FromDateTime(SelectedSyncToDate.Date).AddDays(1).ToDateTime(TimeOnly.MinValue);

            var result = await _syncManager.SyncAsync(
                rangeStart: startDate,
                rangeEnd: endDateExclusive,
                ct: CancellationToken.None);

            if (result.WasCancelled)
            {
                return;
            }

            if (result.Success)
            {
                await RefreshAsync();
                WeakReferenceMessenger.Default.Send(new SyncCompletedMessage());
                return;
            }

            await _dialogService.ShowErrorAsync(
                "Google Calendar Sync",
                result.ErrorMessage ?? "Unable to sync Google Calendar.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while syncing Google Calendar from the main shell.");
            await _dialogService.ShowErrorAsync(
                "Google Calendar Sync",
                "Unable to sync Google Calendar. Check the log for details and try again.");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void OnSyncCompleted()
    {
        if (IsSyncing)
        {
            return;
        }

        _ = RefreshAsync();
    }

    private async Task RefreshStatusAsync()
    {
        var (from, to) = GetDateRange(CurrentViewMode, CurrentDate);
        var (syncFrom, syncTo) = GetSyncStatusRange(CurrentViewMode, from, to);
        var syncStatusTask = _syncStatusService.GetSyncStatusAsync(syncFrom, syncTo);
        var lastSyncTask = _syncStatusService.GetLastSyncTimeAsync();
        await Task.WhenAll(syncStatusTask, lastSyncTask);

        UpdateSyncPresentation(syncStatusTask.Result, lastSyncTask.Result);
    }

    private async Task SwitchViewModeAsync(ViewMode mode)
    {
        CurrentViewMode = mode;
        await RefreshAsync();
    }

    private async Task NavigatePreviousAsync()
    {
        CurrentDate = CurrentViewMode switch
        {
            ViewMode.Year => CurrentDate.AddYears(-1),
            ViewMode.Month => CurrentDate.AddMonths(-1),
            ViewMode.Week => CurrentDate.AddDays(-7),
            ViewMode.Day => CurrentDate.AddDays(-1),
            _ => CurrentDate
        };

        await RefreshAsync();
    }

    private async Task NavigateNextAsync()
    {
        CurrentDate = CurrentViewMode switch
        {
            ViewMode.Year => CurrentDate.AddYears(1),
            ViewMode.Month => CurrentDate.AddMonths(1),
            ViewMode.Week => CurrentDate.AddDays(7),
            ViewMode.Day => CurrentDate.AddDays(1),
            _ => CurrentDate
        };

        await RefreshAsync();
    }

    private async Task NavigateTodayAsync()
    {
        CurrentDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        await RefreshAsync();
    }

    private async Task JumpToDateAsync(DateOnly date)
    {
        CurrentDate = date;
        await RefreshAsync();
    }

    private async Task InitializeCoreAsync()
    {
        var state = await _navigationStateService.LoadAsync();
        CurrentViewMode = state.ViewMode;
        CurrentDate = state.CurrentDate;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        // Cancel any in-flight refresh; start a fresh token for this run.
        _refreshCts.Cancel();
        _refreshCts.Dispose();
        _refreshCts = new CancellationTokenSource();
        var ct = _refreshCts.Token;

        IsLoading = true;

        try
        {
            var (from, to) = GetDateRange(CurrentViewMode, CurrentDate);
            var (syncFrom, syncTo) = GetSyncStatusRange(CurrentViewMode, from, to);
            var eventsTask = _calendarQueryService.GetEventsForRangeAsync(from, to, ct);
            var syncStatusTask = _syncStatusService.GetSyncStatusAsync(syncFrom, syncTo, ct);
            var lastSyncTask = _syncStatusService.GetLastSyncTimeAsync(ct);

            await Task.WhenAll(eventsTask, syncStatusTask, lastSyncTask);

            UpdateSyncPresentation(syncStatusTask.Result, lastSyncTask.Result);
            CurrentEvents = eventsTask.Result;
            BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
            await _navigationStateService.SaveAsync(new NavigationState(CurrentViewMode, CurrentDate), ct);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer navigation — discard silently.
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to refresh the calendar view for {ViewMode} on {Date}.", CurrentViewMode, CurrentDate);
            CurrentEvents = [];
            BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void UpdateSyncPresentation(IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap, DateTime? lastSyncTime)
    {
        _syncStatusMap = syncStatusMap;
        _lastSyncTime = lastSyncTime;
        OnPropertyChanged(nameof(SyncStatusMap));
        OnPropertyChanged(nameof(LastSyncLabel));
        OnPropertyChanged(nameof(LastSyncTooltip));
    }

    private void ApplySyncRange(DateOnly from, DateOnly to)
    {
        SelectedSyncFromDate = ToLocalDateOffset(from);
        SelectedSyncToDate = ToLocalDateOffset(to);
    }

    private (DateOnly From, DateOnly To) GetDefaultSyncRange()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        return (today.AddMonths(-6), today.AddMonths(1));
    }

    private (DateOnly From, DateOnly To) GetVisibleSyncRange()
    {
        return GetDateRange(CurrentViewMode, CurrentDate);
    }

    private void UpdateSyncValidation()
    {
        var from = DateOnly.FromDateTime(SelectedSyncFromDate.Date);
        var to = DateOnly.FromDateTime(SelectedSyncToDate.Date);
        SyncValidationText = from > to ? "Start date must be before end date" : string.Empty;
    }

    private static DateTimeOffset ToLocalDateOffset(DateOnly date)
    {
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return new DateTimeOffset(localDateTime);
    }

    private static (DateOnly From, DateOnly To) GetDateRange(ViewMode viewMode, DateOnly date)
    {
        return viewMode switch
        {
            ViewMode.Year => (new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31)),
            ViewMode.Month => (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))),
            ViewMode.Week => GetWeekRange(date),
            ViewMode.Day => (date, date),
            _ => (date, date)
        };
    }

    /// <summary>
    /// For Month view, expands the sync-status query range to include the padding weeks that
    /// the month grid renders (Monday of the first displayed week through Sunday of the last).
    /// Other view modes return the event range unchanged.
    /// </summary>
    private static (DateOnly From, DateOnly To) GetSyncStatusRange(ViewMode viewMode, DateOnly from, DateOnly to)
    {
        if (viewMode != ViewMode.Month)
        {
            return (from, to);
        }

        return (GetWeekRange(from).From, GetWeekRange(to).To);
    }

    private static string BuildBreadcrumb(ViewMode viewMode, DateOnly date)
    {
        var culture = CultureInfo.CurrentCulture;

        return viewMode switch
        {
            ViewMode.Year => date.Year.ToString(culture),
            ViewMode.Month => date.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture),
            ViewMode.Week => BuildWeekBreadcrumb(date, culture),
            ViewMode.Day => date.ToDateTime(TimeOnly.MinValue).ToString("dddd, dd MMMM yyyy", culture),
            _ => string.Empty
        };
    }

    private static (DateOnly From, DateOnly To) GetWeekRange(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var monday = date.AddDays(-daysFromMonday);
        return (monday, monday.AddDays(6));
    }

    private static string BuildWeekBreadcrumb(DateOnly date, CultureInfo culture)
    {
        var (from, to) = GetWeekRange(date);
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.ToDateTime(TimeOnly.MinValue);

        if (from.Year != to.Year)
        {
            return $"{fromDate.ToString("MMM d, yyyy", culture)}\u2013{toDate.ToString("MMM d, yyyy", culture)}";
        }

        if (from.Month != to.Month)
        {
            return $"{fromDate.ToString("MMM d", culture)}\u2013{toDate.ToString("MMM d", culture)}, {to.Year.ToString(culture)}";
        }

        return $"{fromDate.ToString("MMM d", culture)}\u2013{to.Day.ToString(culture)}, {to.Year.ToString(culture)}";
    }

    public static string FormatRelativeLastSyncLabel(DateTime? lastSyncTime, DateTime now)
    {
        if (lastSyncTime is null)
        {
            return "Never synced - click to sync";
        }

        var localSyncTime = lastSyncTime.Value.ToLocalTime();
        var elapsed = now - localSyncTime;

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Last synced just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)elapsed.TotalMinutes);
            return $"Last synced {minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)elapsed.TotalHours);
            return $"Last synced {hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        return $"Last synced on {localSyncTime.ToString("dddd, MMMM d", CultureInfo.CurrentCulture)}";
    }

    public static string FormatLastSyncTooltip(DateTime? lastSyncTime)
    {
        if (lastSyncTime is null)
        {
            return "No sync on record";
        }

        var localSyncTime = lastSyncTime.Value.ToLocalTime();
        var timeZone = GetLocalTimeZoneDisplayName(localSyncTime);
        return $"Last synced {localSyncTime:dddd, MMMM d, yyyy h:mm tt} {timeZone}";
    }

    private static string GetLocalTimeZoneDisplayName(DateTime localSyncTime)
    {
        var localTimeZone = TimeZoneInfo.Local;
        return localTimeZone.IsDaylightSavingTime(localSyncTime)
            ? localTimeZone.DaylightName
            : localTimeZone.StandardName;
    }
}

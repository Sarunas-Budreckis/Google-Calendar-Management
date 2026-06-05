using System.Globalization;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MainViewModel : ObservableObject, ICalendarViewRangeProvider
{
    private const int YearViewPreloadRadius = 5;
    private readonly ICalendarQueryService _calendarQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly ISyncStatusService _syncStatusService;
    private readonly ISyncManager _syncManager;
    private readonly IContentDialogService _dialogService;
    private readonly IPendingEventPublishService _pendingEventPublishService;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly ICalendarDaySelectionService? _calendarDaySelectionService;
    private readonly IIcsExportService _icsExportService;
    private readonly IIcsImportService _icsImportService;
    private readonly IColorMappingService _colorMappingService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _yearViewCacheGate = new();
    private readonly Dictionary<int, YearViewCacheEntry> _yearViewCache = [];
    private readonly Dictionary<int, Task<YearViewCacheEntry>> _yearViewLoadTasks = [];
    private long _yearViewCacheAccessSequence;
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
    private bool _isExporting;
    private bool _isImporting;
    private int _syncFlyoutOpenRequestId;
    private string _notificationMessage = string.Empty;
    private InfoBarSeverity _notificationSeverity = InfoBarSeverity.Informational;
    private bool _isNotificationOpen;
    private string? _notificationDetails;
    private readonly ObservableCollection<PendingPublishItemViewModel> _pendingPublishItems = [];
    private bool _isPublishingPendingEvents;
    private int _publishCompletedCount;
    private int _publishTotalCount;
    private string _pendingPublishSummaryText = string.Empty;
    private bool _isUndoToastVisible;
    private string _undoToastMessage = string.Empty;
    private Func<CancellationToken, Task>? _pendingUndoAction;

    public MainViewModel(
        ICalendarQueryService calendarQueryService,
        INavigationStateService navigationStateService,
        ISyncStatusService syncStatusService,
        ISyncManager syncManager,
        IContentDialogService dialogService,
        IPendingEventPublishService pendingEventPublishService,
        ICalendarSelectionService calendarSelectionService,
        IIcsExportService icsExportService,
        IIcsImportService icsImportService,
        IColorMappingService colorMappingService,
        ILogger<MainViewModel> logger,
        TimeProvider? timeProvider = null,
        ICalendarDaySelectionService? calendarDaySelectionService = null)
    {
        _calendarQueryService = calendarQueryService;
        _navigationStateService = navigationStateService;
        _syncStatusService = syncStatusService;
        _syncManager = syncManager;
        _dialogService = dialogService;
        _pendingEventPublishService = pendingEventPublishService;
        _calendarSelectionService = calendarSelectionService;
        _calendarDaySelectionService = calendarDaySelectionService;
        _icsExportService = icsExportService;
        _icsImportService = icsImportService;
        _colorMappingService = colorMappingService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        SwitchViewModeCommand = new AsyncRelayCommand<ViewMode>(SwitchViewModeAsync);
        NavigatePreviousCommand = new AsyncRelayCommand(NavigatePreviousAsync);
        NavigateNextCommand = new AsyncRelayCommand(NavigateNextAsync);
        NavigateTodayCommand = new AsyncRelayCommand(NavigateTodayAsync);
        JumpToDateCommand = new AsyncRelayCommand<DateOnly>(JumpToDateAsync);
        RefreshStatusCommand = new AsyncRelayCommand(RefreshStatusAsync);
        SelectAllPendingPublishCommand = new RelayCommand(ToggleSelectAllPendingPublishItems);
        PublishSelectedPendingEventsCommand = new AsyncRelayCommand(
            PublishSelectedPendingEventsAsync,
            () => CanPublishSelectedPendingEvents);
        ShowNotificationDetailsCommand = new AsyncRelayCommand(
            ShowNotificationDetailsAsync,
            () => !string.IsNullOrWhiteSpace(NotificationDetails));
        UndoCommand = new AsyncRelayCommand(ExecuteUndoAsync);

        var (defaultFrom, defaultTo) = GetDefaultSyncRange();
        _selectedSyncFromDate = ToLocalDateOffset(defaultFrom);
        _selectedSyncToDate = ToLocalDateOffset(defaultTo);
        UpdateSyncValidation();

        WeakReferenceMessenger.Default.Register<MainViewModel, SyncCompletedMessage>(
            this,
            static (recipient, _) => recipient.OnSyncCompleted());
        WeakReferenceMessenger.Default.Register<MainViewModel, EventUpdatedMessage>(
            this,
            static (recipient, message) => recipient.OnEventUpdated(message));
        WeakReferenceMessenger.Default.Register<MainViewModel, RequestUndoToastMessage>(
            this,
            static (recipient, message) => recipient.OnRequestUndoToast(message));
        WeakReferenceMessenger.Default.Register<MainViewModel, DataSourceDayOpenRequestedMessage>(
            this,
            static (recipient, message) => _ = recipient.OpenDataSourceDayAsync(message));
    }

    public ViewMode CurrentViewMode
    {
        get => _currentViewMode;
        private set
        {
            var previousMode = _currentViewMode;
            if (SetProperty(ref _currentViewMode, value))
            {
                OnCurrentViewModeChanged(previousMode, value);
            }
        }
    }

    public DateOnly CurrentDate
    {
        get => _currentDate;
        private set
        {
            if (SetProperty(ref _currentDate, value) && CurrentViewMode == ViewMode.Day)
            {
                _calendarDaySelectionService?.AutoSelectDay(value);
            }
        }
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

    public bool IsExporting
    {
        get => _isExporting;
        private set => SetProperty(ref _isExporting, value);
    }

    public bool IsImporting
    {
        get => _isImporting;
        private set => SetProperty(ref _isImporting, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public InfoBarSeverity NotificationSeverity
    {
        get => _notificationSeverity;
        private set => SetProperty(ref _notificationSeverity, value);
    }

    public bool IsNotificationOpen
    {
        get => _isNotificationOpen;
        private set => SetProperty(ref _isNotificationOpen, value);
    }

    public string? NotificationDetails
    {
        get => _notificationDetails;
        private set
        {
            if (SetProperty(ref _notificationDetails, value))
            {
                OnPropertyChanged(nameof(NotificationDetailsVisibility));
                ShowNotificationDetailsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility NotificationDetailsVisibility =>
        string.IsNullOrWhiteSpace(NotificationDetails) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsUndoToastVisible
    {
        get => _isUndoToastVisible;
        private set
        {
            if (SetProperty(ref _isUndoToastVisible, value))
            {
                OnPropertyChanged(nameof(UndoToastVisibility));
            }
        }
    }

    public Visibility UndoToastVisibility =>
        _isUndoToastVisible ? Visibility.Visible : Visibility.Collapsed;

    public string UndoToastMessage
    {
        get => _undoToastMessage;
        private set => SetProperty(ref _undoToastMessage, value);
    }

    public ObservableCollection<PendingPublishItemViewModel> PendingPublishItems => _pendingPublishItems;

    public int PendingPublishCount => _pendingPublishItems.Count;

    public Visibility PendingPublishBadgeVisibility =>
        PendingPublishCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    public bool CanOpenPendingPublishFlyout => PendingPublishCount > 0 && !IsPublishingPendingEvents;

    public int SelectedPendingPublishCount => _pendingPublishItems.Count(item => item.IsSelected);

    public bool CanPublishSelectedPendingEvents => SelectedPendingPublishCount > 0 && !IsPublishingPendingEvents;

    public bool AllPendingPublishItemsSelected =>
        PendingPublishCount > 0 && _pendingPublishItems.All(item => item.IsSelected);

    public bool IsPublishingPendingEvents
    {
        get => _isPublishingPendingEvents;
        private set
        {
            if (SetProperty(ref _isPublishingPendingEvents, value))
            {
                OnPropertyChanged(nameof(CanOpenPendingPublishFlyout));
                OnPropertyChanged(nameof(CanPublishSelectedPendingEvents));
                PublishSelectedPendingEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string PublishProgressText =>
        IsPublishingPendingEvents ? $"{_publishCompletedCount} / {_publishTotalCount}" : string.Empty;

    public Visibility PublishProgressVisibility =>
        IsPublishingPendingEvents ? Visibility.Visible : Visibility.Collapsed;

    public string PendingPublishSummaryText
    {
        get => _pendingPublishSummaryText;
        private set
        {
            if (SetProperty(ref _pendingPublishSummaryText, value))
            {
                OnPropertyChanged(nameof(PendingPublishSummaryVisibility));
            }
        }
    }

    public Visibility PendingPublishSummaryVisibility =>
        string.IsNullOrWhiteSpace(PendingPublishSummaryText) ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand<ViewMode> SwitchViewModeCommand { get; }

    public IAsyncRelayCommand NavigatePreviousCommand { get; }

    public IAsyncRelayCommand NavigateNextCommand { get; }

    public IAsyncRelayCommand NavigateTodayCommand { get; }

    public IAsyncRelayCommand<DateOnly> JumpToDateCommand { get; }

    public IAsyncRelayCommand RefreshStatusCommand { get; }

    public IRelayCommand SelectAllPendingPublishCommand { get; }

    public IAsyncRelayCommand PublishSelectedPendingEventsCommand { get; }

    public IAsyncRelayCommand ShowNotificationDetailsCommand { get; }

    public IAsyncRelayCommand UndoCommand { get; }

    public Task InitializeAsync()
    {
        return _initializationTask ??= InitializeCoreAsync();
    }

    public (DateOnly From, DateOnly To) GetVisibleDateRange()
    {
        return GetDateRange(CurrentViewMode, CurrentDate);
    }

    public (DateOnly From, DateOnly To) GetCurrentViewDisplayRange()
    {
        return GetDisplayDateRange(CurrentViewMode, CurrentDate);
    }

    public async Task<(DateOnly From, DateOnly To)> GetExportDateRangeDefaultsAsync(CancellationToken ct = default)
    {
        var storedRange = await _icsExportService.GetStoredEventRangeAsync(ct);
        return storedRange ?? GetVisibleDateRange();
    }

    public async Task<YearViewDataSnapshot> EnsureYearViewDataAsync(int year, CancellationToken ct = default)
    {
        var data = await GetYearViewDataAsync(year, ct);
        return new YearViewDataSnapshot(
            data.Year,
            data.Events,
            data.SyncStatusMap,
            data.LastSyncTime);
    }

    /// <summary>Navigates to a specific date and view mode in a single operation, triggering only one data refresh.</summary>
    public async Task NavigateToAsync(DateOnly date, ViewMode mode)
    {
        CurrentDate = date;
        CurrentViewMode = mode;
        await RefreshAsync();
    }

    private Task OpenDataSourceDayAsync(DataSourceDayOpenRequestedMessage message)
    {
        return NavigateToAsync(message.Date, ViewMode.Day);
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

    public void DismissNotification()
    {
        IsNotificationOpen = false;
    }

    public void DismissUndoToast()
    {
        _pendingUndoAction = null;
        IsUndoToastVisible = false;
    }

    public bool IsPendingEventSelectedForPush(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        return _pendingPublishItems.Any(item =>
            item.IsSelected &&
            string.Equals(item.DisplayEventId, eventId, StringComparison.Ordinal));
    }

    public async Task TogglePendingPublishSelectionForEventAsync(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        var matchingItem = _pendingPublishItems.FirstOrDefault(item =>
            string.Equals(item.DisplayEventId, eventId, StringComparison.Ordinal));
        if (matchingItem is null)
        {
            await LoadPendingPublishItemsAsync();
            matchingItem = _pendingPublishItems.FirstOrDefault(item =>
                string.Equals(item.DisplayEventId, eventId, StringComparison.Ordinal));
        }

        if (matchingItem is null)
        {
            return;
        }

        matchingItem.IsSelected = !matchingItem.IsSelected;
    }

    public async Task GoToPendingPublishItemAsync(PendingPublishItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var targetEvent = await _calendarQueryService.GetEventByIdAsync(item.DisplayEventId);
        if (targetEvent is null)
        {
            return;
        }

        var targetDate = DateOnly.FromDateTime(targetEvent.StartLocal.Date);
        await NavigateToAsync(targetDate, CurrentViewMode);
        _calendarSelectionService.Select(item.DisplayEventId, item.EventSourceKind);
    }

    public async Task ShowPendingPublishErrorDetailsAsync(PendingPublishItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.PublishErrorDetails))
        {
            return;
        }

        await _dialogService.ShowSelectableTextAsync(
            $"{item.Title} publish failure",
            item.PublishErrorDetails,
            "Close");
    }

    public IReadOnlyList<CalendarColorOption> AvailableColors => _colorMappingService.PickerColors;

    public async Task RevertPendingPublishItemAsync(PendingPublishItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            if (string.Equals(_calendarSelectionService.SelectedEventId, item.PendingEventId, StringComparison.Ordinal)
                && item.EventSourceKind == CalendarEventSourceKind.Pending)
            {
                _calendarSelectionService.ClearSelection();
            }

            await _pendingEventPublishService.RevertAsync(item.PendingEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revert pending event {PendingEventId}.", item.PendingEventId);
        }
    }

    public async Task ChangePendingPublishItemColorAsync(PendingPublishItemViewModel item, string colorKey)
    {
        ArgumentNullException.ThrowIfNull(item);

        try
        {
            await _pendingEventPublishService.UpdateColorAsync(item.PendingEventId, colorKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update color for pending event {PendingEventId}.", item.PendingEventId);
        }
    }

    public async Task LoadPendingPublishItemsAsync(CancellationToken ct = default)
    {
        try
        {
            var existingSelection = _pendingPublishItems
                .Where(item => item.IsSelected)
                .Select(item => item.PendingEventId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var existingItem in _pendingPublishItems)
            {
                existingItem.PropertyChanged -= PendingPublishItem_PropertyChanged;
            }

            var pendingItems = await _pendingEventPublishService.GetPendingItemsAsync(ct);
            _pendingPublishItems.Clear();

            foreach (var item in pendingItems)
            {
                var viewModelItem = new PendingPublishItemViewModel(
                    item.PendingEventId,
                    item.GcalEventId ?? item.PendingEventId,
                    item.GcalEventId is null ? CalendarEventSourceKind.Pending : CalendarEventSourceKind.Google,
                    item.Title,
                    FormatPendingPublishDateTimeSummary(item.StartDateTimeUtc, item.EndDateTimeUtc, item.IsAllDay),
                    item.SourceLabel,
                    item.ColorKey,
                    item.ColorHex,
                    item.IsRecurringInstance,
                    item.PublishError)
                {
                    IsSelected = existingSelection.Contains(item.PendingEventId)
                };

                viewModelItem.PropertyChanged += PendingPublishItem_PropertyChanged;
                _pendingPublishItems.Add(viewModelItem);
            }

            UpdatePendingPublishDerivedState();
            ApplyPendingPublishSelectionStateToCurrentEvents();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to load pending publish items.");
        }
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
                InvalidateYearViewCache();
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

    private async Task PublishSelectedPendingEventsAsync()
    {
        var selectedItems = _pendingPublishItems
            .Where(item => item.IsSelected)
            .ToList();
        if (selectedItems.Count == 0 || IsPublishingPendingEvents)
        {
            return;
        }

        var confirmationMessage = BuildPendingPublishConfirmationMessage(selectedItems);
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Push Events to Google Calendar?",
            confirmationMessage,
            "Push to GCal");
        if (!confirmed)
        {
            return;
        }

        if (selectedItems.Any(item => item.IsRecurringInstance))
        {
            await _dialogService.ShowMessageAsync(
                "Recurring Instance",
                "This publish applies only to the selected recurring instance. Series-wide publish scopes are not included here.");
        }

        IsPublishingPendingEvents = true;
        _publishCompletedCount = 0;
        _publishTotalCount = selectedItems.Count;
        PendingPublishSummaryText = string.Empty;
        OnPropertyChanged(nameof(PublishProgressText));
        OnPropertyChanged(nameof(PublishProgressVisibility));

        try
        {
            var progress = new Progress<PendingPublishProgress>(value =>
            {
                _publishCompletedCount = value.CompletedCount;
                _publishTotalCount = value.TotalCount;
                OnPropertyChanged(nameof(PublishProgressText));
            });

            var result = await _pendingEventPublishService.PublishAsync(
                selectedItems.Select(item => item.PendingEventId).ToList(),
                progress,
                CancellationToken.None);

            var failureDetails = BuildPendingPublishFailureDetails(result, selectedItems);
            PendingPublishSummaryText = result.FailureCount == 0
                ? $"{result.SuccessCount} event(s) published."
                : BuildPendingPublishFailureSummary(result);

            var severity = result.FailureCount switch
            {
                0 => InfoBarSeverity.Success,
                var failures when failures == result.TotalCount => InfoBarSeverity.Error,
                _ => InfoBarSeverity.Warning
            };
            ShowNotification(PendingPublishSummaryText, severity, failureDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing pending events.");
            PendingPublishSummaryText = "Unable to publish the selected events. Check the log for details and try again.";
            ShowNotification(PendingPublishSummaryText, InfoBarSeverity.Error, ex.ToString());
        }
        finally
        {
            IsPublishingPendingEvents = false;
            _publishCompletedCount = 0;
            _publishTotalCount = 0;
            OnPropertyChanged(nameof(PublishProgressText));
            OnPropertyChanged(nameof(PublishProgressVisibility));
            await LoadPendingPublishItemsAsync();
        }
    }

    public async Task ExportToIcsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (IsExporting)
        {
            return;
        }

        IsExporting = true;

        try
        {
            var result = await _icsExportService.ExportToFileAsync(from, to, ct);
            if (result.WasCancelled)
            {
                return;
            }

            if (result.Success)
            {
                if (result.ExportedEventCount == 0)
                {
                    ShowNotification("No events were found in the selected range. No file was written.", InfoBarSeverity.Informational);
                }
                else
                {
                    ShowNotification($"Exported {result.ExportedEventCount} events to {result.FileName}.", InfoBarSeverity.Success);
                }

                return;
            }

            ShowNotification(
                result.ErrorMessage ?? "Unable to export the ICS file. Check the log for details and try again.",
                InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while exporting calendar events to ICS.");
            ShowNotification(
                "Unable to export the ICS file. Check the log for details and try again.",
                InfoBarSeverity.Error);
        }
        finally
        {
            IsExporting = false;
        }
    }

    public async Task ImportFromIcsAsync(StorageFile file, CancellationToken ct = default)
    {
        if (IsImporting)
        {
            return;
        }

        IsImporting = true;

        try
        {
            var result = await _icsImportService.ImportFromFileAsync(file, ct);
            if (result.Success)
            {
                var notificationMessage =
                    $"Imported {result.ImportedEventCount} events ({result.NewEventCount} new, {result.UpdatedEventCount} updated, {result.SkippedInvalidEventCount} skipped as invalid).";

                if (result.SkippedRecurringEventCount > 0)
                {
                    notificationMessage += $" {result.SkippedRecurringEventCount} recurring event(s) were skipped because recurrence import is not supported.";
                }

                ShowNotification(notificationMessage, InfoBarSeverity.Success);
                WeakReferenceMessenger.Default.Send(new SyncCompletedMessage());
                return;
            }

            ShowNotification(
                result.ErrorMessage ?? "Unable to import the ICS file. Check the log for details and try again.",
                InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while importing calendar events from ICS.");
            ShowNotification(
                "Unable to import the ICS file. Check the log for details and try again.",
                InfoBarSeverity.Error);
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void OnSyncCompleted()
    {
        if (IsSyncing)
        {
            return;
        }

        InvalidateYearViewCache();
        _ = RefreshAsync();
        _ = LoadPendingPublishItemsAsync();
    }

    private void OnEventUpdated(EventUpdatedMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.EventId))
        {
            return;
        }

        if (message.PreviewEvent is not null)
        {
            ApplyAffectedEventUpdate(message.EventId, message.PreviewEvent);
            _ = LoadPendingPublishItemsAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(message.PreviousEventId) &&
            string.Equals(_calendarSelectionService.SelectedEventId, message.PreviousEventId, StringComparison.Ordinal))
        {
            _calendarSelectionService.Select(message.EventId, CalendarEventSourceKind.Google);
        }

        _ = RefreshAffectedEventAsync(message.EventId, message.PreviousEventId, message.AnimateOpacityTransition);
        _ = LoadPendingPublishItemsAsync();
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
        if (mode == ViewMode.Day && CurrentViewMode != ViewMode.Day)
        {
            CurrentDate = ResolveDayViewDateOnSwitch(CurrentViewMode, CurrentDate);
        }

        CurrentViewMode = mode;
        await RefreshAsync();
    }

    private DateOnly ResolveDayViewDateOnSwitch(ViewMode previousMode, DateOnly previousDate)
    {
        if (_calendarDaySelectionService?.ManuallySelectedDay is { } selectedDay)
        {
            return selectedDay;
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var (from, to) = GetDateRange(previousMode, previousDate);
        return today >= from && today <= to
            ? today
            : from;
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
        CurrentDate = state.CurrentDate;
        CurrentViewMode = state.ViewMode;
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
            if (CurrentViewMode == ViewMode.Year)
            {
                var yearViewData = await GetYearViewDataAsync(CurrentDate.Year, ct);
                UpdateSyncPresentation(yearViewData.SyncStatusMap, yearViewData.LastSyncTime);
                CurrentEvents = yearViewData.Events;
                BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
                PublishViewRangeChanged();
                await _navigationStateService.SaveAsync(CreateNavigationState(), ct);
                await LoadPendingPublishItemsAsync(ct);
                StartYearViewPreloads(CurrentDate.Year);
                return;
            }

            var (from, to) = GetDateRange(CurrentViewMode, CurrentDate);
            var (syncFrom, syncTo) = GetSyncStatusRange(CurrentViewMode, from, to);
            var eventsTask = _calendarQueryService.GetEventsForRangeAsync(from, to, ct);
            var syncStatusTask = _syncStatusService.GetSyncStatusAsync(syncFrom, syncTo, ct);
            var lastSyncTask = _syncStatusService.GetLastSyncTimeAsync(ct);

            await Task.WhenAll(eventsTask, syncStatusTask, lastSyncTask);

            UpdateSyncPresentation(syncStatusTask.Result, lastSyncTask.Result);
            CurrentEvents = eventsTask.Result;
            BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
            PublishViewRangeChanged();
            await _navigationStateService.SaveAsync(CreateNavigationState(), ct);
            await LoadPendingPublishItemsAsync(ct);
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
            PublishViewRangeChanged();
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void OnCurrentViewModeChanged(ViewMode previousMode, ViewMode currentMode)
    {
        if (_calendarDaySelectionService is null)
        {
            return;
        }

        if (currentMode == ViewMode.Day)
        {
            _calendarDaySelectionService.AutoSelectDay(CurrentDate);
        }
        else if (previousMode == ViewMode.Day)
        {
            _calendarDaySelectionService.RestoreManualSelection();
        }
    }

    private NavigationState CreateNavigationState()
    {
        return new NavigationState(
            CurrentViewMode,
            CurrentDate,
            _calendarDaySelectionService?.ManuallySelectedDay);
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

    private void PendingPublishItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PendingPublishItemViewModel.IsSelected))
        {
            UpdatePendingPublishDerivedState();
            ApplyPendingPublishSelectionStateToCurrentEvents();
        }
    }

    private void ToggleSelectAllPendingPublishItems()
    {
        var selectAll = !AllPendingPublishItemsSelected;
        foreach (var item in _pendingPublishItems)
        {
            item.IsSelected = selectAll;
        }

        UpdatePendingPublishDerivedState();
    }

    private void UpdatePendingPublishDerivedState()
    {
        OnPropertyChanged(nameof(PendingPublishCount));
        OnPropertyChanged(nameof(PendingPublishBadgeVisibility));
        OnPropertyChanged(nameof(CanOpenPendingPublishFlyout));
        OnPropertyChanged(nameof(SelectedPendingPublishCount));
        OnPropertyChanged(nameof(CanPublishSelectedPendingEvents));
        OnPropertyChanged(nameof(AllPendingPublishItemsSelected));
        PublishSelectedPendingEventsCommand.NotifyCanExecuteChanged();
    }

    private void OnRequestUndoToast(RequestUndoToastMessage message)
    {
        _pendingUndoAction = message.OnUndo;
        UndoToastMessage = message.Message;
        IsUndoToastVisible = true;
    }

    private async Task ExecuteUndoAsync()
    {
        var action = _pendingUndoAction;
        DismissUndoToast();
        if (action is not null)
        {
            await action(CancellationToken.None);
        }
    }

    private void ShowNotification(string message, InfoBarSeverity severity, string? details = null)
    {
        IsNotificationOpen = false;
        NotificationDetails = details;
        NotificationMessage = message;
        NotificationSeverity = severity;
        IsNotificationOpen = true;
    }

    private async Task ShowNotificationDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(NotificationDetails))
        {
            return;
        }

        await _dialogService.ShowSelectableTextAsync(
            "Push failure details",
            NotificationDetails,
            "Close");
    }

    private async Task RefreshAffectedEventAsync(string eventId)
    {
        await RefreshAffectedEventAsync(eventId, null, animateOpacityTransition: false);
    }

    private async Task RefreshAffectedEventAsync(
        string eventId,
        string? previousEventId,
        bool animateOpacityTransition)
    {
        try
        {
            var refreshedEvent = await _calendarQueryService.GetEventByIdAsync(eventId);
            if (animateOpacityTransition &&
                refreshedEvent is not null &&
                !refreshedEvent.IsPending)
            {
                await AnimatePublishedEventAsync(eventId, previousEventId, refreshedEvent);
                return;
            }

            ApplyAffectedEventUpdate(eventId, refreshedEvent, previousEventId);
        }
        catch (OperationCanceledException)
        {
            // Ignore event-specific refreshes that lose a race with navigation changes.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to refresh calendar event {EventId} after a local edit.", eventId);
        }
    }

    private async Task AnimatePublishedEventAsync(
        string eventId,
        string? previousEventId,
        CalendarEventDisplayModel refreshedEvent)
    {
        var initialOpacity = GetCurrentEventOpacity(previousEventId ?? eventId) ?? 0.6;
        ApplyAffectedEventUpdate(eventId, refreshedEvent with { Opacity = initialOpacity }, previousEventId);

        const int steps = 5;
        for (var step = 1; step <= steps; step++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(60));
            var nextOpacity = initialOpacity + ((1.0 - initialOpacity) * step / steps);
            var nextEvent = refreshedEvent with { Opacity = nextOpacity };
            ApplyAffectedEventUpdate(eventId, nextEvent);
        }
    }

    private void ApplyAffectedEventUpdate(
        string eventId,
        CalendarEventDisplayModel? refreshedEvent,
        string? previousEventId = null)
    {
        var updatedEvents = CurrentEvents.ToList();
        var existingIndex = updatedEvents.FindIndex(
            item => string.Equals(item.EventId, previousEventId ?? eventId, StringComparison.Ordinal));
        var (from, to) = GetVisibleDateRange();
        var isVisibleInCurrentRange = refreshedEvent is not null && OverlapsVisibleRange(refreshedEvent, from, to);

        if (existingIndex >= 0)
        {
            if (isVisibleInCurrentRange)
            {
                updatedEvents[existingIndex] = refreshedEvent!;
            }
            else
            {
                updatedEvents.RemoveAt(existingIndex);
            }
        }
        else if (isVisibleInCurrentRange)
        {
            updatedEvents.Add(refreshedEvent!);
        }
        else
        {
            return;
        }

        CurrentEvents = updatedEvents
            .OrderBy(item => item.StartLocal)
            .ThenBy(item => item.EndLocal)
            .ThenBy(item => item.Title, StringComparer.CurrentCulture)
            .ToList();

        ApplyPendingPublishSelectionStateToCurrentEvents();

        InvalidateYearViewCache();
    }

    private void ApplyPendingPublishSelectionStateToCurrentEvents()
    {
        if (CurrentEvents.Count == 0)
        {
            return;
        }

        var selectedDisplayEventIds = _pendingPublishItems
            .Where(item => item.IsSelected)
            .Select(item => item.DisplayEventId)
            .ToHashSet(StringComparer.Ordinal);

        var updatedEvents = CurrentEvents
            .Select(item => item with
            {
                IsSelectedForPush = selectedDisplayEventIds.Contains(item.EventId)
            })
            .ToList();

        if (CurrentEvents.SequenceEqual(updatedEvents))
        {
            return;
        }

        CurrentEvents = updatedEvents;
    }

    private static string BuildPendingPublishConfirmationMessage(
        IReadOnlyList<PendingPublishItemViewModel> selectedItems)
    {
        var lines = selectedItems
            .Select(item => $"• {item.Title} ({item.DateTimeSummary})")
            .ToList();
        return $"You are about to publish {selectedItems.Count} event(s) to Google Calendar.\n\n{string.Join("\n", lines)}";
    }

    private static string BuildPendingPublishFailureSummary(PendingPublishBatchResult result)
    {
        var firstFailureSummary = result.ItemResults
            .Where(item => !item.Success)
            .Select(item => ExtractSingleLineErrorSummary(item.ErrorMessage ?? item.ErrorDetails))
            .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary));

        return string.IsNullOrWhiteSpace(firstFailureSummary)
            ? $"{result.SuccessCount} published, {result.FailureCount} failed."
            : $"{result.SuccessCount} published, {result.FailureCount} failed. First failure: {firstFailureSummary}";
    }

    private static string? BuildPendingPublishFailureDetails(
        PendingPublishBatchResult result,
        IReadOnlyList<PendingPublishItemViewModel> selectedItems)
    {
        var selectedItemsByPendingId = selectedItems.ToDictionary(item => item.PendingEventId, StringComparer.Ordinal);
        var failureDetails = result.ItemResults
            .Where(item => !item.Success)
            .Select(item =>
            {
                var title = selectedItemsByPendingId.TryGetValue(item.PendingEventId, out var selectedItem)
                    ? selectedItem.Title
                    : item.PendingEventId;
                var details = item.ErrorDetails ?? item.ErrorMessage;
                return string.IsNullOrWhiteSpace(details)
                    ? null
                    : $"Event: {title}\n{details}";
            })
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .ToList();

        return failureDetails.Count == 0
            ? null
            : string.Join("\n\n", failureDetails);
    }

    private static string ExtractSingleLineErrorSummary(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "Unknown failure.";
        }

        var firstLine = error
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
            ?.Trim();

        return string.IsNullOrWhiteSpace(firstLine)
            ? "Unknown failure."
            : firstLine;
    }

    private static string FormatPendingPublishDateTimeSummary(
        DateTime? startUtc,
        DateTime? endUtc,
        bool isAllDay)
    {
        if (!startUtc.HasValue)
        {
            return "Time not set";
        }

        if (isAllDay)
        {
            var startDate = DateOnly.FromDateTime(startUtc.Value.Date);
            var endDateExclusive = DateOnly.FromDateTime((endUtc ?? startUtc.Value.AddDays(1)).Date);
            var inclusiveEndDate = endDateExclusive > startDate ? endDateExclusive.AddDays(-1) : startDate;
            return startDate == inclusiveEndDate
                ? $"{startDate:ddd, MMM d} (all day)"
                : $"{startDate:ddd, MMM d} - {inclusiveEndDate:ddd, MMM d} (all day)";
        }

        var startLocal = startUtc.Value.ToLocalTime();
        var endLocal = (endUtc ?? startUtc.Value).ToLocalTime();
        return $"{startLocal:ddd, MMM d h:mm tt} - {endLocal:h:mm tt}";
    }

    private double? GetCurrentEventOpacity(string eventId)
    {
        return CurrentEvents
            .FirstOrDefault(item => string.Equals(item.EventId, eventId, StringComparison.Ordinal))
            ?.Opacity;
    }

    private async Task<YearViewCacheEntry> GetYearViewDataAsync(int year, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        if (TryGetCachedYearViewData(year, out var cached))
        {
            timer.Stop();
            _logger.LogInformation(
                "Year view data cache hit for year {Year}. Data ready in {ElapsedMs}ms.",
                year,
                timer.ElapsedMilliseconds);
            return cached;
        }

        var loadTask = GetOrCreateYearViewLoadTask(year);
        var loaded = await loadTask.WaitAsync(ct);
        timer.Stop();
        _logger.LogInformation(
            "Year view data cache miss for year {Year}. Data ready in {ElapsedMs}ms.",
            year,
            timer.ElapsedMilliseconds);
        return loaded;
    }

    private Task<YearViewCacheEntry> GetOrCreateYearViewLoadTask(int year)
    {
        using var scope = _yearViewCacheGate.EnterScope();

        if (_yearViewLoadTasks.TryGetValue(year, out var existingTask))
        {
            return existingTask;
        }

        var cacheVersion = _yearViewCacheVersion;
        var loadTask = LoadAndCacheYearViewDataAsync(year, cacheVersion);
        _yearViewLoadTasks[year] = loadTask;

        _ = loadTask.ContinueWith(
            task =>
            {
                RemoveCompletedYearViewLoadTask(year, task);

                if (task.IsFaulted)
                {
                    _logger.LogDebug(task.Exception, "Year view preload for {Year} completed with an error.", year);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return loadTask;
    }

    private async Task<YearViewCacheEntry> LoadAndCacheYearViewDataAsync(int year, int cacheVersion)
    {
        var loaded = await LoadYearViewDataAsync(year, CancellationToken.None);
        CacheYearViewData(loaded, cacheVersion);
        return loaded;
    }

    private async Task<YearViewCacheEntry> LoadYearViewDataAsync(int year, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var eventsTask = _calendarQueryService.GetEventsForRangeAsync(from, to, ct);
        var syncStatusTask = _syncStatusService.GetSyncStatusAsync(from, to, ct);
        var lastSyncTask = _syncStatusService.GetLastSyncTimeAsync(ct);

        await Task.WhenAll(eventsTask, syncStatusTask, lastSyncTask);
        timer.Stop();
        _logger.LogInformation(
            "Year view data loaded for year {Year}. Events={EventCount} SyncDays={SyncDayCount} Load={ElapsedMs}ms.",
            year,
            eventsTask.Result.Count,
            syncStatusTask.Result.Count,
            timer.ElapsedMilliseconds);

        return new YearViewCacheEntry(
            year,
            eventsTask.Result,
            syncStatusTask.Result,
            lastSyncTask.Result,
            0);
    }

    private void StartYearViewPreloads(int selectedYear)
    {
        var actualCurrentYear = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime).Year;
        var yearsToPreload = new HashSet<int>();

        for (var year = selectedYear - YearViewPreloadRadius; year <= selectedYear + YearViewPreloadRadius; year++)
        {
            yearsToPreload.Add(year);
        }

        yearsToPreload.Add(actualCurrentYear);

        foreach (var year in yearsToPreload.OrderBy(static year => year))
        {
            StartYearViewPreload(year);
        }
    }

    private void StartYearViewPreload(int year)
    {
        if (TryGetCachedYearViewData(year, out _))
        {
            _logger.LogDebug("Year view preload skipped for year {Year}: already cached.", year);
            return;
        }

        _logger.LogDebug("Year view preload queued for year {Year}.", year);
        _ = GetOrCreateYearViewLoadTask(year);
    }

    private bool TryGetCachedYearViewData(int year, out YearViewCacheEntry entry)
    {
        using var scope = _yearViewCacheGate.EnterScope();

        if (_yearViewCache.TryGetValue(year, out var cachedEntry))
        {
            entry = cachedEntry with { AccessSequence = ++_yearViewCacheAccessSequence };
            _yearViewCache[year] = entry;
            return true;
        }

        entry = default!;
        return false;
    }

    private void CacheYearViewData(YearViewCacheEntry entry, int expectedVersion)
    {
        using var scope = _yearViewCacheGate.EnterScope();
        if (expectedVersion != _yearViewCacheVersion)
        {
            return;
        }

        _yearViewCache[entry.Year] = entry with { AccessSequence = ++_yearViewCacheAccessSequence };
    }

    private void RemoveCompletedYearViewLoadTask(int year, Task<YearViewCacheEntry> completedTask)
    {
        using var scope = _yearViewCacheGate.EnterScope();
        if (_yearViewLoadTasks.TryGetValue(year, out var existingTask) &&
            ReferenceEquals(existingTask, completedTask))
        {
            _yearViewLoadTasks.Remove(year);
        }
    }

    private void InvalidateYearViewCache()
    {
        using var scope = _yearViewCacheGate.EnterScope();
        _yearViewCacheVersion++;
        _yearViewCache.Clear();
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

    private void PublishViewRangeChanged()
    {
        var (from, to) = GetCurrentViewDisplayRange();
        WeakReferenceMessenger.Default.Send(new CalendarViewRangeChangedMessage(from, to));
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

    private static (DateOnly From, DateOnly To) GetDisplayDateRange(ViewMode viewMode, DateOnly date)
    {
        if (viewMode != ViewMode.Month)
        {
            return GetDateRange(viewMode, date);
        }

        var firstDay = new DateOnly(date.Year, date.Month, 1);
        var lastDay = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        return (GetWeekRange(firstDay).From, GetWeekRange(lastDay).To);
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

    private static bool OverlapsVisibleRange(CalendarEventDisplayModel item, DateOnly from, DateOnly to)
    {
        var rangeStartLocal = from.ToDateTime(TimeOnly.MinValue);
        var rangeEndExclusiveLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var effectiveEnd = item.EndLocal > item.StartLocal
            ? item.EndLocal
            : item.StartLocal;

        return item.StartLocal < rangeEndExclusiveLocal && effectiveEnd >= rangeStartLocal;
    }

    private int _yearViewCacheVersion;

    private sealed record YearViewCacheEntry(
        int Year,
        IList<CalendarEventDisplayModel> Events,
        IReadOnlyDictionary<DateOnly, SyncStatus> SyncStatusMap,
        DateTime? LastSyncTime,
        long AccessSequence);
}

public sealed record YearViewDataSnapshot(
    int Year,
    IList<CalendarEventDisplayModel> Events,
    IReadOnlyDictionary<DateOnly, SyncStatus> SyncStatusMap,
    DateTime? LastSyncTime);

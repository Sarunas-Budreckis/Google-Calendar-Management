using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class EventDetailsPanelViewModel : ObservableObject
{
    private const string SavingStatusText = "Saving...";
    private const string SavedStatusText = "Saved";
    private const string TitleRequiredMessage = "Title is required";
    private const string EndBeforeStartMessage = "End time must be after start time";
    private const string DefaultSourceDisplay = "From Google Calendar";
    private const string PendingSourceDisplay = "Pending local edit";
    private const string NoDescriptionPlaceholder = "No description provided.";
    private const string UndoFieldEditTitle = nameof(EditTitle);
    private const string UndoFieldEditStartDate = nameof(EditStartDate);
    private const string UndoFieldEditStartTime = nameof(EditStartTime);
    private const string UndoFieldEditEndDate = nameof(EditEndDate);
    private const string UndoFieldEditEndTime = nameof(EditEndTime);
    private const string UndoFieldEditDescription = nameof(EditDescription);

    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarSelectionService _selectionService;
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource _loadCts = new();

    private string? _currentGcalEventId;
    private CalendarEventDisplayModel? _currentEvent;
    private bool _isPanelVisible;
    private string _title = string.Empty;
    private string _startEndDisplay = string.Empty;
    private string _colorHex = string.Empty;
    private string _colorName = string.Empty;
    private string _descriptionDisplay = string.Empty;
    private string _sourceDisplay = DefaultSourceDisplay;
    private string _lastSyncedDisplay = string.Empty;
    private bool _isEditMode;
    private string _editTitle = string.Empty;
    private DateOnly _editStartDate = DateOnly.FromDateTime(DateTime.Today);
    private TimeOnly _editStartTime = TimeOnly.MinValue;
    private DateOnly _editEndDate = DateOnly.FromDateTime(DateTime.Today);
    private TimeOnly _editEndTime = TimeOnly.MinValue;
    private string _editDescription = string.Empty;
    private string _titleError = string.Empty;
    private string _dateTimeError = string.Empty;
    private string _saveStatusText = string.Empty;
    private DispatcherTimer? _debounceTimer;
    private bool _isInitializingEditFields;
    private bool _hasPendingLocalChanges;
    private bool _isSaving;
    private UndoState? _undoState;

    public EventDetailsPanelViewModel(
        ICalendarQueryService queryService,
        ICalendarSelectionService selectionService,
        IGcalEventRepository gcalEventRepository,
        IPendingEventRepository pendingEventRepository,
        TimeProvider? timeProvider = null)
    {
        _queryService = queryService;
        _selectionService = selectionService;
        _gcalEventRepository = gcalEventRepository;
        _pendingEventRepository = pendingEventRepository;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _timeProvider = timeProvider ?? TimeProvider.System;

        CloseCommand = new AsyncRelayCommand(ClosePanelAsync);
        EnterEditModeCommand = new RelayCommand(EnterEditMode);

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModel, EventSelectedMessage>(
            this,
            static (recipient, message) => recipient.OnEventSelected(message));

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModel, SyncCompletedMessage>(
            this,
            static (recipient, _) => recipient.OnSyncCompleted());
    }

    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        private set
        {
            if (SetProperty(ref _isPanelVisible, value))
            {
                OnPropertyChanged(nameof(PanelVisibility));
            }
        }
    }

    public Visibility PanelVisibility => _isPanelVisible ? Visibility.Visible : Visibility.Collapsed;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string StartEndDisplay
    {
        get => _startEndDisplay;
        private set => SetProperty(ref _startEndDisplay, value);
    }

    public string ColorHex
    {
        get => _colorHex;
        private set => SetProperty(ref _colorHex, value);
    }

    public string ColorName
    {
        get => _colorName;
        private set
        {
            if (SetProperty(ref _colorName, value))
            {
                OnPropertyChanged(nameof(ColorPlaceholderText));
            }
        }
    }

    public string ColorPlaceholderText => $"Color: {ColorName}";

    public string DescriptionDisplay
    {
        get => _descriptionDisplay;
        private set => SetProperty(ref _descriptionDisplay, value);
    }

    public string SourceDisplay
    {
        get => _sourceDisplay;
        private set => SetProperty(ref _sourceDisplay, value);
    }

    public string LastSyncedDisplay
    {
        get => _lastSyncedDisplay;
        private set => SetProperty(ref _lastSyncedDisplay, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(ReadOnlyVisibility));
                OnPropertyChanged(nameof(EditModeVisibility));
            }
        }
    }

    public Visibility ReadOnlyVisibility => IsEditMode ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditModeVisibility => IsEditMode ? Visibility.Visible : Visibility.Collapsed;

    public string EditTitle
    {
        get => _editTitle;
        set => SetEditableProperty(ref _editTitle, value, UndoFieldEditTitle);
    }

    public DateOnly EditStartDate
    {
        get => _editStartDate;
        set => SetEditableProperty(ref _editStartDate, value, UndoFieldEditStartDate);
    }

    public TimeOnly EditStartTime
    {
        get => _editStartTime;
        set => SetEditableProperty(ref _editStartTime, value, UndoFieldEditStartTime);
    }

    public DateOnly EditEndDate
    {
        get => _editEndDate;
        set => SetEditableProperty(ref _editEndDate, value, UndoFieldEditEndDate);
    }

    public TimeOnly EditEndTime
    {
        get => _editEndTime;
        set => SetEditableProperty(ref _editEndTime, value, UndoFieldEditEndTime);
    }

    public string EditDescription
    {
        get => _editDescription;
        set => SetEditableProperty(ref _editDescription, value, UndoFieldEditDescription);
    }

    public string TitleError
    {
        get => _titleError;
        private set
        {
            if (SetProperty(ref _titleError, value))
            {
                OnPropertyChanged(nameof(TitleErrorVisibility));
            }
        }
    }

    public Visibility TitleErrorVisibility =>
        string.IsNullOrWhiteSpace(TitleError) ? Visibility.Collapsed : Visibility.Visible;

    public string DateTimeError
    {
        get => _dateTimeError;
        private set
        {
            if (SetProperty(ref _dateTimeError, value))
            {
                OnPropertyChanged(nameof(DateTimeErrorVisibility));
            }
        }
    }

    public Visibility DateTimeErrorVisibility =>
        string.IsNullOrWhiteSpace(DateTimeError) ? Visibility.Collapsed : Visibility.Visible;

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set
        {
            if (SetProperty(ref _saveStatusText, value))
            {
                OnPropertyChanged(nameof(SaveStatusVisibility));
            }
        }
    }

    public Visibility SaveStatusVisibility =>
        string.IsNullOrWhiteSpace(SaveStatusText) ? Visibility.Collapsed : Visibility.Visible;

    public bool HasValidationErrors =>
        !string.IsNullOrWhiteSpace(TitleError) || !string.IsNullOrWhiteSpace(DateTimeError);

    public IAsyncRelayCommand CloseCommand { get; }

    public IRelayCommand EnterEditModeCommand { get; }

    public void EnterEditMode()
    {
        if (!IsPanelVisible || _currentEvent is null)
        {
            return;
        }

        StopDebounce();
        _hasPendingLocalChanges = false;
        _undoState = null;
        SaveStatusText = string.Empty;

        _isInitializingEditFields = true;
        EditTitle = _currentEvent.Title;
        EditStartDate = DateOnly.FromDateTime(_currentEvent.StartLocal.Date);
        EditStartTime = TimeOnly.FromDateTime(_currentEvent.StartLocal);
        EditEndDate = DateOnly.FromDateTime(_currentEvent.EndLocal.Date);
        EditEndTime = TimeOnly.FromDateTime(_currentEvent.EndLocal);
        EditDescription = _currentEvent.Description ?? string.Empty;
        _isInitializingEditFields = false;

        ValidateFields();
        IsEditMode = true;
    }

    public bool ValidateFields()
    {
        TitleError = string.IsNullOrWhiteSpace(EditTitle) ? TitleRequiredMessage : string.Empty;

        var (_, _, startLocal, endLocal) = BuildEditDateTimes();
        DateTimeError = endLocal <= startLocal ? EndBeforeStartMessage : string.Empty;
        return !HasValidationErrors;
    }

    public void UndoLastChange()
    {
        if (!IsEditMode || _undoState is null)
        {
            return;
        }

        _isInitializingEditFields = true;

        switch (_undoState.FieldName)
        {
            case UndoFieldEditTitle:
                EditTitle = (string?)_undoState.PreviousValue ?? string.Empty;
                break;
            case UndoFieldEditStartDate:
                EditStartDate = _undoState.PreviousValue is DateOnly startDate
                    ? startDate
                    : EditStartDate;
                break;
            case UndoFieldEditStartTime:
                EditStartTime = _undoState.PreviousValue is TimeOnly startTime
                    ? startTime
                    : EditStartTime;
                break;
            case UndoFieldEditEndDate:
                EditEndDate = _undoState.PreviousValue is DateOnly endDate
                    ? endDate
                    : EditEndDate;
                break;
            case UndoFieldEditEndTime:
                EditEndTime = _undoState.PreviousValue is TimeOnly endTime
                    ? endTime
                    : EditEndTime;
                break;
            case UndoFieldEditDescription:
                EditDescription = (string?)_undoState.PreviousValue ?? string.Empty;
                break;
        }

        _isInitializingEditFields = false;
        _undoState = null;
        _hasPendingLocalChanges = true;

        if (ValidateFields())
        {
            StartDebounce();
        }
        else
        {
            SaveStatusText = string.Empty;
            StopDebounce();
        }
    }

    public async Task SaveNowAsync(CancellationToken ct = default)
    {
        if (_isSaving || _currentGcalEventId is null)
        {
            return;
        }

        StopDebounce();
        if (!ValidateFields())
        {
            SaveStatusText = string.Empty;
            return;
        }

        _isSaving = true;
        SaveStatusText = SavingStatusText;

        try
        {
            var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(_currentGcalEventId, ct);
            if (gcalEvent is null)
            {
                SaveStatusText = string.Empty;
                return;
            }

            var pendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(_currentGcalEventId, ct);
            var (startUtc, endUtc, _, _) = BuildEditDateTimes();
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

            if (pendingEvent is null)
            {
                pendingEvent = new PendingEvent
                {
                    Id = Guid.NewGuid(),
                    GcalEventId = _currentGcalEventId,
                    Summary = EditTitle.Trim(),
                    Description = NormalizeDescription(EditDescription),
                    StartDatetime = startUtc,
                    EndDatetime = endUtc,
                    ColorId = gcalEvent.ColorId ?? "azure",
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };
            }
            else
            {
                pendingEvent.Summary = EditTitle.Trim();
                pendingEvent.Description = NormalizeDescription(EditDescription);
                pendingEvent.StartDatetime = startUtc;
                pendingEvent.EndDatetime = endUtc;
                pendingEvent.UpdatedAt = utcNow;
            }

            await _pendingEventRepository.UpsertAsync(pendingEvent, ct);

            var refreshedEvent = await _queryService.GetEventByGcalIdAsync(_currentGcalEventId, ct);
            if (refreshedEvent is not null)
            {
                RunOnUiThread(() => ApplyEventDetails(refreshedEvent, _currentGcalEventId!, keepEditMode: true));
            }

            _hasPendingLocalChanges = false;
            SaveStatusText = SavedStatusText;
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(_currentGcalEventId));
        }
        finally
        {
            _isSaving = false;
        }
    }

    public async Task<bool> HandleEscapeAsync()
    {
        if (!IsPanelVisible)
        {
            return false;
        }

        if (!IsEditMode)
        {
            _selectionService.ClearSelection();
            return true;
        }

        if (_hasPendingLocalChanges)
        {
            if (!ValidateFields())
            {
                return true;
            }

            await SaveNowAsync();
        }

        _selectionService.ClearSelection();
        return true;
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();

        if (message.GcalEventId is null)
        {
            RunOnUiThread(HidePanel);
            return;
        }

        _ = LoadEventAsync(message.GcalEventId, _loadCts.Token);
    }

    private async Task LoadEventAsync(string gcalEventId, CancellationToken ct)
    {
        CalendarEventDisplayModel? evt;
        try
        {
            evt = await _queryService.GetEventByGcalIdAsync(gcalEventId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (evt is null)
        {
            RunOnUiThread(HidePanel);
            return;
        }

        var loadedEvent = evt;
        RunOnUiThread(() =>
        {
            if (!ct.IsCancellationRequested)
            {
                ApplyEventDetails(loadedEvent, gcalEventId);
            }
        });
    }

    private void ApplyEventDetails(CalendarEventDisplayModel evt, string gcalEventId, bool keepEditMode = false)
    {
        var isSameEvent = string.Equals(_currentGcalEventId, gcalEventId, StringComparison.Ordinal);
        _currentGcalEventId = gcalEventId;
        _currentEvent = evt;

        Title = evt.Title;
        ColorHex = evt.ColorHex;
        ColorName = evt.ColorName;
        DescriptionDisplay = string.IsNullOrEmpty(evt.Description)
            ? NoDescriptionPlaceholder
            : evt.Description;
        SourceDisplay = evt.IsPending ? PendingSourceDisplay : DefaultSourceDisplay;
        LastSyncedDisplay = evt.LastSyncedAt.HasValue
            ? evt.LastSyncedAt.Value.ToLocalTime().ToString("g")
            : "Never";
        StartEndDisplay = BuildStartEndDisplay(evt.StartLocal, evt.EndLocal, evt.IsAllDay);
        IsPanelVisible = true;

        if (!keepEditMode || !isSameEvent)
        {
            ResetEditSession();
        }
    }

    private void OnSyncCompleted()
    {
        if (_currentGcalEventId is null || IsEditMode)
        {
            return;
        }

        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadEventAsync(_currentGcalEventId, _loadCts.Token);
    }

    private async Task ClosePanelAsync()
    {
        await HandleEscapeAsync();
    }

    private void HidePanel()
    {
        _currentGcalEventId = null;
        _currentEvent = null;
        IsPanelVisible = false;
        Title = string.Empty;
        StartEndDisplay = string.Empty;
        ColorHex = string.Empty;
        ColorName = string.Empty;
        DescriptionDisplay = string.Empty;
        SourceDisplay = DefaultSourceDisplay;
        LastSyncedDisplay = string.Empty;
        ResetEditSession();
    }

    private void ResetEditSession()
    {
        StopDebounce();
        _isInitializingEditFields = true;
        IsEditMode = false;
        EditTitle = string.Empty;
        EditStartDate = DateOnly.FromDateTime(DateTime.Today);
        EditStartTime = TimeOnly.MinValue;
        EditEndDate = DateOnly.FromDateTime(DateTime.Today);
        EditEndTime = TimeOnly.MinValue;
        EditDescription = string.Empty;
        _isInitializingEditFields = false;
        TitleError = string.Empty;
        DateTimeError = string.Empty;
        SaveStatusText = string.Empty;
        _hasPendingLocalChanges = false;
        _undoState = null;
    }

    private void SetEditableProperty<T>(ref T storage, T value, string fieldName)
    {
        var previousValue = storage;
        if (!SetProperty(ref storage, value))
        {
            return;
        }

        if (_isInitializingEditFields || !IsEditMode)
        {
            return;
        }

        TrackUndo(fieldName, previousValue);
        _hasPendingLocalChanges = true;

        if (ValidateFields())
        {
            StartDebounce();
        }
        else
        {
            SaveStatusText = string.Empty;
            StopDebounce();
        }
    }

    private void TrackUndo<T>(string fieldName, T previousValue)
    {
        if (_undoState is null || !string.Equals(_undoState.FieldName, fieldName, StringComparison.Ordinal) || !_hasPendingLocalChanges)
        {
            _undoState = new UndoState(fieldName, previousValue);
        }
    }

    private void StartDebounce()
    {
        SaveStatusText = SavingStatusText;

        if (_dispatcherQueue is null)
        {
            return;
        }

        _debounceTimer?.Stop();
        _debounceTimer ??= CreateDebounceTimer();
        _debounceTimer.Start();
    }

    private DispatcherTimer CreateDebounceTimer()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };

        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await SaveNowAsync();
        };

        return timer;
    }

    private void StopDebounce()
    {
        _debounceTimer?.Stop();
    }

    private (DateTime StartUtc, DateTime EndUtc, DateTime StartLocal, DateTime EndLocal) BuildEditDateTimes()
    {
        var startLocal = DateTime.SpecifyKind(
            EditStartDate.ToDateTime(EditStartTime),
            DateTimeKind.Local);
        var endLocal = DateTime.SpecifyKind(
            EditEndDate.ToDateTime(EditEndTime),
            DateTimeKind.Local);

        return (startLocal.ToUniversalTime(), endLocal.ToUniversalTime(), startLocal, endLocal);
    }

    private static string BuildStartEndDisplay(DateTime startLocal, DateTime endLocal, bool isAllDay)
    {
        if (isAllDay)
        {
            var displayEnd = endLocal.AddDays(-1);
            return startLocal.Date == displayEnd.Date
                ? $"{startLocal:ddd, MMM d, yyyy} (All day)"
                : $"{startLocal:ddd, MMM d} \u2013 {displayEnd:ddd, MMM d, yyyy}";
        }

        return $"{startLocal:ddd, MMM d, yyyy, h:mm tt} \u2013 {endLocal:h:mm tt}";
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description;
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue is null)
        {
            action();
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
    }

    private sealed record UndoState(string FieldName, object? PreviousValue);
}

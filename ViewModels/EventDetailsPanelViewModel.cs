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
    private const string PendingSourceDisplay = "Local changes, pending push to GCal";
    private const string PendingDeleteSourceDisplay = "Pending delete — will be removed from Google Calendar when pushed";
    private const string DraftSourceDisplay = "Not yet published to Google Calendar";
    private const string NoDescriptionPlaceholder = "No description provided.";
    private const string UndoFieldEditTitle = nameof(EditTitle);
    private const string UndoFieldEditSingleDate = nameof(EditSingleDate);
    private const string UndoFieldEditStartDate = nameof(EditStartDate);
    private const string UndoFieldEditStartTime = nameof(EditStartTime);
    private const string UndoFieldEditDraggedRange = "DraggedTimeRange";
    private const string UndoFieldEditResizedEnd = "ResizedEndTime";
    private const string UndoFieldEditEndDate = nameof(EditEndDate);
    private const string UndoFieldEditEndTime = nameof(EditEndTime);
    private const string UndoFieldEditDescription = nameof(EditDescription);

    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarSelectionService _selectionService;
    private readonly IColorMappingService _colorMappingService;
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly IContentDialogService? _contentDialogService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource _loadCts = new();

    private string? _currentEventId;
    private CalendarEventSourceKind? _currentSourceKind;
    private CalendarEventDisplayModel? _currentEvent;
    private bool _isPanelVisible;
    private string _title = string.Empty;
    private string _startEndDisplay = string.Empty;
    private string _colorHex = string.Empty;
    private string _colorName = string.Empty;
    private string _descriptionDisplay = string.Empty;
    private string _sourceDisplay = DefaultSourceDisplay;
    private string _lastSyncedDisplay = string.Empty;
    private string _lastSavedLocallyDisplay = "No local changes";
    private bool _isPendingEvent;
    private bool _isPendingDeleteEvent;
    private bool _isEditMode;
    private string _editTitle = string.Empty;
    private DateOnly _editStartDate = DateOnly.FromDateTime(DateTime.Today);
    private TimeOnly _editStartTime = TimeOnly.MinValue;
    private DateOnly _editEndDate = DateOnly.FromDateTime(DateTime.Today);
    private TimeOnly _editEndTime = TimeOnly.MinValue;
    private string _editDescription = string.Empty;
    private string _editColorId = "azure";
    private string _editColorName = "Azure";
    private string _editColorHex = "#0088CC";
    private string _titleError = string.Empty;
    private string _dateTimeError = string.Empty;
    private string _saveStatusText = string.Empty;
    private DispatcherTimer? _debounceTimer;
    private bool _isInitializingEditFields;
    private bool _hasPendingLocalChanges;
    private bool _isSaving;
    private bool _isUneditedNewDraft;
    private UndoState? _undoState;
    private DragRescheduleUndoState? _dragRescheduleUndoState;

    public EventDetailsPanelViewModel(
        ICalendarQueryService queryService,
        ICalendarSelectionService selectionService,
        IColorMappingService colorMappingService,
        IGcalEventRepository gcalEventRepository,
        IPendingEventRepository pendingEventRepository,
        TimeProvider? timeProvider = null,
        IContentDialogService? contentDialogService = null)
    {
        _queryService = queryService;
        _selectionService = selectionService;
        _colorMappingService = colorMappingService;
        _gcalEventRepository = gcalEventRepository;
        _pendingEventRepository = pendingEventRepository;
        _contentDialogService = contentDialogService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _timeProvider = timeProvider ?? TimeProvider.System;

        AvailableColors = _colorMappingService.PickerColors;
        CloseCommand = new AsyncRelayCommand(ClosePanelAsync);
        EnterEditModeCommand = new RelayCommand(EnterEditMode);
        SaveAndExitEditModeCommand = new AsyncRelayCommand(SaveAndExitEditModeAsync);
        RevertPendingChangesCommand = new AsyncRelayCommand(RevertPendingChangesAsync);
        SelectColorCommand = new AsyncRelayCommand<string?>(SelectColorAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteEventAsync);

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModel, EventSelectedMessage>(
            this,
            static (recipient, message) => recipient.OnEventSelected(message));

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModel, SyncCompletedMessage>(
            this,
            static (recipient, _) => recipient.OnSyncCompleted());

        WeakReferenceMessenger.Default.Register<EventDetailsPanelViewModel, EventUpdatedMessage>(
            this,
            static (recipient, message) => recipient.OnEventUpdated(message));
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

    public string LastSavedLocallyDisplay
    {
        get => _lastSavedLocallyDisplay;
        private set => SetProperty(ref _lastSavedLocallyDisplay, value);
    }

    public bool IsPendingEvent
    {
        get => _isPendingEvent;
        private set
        {
            if (SetProperty(ref _isPendingEvent, value))
            {
                OnPropertyChanged(nameof(RevertButtonVisibility));
            }
        }
    }

    public bool IsPendingDeleteEvent
    {
        get => _isPendingDeleteEvent;
        private set => SetProperty(ref _isPendingDeleteEvent, value);
    }

    public Visibility RevertButtonVisibility =>
        IsPendingEvent ? Visibility.Visible : Visibility.Collapsed;

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
        set
        {
            if (SetEditableProperty(ref _editStartDate, value, UndoFieldEditStartDate))
            {
                NotifyDateEditorShapeChanged();
            }
        }
    }

    public TimeOnly EditStartTime
    {
        get => _editStartTime;
        set => SetEditStartTime(value);
    }

    public DateOnly EditEndDate
    {
        get => _editEndDate;
        set
        {
            if (SetEditableProperty(ref _editEndDate, value, UndoFieldEditEndDate))
            {
                NotifyDateEditorShapeChanged();
            }
        }
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

    public string EditColorId
    {
        get => _editColorId;
        private set => SetProperty(ref _editColorId, value);
    }

    public string EditColorName
    {
        get => _editColorName;
        private set => SetProperty(ref _editColorName, value);
    }

    public string EditColorHex
    {
        get => _editColorHex;
        private set => SetProperty(ref _editColorHex, value);
    }

    public IReadOnlyList<CalendarColorOption> AvailableColors { get; }

    public DateOnly EditSingleDate
    {
        get => _editStartDate;
        set => SetEditSingleDate(value);
    }

    public bool UsesSingleDateEditor => EditStartDate == EditEndDate;

    public bool IsNewUneditedDraft => _isUneditedNewDraft;

    public bool CanInteractivelyAdjustSelectedTimedEvent =>
        IsEditMode &&
        _currentEvent is not null &&
        !_currentEvent.IsAllDay &&
        _currentEventId is not null;

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

    public IAsyncRelayCommand SaveAndExitEditModeCommand { get; }

    public IAsyncRelayCommand RevertPendingChangesCommand { get; }

    public IAsyncRelayCommand<string?> SelectColorCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

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
        ApplyEditColor(_currentEvent.ColorKey);
        _isInitializingEditFields = false;

        ValidateFields();
        IsEditMode = true;
    }

    public bool ValidateFields()
    {
        TitleError = string.IsNullOrWhiteSpace(EditTitle) && !AllowsBlankDraftTitle()
            ? TitleRequiredMessage
            : string.Empty;

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
            case UndoFieldEditSingleDate:
                if (_undoState.PreviousValue is SingleDateUndoValue singleDate)
                {
                    EditStartDate = singleDate.StartDate;
                    EditEndDate = singleDate.EndDate;
                }
                break;
            case UndoFieldEditStartDate:
                EditStartDate = _undoState.PreviousValue is DateOnly startDate
                    ? startDate
                    : EditStartDate;
                break;
            case UndoFieldEditStartTime:
                if (_undoState.PreviousValue is StartTimeUndoValue startTime)
                {
                    EditStartTime = startTime.StartTime;
                    EditEndDate = startTime.EndDate;
                    EditEndTime = startTime.EndTime;
                }
                break;
            case UndoFieldEditDraggedRange:
                if (_undoState.PreviousValue is TimeRangeUndoValue draggedRange)
                {
                    EditStartDate = draggedRange.StartDate;
                    EditStartTime = draggedRange.StartTime;
                    EditEndDate = draggedRange.EndDate;
                    EditEndTime = draggedRange.EndTime;
                }
                break;
            case UndoFieldEditResizedEnd:
                if (_undoState.PreviousValue is EndTimeUndoValue resizedEnd)
                {
                    EditEndDate = resizedEnd.EndDate;
                    EditEndTime = resizedEnd.EndTime;
                }
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

    public async Task<bool> SaveNowAsync(bool keepEditMode = true, CancellationToken ct = default)
    {
        if (_isSaving || _currentEventId is null || _currentSourceKind is null)
        {
            return false;
        }

        StopDebounce();
        if (!ValidateFields())
        {
            SaveStatusText = string.Empty;
            return false;
        }

        _isSaving = true;
        SaveStatusText = SavingStatusText;

        try
        {
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            PendingEvent? pendingEvent;

            if (_currentSourceKind == CalendarEventSourceKind.Pending)
            {
                pendingEvent = await _pendingEventRepository.GetByPendingEventIdAsync(_currentEventId, ct);
                if (pendingEvent is null)
                {
                    SaveStatusText = string.Empty;
                    return false;
                }

                var (draftStartUtc, draftEndUtc, _, _) = BuildEditDateTimes();
                pendingEvent.Summary = EditTitle.Trim();
                pendingEvent.Description = NormalizeDescription(EditDescription);
                pendingEvent.StartDatetime = draftStartUtc;
                pendingEvent.EndDatetime = draftEndUtc;
                pendingEvent.ColorId = EditColorId;
                pendingEvent.UpdatedAt = utcNow;
                await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
            }
            else
            {
                var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(_currentEventId, ct);
                if (gcalEvent is null)
                {
                    SaveStatusText = string.Empty;
                    return false;
                }

                pendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(_currentEventId, ct);
                var (startUtc, endUtc, _, _) = BuildEditDateTimes();

                if (pendingEvent is null)
                {
                    pendingEvent = new PendingEvent
                    {
                        PendingEventId = $"pending_{Guid.NewGuid():N}",
                        GcalEventId = _currentEventId,
                        CalendarId = gcalEvent.CalendarId,
                        Summary = EditTitle.Trim(),
                        Description = NormalizeDescription(EditDescription),
                        StartDatetime = startUtc,
                        EndDatetime = endUtc,
                        IsAllDay = gcalEvent.IsAllDay,
                        ColorId = EditColorId,
                        AppCreated = false,
                        SourceSystem = gcalEvent.SourceSystem ?? "google-overlay",
                        ReadyToPublish = false,
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
                    pendingEvent.IsAllDay = gcalEvent.IsAllDay;
                    pendingEvent.ColorId = EditColorId;
                    pendingEvent.UpdatedAt = utcNow;
                }

                await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
            }

            var refreshedEvent = await _queryService.GetEventByIdAsync(_currentEventId, ct);
            if (refreshedEvent is not null)
            {
                RunOnUiThread(() => ApplyEventDetails(refreshedEvent, _currentEventId!, keepEditMode));
            }
            else if (!keepEditMode)
            {
                RunOnUiThread(() => IsEditMode = false);
            }

            _hasPendingLocalChanges = false;
            SaveStatusText = SavedStatusText;
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(_currentEventId));
            return true;
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

        if (_isUneditedNewDraft)
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

    public async Task SaveAndExitEditModeAsync()
    {
        if (!IsEditMode)
        {
            return;
        }

        if (_hasPendingLocalChanges)
        {
            var saved = await SaveNowAsync(keepEditMode: false);
            if (!saved)
            {
                return;
            }
        }
        else
        {
            StopDebounce();
            SaveStatusText = string.Empty;
            IsEditMode = false;
        }
    }

    public async Task SelectColorAsync(string? selectedColorId, CancellationToken ct = default)
    {
        if (!IsEditMode || _currentEventId is null)
        {
            return;
        }

        var normalizedColorKey = _colorMappingService.NormalizeColorKey(selectedColorId);
        if (string.Equals(EditColorId, normalizedColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _isUneditedNewDraft = false;
        OnPropertyChanged(nameof(IsNewUneditedDraft));
        ApplyEditColor(normalizedColorKey);
        _hasPendingLocalChanges = true;
        PublishOptimisticEventUpdate();

        var saved = await SaveNowAsync(keepEditMode: true, ct);
        if (saved)
        {
            return;
        }

        var eventId = _currentEventId;
        var refreshedEvent = await _queryService.GetEventByIdAsync(eventId, ct);
        if (refreshedEvent is not null)
        {
            RunOnUiThread(() => ApplyEventDetails(refreshedEvent, eventId, keepEditMode: true));
        }
    }

    public async Task ApplyColorToEventAsync(
        string eventId,
        CalendarEventSourceKind sourceKind,
        string? selectedColorId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        if (IsEditMode &&
            string.Equals(_currentEventId, eventId, StringComparison.Ordinal) &&
            _currentSourceKind == sourceKind)
        {
            await SelectColorAsync(selectedColorId, ct);
            return;
        }

        var normalizedColorKey = _colorMappingService.NormalizeColorKey(selectedColorId);
        var currentDisplayEvent = await _queryService.GetEventByIdAsync(eventId, ct);
        if (currentDisplayEvent is null ||
            string.Equals(currentDisplayEvent.ColorKey, normalizedColorKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        switch (sourceKind)
        {
            case CalendarEventSourceKind.Pending:
            {
                var pendingDraft = await _pendingEventRepository.GetByPendingEventIdAsync(eventId, ct);
                if (pendingDraft is null)
                {
                    return;
                }

                pendingDraft.ColorId = normalizedColorKey;
                pendingDraft.UpdatedAt = utcNow;
                await _pendingEventRepository.UpsertAsync(pendingDraft, ct);
                break;
            }

            case CalendarEventSourceKind.Google:
            {
                var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(eventId, ct);
                if (gcalEvent is null)
                {
                    return;
                }

                var pendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(eventId, ct);
                if (pendingEvent is null)
                {
                    pendingEvent = new PendingEvent
                    {
                        PendingEventId = $"pending_{Guid.NewGuid():N}",
                        GcalEventId = eventId,
                        CalendarId = gcalEvent.CalendarId,
                        Summary = currentDisplayEvent.Title,
                        Description = NormalizeDescription(currentDisplayEvent.Description),
                        StartDatetime = currentDisplayEvent.StartUtc,
                        EndDatetime = currentDisplayEvent.EndUtc,
                        IsAllDay = currentDisplayEvent.IsAllDay,
                        ColorId = normalizedColorKey,
                        AppCreated = false,
                        SourceSystem = gcalEvent.SourceSystem ?? "google-overlay",
                        ReadyToPublish = false,
                        CreatedAt = utcNow,
                        UpdatedAt = utcNow
                    };
                }
                else
                {
                    pendingEvent.ColorId = normalizedColorKey;
                    pendingEvent.UpdatedAt = utcNow;
                }

                await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
                break;
            }
        }

        if (string.Equals(_currentEventId, eventId, StringComparison.Ordinal))
        {
            var refreshedEvent = await _queryService.GetEventByIdAsync(eventId, ct);
            if (refreshedEvent is not null)
            {
                RunOnUiThread(() => ApplyEventDetails(refreshedEvent, eventId, keepEditMode: false));
            }
        }

        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
    }

    public async Task RevertPendingChangesAsync()
    {
        if (_currentEventId is null || _currentSourceKind is null || !IsPendingEvent)
        {
            return;
        }

        await RevertPendingChangesForEventAsync(_currentEventId, _currentSourceKind.Value);
    }

    public async Task RevertPendingChangesForEventAsync(
        string eventId,
        CalendarEventSourceKind sourceKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        var isCurrentEvent = string.Equals(_currentEventId, eventId, StringComparison.Ordinal) &&
            _currentSourceKind == sourceKind;

        if (isCurrentEvent)
        {
            StopDebounce();
        }

        switch (sourceKind)
        {
            case CalendarEventSourceKind.Pending:
                await _pendingEventRepository.DeleteByPendingEventIdAsync(eventId, ct);
                break;

            case CalendarEventSourceKind.Google:
            {
                var currentDisplayEvent = isCurrentEvent
                    ? _currentEvent
                    : await _queryService.GetEventByIdAsync(eventId, ct);
                if (currentDisplayEvent is null || !currentDisplayEvent.IsPending)
                {
                    return;
                }

                await _pendingEventRepository.DeleteByGcalEventIdAsync(eventId, ct);
                break;
            }

            default:
                return;
        }

        if (isCurrentEvent)
        {
            if (sourceKind == CalendarEventSourceKind.Pending)
            {
                RunOnUiThread(HidePanel);
            }
            else
            {
                var refreshedEvent = await _queryService.GetEventByIdAsync(eventId, ct);
                if (refreshedEvent is not null)
                {
                    RunOnUiThread(() => ApplyEventDetails(refreshedEvent, eventId, keepEditMode: false));
                }
                else
                {
                    RunOnUiThread(HidePanel);
                }
            }
        }

        SaveStatusText = string.Empty;
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
    }

    public bool IsEditingSelectedTimedEvent(string? eventId)
    {
        return IsEditingSelectedTimedEvent(eventId, null);
    }

    public bool IsEditingSelectedTimedEvent(string? eventId, CalendarEventSourceKind? sourceKind)
    {
        return CanInteractivelyAdjustSelectedTimedEvent &&
            string.Equals(_currentEventId, eventId, StringComparison.Ordinal) &&
            (sourceKind is null || _currentSourceKind == sourceKind);
    }

    public bool TryGetEditableTimedRange(string? eventId, out DateTime startLocal, out DateTime endLocal)
    {
        return TryGetEditableTimedRange(eventId, null, out startLocal, out endLocal);
    }

    public bool TryGetEditableTimedRange(
        string? eventId,
        CalendarEventSourceKind? sourceKind,
        out DateTime startLocal,
        out DateTime endLocal)
    {
        if (!IsEditingSelectedTimedEvent(eventId, sourceKind))
        {
            startLocal = default;
            endLocal = default;
            return false;
        }

        (_, _, startLocal, endLocal) = BuildEditDateTimes();
        return true;
    }

    public void ApplyDraggedTimeRange(string? eventId, DateTime startLocal, DateTime endLocal)
    {
        if (!IsEditingSelectedTimedEvent(eventId))
        {
            return;
        }

        var previousValue = new TimeRangeUndoValue(EditStartDate, EditStartTime, EditEndDate, EditEndTime);
        ApplyTimeRangeInternal(startLocal, endLocal);
        _undoState = new UndoState(UndoFieldEditDraggedRange, previousValue);
        _hasPendingLocalChanges = true;
        PublishOptimisticEventUpdate(startLocal, endLocal);
        TriggerValidationAndSaveState();
    }

    public async Task<bool> ApplyDroppedTimeRangeAsync(
        string? eventId,
        CalendarEventSourceKind sourceKind,
        DateTime startLocal,
        DateTime endLocal,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventId) || endLocal <= startLocal)
        {
            return false;
        }

        var currentDisplayEvent = IsCurrentEvent(eventId, sourceKind)
            ? _currentEvent
            : await _queryService.GetEventByIdAsync(eventId, ct);
        if (currentDisplayEvent is null ||
            currentDisplayEvent.IsAllDay ||
            currentDisplayEvent.IsPendingDelete)
        {
            return false;
        }

        var startLocalWithKind = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        var endLocalWithKind = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        var startUtc = startLocalWithKind.ToUniversalTime();
        var endUtc = endLocalWithKind.ToUniversalTime();
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        PendingEvent? previousPendingEvent;
        PendingEvent pendingEvent;
        switch (sourceKind)
        {
            case CalendarEventSourceKind.Pending:
                previousPendingEvent = await _pendingEventRepository.GetByPendingEventIdAsync(eventId, ct);
                if (previousPendingEvent is null)
                {
                    return false;
                }

                pendingEvent = ClonePendingEvent(previousPendingEvent);
                pendingEvent.StartDatetime = startUtc;
                pendingEvent.EndDatetime = endUtc;
                pendingEvent.IsAllDay = false;
                pendingEvent.OperationType = "edit";
                pendingEvent.UpdatedAt = utcNow;
                break;

            case CalendarEventSourceKind.Google:
            {
                var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(eventId, ct);
                if (gcalEvent is null || gcalEvent.IsAllDay == true)
                {
                    return false;
                }

                previousPendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(eventId, ct);
                if (previousPendingEvent?.OperationType == "delete")
                {
                    return false;
                }

                pendingEvent = previousPendingEvent is null
                    ? new PendingEvent
                    {
                        PendingEventId = $"pending_{Guid.NewGuid():N}",
                        GcalEventId = eventId,
                        CalendarId = gcalEvent.CalendarId,
                        Summary = currentDisplayEvent.Title,
                        Description = NormalizeDescription(currentDisplayEvent.Description),
                        IsAllDay = false,
                        ColorId = currentDisplayEvent.ColorKey,
                        AppCreated = false,
                        SourceSystem = gcalEvent.SourceSystem ?? "google-overlay",
                        ReadyToPublish = false,
                        OperationType = "edit",
                        CreatedAt = utcNow
                    }
                    : ClonePendingEvent(previousPendingEvent);

                pendingEvent.StartDatetime = startUtc;
                pendingEvent.EndDatetime = endUtc;
                pendingEvent.IsAllDay = false;
                pendingEvent.OperationType = "edit";
                pendingEvent.UpdatedAt = utcNow;
                break;
            }

            default:
                return false;
        }

        _dragRescheduleUndoState = new DragRescheduleUndoState(
            eventId,
            sourceKind,
            previousPendingEvent is null ? null : ClonePendingEvent(previousPendingEvent));

        var previewEvent = BuildReschedulePreviewEvent(currentDisplayEvent, startLocalWithKind, endLocalWithKind, utcNow);
        ApplyCurrentEventAfterReschedule(eventId, sourceKind, previewEvent);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId, previewEvent));

        await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
        return true;
    }

    public async Task<bool> UndoLastDragRescheduleAsync(CancellationToken ct = default)
    {
        if (_dragRescheduleUndoState is null)
        {
            return false;
        }

        var undoState = _dragRescheduleUndoState;
        _dragRescheduleUndoState = null;

        switch (undoState.SourceKind)
        {
            case CalendarEventSourceKind.Pending:
                if (undoState.PreviousPendingEvent is null)
                {
                    return false;
                }

                await _pendingEventRepository.UpsertAsync(ClonePendingEvent(undoState.PreviousPendingEvent), ct);
                break;

            case CalendarEventSourceKind.Google:
                if (undoState.PreviousPendingEvent is null)
                {
                    await _pendingEventRepository.DeleteByGcalEventIdAsync(undoState.EventId, ct);
                }
                else
                {
                    await _pendingEventRepository.UpsertAsync(ClonePendingEvent(undoState.PreviousPendingEvent), ct);
                }

                break;

            default:
                return false;
        }

        if (IsCurrentEvent(undoState.EventId, undoState.SourceKind))
        {
            var refreshedEvent = await _queryService.GetEventByIdAsync(undoState.EventId, ct);
            if (refreshedEvent is not null)
            {
                RunOnUiThread(() => ApplyCurrentEventAfterReschedule(
                    undoState.EventId,
                    undoState.SourceKind,
                    refreshedEvent));
            }
        }

        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(undoState.EventId));
        return true;
    }

    public async Task UndoLastInteractiveChangeAsync(CancellationToken ct = default)
    {
        if (!await UndoLastDragRescheduleAsync(ct))
        {
            UndoLastChange();
        }
    }

    public void ApplyResizedEndTime(string? eventId, DateTime endLocal)
    {
        if (!IsEditingSelectedTimedEvent(eventId))
        {
            return;
        }

        var previousValue = new EndTimeUndoValue(EditEndDate, EditEndTime);
        _isInitializingEditFields = true;
        EditEndDate = DateOnly.FromDateTime(endLocal);
        EditEndTime = TimeOnly.FromDateTime(endLocal);
        _isInitializingEditFields = false;
        NotifyDateEditorShapeChanged();
        _undoState = new UndoState(UndoFieldEditResizedEnd, previousValue);
        _hasPendingLocalChanges = true;
        PublishOptimisticEventUpdate(
            EditStartDate.ToDateTime(EditStartTime),
            endLocal);
        TriggerValidationAndSaveState();
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = HandleEventSelectedAsync(message, _loadCts.Token);
    }

    private async Task HandleEventSelectedAsync(EventSelectedMessage message, CancellationToken ct)
    {
        var previousUneditedDraftId = _isUneditedNewDraft ? _currentEventId : null;
        var isSwitchingAwayFromUneditedDraft =
            previousUneditedDraftId is not null &&
            !string.Equals(previousUneditedDraftId, message.EventId, StringComparison.Ordinal);

        if (isSwitchingAwayFromUneditedDraft)
        {
            _isUneditedNewDraft = false;
            OnPropertyChanged(nameof(IsNewUneditedDraft));
        }

        if (message.EventId is null)
        {
            if (previousUneditedDraftId is not null)
            {
                _ = DeleteUneditedDraftAsync(previousUneditedDraftId);
            }
            else if (_hasPendingLocalChanges && _currentSourceKind == CalendarEventSourceKind.Pending)
            {
                await SaveNowAsync(keepEditMode: false, ct);
            }

            RunOnUiThread(HidePanel);
            return;
        }

        if (!isSwitchingAwayFromUneditedDraft &&
            _hasPendingLocalChanges &&
            _currentSourceKind == CalendarEventSourceKind.Pending)
        {
            await SaveNowAsync(keepEditMode: false, ct);
        }

        if (!ct.IsCancellationRequested)
        {
            await LoadEventAsync(message, ct);
        }

        if (previousUneditedDraftId is not null && !ct.IsCancellationRequested)
        {
            _ = DeleteUneditedDraftAsync(previousUneditedDraftId);
        }
    }

    private async Task LoadEventAsync(EventSelectedMessage message, CancellationToken ct)
    {
        CalendarEventDisplayModel? evt;
        try
        {
            evt = await _queryService.GetEventByIdAsync(message.EventId!, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
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
                ApplyEventDetails(loadedEvent, message.EventId!, keepEditMode: false);
                if (message.OpenInEditMode && loadedEvent.SourceKind == CalendarEventSourceKind.Pending)
                {
                    _isUneditedNewDraft = true;
                    OnPropertyChanged(nameof(IsNewUneditedDraft));
                }

                EnterEditMode();
            }
        });
    }

    private void ApplyEventDetails(CalendarEventDisplayModel evt, string eventId, bool keepEditMode = false)
    {
        var isSameEvent = string.Equals(_currentEventId, eventId, StringComparison.Ordinal);
        _currentEventId = eventId;
        _currentSourceKind = evt.SourceKind;
        _currentEvent = evt;

        Title = evt.Title;
        ColorHex = evt.ColorHex;
        ColorName = evt.ColorName;
        DescriptionDisplay = string.IsNullOrEmpty(evt.Description)
            ? NoDescriptionPlaceholder
            : evt.Description;
        IsPendingEvent = evt.IsPending;
        IsPendingDeleteEvent = evt.IsPendingDelete;
        SourceDisplay = evt.SourceKind == CalendarEventSourceKind.Pending
            ? DraftSourceDisplay
            : evt.IsPendingDelete
                ? PendingDeleteSourceDisplay
                : evt.IsPending
                    ? PendingSourceDisplay
                    : DefaultSourceDisplay;
        LastSyncedDisplay = evt.LastSyncedAt.HasValue
            ? evt.LastSyncedAt.Value.ToLocalTime().ToString("g")
            : "Never";
        LastSavedLocallyDisplay = evt.PendingUpdatedAt.HasValue
            ? evt.PendingUpdatedAt.Value.ToLocalTime().ToString("g")
            : "No local changes";
        StartEndDisplay = BuildStartEndDisplay(evt.StartLocal, evt.EndLocal, evt.IsAllDay);
        IsPanelVisible = true;

        if (!keepEditMode || !isSameEvent)
        {
            ResetEditSession();
        }
        else
        {
            ApplyEditColor(evt.ColorKey);
        }
    }

    private void OnSyncCompleted()
    {
        if (_currentEventId is null || IsEditMode)
        {
            return;
        }

        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadEventAsync(new EventSelectedMessage(_currentEventId, _currentSourceKind), _loadCts.Token);
    }

    private void OnEventUpdated(EventUpdatedMessage message)
    {
        if (message.PreviewEvent is not null ||
            _currentEventId is null ||
            IsEditMode)
        {
            return;
        }

        var matchesCurrentEvent =
            string.Equals(_currentEventId, message.EventId, StringComparison.Ordinal) ||
            string.Equals(_currentEventId, message.PreviousEventId, StringComparison.Ordinal);

        if (!matchesCurrentEvent)
        {
            return;
        }

        if (string.Equals(_currentEventId, message.PreviousEventId, StringComparison.Ordinal))
        {
            _currentEventId = message.EventId;
            _currentSourceKind = CalendarEventSourceKind.Google;
        }

        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadEventAsync(new EventSelectedMessage(_currentEventId, _currentSourceKind), _loadCts.Token);
    }

    private async Task ClosePanelAsync()
    {
        await HandleEscapeAsync();
    }

    private void HidePanel()
    {
        if (_isUneditedNewDraft && _currentEventId is not null)
        {
            var idToDelete = _currentEventId;
            _ = DeleteUneditedDraftAsync(idToDelete);
        }

        _currentEventId = null;
        _currentSourceKind = null;
        _currentEvent = null;
        IsPanelVisible = false;
        Title = string.Empty;
        StartEndDisplay = string.Empty;
        ColorHex = string.Empty;
        ColorName = string.Empty;
        DescriptionDisplay = string.Empty;
        IsPendingEvent = false;
        IsPendingDeleteEvent = false;
        SourceDisplay = DefaultSourceDisplay;
        LastSyncedDisplay = string.Empty;
        LastSavedLocallyDisplay = "No local changes";
        ResetEditSession();
    }

    private void ResetEditSession()
    {
        StopDebounce();
        _isUneditedNewDraft = false;
        _isInitializingEditFields = true;
        IsEditMode = false;
        EditTitle = string.Empty;
        EditStartDate = DateOnly.FromDateTime(DateTime.Today);
        EditStartTime = TimeOnly.MinValue;
        EditEndDate = DateOnly.FromDateTime(DateTime.Today);
        EditEndTime = TimeOnly.MinValue;
        EditDescription = string.Empty;
        ApplyEditColor("azure");
        _isInitializingEditFields = false;
        NotifyDateEditorShapeChanged();
        TitleError = string.Empty;
        DateTimeError = string.Empty;
        SaveStatusText = string.Empty;
        _hasPendingLocalChanges = false;
        _undoState = null;
    }

    private bool SetEditableProperty<T>(ref T storage, T value, string fieldName)
    {
        var previousValue = storage;
        if (!SetProperty(ref storage, value))
        {
            return false;
        }

        if (_isInitializingEditFields || !IsEditMode)
        {
            return true;
        }

        MarkEditedDraftCandidate(fieldName, value);
        HandleEditableChange(fieldName, previousValue);
        return true;
    }

    private bool AllowsBlankDraftTitle()
    {
        return _currentSourceKind == CalendarEventSourceKind.Pending &&
            _currentEvent?.IsPending == true;
    }

    private void MarkEditedDraftCandidate<T>(string fieldName, T value)
    {
        if (!_isUneditedNewDraft)
        {
            return;
        }

        if (string.Equals(fieldName, UndoFieldEditTitle, StringComparison.Ordinal) &&
            value is string title &&
            !string.IsNullOrWhiteSpace(title))
        {
            _isUneditedNewDraft = false;
            OnPropertyChanged(nameof(IsNewUneditedDraft));
        }
    }

    private void DeleteCurrentUneditedDraftIfChangingSelection(string nextEventId)
    {
        if (!_isUneditedNewDraft ||
            _currentEventId is null ||
            string.Equals(_currentEventId, nextEventId, StringComparison.Ordinal))
        {
            return;
        }

        var pendingEventId = _currentEventId;
        _isUneditedNewDraft = false;
        OnPropertyChanged(nameof(IsNewUneditedDraft));
        _ = DeleteUneditedDraftAsync(pendingEventId);
    }

    private void SetEditStartTime(TimeOnly value)
    {
        if (_editStartTime == value)
        {
            return;
        }

        var previousStartTime = _editStartTime;
        var previousEndDate = _editEndDate;
        var previousEndTime = _editEndTime;
        var originalDuration = TryGetCurrentDuration();

        if (!SetProperty(ref _editStartTime, value))
        {
            return;
        }

        if (_isInitializingEditFields || !IsEditMode)
        {
            return;
        }

        if (originalDuration.HasValue)
        {
            var shiftedEndLocal = DateTime.SpecifyKind(EditStartDate.ToDateTime(value), DateTimeKind.Local) + originalDuration.Value;

            _isInitializingEditFields = true;
            EditEndDate = DateOnly.FromDateTime(shiftedEndLocal);
            EditEndTime = TimeOnly.FromDateTime(shiftedEndLocal);
            _isInitializingEditFields = false;
            NotifyDateEditorShapeChanged();
        }

        _undoState = new UndoState(
            UndoFieldEditStartTime,
            new StartTimeUndoValue(previousStartTime, previousEndDate, previousEndTime));
        _hasPendingLocalChanges = true;
        TriggerValidationAndSaveState();
    }

    private void SetEditSingleDate(DateOnly value)
    {
        if (EditStartDate == value && EditEndDate == value)
        {
            return;
        }

        var previousValue = new SingleDateUndoValue(EditStartDate, EditEndDate);

        _isInitializingEditFields = true;
        EditStartDate = value;
        EditEndDate = value;
        _isInitializingEditFields = false;
        NotifyDateEditorShapeChanged();

        if (!IsEditMode)
        {
            return;
        }

        _undoState = new UndoState(UndoFieldEditSingleDate, previousValue);
        _hasPendingLocalChanges = true;
        TriggerValidationAndSaveState();
    }

    private void ApplyTimeRangeInternal(DateTime startLocal, DateTime endLocal)
    {
        _isInitializingEditFields = true;
        EditStartDate = DateOnly.FromDateTime(startLocal);
        EditStartTime = TimeOnly.FromDateTime(startLocal);
        EditEndDate = DateOnly.FromDateTime(endLocal);
        EditEndTime = TimeOnly.FromDateTime(endLocal);
        _isInitializingEditFields = false;
        NotifyDateEditorShapeChanged();
    }

    private void HandleEditableChange<T>(string fieldName, T previousValue)
    {
        TrackUndo(fieldName, previousValue);
        _hasPendingLocalChanges = true;
        TriggerValidationAndSaveState();
    }

    private void TrackUndo<T>(string fieldName, T previousValue)
    {
        if (_undoState is null || !string.Equals(_undoState.FieldName, fieldName, StringComparison.Ordinal) || !_hasPendingLocalChanges)
        {
            _undoState = new UndoState(fieldName, previousValue);
        }
    }

    private void TriggerValidationAndSaveState()
    {
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

    private TimeSpan? TryGetCurrentDuration()
    {
        var (_, _, startLocal, endLocal) = BuildEditDateTimes();
        return endLocal > startLocal ? endLocal - startLocal : null;
    }

    private void NotifyDateEditorShapeChanged()
    {
        OnPropertyChanged(nameof(EditSingleDate));
        OnPropertyChanged(nameof(UsesSingleDateEditor));
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

    private void PublishOptimisticEventUpdate(DateTime? startLocalOverride = null, DateTime? endLocalOverride = null)
    {
        if (_currentEvent is null || _currentEventId is null)
        {
            return;
        }

        var (_, _, editStartLocal, editEndLocal) = BuildEditDateTimes();
        var startLocalWithKind = DateTime.SpecifyKind(startLocalOverride ?? editStartLocal, DateTimeKind.Local);
        var endLocalWithKind = DateTime.SpecifyKind(endLocalOverride ?? editEndLocal, DateTimeKind.Local);
        var previewEvent = _currentEvent with
        {
            Title = string.IsNullOrWhiteSpace(EditTitle) ? _currentEvent.Title : EditTitle.Trim(),
            StartLocal = startLocalWithKind,
            EndLocal = endLocalWithKind,
            StartUtc = startLocalWithKind.ToUniversalTime(),
            EndUtc = endLocalWithKind.ToUniversalTime(),
            Description = NormalizeDescription(EditDescription),
            ColorHex = EditColorHex,
            ColorName = EditColorName,
            ColorKey = EditColorId,
            IsPending = true,
            Opacity = 0.6,
            PendingUpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            StatusLabel = _currentEvent.SourceKind == CalendarEventSourceKind.Pending
                ? DraftSourceDisplay
                : PendingSourceDisplay
        };

        _currentEvent = previewEvent;
        Title = previewEvent.Title;
        ColorHex = previewEvent.ColorHex;
        ColorName = previewEvent.ColorName;
        DescriptionDisplay = string.IsNullOrEmpty(previewEvent.Description)
            ? NoDescriptionPlaceholder
            : previewEvent.Description;
        IsPendingEvent = true;
        SourceDisplay = previewEvent.StatusLabel;
        StartEndDisplay = BuildStartEndDisplay(previewEvent.StartLocal, previewEvent.EndLocal, previewEvent.IsAllDay);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(_currentEventId, previewEvent));
    }

    private CalendarEventDisplayModel BuildReschedulePreviewEvent(
        CalendarEventDisplayModel currentDisplayEvent,
        DateTime startLocal,
        DateTime endLocal,
        DateTime utcNow)
    {
        var startLocalWithKind = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        var endLocalWithKind = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        return currentDisplayEvent with
        {
            StartLocal = startLocalWithKind,
            EndLocal = endLocalWithKind,
            StartUtc = startLocalWithKind.ToUniversalTime(),
            EndUtc = endLocalWithKind.ToUniversalTime(),
            IsPending = true,
            Opacity = 0.6,
            PendingUpdatedAt = utcNow,
            StatusLabel = currentDisplayEvent.SourceKind == CalendarEventSourceKind.Pending
                ? DraftSourceDisplay
                : PendingSourceDisplay
        };
    }

    private void ApplyCurrentEventAfterReschedule(
        string eventId,
        CalendarEventSourceKind sourceKind,
        CalendarEventDisplayModel previewEvent)
    {
        if (!IsCurrentEvent(eventId, sourceKind))
        {
            return;
        }

        _currentEvent = previewEvent;
        Title = previewEvent.Title;
        ColorHex = previewEvent.ColorHex;
        ColorName = previewEvent.ColorName;
        DescriptionDisplay = string.IsNullOrEmpty(previewEvent.Description)
            ? NoDescriptionPlaceholder
            : previewEvent.Description;
        IsPendingEvent = previewEvent.IsPending;
        IsPendingDeleteEvent = previewEvent.IsPendingDelete;
        SourceDisplay = previewEvent.StatusLabel;
        LastSavedLocallyDisplay = previewEvent.PendingUpdatedAt.HasValue
            ? previewEvent.PendingUpdatedAt.Value.ToLocalTime().ToString("g")
            : "No local changes";
        StartEndDisplay = BuildStartEndDisplay(previewEvent.StartLocal, previewEvent.EndLocal, previewEvent.IsAllDay);

        if (IsEditMode)
        {
            var previousValue = new TimeRangeUndoValue(EditStartDate, EditStartTime, EditEndDate, EditEndTime);
            ApplyTimeRangeInternal(previewEvent.StartLocal, previewEvent.EndLocal);
            _undoState = new UndoState(UndoFieldEditDraggedRange, previousValue);
            _hasPendingLocalChanges = false;
            SaveStatusText = SavedStatusText;
        }
    }

    private bool IsCurrentEvent(string eventId, CalendarEventSourceKind sourceKind)
    {
        return string.Equals(_currentEventId, eventId, StringComparison.Ordinal) &&
            _currentSourceKind == sourceKind;
    }

    private static PendingEvent ClonePendingEvent(PendingEvent source)
    {
        return new PendingEvent
        {
            PendingEventId = source.PendingEventId,
            GcalEventId = source.GcalEventId,
            CalendarId = source.CalendarId,
            Summary = source.Summary,
            Description = source.Description,
            StartDatetime = source.StartDatetime,
            EndDatetime = source.EndDatetime,
            IsAllDay = source.IsAllDay,
            ColorId = source.ColorId,
            AppCreated = source.AppCreated,
            SourceSystem = source.SourceSystem,
            ReadyToPublish = source.ReadyToPublish,
            PublishAttemptedAt = source.PublishAttemptedAt,
            PublishError = source.PublishError,
            OperationType = source.OperationType,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };
    }

    private void ApplyEditColor(string? colorId)
    {
        var normalizedColorKey = _colorMappingService.NormalizeColorKey(colorId);
        EditColorId = normalizedColorKey;
        EditColorName = _colorMappingService.GetDisplayName(normalizedColorKey);
        EditColorHex = _colorMappingService.GetHexColor(normalizedColorKey);
    }

    public async Task DeleteEventAsync(CancellationToken ct = default)
    {
        if (_currentEventId is null || _currentSourceKind is null || _currentEvent is null || _contentDialogService is null)
        {
            return;
        }

        if (_isPendingDeleteEvent)
        {
            await _contentDialogService.ShowMessageAsync(
                "Already Staged for Deletion",
                "This event is already staged for deletion and will be removed from Google Calendar when you push your changes.",
                "OK");
            return;
        }

        if (_currentSourceKind == CalendarEventSourceKind.Pending)
        {
            var confirmed = await _contentDialogService.ShowConfirmationAsync(
                "Delete Draft",
                "This local draft will be permanently deleted.",
                "Delete");
            if (!confirmed)
            {
                return;
            }

            var idToDelete = _currentEventId;
            StopDebounce();
            await _pendingEventRepository.DeleteByPendingEventIdAsync(idToDelete, ct);
            RunOnUiThread(HidePanel);
            _selectionService.ClearSelection();
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(idToDelete));
            return;
        }

        var pendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(_currentEventId, ct);

        if (pendingEvent is null)
        {
            var confirmed = await _contentDialogService.ShowConfirmationAsync(
                "Stage Deletion",
                "This event will be staged for deletion and removed from Google Calendar when you push your changes.",
                "Stage Delete");
            if (!confirmed)
            {
                return;
            }

            var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(_currentEventId, ct);
            if (gcalEvent is null)
            {
                return;
            }

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            var deletePending = new PendingEvent
            {
                PendingEventId = $"pending_{Guid.NewGuid():N}",
                GcalEventId = _currentEventId,
                CalendarId = gcalEvent.CalendarId,
                Summary = _currentEvent.Title,
                Description = _currentEvent.Description,
                StartDatetime = _currentEvent.StartUtc,
                EndDatetime = _currentEvent.EndUtc,
                IsAllDay = _currentEvent.IsAllDay,
                ColorId = _currentEvent.ColorKey,
                AppCreated = false,
                SourceSystem = gcalEvent.SourceSystem ?? "google-overlay",
                ReadyToPublish = false,
                OperationType = "delete",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            await _pendingEventRepository.UpsertAsync(deletePending, ct);
        }
        else if (pendingEvent.OperationType == "edit")
        {
            var choice = await _contentDialogService.ShowDeleteWithPendingEditAsync(_currentEvent.Title);
            switch (choice)
            {
                case DeleteWithPendingEditChoice.Cancel:
                    return;
                case DeleteWithPendingEditChoice.RevertChanges:
                    await RevertPendingChangesForEventAsync(_currentEventId, CalendarEventSourceKind.Google, ct);
                    return;
                case DeleteWithPendingEditChoice.DeleteEvent:
                    pendingEvent.OperationType = "delete";
                    pendingEvent.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
                    break;
            }
        }

        var eventId = _currentEventId;
        var refreshedEvent = await _queryService.GetEventByIdAsync(eventId, ct);
        if (refreshedEvent is not null)
        {
            RunOnUiThread(() => ApplyEventDetails(refreshedEvent, eventId, keepEditMode: false));
        }

        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
    }

    public async Task DeleteEventByIdAsync(string eventId, CalendarEventSourceKind sourceKind, CancellationToken ct = default)
    {
        if (_contentDialogService is null)
        {
            return;
        }

        var evt = await _queryService.GetEventByIdAsync(eventId, ct);
        if (evt is null)
        {
            return;
        }

        var isCurrentlySelected = string.Equals(_currentEventId, eventId, StringComparison.Ordinal);

        if (evt.IsPendingDelete)
        {
            await _contentDialogService.ShowMessageAsync(
                "Already Staged for Deletion",
                "This event is already staged for deletion and will be removed from Google Calendar when you push your changes.",
                "OK");
            return;
        }

        if (sourceKind == CalendarEventSourceKind.Pending)
        {
            var confirmed = await _contentDialogService.ShowConfirmationAsync(
                "Delete Draft",
                "This local draft will be permanently deleted.",
                "Delete");
            if (!confirmed)
            {
                return;
            }

            StopDebounce();
            await _pendingEventRepository.DeleteByPendingEventIdAsync(eventId, ct);
            if (isCurrentlySelected)
            {
                RunOnUiThread(HidePanel);
                _selectionService.ClearSelection();
            }

            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
            return;
        }

        var pendingEvent = await _pendingEventRepository.GetByGcalEventIdAsync(eventId, ct);

        if (pendingEvent is null)
        {
            var confirmed = await _contentDialogService.ShowConfirmationAsync(
                "Stage Deletion",
                "This event will be staged for deletion and removed from Google Calendar when you push your changes.",
                "Stage Delete");
            if (!confirmed)
            {
                return;
            }

            var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(eventId, ct);
            if (gcalEvent is null)
            {
                return;
            }

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            await _pendingEventRepository.UpsertAsync(new PendingEvent
            {
                PendingEventId = $"pending_{Guid.NewGuid():N}",
                GcalEventId = eventId,
                CalendarId = gcalEvent.CalendarId,
                Summary = evt.Title,
                Description = evt.Description,
                StartDatetime = evt.StartUtc,
                EndDatetime = evt.EndUtc,
                IsAllDay = evt.IsAllDay,
                ColorId = evt.ColorKey,
                AppCreated = false,
                SourceSystem = gcalEvent.SourceSystem ?? "google-overlay",
                ReadyToPublish = false,
                OperationType = "delete",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, ct);
        }
        else if (pendingEvent.OperationType == "edit")
        {
            var choice = await _contentDialogService.ShowDeleteWithPendingEditAsync(evt.Title);
            switch (choice)
            {
                case DeleteWithPendingEditChoice.Cancel:
                    return;
                case DeleteWithPendingEditChoice.RevertChanges:
                    await RevertPendingChangesForEventAsync(eventId, CalendarEventSourceKind.Google, ct);
                    return;
                case DeleteWithPendingEditChoice.DeleteEvent:
                    pendingEvent.OperationType = "delete";
                    pendingEvent.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await _pendingEventRepository.UpsertAsync(pendingEvent, ct);
                    break;
            }
        }

        if (isCurrentlySelected)
        {
            var refreshedEvent = await _queryService.GetEventByIdAsync(eventId, ct);
            if (refreshedEvent is not null)
            {
                RunOnUiThread(() => ApplyEventDetails(refreshedEvent, eventId, keepEditMode: false));
            }
        }

        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId));
    }

    private async Task DeleteUneditedDraftAsync(string pendingEventId)
    {
        await _pendingEventRepository.DeleteByPendingEventIdAsync(pendingEventId);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(pendingEventId));
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
    private sealed record StartTimeUndoValue(TimeOnly StartTime, DateOnly EndDate, TimeOnly EndTime);
    private sealed record SingleDateUndoValue(DateOnly StartDate, DateOnly EndDate);
    private sealed record TimeRangeUndoValue(DateOnly StartDate, TimeOnly StartTime, DateOnly EndDate, TimeOnly EndTime);
    private sealed record EndTimeUndoValue(DateOnly EndDate, TimeOnly EndTime);
    private sealed record DragRescheduleUndoState(
        string EventId,
        CalendarEventSourceKind SourceKind,
        PendingEvent? PreviousPendingEvent);
}

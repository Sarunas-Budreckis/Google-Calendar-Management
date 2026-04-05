using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class EventDetailsPanelViewModel : ObservableObject
{
    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarSelectionService _selectionService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private CancellationTokenSource _loadCts = new();

    private string? _currentGcalEventId;
    private bool _isPanelVisible;
    private string _title = string.Empty;
    private string _startEndDisplay = string.Empty;
    private string _colorHex = string.Empty;
    private string _colorName = string.Empty;
    private string _descriptionDisplay = string.Empty;
    private string _sourceDisplay = "From Google Calendar";
    private string _lastSyncedDisplay = string.Empty;

    public EventDetailsPanelViewModel(
        ICalendarQueryService queryService,
        ICalendarSelectionService selectionService)
    {
        _queryService = queryService;
        _selectionService = selectionService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        CloseCommand = new RelayCommand(() => _selectionService.ClearSelection());

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
                OnPropertyChanged(nameof(PanelVisibility));
        }
    }

    public Visibility PanelVisibility =>
        _isPanelVisible ? Visibility.Visible : Visibility.Collapsed;

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
        private set => SetProperty(ref _colorName, value);
    }

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

    public IRelayCommand CloseCommand { get; }

    private void OnEventSelected(EventSelectedMessage message)
    {
        // Cancel any in-flight load so a stale result cannot overwrite the latest selection.
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
            return;

        if (evt is null)
        {
            RunOnUiThread(HidePanel);
            return;
        }

        // evt is non-null here; use null-forgiving to give the closure a non-nullable type,
        // since the async state machine prevents the compiler from narrowing across the closure boundary.
        var loaded = evt!;
        RunOnUiThread(() =>
        {
            if (ct.IsCancellationRequested)
                return;

            _currentGcalEventId = gcalEventId;
            Title = loaded.Title;
            ColorHex = loaded.ColorHex;
            ColorName = loaded.ColorName;
            DescriptionDisplay = string.IsNullOrEmpty(loaded.Description)
                ? "No description provided."
                : loaded.Description;
            SourceDisplay = "From Google Calendar";
            LastSyncedDisplay = loaded.LastSyncedAt.HasValue
                ? loaded.LastSyncedAt.Value.ToLocalTime().ToString("g")
                : "Never";
            StartEndDisplay = BuildStartEndDisplay(loaded.StartLocal, loaded.EndLocal, loaded.IsAllDay);
            IsPanelVisible = true;
        });
    }

    private static string BuildStartEndDisplay(DateTime startLocal, DateTime endLocal, bool isAllDay)
    {
        if (isAllDay)
        {
            // Google stores all-day end as exclusive midnight of the NEXT day.
            // Subtract 1 day to get the true last day of the event.
            var displayEnd = endLocal.AddDays(-1);
            return startLocal.Date == displayEnd.Date
                ? $"{startLocal:ddd, MMM d, yyyy} (All day)"
                : $"{startLocal:ddd, MMM d} \u2013 {displayEnd:ddd, MMM d, yyyy}";
        }

        return $"{startLocal:ddd, MMM d, yyyy, h:mm tt} \u2013 {endLocal:h:mm tt}";
    }

    private void OnSyncCompleted()
    {
        if (_currentGcalEventId is null)
            return;

        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        _ = LoadEventAsync(_currentGcalEventId, _loadCts.Token);
    }

    private void HidePanel()
    {
        _currentGcalEventId = null;
        IsPanelVisible = false;
        Title = string.Empty;
        StartEndDisplay = string.Empty;
        ColorHex = string.Empty;
        ColorName = string.Empty;
        DescriptionDisplay = string.Empty;
        LastSyncedDisplay = string.Empty;
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
}

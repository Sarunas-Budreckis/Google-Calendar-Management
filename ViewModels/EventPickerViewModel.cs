using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Dispatching;

namespace GoogleCalendarManagement.ViewModels;

public sealed class EventPickerGroup : ObservableCollection<EventPickerItem>
{
    public string Key { get; }
    public EventPickerGroup(string key, IEnumerable<EventPickerItem> items) : base(items) => Key = key;
}

public sealed class EventPickerViewModel : ObservableObject
{
    private readonly IEventPickerService _pickerService;
    private readonly ILinkService _linkService;
    private readonly DateTimeOffset _rangeStart;
    private readonly DateTimeOffset _rangeEnd;
    private readonly IReadOnlyList<int> _dataPointIds;
    private readonly DispatcherQueue? _dispatcherQueue;

    private CancellationTokenSource _searchCts = new();
    private int _loadVersion;
    private string _searchText = string.Empty;
    private EventPickerItem? _selectedItem;
    private bool _isEmpty = true;
    private string? _errorMessage;

    public EventPickerViewModel(
        IEventPickerService pickerService,
        ILinkService linkService,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        IReadOnlyList<int> dataPointIds,
        DispatcherQueue? dispatcherQueue = null)
    {
        _pickerService = pickerService;
        _linkService = linkService;
        _rangeStart = rangeStart;
        _rangeEnd = rangeEnd;
        _dataPointIds = dataPointIds;
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        ConfirmLinkCommand = new AsyncRelayCommand(ExecuteConfirmLinkAsync, () => SelectedItem != null);

        _ = LoadAsync(null, _searchCts.Token);
    }

    public ObservableCollection<EventPickerItem> ConcurrentEvents { get; } = [];
    public ObservableCollection<EventPickerItem> OtherEvents { get; } = [];
    public ObservableCollection<EventPickerGroup> Groups { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
                return;

            var cts = new CancellationTokenSource();
            var old = Interlocked.Exchange(ref _searchCts, cts);
            old.Cancel();
            old.Dispose();

            _ = DebounceSearchAsync(value, cts.Token);
        }
    }

    public EventPickerItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SetProperty(ref _selectedItem, value);
            ConfirmLinkCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            SetProperty(ref _errorMessage, value);
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => _errorMessage != null;

    public IAsyncRelayCommand ConfirmLinkCommand { get; }

    public async Task LoadAsync(string? searchText)
    {
        await LoadAsync(searchText, CancellationToken.None);
    }

    private async Task LoadAsync(string? searchText, CancellationToken ct)
    {
        var version = Interlocked.Increment(ref _loadVersion);

        try
        {
            var result = await _pickerService.GetCandidatesAsync(_rangeStart, _rangeEnd, searchText, ct);
            if (ct.IsCancellationRequested || version != Volatile.Read(ref _loadVersion))
                return;

            UpdateCollections(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version != Volatile.Read(ref _loadVersion))
                return;

            ErrorMessage = ex.Message;
        }
    }

    private void UpdateCollections(EventPickerResult result)
    {
        void Apply()
        {
            ConcurrentEvents.Clear();
            foreach (var item in result.ConcurrentEvents)
                ConcurrentEvents.Add(item);

            OtherEvents.Clear();
            foreach (var item in result.OtherEvents)
                OtherEvents.Add(item);

            Groups.Clear();
            if (result.ConcurrentEvents.Count > 0)
                Groups.Add(new EventPickerGroup("Concurrent events", result.ConcurrentEvents));
            if (result.OtherEvents.Count > 0)
                Groups.Add(new EventPickerGroup("Other events", result.OtherEvents));

            IsEmpty = ConcurrentEvents.Count == 0 && OtherEvents.Count == 0;
        }

        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
            Apply();
        else
            _dispatcherQueue.TryEnqueue(Apply);
    }

    private async Task DebounceSearchAsync(string searchText, CancellationToken ct)
    {
        try
        {
            await Task.Delay(300, ct);
            await LoadAsync(searchText, ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ExecuteConfirmLinkAsync()
    {
        if (SelectedItem is null)
            return;

        ErrorMessage = null;

        try
        {
            if (_dataPointIds.Count == 1)
                await _linkService.LinkAsync(_dataPointIds[0], SelectedItem.EventId);
            else
                await _linkService.LinkClumpAsync(_dataPointIds, SelectedItem.EventId);

            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(SelectedItem.EventId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
    }
}

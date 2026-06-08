using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GoogleCalendarManagement.ViewModels;

public sealed class ComfyUIDrilldownViewModel : ObservableObject
{
    private readonly IComfyUIRepository _repository;
    private readonly ComfyUIImportHandler _importHandler;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly IWindowService _windowService;
    private readonly TimeProvider _timeProvider;

    private readonly List<ComfyUIScanPoint> _points = [];
    private DateOnly _currentDate;
    private bool _hasPoints;
    private bool _isScanning;
    private string _scanStatusMessage = "";

    public ComfyUIDrilldownViewModel(
        IComfyUIRepository repository,
        ComfyUIImportHandler importHandler,
        IPendingEventDraftService pendingEventDraftService,
        IPendingEventRepository pendingEventRepository,
        ICalendarSelectionService calendarSelectionService,
        IWindowService windowService,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _importHandler = importHandler;
        _pendingEventDraftService = pendingEventDraftService;
        _pendingEventRepository = pendingEventRepository;
        _calendarSelectionService = calendarSelectionService;
        _windowService = windowService;
        _timeProvider = timeProvider ?? TimeProvider.System;

        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync, () => !IsScanning);
        ScanFoldersCommand = new AsyncRelayCommand(ImportAsync, () => !IsScanning);
        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasPoints && !IsScanning);
    }

    public ObservableCollection<ComfyUIFolderItemViewModel> Folders { get; } = [];
    public ObservableCollection<ComfyUIScanPointViewModel> ScanPoints { get; } = [];

    public bool HasPoints
    {
        get => _hasPoints;
        private set
        {
            if (SetProperty(ref _hasPoints, value))
            {
                OnPropertyChanged(nameof(PointsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                AddFolderCommand.NotifyCanExecuteChanged();
                ScanFoldersCommand.NotifyCanExecuteChanged();
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string ScanStatusMessage
    {
        get => _scanStatusMessage;
        private set
        {
            if (SetProperty(ref _scanStatusMessage, value))
            {
                OnPropertyChanged(nameof(ScanStatusVisibility));
            }
        }
    }

    public Visibility ScanStatusVisibility => string.IsNullOrEmpty(ScanStatusMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PointsVisibility => HasPoints ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasPoints ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand AddFolderCommand { get; }
    public IAsyncRelayCommand ScanFoldersCommand { get; }
    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        await RefreshFoldersAsync(ct);
        await RefreshPointsAsync(ct);
    }

    private async Task RefreshFoldersAsync(CancellationToken ct = default)
    {
        var folders = await _repository.GetActiveFoldersAsync(ct);
        Folders.Clear();
        foreach (var f in folders)
        {
            Folders.Add(new ComfyUIFolderItemViewModel(f.Id, f.FolderPath, RemoveFolderAsync));
        }
    }

    private async Task RefreshPointsAsync(CancellationToken ct = default)
    {
        var points = await _repository.GetPointsForDateAsync(_currentDate, ct);
        _points.Clear();
        _points.AddRange(points);

        ScanPoints.Clear();
        foreach (var p in points)
        {
            ScanPoints.Add(new ComfyUIScanPointViewModel(p));
        }

        HasPoints = ScanPoints.Count > 0;
        ScanStatusMessage = "";
    }

    private async Task AddFolderAsync()
    {
        var window = _windowService.GetWindow();
        if (window is null)
        {
            return;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        Windows.Storage.StorageFolder? folder;
        try
        {
            folder = await picker.PickSingleFolderAsync();
        }
        catch (COMException)
        {
            return;
        }

        if (folder is null)
        {
            return;
        }

        await _repository.AddFolderAsync(folder.Path, _timeProvider.GetUtcNow().UtcDateTime);
        await RefreshFoldersAsync();
    }

    private async Task RemoveFolderAsync(int folderId)
    {
        await _repository.DeactivateFolderAsync(folderId);
        await RefreshFoldersAsync();
    }

    private async Task ImportAsync()
    {
        IsScanning = true;
        ScanStatusMessage = "Importing…";
        try
        {
            await _importHandler.TriggerImportAsync();
            ScanStatusMessage = "";
            await RefreshPointsAsync();
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task CreateCandidateEventsAsync()
    {
        if (_points.Count == 0)
        {
            return;
        }

        var windows = ComfyUISessionCoalescer.CoalesceIntoWindows(_points);
        string? firstDraftId = null;

        foreach (var window in windows)
        {
            var startLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
                NormalizeUtc(window.WindowStart).ToLocalTime());
            var endLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
                NormalizeUtc(window.WindowEnd).ToLocalTime());

            if (endLocal <= startLocal)
            {
                endLocal = startLocal.AddMinutes(15);
            }

            var draft = await _pendingEventDraftService.CreateDraftAsync(startLocal, endLocal, "ComfyUI");
            draft.Summary = "ComfyUI";
            draft.IsAllDay = false;
            draft.SourceSystem = ComfyUIFolderScannerService.SourceKey;
            draft.ColorId = "navy";
            await _pendingEventRepository.UpsertAsync(draft);
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.PendingEventId));
            firstDraftId ??= draft.PendingEventId;
        }

        if (firstDraftId is not null)
        {
            _calendarSelectionService.Select(firstDraftId, CalendarEventSourceKind.Pending, openInEditMode: true);
        }
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

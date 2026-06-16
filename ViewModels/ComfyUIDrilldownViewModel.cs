using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
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
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly IWindowService _windowService;
    private readonly TimeProvider _timeProvider;
    private readonly IDataPointReconciliationSweepService _sweepService;

    private readonly List<ComfyUIScanPoint> _points = [];
    private DateOnly _currentDate;
    private bool _hasPoints;
    private bool _isScanning;
    private string _scanStatusMessage = "";

    public ComfyUIDrilldownViewModel(
        IComfyUIRepository repository,
        ComfyUIImportHandler importHandler,
        IPendingEventDraftService pendingEventDraftService,
        ICalendarSelectionService calendarSelectionService,
        IWindowService windowService,
        IDataPointReconciliationSweepService sweepService,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _importHandler = importHandler;
        _pendingEventDraftService = pendingEventDraftService;
        _calendarSelectionService = calendarSelectionService;
        _windowService = windowService;
        _sweepService = sweepService;
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
        await _sweepService.RunPostImportAsync(ComfyUIFolderScannerService.SourceKey);
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

            var candidate = await _pendingEventDraftService.CreateCandidateAsync(
                startLocal,
                endLocal,
                "ComfyUI",
                sourceSystem: ComfyUIFolderScannerService.SourceKey,
                colorId: "navy");
            firstDraftId ??= candidate.EventId;
        }

        if (firstDraftId is not null)
        {
            _calendarSelectionService.Select(firstDraftId, CalendarEventSourceKind.Candidate, openInEditMode: true);
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

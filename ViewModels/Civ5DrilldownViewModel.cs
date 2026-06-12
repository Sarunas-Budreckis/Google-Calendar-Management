using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class Civ5DrilldownViewModel : ObservableObject
{
    private readonly ICiv5SessionRepository _repository;
    private readonly ICiv5SaveScannerService _scannerService;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly List<Civ5SessionPoint> _points = [];
    private DateOnly _currentDate;
    private bool _hasPoints;
    private bool _isScanning;
    private string _scanStatusMessage = "";

    public Civ5DrilldownViewModel(
        ICiv5SessionRepository repository,
        ICiv5SaveScannerService scannerService,
        IPendingEventDraftService pendingEventDraftService,
        ICalendarSelectionService calendarSelectionService)
    {
        _repository = repository;
        _scannerService = scannerService;
        _pendingEventDraftService = pendingEventDraftService;
        _calendarSelectionService = calendarSelectionService;

        ScanSavesCommand = new AsyncRelayCommand(ScanSavesAsync, () => !IsScanning);
        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasPoints && !IsScanning);
    }

    public ObservableCollection<Civ5SessionPointViewModel> SessionPoints { get; } = [];

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
                ScanSavesCommand.NotifyCanExecuteChanged();
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

    public IAsyncRelayCommand ScanSavesCommand { get; }
    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        await RefreshPointsAsync(ct);
    }

    private async Task RefreshPointsAsync(CancellationToken ct = default)
    {
        var points = await _repository.GetPointsForDateAsync(_currentDate, ct);
        _points.Clear();
        _points.AddRange(points);

        SessionPoints.Clear();
        foreach (var p in points)
        {
            SessionPoints.Add(new Civ5SessionPointViewModel(p));
        }

        HasPoints = SessionPoints.Count > 0;
        ScanStatusMessage = "";
    }

    private async Task ScanSavesAsync()
    {
        IsScanning = true;
        ScanStatusMessage = "Scanning…";
        try
        {
            var result = await _scannerService.ScanAsync();
            ScanStatusMessage = result.Success
                ? $"Scan complete — {result.NewPointsAdded} new point{(result.NewPointsAdded == 1 ? "" : "s")} added"
                : $"Scan failed: {result.ErrorMessage}";
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

        var windows = Civ5SessionCoalescer.CoalesceIntoWindows(_points);
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

            var title = Civ5SessionCoalescer.GetEventTitle(window);
            var candidate = await _pendingEventDraftService.CreateCandidateAsync(
                startLocal,
                endLocal,
                title,
                sourceSystem: "civ5",
                colorId: "yellow");
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

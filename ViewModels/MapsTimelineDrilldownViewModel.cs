using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MapsTimelineDrilldownViewModel : ObservableObject
{
    private readonly IMapsTimelineRepository _repository;
    private readonly MapsTimelineParser _parser;
    private readonly MapsTimelineImportHandler _importHandler;
    private MapsTimelineRaw? _currentRecord;
    private bool _hasSegments;
    private bool _hasTimeline;
    private string _emptyMessage = "No data for this day.";

    public MapsTimelineDrilldownViewModel(
        IMapsTimelineRepository repository,
        MapsTimelineParser parser,
        MapsTimelineImportHandler importHandler)
    {
        _repository = repository;
        _parser = parser;
        _importHandler = importHandler;
        CopyToViewerCommand = new AsyncRelayCommand(CopyToViewerAsync, () => HasTimeline);
    }

    public ObservableCollection<MapsTimelineSegmentViewModel> Segments { get; } = [];

    public bool HasSegments
    {
        get => _hasSegments;
        private set
        {
            if (SetProperty(ref _hasSegments, value))
            {
                OnPropertyChanged(nameof(SegmentsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool HasTimeline
    {
        get => _hasTimeline;
        private set
        {
            if (SetProperty(ref _hasTimeline, value))
            {
                OnPropertyChanged(nameof(CopyButtonVisibility));
                CopyToViewerCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    public Visibility SegmentsVisibility => HasSegments ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasSegments ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CopyButtonVisibility => HasTimeline ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand CopyToViewerCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentRecord = await _repository.GetLatestAsync(ct);
        HasTimeline = _currentRecord is not null;

        Segments.Clear();
        HasSegments = false;

        if (_currentRecord is null)
        {
            EmptyMessage = "No timeline imported yet.";
            return;
        }

        var coversDay = _currentRecord.CoveredDateMin <= date && date <= _currentRecord.CoveredDateMax;
        if (!coversDay)
        {
            EmptyMessage = "No data for this day.";
            return;
        }

        var segments = _parser.GetSegmentsForDate(_currentRecord.RawJson, date);
        foreach (var seg in segments)
        {
            Segments.Add(new MapsTimelineSegmentViewModel(seg));
        }

        HasSegments = Segments.Count > 0;
        EmptyMessage = HasSegments ? "" : "Timeline covers this day but no segments were parsed.";
    }

    private async Task CopyToViewerAsync()
    {
        if (_currentRecord is null)
        {
            return;
        }

        await _importHandler.CopyToViewerAndOpenAsync(_currentRecord);
    }
}

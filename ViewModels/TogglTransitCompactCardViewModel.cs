using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglTransitCompactCardViewModel : ObservableObject
{
    private readonly ITogglTransitRepository _repository;
    private string _totalDurationLabel = "";
    private string _tripCountLabel = "";
    private bool _hasEntries;
    private bool _hasNoEntries = true;

    public TogglTransitCompactCardViewModel(ITogglTransitRepository repository)
    {
        _repository = repository;
    }

    public string TotalDurationLabel
    {
        get => _totalDurationLabel;
        private set => SetProperty(ref _totalDurationLabel, value);
    }

    public string TripCountLabel
    {
        get => _tripCountLabel;
        private set => SetProperty(ref _tripCountLabel, value);
    }

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(EntriesVisibility));
            }
        }
    }

    public bool HasNoEntries
    {
        get => _hasNoEntries;
        private set
        {
            if (SetProperty(ref _hasNoEntries, value))
            {
                OnPropertyChanged(nameof(NoEntriesVisibility));
            }
        }
    }

    public Visibility EntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoEntriesVisibility => HasNoEntries ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetTransitEntriesForDateAsync(date, ct);

        HasEntries = entries.Count > 0;
        HasNoEntries = entries.Count == 0;

        if (entries.Count == 0)
        {
            TotalDurationLabel = "";
            TripCountLabel = "";
            return;
        }

        var totalSeconds = entries.Sum(e =>
        {
            if (e.EndTime is { } end)
            {
                return (int)(end - e.StartTime).TotalSeconds;
            }

            return Math.Max(0, e.DurationSeconds ?? 0);
        });

        var total = TimeSpan.FromSeconds(totalSeconds);
        var totalHours = (int)total.TotalHours;
        TotalDurationLabel = totalHours > 0
            ? $"{totalHours}h {total.Minutes}m total"
            : $"{total.Minutes}m total";

        TripCountLabel = entries.Count == 1 ? "1 trip" : $"{entries.Count} trips";
    }
}

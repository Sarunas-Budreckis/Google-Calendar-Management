using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglPhoneCompactCardViewModel : ObservableObject
{
    private readonly ITogglPhoneRepository _repository;
    private string _summaryLabel = "";
    private bool _hasEntries;
    private bool _hasNoEntries = true;

    public TogglPhoneCompactCardViewModel(ITogglPhoneRepository repository)
    {
        _repository = repository;
    }

    public string SummaryLabel
    {
        get => _summaryLabel;
        private set => SetProperty(ref _summaryLabel, value);
    }

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(HasEntriesVisibility));
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

    public Visibility HasEntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoEntriesVisibility => HasNoEntries ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetPhoneEntriesForDateAsync(date, ct);

        HasEntries = entries.Count > 0;
        HasNoEntries = entries.Count == 0;

        if (entries.Count > 0)
        {
            var totalSeconds = entries.Sum(e => e.DurationSeconds ?? 0);
            var totalMinutes = totalSeconds / 60;
            var durationLabel = totalMinutes >= 60
                ? $"{totalMinutes / 60}h {totalMinutes % 60}m"
                : $"{totalMinutes}m";
            SummaryLabel = $"{entries.Count} {(entries.Count == 1 ? "entry" : "entries")} · {durationLabel}";
        }
        else
        {
            SummaryLabel = "";
        }
    }
}

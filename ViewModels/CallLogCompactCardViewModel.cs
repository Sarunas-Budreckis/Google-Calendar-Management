using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class CallLogCompactCardViewModel : ObservableObject
{
    private readonly ICallLogRepository _repository;
    private string _summaryLabel = "";
    private bool _hasData;

    public CallLogCompactCardViewModel(ICallLogRepository repository)
    {
        _repository = repository;
    }

    public string SummaryLabel
    {
        get => _summaryLabel;
        private set => SetProperty(ref _summaryLabel, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (SetProperty(ref _hasData, value))
            {
                OnPropertyChanged(nameof(DataVisibility));
                OnPropertyChanged(nameof(NoDataVisibility));
            }
        }
    }

    public Visibility DataVisibility => HasData ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoDataVisibility => HasData ? Visibility.Collapsed : Visibility.Visible;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetEntriesForDateAsync(date, ct);

        if (entries.Count == 0)
        {
            HasData = false;
            SummaryLabel = "";
            return;
        }

        HasData = true;
        var totalSeconds = entries.Sum(e => e.DurationSeconds);
        var callWord = entries.Count == 1 ? "call" : "calls";
        SummaryLabel = $"{entries.Count} {callWord} · {FormatTotalDuration(totalSeconds)}";
    }

    private static string FormatTotalDuration(int totalSeconds)
    {
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;

        if (hours > 0 && minutes > 0)
        {
            return $"{hours} hr {minutes} min";
        }

        if (hours > 0)
        {
            return $"{hours} hr";
        }

        return $"{minutes} min";
    }
}

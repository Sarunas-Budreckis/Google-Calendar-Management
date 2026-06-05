using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class SpotifyCompactCardViewModel : ObservableObject
{
    private readonly ISpotifyStreamRepository _repository;
    private string _listeningTimeLabel = "";
    private string _trackCountLabel = "";
    private Visibility _dataVisibility = Visibility.Collapsed;
    private Visibility _noDataVisibility = Visibility.Visible;

    public SpotifyCompactCardViewModel(ISpotifyStreamRepository repository)
    {
        _repository = repository;
    }

    public string ListeningTimeLabel
    {
        get => _listeningTimeLabel;
        private set => SetProperty(ref _listeningTimeLabel, value);
    }

    public string TrackCountLabel
    {
        get => _trackCountLabel;
        private set => SetProperty(ref _trackCountLabel, value);
    }

    public Visibility DataVisibility
    {
        get => _dataVisibility;
        private set => SetProperty(ref _dataVisibility, value);
    }

    public Visibility NoDataVisibility
    {
        get => _noDataVisibility;
        private set => SetProperty(ref _noDataVisibility, value);
    }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var streams = await _repository.GetStreamsForDateAsync(date, ct);

        if (streams.Count == 0)
        {
            DataVisibility = Visibility.Collapsed;
            NoDataVisibility = Visibility.Visible;
            return;
        }

        var totalMs = streams.Sum(s => (long)s.MsPlayed);
        var totalHours = (int)(totalMs / 3_600_000);
        var totalMinutes = (int)(totalMs % 3_600_000 / 60_000);

        ListeningTimeLabel = totalHours > 0
            ? $"{totalHours}h {totalMinutes}m"
            : $"{totalMinutes}m";
        TrackCountLabel = streams.Count == 1 ? "1 track" : $"{streams.Count} tracks";

        DataVisibility = Visibility.Visible;
        NoDataVisibility = Visibility.Collapsed;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class SpotifyDrilldownViewModel : ObservableObject
{
    private readonly ISpotifyStreamRepository _repository;
    private IReadOnlyList<VerticalDotItem> _dotItems = [];
    private string _summaryLabel = "";
    private Visibility _dataVisibility = Visibility.Collapsed;
    private Visibility _emptyStateVisibility = Visibility.Visible;

    public SpotifyDrilldownViewModel(ISpotifyStreamRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<VerticalDotItem> DotItems
    {
        get => _dotItems;
        private set => SetProperty(ref _dotItems, value);
    }

    public string SummaryLabel
    {
        get => _summaryLabel;
        private set => SetProperty(ref _summaryLabel, value);
    }

    public Visibility DataVisibility
    {
        get => _dataVisibility;
        private set => SetProperty(ref _dataVisibility, value);
    }

    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        private set => SetProperty(ref _emptyStateVisibility, value);
    }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var streams = await _repository.GetStreamsForDateAsync(date, ct);

        if (streams.Count == 0)
        {
            DotItems = [];
            DataVisibility = Visibility.Collapsed;
            EmptyStateVisibility = Visibility.Visible;
            return;
        }

        var totalMs = streams.Sum(s => (long)s.MsPlayed);
        var totalHours = (int)(totalMs / 3_600_000);
        var totalMinutes = (int)(totalMs % 3_600_000 / 60_000);
        var timeLabel = totalHours > 0 ? $"{totalHours}h {totalMinutes}m" : $"{totalMinutes}m";
        SummaryLabel = $"{streams.Count} tracks · {timeLabel} listened";

        DotItems = streams.Select(s =>
        {
            var localTime = s.PlayedAt.Kind == DateTimeKind.Utc ? s.PlayedAt.ToLocalTime() : s.PlayedAt;
            var timeStr = localTime.ToString("HH:mm");
            var durationSec = s.DurationMs / 1000;
            var playedSec = s.MsPlayed / 1000;
            var durationLabel = durationSec >= 60
                ? $"{durationSec / 60}:{durationSec % 60:D2}"
                : $"0:{durationSec:D2}";
            var playedLabel = playedSec >= 60
                ? $"{playedSec / 60}:{playedSec % 60:D2}"
                : $"0:{playedSec:D2}";
            var isPartial = s.MsPlayed < s.DurationMs;
            var tertiaryLabel = isPartial
                ? $"Played {playedLabel} / {durationLabel} (skipped)"
                : $"Duration: {durationLabel}";

            return new VerticalDotItem(
                Timestamp: s.PlayedAt,
                PrimaryLabel: $"{timeStr} — {s.TrackName}",
                SecondaryLabel: string.IsNullOrEmpty(s.ArtistName) ? null : s.ArtistName + (s.AlbumName is null ? "" : $" · {s.AlbumName}"),
                TertiaryLabel: tertiaryLabel,
                IsPartial: isPartial);
        }).ToList();

        DataVisibility = Visibility.Visible;
        EmptyStateVisibility = Visibility.Collapsed;
    }
}

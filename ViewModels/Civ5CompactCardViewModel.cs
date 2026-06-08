using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class Civ5CompactCardViewModel : ObservableObject
{
    private readonly ICiv5SessionRepository _repository;
    private string _summaryLabel = "";
    private bool _hasData;
    private bool _hasNoData = true;

    public Civ5CompactCardViewModel(ICiv5SessionRepository repository)
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
            }
        }
    }

    public bool HasNoData
    {
        get => _hasNoData;
        private set
        {
            if (SetProperty(ref _hasNoData, value))
            {
                OnPropertyChanged(nameof(NoDataVisibility));
            }
        }
    }

    public Visibility DataVisibility => HasData ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoDataVisibility => HasNoData ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var points = await _repository.GetPointsForDateAsync(date, ct);

        HasData = points.Count > 0;
        HasNoData = points.Count == 0;

        if (points.Count == 0)
        {
            SummaryLabel = "";
            return;
        }

        var modeGroups = points
            .GroupBy(p => p.GameMode)
            .OrderByDescending(g => g.Count())
            .ToList();

        var total = points.Count;
        if (modeGroups.Count == 1)
        {
            var mode = modeGroups[0].Key;
            SummaryLabel = $"{total} save{(total == 1 ? "" : "s")} ({FormatMode(mode)})";
        }
        else
        {
            SummaryLabel = $"{total} saves (multiple types)";
        }
    }

    private static string FormatMode(string gameMode) => gameMode switch
    {
        "single" => "single-player",
        "multi" => "multiplayer",
        "hotseat" => "hotseat",
        "pbem" => "play-by-email",
        "pitboss" => "Pitboss",
        _ => gameMode
    };
}

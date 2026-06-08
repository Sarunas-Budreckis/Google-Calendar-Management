using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class ComfyUICompactCardViewModel : ObservableObject
{
    private readonly IComfyUIRepository _repository;
    private string _summaryLabel = "";
    private bool _hasData;
    private bool _hasNoData = true;

    public ComfyUICompactCardViewModel(IComfyUIRepository repository)
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
        var counts = await _repository.GetCreatedEventCountsForRangeAsync(date, date, ct);
        counts.TryGetValue(date, out var count);

        HasData = count > 0;
        HasNoData = count == 0;

        if (count == 0)
        {
            SummaryLabel = "";
            return;
        }

        SummaryLabel = $"{count} created file event{(count == 1 ? "" : "s")}";
    }
}

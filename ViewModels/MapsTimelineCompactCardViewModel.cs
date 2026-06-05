using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MapsTimelineCompactCardViewModel : ObservableObject
{
    private readonly IMapsTimelineRepository _repository;
    private string _statusLabel = "";
    private bool _hasData;

    public MapsTimelineCompactCardViewModel(IMapsTimelineRepository repository)
    {
        _repository = repository;
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetProperty(ref _statusLabel, value);
    }

    public bool HasData
    {
        get => _hasData;
        private set
        {
            if (SetProperty(ref _hasData, value))
            {
                OnPropertyChanged(nameof(HasDataVisibility));
                OnPropertyChanged(nameof(NoDataVisibility));
            }
        }
    }

    public Visibility HasDataVisibility => HasData ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoDataVisibility => HasData ? Visibility.Collapsed : Visibility.Visible;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var record = await _repository.GetLatestAsync(ct);
        if (record is null)
        {
            HasData = false;
            StatusLabel = "";
            return;
        }

        var coversDay = record.CoveredDateMin <= date && date <= record.CoveredDateMax;
        HasData = coversDay;
        StatusLabel = coversDay
            ? $"Imported {record.ImportedAt.ToLocalTime():d}"
            : "";
    }
}

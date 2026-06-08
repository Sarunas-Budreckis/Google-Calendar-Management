using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MapsTimelineCompactCardViewModel : ObservableObject
{
    private readonly MapsTimelineCardProvider _cardProvider;
    private string _statusLabel = "";
    private bool _hasData;

    public MapsTimelineCompactCardViewModel(MapsTimelineCardProvider cardProvider)
    {
        _cardProvider = cardProvider;
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
        var (coversDay, importedAt) = await _cardProvider.GetDayMetadataAsync(date, ct);
        HasData = coversDay;
        StatusLabel = coversDay && importedAt.HasValue
            ? $"Imported {importedAt.Value.ToLocalTime():d}"
            : "";
    }
}

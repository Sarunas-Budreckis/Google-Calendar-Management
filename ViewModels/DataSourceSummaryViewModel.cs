using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceSummaryViewModel : ObservableObject
{
    private readonly DataSourceImportHandlerRegistry _handlerRegistry;
    private bool _isImporting;

    public DataSourceSummaryViewModel(
        int dataSourceId,
        string sourceKey,
        string displayName,
        string lastDataDateLabel,
        string? lastImportedRelativeLabel,
        DataSourceImportHandlerRegistry handlerRegistry,
        IReadOnlyList<DataSourceDayDataMarkerViewModel>? dayDataMarkers = null)
    {
        DataSourceId = dataSourceId;
        SourceKey = sourceKey;
        DisplayName = displayName;
        LastDataDateLabel = lastDataDateLabel;
        LastImportedRelativeLabel = lastImportedRelativeLabel;
        _handlerRegistry = handlerRegistry;
        foreach (var marker in dayDataMarkers ?? [])
        {
            DayDataMarkers.Add(marker);
        }

        HasImportHandler = _handlerRegistry.HasHandler(SourceKey);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => IsImportEnabled);
    }

    public int DataSourceId { get; }

    public string SourceKey { get; }

    public string DisplayName { get; }

    public string LastDataDateLabel { get; }

    public string LastDataDateDisplayLabel => $"Last data date: {LastDataDateLabel}";

    public string? LastImportedRelativeLabel { get; }

    public string LastImportedDisplayLabel =>
        LastImportedRelativeLabel is null ? string.Empty : $"Last imported: {LastImportedRelativeLabel}";

    public Visibility LastImportedVisibility =>
        LastImportedRelativeLabel is null ? Visibility.Collapsed : Visibility.Visible;

    public bool HasImportHandler { get; }

    public ObservableCollection<DataSourceDayDataMarkerViewModel> DayDataMarkers { get; } = [];

    public bool HasDataInCurrentView => DayDataMarkers.Any(marker => marker.HasData);

    public Visibility DayDataMarkersVisibility =>
        DayDataMarkers.Count == 7 ? Visibility.Visible : Visibility.Collapsed;

    public bool IsImporting
    {
        get => _isImporting;
        private set
        {
            if (!SetProperty(ref _isImporting, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsImportEnabled));
            OnPropertyChanged(nameof(ImportButtonContent));
            OnPropertyChanged(nameof(ImportProgressVisibility));
            ImportCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsImportEnabled => HasImportHandler && !IsImporting;

    public string ImportButtonContent => IsImporting ? "Importing..." : "Import...";

    public Visibility ImportProgressVisibility => IsImporting ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand ImportCommand { get; }

    private async Task ImportAsync()
    {
        var handler = _handlerRegistry.GetHandler(SourceKey);
        if (handler is not null)
        {
            IsImporting = true;
            try
            {
                await handler.TriggerImportAsync();
            }
            finally
            {
                IsImporting = false;
            }
        }
    }
}

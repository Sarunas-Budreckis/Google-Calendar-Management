using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceSummaryViewModel : ObservableObject
{
    private readonly DataSourceImportHandlerRegistry _handlerRegistry;
    private readonly IDataSourceRepository _dataSourceRepository;
    private bool _isImporting;
    private bool _isCsvImporting;
    private string? _colorHex;

    public DataSourceSummaryViewModel(
        int dataSourceId,
        string sourceKey,
        string displayName,
        string lastDataDateLabel,
        string? lastImportedRelativeLabel,
        DataSourceImportHandlerRegistry handlerRegistry,
        IDataSourceRepository dataSourceRepository,
        string? colorHex = null,
        IReadOnlyList<DataSourceDayDataMarkerViewModel>? dayDataMarkers = null)
    {
        DataSourceId = dataSourceId;
        SourceKey = sourceKey;
        DisplayName = displayName;
        LastDataDateLabel = lastDataDateLabel;
        LastImportedRelativeLabel = lastImportedRelativeLabel;
        _handlerRegistry = handlerRegistry;
        _dataSourceRepository = dataSourceRepository;
        _colorHex = colorHex;
        foreach (var marker in dayDataMarkers ?? [])
        {
            DayDataMarkers.Add(marker);
        }

        HasImportHandler = _handlerRegistry.HasHandler(SourceKey);
        IsApiFetch = _handlerRegistry.IsApiFetch(SourceKey);
        HasCsvImportHandler = _handlerRegistry.HasCsvHandler(SourceKey);
        ImportCommand = new AsyncRelayCommand(ImportAsync, () => IsImportEnabled);
        CsvImportCommand = new AsyncRelayCommand(CsvImportAsync, () => IsCsvImportEnabled);
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

    public bool IsApiFetch { get; }

    public bool HasCsvImportHandler { get; }

    public ObservableCollection<DataSourceDayDataMarkerViewModel> DayDataMarkers { get; } = [];

    public bool HasDataInCurrentView => DayDataMarkers.Any(marker => marker.HasData);

    public Visibility DayDataMarkersVisibility =>
        DayDataMarkers.Count == 7 ? Visibility.Visible : Visibility.Collapsed;

    public string? ColorHex
    {
        get => _colorHex;
        private set => SetProperty(ref _colorHex, value);
    }

    public Brush ColorBrush => ParseColorBrush(_colorHex, fallbackHex: "#888888");

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

    public bool IsCsvImporting
    {
        get => _isCsvImporting;
        private set
        {
            if (!SetProperty(ref _isCsvImporting, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCsvImportEnabled));
            OnPropertyChanged(nameof(CsvImportButtonContent));
            OnPropertyChanged(nameof(CsvImportProgressVisibility));
            CsvImportCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsImportEnabled => HasImportHandler && !IsImporting;

    public bool IsCsvImportEnabled => HasCsvImportHandler && !IsCsvImporting;

    public string ImportButtonContent => IsImporting ? "Importing..." : IsApiFetch ? "API Fetch" : "Import...";

    public string CsvImportButtonContent => IsCsvImporting ? "Importing..." : "Import CSV";

    public Visibility ImportProgressVisibility => IsImporting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CsvImportProgressVisibility => IsCsvImporting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CsvImportVisibility => HasCsvImportHandler ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand CsvImportCommand { get; }

    public async Task UpdateColorAsync(string? colorHex, CancellationToken ct = default)
    {
        if (DataSourceId == 0)
        {
            return;
        }

        await _dataSourceRepository.UpdateSourceColorAsync(DataSourceId, colorHex, ct);
        _colorHex = colorHex;
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(ColorBrush));
        RefreshDayMarkerColors(colorHex);
    }

    private void RefreshDayMarkerColors(string? colorHex)
    {
        var existing = DayDataMarkers.ToList();
        DayDataMarkers.Clear();
        foreach (var marker in existing)
        {
            DayDataMarkers.Add(new DataSourceDayDataMarkerViewModel(
                marker.Date,
                marker.HasData,
                marker.Count,
                null,
                colorHex));
        }
    }

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

    private async Task CsvImportAsync()
    {
        var handler = _handlerRegistry.GetCsvHandler(SourceKey);
        if (handler is not null)
        {
            IsCsvImporting = true;
            try
            {
                await handler.TriggerImportAsync();
            }
            finally
            {
                IsCsvImporting = false;
            }
        }
    }

    private static Brush ParseColorBrush(string? colorHex, string fallbackHex)
    {
        var hex = !string.IsNullOrEmpty(colorHex) ? colorHex : fallbackHex;
        try
        {
            var s = hex.TrimStart('#');
            var r = Convert.ToByte(s.Substring(0, 2), 16);
            var g = Convert.ToByte(s.Substring(2, 2), 16);
            var b = Convert.ToByte(s.Substring(4, 2), 16);
            return new SolidColorBrush(ColorHelper.FromArgb(0xFF, r, g, b));
        }
        catch (FormatException)
        {
            var s = fallbackHex.TrimStart('#');
            var r = Convert.ToByte(s.Substring(0, 2), 16);
            var g = Convert.ToByte(s.Substring(2, 2), 16);
            var b = Convert.ToByte(s.Substring(4, 2), 16);
            return new SolidColorBrush(ColorHelper.FromArgb(0xFF, r, g, b));
        }
    }
}

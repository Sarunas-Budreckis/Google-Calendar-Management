using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;

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
        DataSourceImportHandlerRegistry handlerRegistry)
    {
        DataSourceId = dataSourceId;
        SourceKey = sourceKey;
        DisplayName = displayName;
        LastDataDateLabel = lastDataDateLabel;
        LastImportedRelativeLabel = lastImportedRelativeLabel;
        _handlerRegistry = handlerRegistry;
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

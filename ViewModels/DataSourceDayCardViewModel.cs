using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceDayCardViewModel : ObservableObject
{
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly DateOnly _date;
    private readonly Action<DataSourceDayCardViewModel> _expand;
    private bool _isIntegrated;
    private UIElement? _drilldownView;

    public DataSourceDayCardViewModel(
        int dataSourceId,
        string sourceKey,
        string displayName,
        bool isIntegrated,
        bool isGreyedOut,
        DateOnly date,
        IDataSourceRepository dataSourceRepository,
        Action<DataSourceDayCardViewModel> expand,
        UIElement? compactSummaryView,
        Func<UIElement> drilldownViewFactory)
    {
        DataSourceId = dataSourceId;
        SourceKey = sourceKey;
        DisplayName = displayName;
        _isIntegrated = isIntegrated;
        IsGreyedOut = isGreyedOut;
        _date = date;
        _dataSourceRepository = dataSourceRepository;
        _expand = expand;
        CompactSummaryView = compactSummaryView;
        DrilldownViewFactory = drilldownViewFactory;
        ToggleIntegrationCommand = new AsyncRelayCommand(ToggleIntegrationAsync, () => !IsGreyedOut);
        ExpandCommand = new RelayCommand(() => _expand(this));
    }

    public int DataSourceId { get; }

    public string SourceKey { get; }

    public string DisplayName { get; }

    public bool IsIntegrated
    {
        get => _isIntegrated;
        private set => SetProperty(ref _isIntegrated, value);
    }

    public bool IsGreyedOut { get; }

    public bool IsIntegrationEnabled => !IsGreyedOut;

    public IAsyncRelayCommand ToggleIntegrationCommand { get; }

    public IRelayCommand ExpandCommand { get; }

    public UIElement? CompactSummaryView { get; }

    public Func<UIElement> DrilldownViewFactory { get; }

    public UIElement DrilldownView => _drilldownView ??= DrilldownViewFactory();

    private async Task ToggleIntegrationAsync()
    {
        if (IsGreyedOut)
        {
            return;
        }

        var nextValue = !IsIntegrated;
        IsIntegrated = nextValue;

        try
        {
            await _dataSourceRepository.SetIntegrationAsync(_date, DataSourceId, nextValue);
        }
        catch
        {
            IsIntegrated = !nextValue;
            throw;
        }
    }

    public static UIElement CreatePlaceholderDrilldown(string displayName)
    {
        return new TextBlock
        {
            Margin = new Thickness(12, 16, 12, 0),
            Opacity = 0.72,
            Text = $"Detailed view for {displayName} - coming soon",
            TextWrapping = TextWrapping.WrapWholeWords
        };
    }
}

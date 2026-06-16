using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GoogleCalendarManagement.ViewModels;

public sealed class DataSourceDayCardViewModel : ObservableObject
{
    private readonly Action<DataSourceDayCardViewModel> _expand;
    private UIElement? _drilldownView;

    public DataSourceDayCardViewModel(
        int dataSourceId,
        string sourceKey,
        string displayName,
        CoverageResult coverage,
        bool isGreyedOut,
        DateOnly date,
        Action<DataSourceDayCardViewModel> expand,
        UIElement? compactSummaryView,
        Func<UIElement> drilldownViewFactory,
        Func<Task>? addAction = null,
        string addButtonContent = "Add",
        bool allowAddWhenGreyedOut = false)
    {
        DataSourceId = dataSourceId;
        SourceKey = sourceKey;
        DisplayName = displayName;
        Coverage = coverage;
        IsGreyedOut = isGreyedOut;
        _expand = expand;
        CompactSummaryView = compactSummaryView;
        DrilldownViewFactory = drilldownViewFactory;
        AddButtonContent = addButtonContent;
        AddCommand = new AsyncRelayCommand(
            async () =>
            {
                if (addAction is not null)
                {
                    await addAction();
                }
            },
            () => addAction is not null && (allowAddWhenGreyedOut || !IsGreyedOut));
        ExpandCommand = new RelayCommand(() => _expand(this));
    }

    public int DataSourceId { get; }

    public string SourceKey { get; }

    public string DisplayName { get; }

    public CoverageResult Coverage { get; }

    public bool IsGreyedOut { get; }

    public string CoverageLevelSymbol => Coverage.Level switch
    {
        CoverageLevel.Full when Coverage.Total == 0 => "—",
        CoverageLevel.Full => "●",
        CoverageLevel.Partial => "◐",
        CoverageLevel.None => "○",
        _ => "○"
    };

    public string CoverageCountText => Coverage.Total > 0 ? $"{Coverage.Covered}/{Coverage.Total} linked" : string.Empty;

    public Visibility CoverageCountVisibility => Coverage.Total > 0 ? Visibility.Visible : Visibility.Collapsed;

    public IRelayCommand ExpandCommand { get; }

    public IAsyncRelayCommand AddCommand { get; }

    public string AddButtonContent { get; }

    public bool HasAddAction => AddCommand.CanExecute(null);

    public Visibility AddButtonVisibility => HasAddAction ? Visibility.Visible : Visibility.Collapsed;

    public UIElement? CompactSummaryView { get; }

    public Func<UIElement> DrilldownViewFactory { get; }

    public UIElement DrilldownView => _drilldownView ??= DrilldownViewFactory();

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

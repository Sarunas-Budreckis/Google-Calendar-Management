using System.ComponentModel;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace GoogleCalendarManagement.Views;

public sealed partial class MainPage : Page
{
    private readonly Dictionary<ViewMode, Button> _viewModeButtons;
    private readonly Brush _selectorBackground = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorBorderBrush = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorHoverBackground;
    private readonly Brush _selectorSelectedBackground;
    private readonly Brush _selectorSelectedForeground;
    private readonly Brush _selectorUnselectedForeground;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        _selectorHoverBackground = (Brush)Application.Current.Resources["CalendarSelectorHoverBrush"];
        _selectorSelectedBackground = (Brush)Application.Current.Resources["CalendarSelectionHoverBrush"];
        _selectorSelectedForeground = (Brush)Application.Current.Resources["WhiteBrush"];
        _selectorUnselectedForeground = new SolidColorBrush(ColorHelper.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
        _viewModeButtons = new Dictionary<ViewMode, Button>
        {
            [ViewMode.Year] = YearViewButton,
            [ViewMode.Month] = MonthViewButton,
            [ViewMode.Week] = WeekViewButton,
            [ViewMode.Day] = DayViewButton
        };
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public MainViewModel ViewModel { get; }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateViewModeButtons();
        NavigateToCurrentView(force: true);
        JumpToDatePicker.Date = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue);
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentViewMode))
        {
            UpdateViewModeButtons();
            NavigateToCurrentView();
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentDate))
        {
            JumpToDatePicker.Date = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue);
        }
    }

    private async void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } ||
            !Enum.TryParse<ViewMode>(tag, out var mode) ||
            mode == ViewModel.CurrentViewMode)
        {
            return;
        }

        await ViewModel.SwitchViewModeCommand.ExecuteAsync(mode);
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NavigatePreviousCommand.ExecuteAsync(null);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NavigateNextCommand.ExecuteAsync(null);
    }

    private async void JumpToDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is null)
        {
            return;
        }

        var date = DateOnly.FromDateTime(args.NewDate.Value.DateTime);
        await ViewModel.JumpToDateCommand.ExecuteAsync(date);
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = App.GetRequiredService<SettingsPage>(),
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void NavigateToCurrentView(bool force = false)
    {
        var pageType = ViewModel.CurrentViewMode switch
        {
            ViewMode.Year => typeof(YearViewControl),
            ViewMode.Month => typeof(MonthViewControl),
            ViewMode.Week => typeof(WeekViewControl),
            ViewMode.Day => typeof(DayViewControl),
            _ => typeof(YearViewControl)
        };

        if (!force && CalendarFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        CalendarFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo());
    }

    private void UpdateViewModeButtons()
    {
        foreach (var (mode, button) in _viewModeButtons)
        {
            var isSelected = mode == ViewModel.CurrentViewMode;

            button.Background = isSelected ? _selectorSelectedBackground : _selectorBackground;
            button.Foreground = isSelected ? _selectorSelectedForeground : _selectorUnselectedForeground;
            button.BorderThickness = new Thickness(0);
            button.CornerRadius = new CornerRadius(12);
            button.FontWeight = isSelected
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;

            button.Resources["ButtonBackgroundPointerOver"] = isSelected ? _selectorSelectedBackground : _selectorHoverBackground;
            button.Resources["ButtonBackgroundPressed"] = isSelected ? _selectorSelectedBackground : _selectorHoverBackground;
            button.Resources["ButtonBorderBrushPointerOver"] = _selectorBorderBrush;
            button.Resources["ButtonBorderBrushPressed"] = _selectorBorderBrush;
            button.Resources["ButtonForegroundPointerOver"] = isSelected ? _selectorSelectedForeground : _selectorSelectedForeground;
            button.Resources["ButtonForegroundPressed"] = isSelected ? _selectorSelectedForeground : _selectorSelectedForeground;
        }
    }
}

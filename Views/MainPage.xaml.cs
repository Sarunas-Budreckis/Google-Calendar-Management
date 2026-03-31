using System.ComponentModel;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace GoogleCalendarManagement.Views;

public sealed partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
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

    private async void YearButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Year);
    }

    private async void MonthButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Month);
    }

    private async void WeekButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Week);
    }

    private async void DayButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Day);
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
        YearButton.IsEnabled = ViewModel.CurrentViewMode != ViewMode.Year;
        MonthButton.IsEnabled = ViewModel.CurrentViewMode != ViewMode.Month;
        WeekButton.IsEnabled = ViewModel.CurrentViewMode != ViewMode.Week;
        DayButton.IsEnabled = ViewModel.CurrentViewMode != ViewMode.Day;
    }
}

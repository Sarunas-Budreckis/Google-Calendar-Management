using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace GoogleCalendarManagement.Views;

public sealed partial class YearViewControl : Page
{
    public YearViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += YearViewControl_Loaded;
        Unloaded += YearViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void YearViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Rebuild();
    }

    private void YearViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentDate) or nameof(MainViewModel.CurrentEvents))
        {
            Rebuild();
        }
    }

    private async void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: DateOnly date })
        {
            return;
        }

        await ViewModel.JumpToDateCommand.ExecuteAsync(date);
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Month);
    }

    private void Rebuild()
    {
        MonthsGrid.Children.Clear();
        MonthsGrid.RowDefinitions.Clear();
        MonthsGrid.ColumnDefinitions.Clear();

        for (var row = 0; row < 4; row++)
        {
            MonthsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var column = 0; column < 3; column++)
        {
            MonthsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var culture = CultureInfo.CurrentCulture;

        for (var month = 1; month <= 12; month++)
        {
            var monthBorder = BuildMonthPanel(new DateOnly(ViewModel.CurrentDate.Year, month, 1), culture);
            Grid.SetRow(monthBorder, (month - 1) / 3);
            Grid.SetColumn(monthBorder, (month - 1) % 3);
            MonthsGrid.Children.Add(monthBorder);
        }
    }

    private Border BuildMonthPanel(DateOnly firstDay, CultureInfo culture)
    {
        var lastDay = new DateOnly(firstDay.Year, firstDay.Month, DateTime.DaysInMonth(firstDay.Year, firstDay.Month));
        var gridStart = StartOfWeek(firstDay);
        var gridEnd = EndOfWeek(lastDay);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = firstDay.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        });

        var monthGrid = new Grid();
        for (var column = 0; column < 7; column++)
        {
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var totalRows = ((gridEnd.DayNumber - gridStart.DayNumber) / 7) + 1;
        for (var row = 0; row < totalRows; row++)
        {
            monthGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var currentDay = gridStart;
        for (var row = 0; row < totalRows; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                var button = new Button
                {
                    Tag = currentDay,
                    Padding = new Thickness(4),
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Content = BuildDayButtonContent(currentDay, firstDay.Month, culture)
                };
                button.Click += DayButton_Click;

                Grid.SetRow(button, row);
                Grid.SetColumn(button, column);
                monthGrid.Children.Add(button);
                currentDay = currentDay.AddDays(1);
            }
        }

        panel.Children.Add(monthGrid);
        return new Border
        {
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Child = panel
        };
    }

    private static UIElement BuildDayButtonContent(DateOnly date, int activeMonth, CultureInfo culture)
    {
        var stackPanel = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = date.Month == activeMonth ? 1.0 : 0.35
        };

        stackPanel.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(culture),
            HorizontalAlignment = HorizontalAlignment.Center
        });

        // TODO Story 2.4: wire ISyncStatusService here
        stackPanel.Children.Add(new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xA0, 0xA0, 0xA0))
        });

        return stackPanel;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }

    private static DateOnly EndOfWeek(DateOnly date)
    {
        return StartOfWeek(date).AddDays(6);
    }
}

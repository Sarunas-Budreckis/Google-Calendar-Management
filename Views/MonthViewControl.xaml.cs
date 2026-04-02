using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Views;

public sealed partial class MonthViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];
    private static CornerRadius MediumCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusMedium"];

    public MonthViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += MonthViewControl_Loaded;
        Unloaded += MonthViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void MonthViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Rebuild();
    }

    private void MonthViewControl_Unloaded(object sender, RoutedEventArgs e)
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

    private void Rebuild()
    {
        MonthGrid.Children.Clear();
        MonthGrid.RowDefinitions.Clear();
        MonthGrid.ColumnDefinitions.Clear();

        for (var column = 0; column < 7; column++)
        {
            MonthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var firstDay = new DateOnly(ViewModel.CurrentDate.Year, ViewModel.CurrentDate.Month, 1);
        var lastDay = new DateOnly(
            ViewModel.CurrentDate.Year,
            ViewModel.CurrentDate.Month,
            DateTime.DaysInMonth(ViewModel.CurrentDate.Year, ViewModel.CurrentDate.Month));
        var gridStart = StartOfWeek(firstDay);
        var gridEnd = EndOfWeek(lastDay);
        var totalRows = ((gridEnd.DayNumber - gridStart.DayNumber) / 7) + 1;

        for (var row = 0; row < totalRows; row++)
        {
            MonthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
        }

        // Expand multi-day events so they appear in every day they cover, not just their start day.
        var eventsByDay = new Dictionary<DateOnly, List<CalendarEventDisplayModel>>();
        foreach (var evt in ViewModel.CurrentEvents)
        {
            var startDay = DateOnly.FromDateTime(evt.StartLocal.Date);
            var endDay = DateOnly.FromDateTime(evt.EndLocal.Date);
            for (var d = startDay; d <= endDay; d = d.AddDays(1))
            {
                if (!eventsByDay.TryGetValue(d, out var list))
                {
                    list = [];
                    eventsByDay[d] = list;
                }

                list.Add(evt);
            }
        }

        foreach (var list in eventsByDay.Values)
        {
            list.Sort((a, b) => a.StartLocal.CompareTo(b.StartLocal));
        }

        var currentDay = gridStart;
        for (var row = 0; row < totalRows; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                eventsByDay.TryGetValue(currentDay, out var dayEvents);
                var cell = BuildDayCell(currentDay, firstDay.Month, dayEvents ?? [], CultureInfo.CurrentCulture);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                MonthGrid.Children.Add(cell);
                currentDay = currentDay.AddDays(1);
            }
        }
    }

    private Border BuildDayCell(
        DateOnly date,
        int activeMonth,
        IReadOnlyList<CalendarEventDisplayModel> dayEvents,
        CultureInfo culture)
    {
        var stackPanel = new StackPanel { Spacing = 6 };
        stackPanel.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(culture),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        foreach (var item in dayEvents.Take(3))
        {
            stackPanel.Children.Add(new Border
            {
                Padding = new Thickness(4),
                CornerRadius = ElementCornerRadius,
                Background = ToBrush(item.ColorHex),
                Child = new TextBlock
                {
                    Text = item.Title,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            });
        }

        var overflowCount = Math.Max(0, dayEvents.Count - 3);
        if (overflowCount > 0)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"+{overflowCount} more",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        return new Border
        {
            Margin = new Thickness(4),
            Padding = new Thickness(8),
            CornerRadius = MediumCornerRadius,
            Opacity = date.Month == activeMonth ? 1.0 : 0.35,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = stackPanel
        };
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        if (hex.Length == 7 && hex[0] == '#')
        {
            return new SolidColorBrush(ColorHelper.FromArgb(
                0xFF,
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16)));
        }

        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x88, 0xCC));
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

using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Views;

public sealed partial class DayViewControl : Page
{
    public DayViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += DayViewControl_Loaded;
        Unloaded += DayViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void DayViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Rebuild();
    }

    private void DayViewControl_Unloaded(object sender, RoutedEventArgs e)
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
        AllDayPanel.Children.Clear();
        DayGrid.Children.Clear();
        DayGrid.RowDefinitions.Clear();
        DayGrid.ColumnDefinitions.Clear();

        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var hour = 0; hour < 24; hour++)
        {
            DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
        }

        var culture = CultureInfo.CurrentCulture;
        // Include all-day events that start on this day, and timed events that overlap this day.
        var dayEvents = ViewModel.CurrentEvents
            .Where(evt => evt.IsAllDay
                ? DateOnly.FromDateTime(evt.StartLocal.Date) == ViewModel.CurrentDate
                : DateOnly.FromDateTime(evt.StartLocal.Date) <= ViewModel.CurrentDate &&
                  DateOnly.FromDateTime(evt.EndLocal.Date) >= ViewModel.CurrentDate)
            .OrderBy(evt => evt.StartLocal)
            .ToList();

        foreach (var item in dayEvents.Where(evt => evt.IsAllDay))
        {
            AllDayPanel.Children.Add(new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(10),
                Background = ToBrush(item.ColorHex),
                Child = new TextBlock
                {
                    Text = item.Title,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            });
        }

        for (var hour = 0; hour < 24; hour++)
        {
            var label = new TextBlock
            {
                Text = $"{hour:00}:00",
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(label, hour);
            Grid.SetColumn(label, 0);
            DayGrid.Children.Add(label);

            var slotBorder = new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            Grid.SetRow(slotBorder, hour);
            Grid.SetColumn(slotBorder, 1);
            DayGrid.Children.Add(slotBorder);
        }

        foreach (var item in dayEvents.Where(evt => !evt.IsAllDay))
        {
            var eventBlock = new Border
            {
                Margin = new Thickness(4, 1, 4, 1),
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(12),
                Background = ToBrush(item.ColorHex),
                Child = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = item.Title,
                            Foreground = new SolidColorBrush(Colors.White),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                        },
                        new TextBlock
                        {
                            Text = $"{item.StartLocal.ToString("t", culture)} - {item.EndLocal.ToString("t", culture)}",
                            Foreground = new SolidColorBrush(Colors.White)
                        }
                    }
                }
            };

            var startRow = item.StartLocal.Hour;
            var span = (int)Math.Ceiling((item.EndLocal - item.StartLocal).TotalHours);
            Grid.SetRow(eventBlock, startRow);
            Grid.SetColumn(eventBlock, 1);
            Grid.SetRowSpan(eventBlock, Math.Max(1, Math.Min(span, 24 - startRow)));
            DayGrid.Children.Add(eventBlock);
        }

        AllDayPanel.Visibility = AllDayPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
}

using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Views;

public sealed partial class DayViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];

    private const double RowHeight = 72.0;
    private const double EventBottomGap = 3.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;

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
                CornerRadius = ElementCornerRadius,
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
            var topOffset = item.StartLocal.Minute / 60.0 * RowHeight;
            var pixelHeight = (item.EndLocal - item.StartLocal).TotalMinutes / 60.0 * RowHeight;
            var eventHeight = Math.Max(MinimumEventHeight, pixelHeight - EventBottomGap);
            var durationMinutes = (item.EndLocal - item.StartLocal).TotalMinutes;
            var white = new SolidColorBrush(Colors.White);

            UIElement content;
            Thickness padding;

            if (durationMinutes < 45)
            {
                var centeredTopPadding = Math.Max(0, (eventHeight - ShortEventContentHeightEstimate) / 2);
                padding = new Thickness(4, Math.Min(StandardTopPadding, centeredTopPadding), 4, 0);
                content = new TextBlock
                {
                    Text = $"{item.Title}, {item.StartLocal.ToString("t", culture)}",
                    Foreground = white,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
            }
            else
            {
                padding = new Thickness(6);
                var durationInt = (int)durationMinutes;
                var summaryLineCount = 1 + Math.Max(0, (durationInt - 60) / 30);

                content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = item.Title,
                            Foreground = white,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.Wrap,
                            MaxLines = summaryLineCount
                        },
                        new TextBlock
                        {
                            Text = $"{item.StartLocal.ToString("t", culture)} - {item.EndLocal.ToString("t", culture)}",
                            Foreground = white,
                            FontSize = 11
                        }
                    }
                };
            }

            var eventBlock = new Border
            {
                // Top margin encodes the sub-hour start offset so the block begins at the correct pixel.
                Margin = new Thickness(4, topOffset, 4, 0),
                Height = eventHeight,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = padding,
                CornerRadius = ElementCornerRadius,
                Background = ToBrush(item.ColorHex),
                Child = content
            };

            var startRow = item.StartLocal.Hour;
            // Span = number of hour-rows the event occupies, accounting for start-minute offset.
            var totalMinutesFromStartHour = item.StartLocal.Minute + (item.EndLocal - item.StartLocal).TotalMinutes;
            var span = (int)Math.Ceiling(totalMinutesFromStartHour / 60.0);
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

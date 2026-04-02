using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Views;

public sealed partial class WeekViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];

    private const double TimeColumnWidth = 72;
    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 48;
    private const double RowHeight = 72.0;
    private const double EventBottomGap = 3.0;
    private const double EventSideMargin = 4.0;
    private const double OverlapIndent = 10.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;

    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush OverlapOutlineBrush = new SolidColorBrush(Colors.Black);

    private DispatcherTimer? _resizeDebounceTimer;

    public WeekViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += WeekViewControl_Loaded;
        Unloaded += WeekViewControl_Unloaded;
        SizeChanged += WeekViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void WeekViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            Rebuild();
        };
        Rebuild();
    }

    private void WeekViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _resizeDebounceTimer?.Stop();
        _resizeDebounceTimer = null;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentDate) or nameof(MainViewModel.CurrentEvents))
        {
            Rebuild();
        }
    }

    private void WeekViewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_resizeDebounceTimer is null)
        {
            Rebuild();
            return;
        }

        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void Rebuild()
    {
        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();

        var viewportWidth = Math.Max(0d, ActualWidth - HorizontalChromeAllowance);
        var minimumContentWidth = TimeColumnWidth + (MinimumDayColumnWidth * 7);
        var contentWidth = Math.Max(minimumContentWidth, viewportWidth);
        var availableDayWidth = (contentWidth - TimeColumnWidth) / 7d;

        WeekGrid.Width = contentWidth;

        WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
        for (var column = 0; column < 7; column++)
        {
            WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(availableDayWidth) });
        }

        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var hour = 0; hour < 24; hour++)
        {
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
        }

        var culture = CultureInfo.CurrentCulture;
        var (weekStart, _) = GetWeekRange(ViewModel.CurrentDate);

        for (var hour = 0; hour < 24; hour++)
        {
            var label = new TextBlock
            {
                Text = $"{hour:00}:00",
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(label, hour + 2);
            Grid.SetColumn(label, 0);
            WeekGrid.Children.Add(label);
        }

        for (var offset = 0; offset < 7; offset++)
        {
            var currentDay = weekStart.AddDays(offset);
            var dayStart = currentDay.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var column = offset + 1;

            var header = new TextBlock
            {
                Text = currentDay.ToDateTime(TimeOnly.MinValue).ToString("ddd d", culture),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(4)
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, column);
            WeekGrid.Children.Add(header);

            var allDayPanel = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(4)
            };
            foreach (var item in ViewModel.CurrentEvents
                         .Where(evt => evt.IsAllDay && DateOnly.FromDateTime(evt.StartLocal.Date) == currentDay)
                         .OrderBy(evt => evt.StartLocal))
            {
                allDayPanel.Children.Add(CreateEventChip(item.Title, item.ColorHex));
            }

            Grid.SetRow(allDayPanel, 1);
            Grid.SetColumn(allDayPanel, column);
            WeekGrid.Children.Add(allDayPanel);

            for (var hour = 0; hour < 24; hour++)
            {
                var slotBorder = new Border
                {
                    BorderBrush = GridLineBrush,
                    BorderThickness = new Thickness(1, 1, column == 7 ? 1 : 0, hour == 23 ? 1 : 0)
                };
                Grid.SetRow(slotBorder, hour + 2);
                Grid.SetColumn(slotBorder, column);
                WeekGrid.Children.Add(slotBorder);
            }

            var timedSegments = BuildTimedEventSegments(
                ViewModel.CurrentEvents
                    .Where(evt => !evt.IsAllDay && evt.StartLocal < dayEnd && evt.EndLocal > dayStart)
                    .OrderBy(evt => evt.StartLocal)
                    .ThenBy(evt => evt.EndLocal));

            foreach (var segment in timedSegments)
            {
                var eventBlock = CreateTimedEventBlock(segment, culture);
                var startHour = segment.VisibleStart.Hour;
                var startRow = startHour + 2;
                var totalMinutesFromStartHour = segment.VisibleStart.Minute + (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
                var span = (int)Math.Ceiling(totalMinutesFromStartHour / 60.0);

                Grid.SetRow(eventBlock, startRow);
                Grid.SetColumn(eventBlock, column);
                Grid.SetRowSpan(eventBlock, Math.Max(1, Math.Min(span, 24 - startHour)));
                WeekGrid.Children.Add(eventBlock);
            }
        }
    }

    private Border CreateEventChip(string title, string hexColor)
    {
        return new Border
        {
            Padding = new Thickness(4),
            CornerRadius = ElementCornerRadius,
            Background = ToBrush(hexColor),
            Child = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    private Border CreateTimedEventBlock(TimedEventSegment segment, CultureInfo culture)
    {
        var durationMinutes = (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
        var topOffset = segment.VisibleStart.Minute / 60.0 * RowHeight;
        var pixelHeight = durationMinutes / 60.0 * RowHeight;
        var eventHeight = Math.Max(MinimumEventHeight, pixelHeight - EventBottomGap);
        var foreground = new SolidColorBrush(Colors.White);
        var leftOffset = EventSideMargin + (segment.OverlapDepth * OverlapIndent);

        UIElement content;
        Thickness padding;

        if (durationMinutes < 45)
        {
            var centeredTopPadding = Math.Max(0, (eventHeight - ShortEventContentHeightEstimate) / 2);
            padding = new Thickness(4, Math.Min(StandardTopPadding, centeredTopPadding), 4, 0);
            content = new TextBlock
            {
                Text = $"{segment.Item.Title}, {segment.VisibleStart.ToString("t", culture)}",
                Foreground = foreground,
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
                        Text = segment.Item.Title,
                        Foreground = foreground,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = summaryLineCount
                    },
                    new TextBlock
                    {
                        Text = $"{segment.VisibleStart.ToString("t", culture)} - {segment.VisibleEnd.ToString("t", culture)}",
                        Foreground = foreground,
                        FontSize = 11
                    }
                }
            };
        }

        return new Border
        {
            Margin = new Thickness(leftOffset, topOffset, EventSideMargin, 0),
            Height = eventHeight,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = padding,
            CornerRadius = ElementCornerRadius,
            Background = ToBrush(segment.Item.ColorHex),
            BorderBrush = segment.OverlapDepth > 0 ? OverlapOutlineBrush : null,
            BorderThickness = segment.OverlapDepth > 0 ? new Thickness(1) : new Thickness(0),
            Child = content
        };
    }

    private static List<TimedEventSegment> BuildTimedEventSegments(
        IEnumerable<CalendarEventDisplayModel> events)
    {
        var segments = new List<TimedEventSegment>();
        var activeSegments = new List<DateTime>();

        foreach (var item in events)
        {
            if (item.EndLocal <= item.StartLocal)
            {
                continue;
            }

            var visibleStart = item.StartLocal;
            var visibleEnd = item.EndLocal;
            activeSegments.RemoveAll(end => end <= visibleStart);
            var overlapDepth = activeSegments.Count;
            activeSegments.Add(visibleEnd);
            segments.Add(new TimedEventSegment(item, visibleStart, visibleEnd, overlapDepth));
        }

        return segments;
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

    private static (DateOnly From, DateOnly To) GetWeekRange(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var monday = date.AddDays(-daysFromMonday);
        return (monday, monday.AddDays(6));
    }

    private sealed record TimedEventSegment(
        CalendarEventDisplayModel Item,
        DateTime VisibleStart,
        DateTime VisibleEnd,
        int OverlapDepth);
}

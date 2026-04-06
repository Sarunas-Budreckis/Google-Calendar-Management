using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class DayViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];

    private const double RowHeight = 72.0;
    private const double EventBottomGap = 3.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;
    private const double TimeColumnWidth = 72.0;
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush TodayTextBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush CurrentTimeIndicatorBrush = new SolidColorBrush(Colors.Red);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private DispatcherTimer? _currentTimeTimer;
    private DateOnly _lastObservedToday;

    public DayViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        _timeProvider = App.GetRequiredService<TimeProvider>();
        InitializeComponent();
        AllDayPanel.Background = TransparentPanelBrush;
        AllDayPanel.Tapped += CalendarSurface_Tapped;
        DayGrid.Background = TransparentPanelBrush;
        DayGrid.Tapped += CalendarSurface_Tapped;
        Loaded += DayViewControl_Loaded;
        Unloaded += DayViewControl_Unloaded;
        SizeChanged += DayViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void DayViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<DayViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<DayViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());
        _lastObservedToday = GetLocalToday();
        StartCurrentTimeTimer();
        Rebuild();
    }

    private void DayViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        StopCurrentTimeTimer();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _eventBorders.Clear();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentDate)
                           or nameof(MainViewModel.CurrentEvents)
                           or nameof(MainViewModel.SyncStatusMap))
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
        CurrentTimeOverlayCanvas.Children.Clear();
        _eventBorders.Clear();

        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var hour = 0; hour < 24; hour++)
        {
            DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
        }

        // Sync status indicator for the current day
        var isSynced = ViewModel.SyncStatusMap.TryGetValue(ViewModel.CurrentDate, out var syncStatus)
                       && syncStatus == SyncStatus.Synced;
        var syncDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(isSynced ? SyncedColor : NotSyncedColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        var syncHeader = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                syncDot,
                new TextBlock
                {
                    Text = isSynced ? "Synced" : "Not synced",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                }
            }
        };
        var culture = CultureInfo.CurrentCulture;
        var localNow = _timeProvider.GetLocalNow().DateTime;
        var dayHeader = CreateDayHeader(culture, localNow);
        AllDayPanel.Children.Add(dayHeader);
        ToolTipService.SetToolTip(syncHeader, ViewModel.LastSyncTooltip);
        AllDayPanel.Children.Add(syncHeader);
        AllDayPanel.Visibility = Visibility.Visible;

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
            var eventBorder = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = ElementCornerRadius,
                Background = ToBrush(item.ColorHex),
                BorderBrush = TransparentPanelBrush,
                BorderThickness = new Thickness(0),
                Child = new TextBlock
                {
                    Text = item.Title,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };

            ToolTipService.SetToolTip(eventBorder, BuildTooltipText(item, culture));
            eventBorder.Tapped += (sender, e) =>
            {
                _selectionService.Select(item.GcalEventId);
                e.Handled = true;
            };

            RegisterEventBorder(item.GcalEventId, eventBorder);
            AllDayPanel.Children.Add(eventBorder);
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
            if (!CalendarViewVisualStateCalculator.TryClipTimedEventToDay(item, ViewModel.CurrentDate, out var segment))
            {
                continue;
            }

            var minutesFromDayStart = (segment.VisibleStart - ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue)).TotalMinutes;
            var durationMinutes = (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
            var topOffset = minutesFromDayStart / 60.0 * RowHeight;
            var pixelHeight = durationMinutes / 60.0 * RowHeight;
            var eventHeight = Math.Max(MinimumEventHeight, pixelHeight - EventBottomGap);
            var white = new SolidColorBrush(Colors.White);

            UIElement content;
            Thickness padding;

            if (durationMinutes < 45)
            {
                var centeredTopPadding = Math.Max(0, (eventHeight - ShortEventContentHeightEstimate) / 2);
                padding = new Thickness(4, Math.Min(StandardTopPadding, centeredTopPadding), 4, 0);
                content = new TextBlock
                {
                    Text = $"{item.Title}, {GetDisplayStartTime(item, segment).ToString("t", culture)}",
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
                            Text = $"{GetDisplayStartTime(item, segment).ToString("t", culture)} - {GetDisplayEndTime(item, segment).ToString("t", culture)}",
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
                BorderBrush = TransparentPanelBrush,
                BorderThickness = new Thickness(0),
                Child = content
            };

            ToolTipService.SetToolTip(eventBlock, BuildTooltipText(item, culture));
            eventBlock.Tapped += (sender, e) =>
            {
                _selectionService.Select(item.GcalEventId);
                e.Handled = true;
            };

            RegisterEventBorder(item.GcalEventId, eventBlock);

            var startRow = segment.VisibleStart.Hour;
            // Span = number of hour-rows the event occupies, accounting for start-minute offset.
            var totalMinutesFromStartHour = segment.VisibleStart.Minute + (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
            var span = (int)Math.Ceiling(totalMinutesFromStartHour / 60.0);
            Grid.SetRow(eventBlock, startRow);
            Grid.SetColumn(eventBlock, 1);
            Grid.SetRowSpan(eventBlock, Math.Max(1, Math.Min(span, 24 - startRow)));
            DayGrid.Children.Add(eventBlock);
        }

        CurrentTimeOverlayCanvas.Height = RowHeight * 24;
        _ = DispatcherQueue.TryEnqueue(UpdateCurrentTimeIndicator);
        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private void CalendarSurface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectionService.ClearSelection();
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplySelectionVisualState(message.GcalEventId));
    }

    private void OnSyncCompleted()
    {
        _ = DispatcherQueue.TryEnqueue(Rebuild);
    }

    private void DayViewControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCurrentTimeIndicator();
    }

    private FrameworkElement CreateDayHeader(CultureInfo culture, DateTime localNow)
    {
        var isToday = CalendarViewVisualStateCalculator.IsToday(ViewModel.CurrentDate, localNow);
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        header.Children.Add(new TextBlock
        {
            Text = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM", culture),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new Border
        {
            Width = 28,
            Height = 28,
            Background = TransparentPanelBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Grid
            {
                Children =
                {
                    new Ellipse
                    {
                        Fill = isToday ? TodayHighlightBrush : TransparentPanelBrush,
                        Stroke = isToday ? TodayHighlightStrokeBrush : TransparentPanelBrush,
                        StrokeThickness = isToday ? 1.25 : 0
                    },
                    new TextBlock
                    {
                        Text = ViewModel.CurrentDate.Day.ToString(culture),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = isToday
                            ? TodayTextBrush
                            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            }
        });
        return header;
    }

    private void StartCurrentTimeTimer()
    {
        if (_currentTimeTimer is not null)
        {
            _currentTimeTimer.Start();
            return;
        }

        _currentTimeTimer = new DispatcherTimer();
        _currentTimeTimer.Tick += CurrentTimeTimer_Tick;
        _currentTimeTimer.Interval = GetDelayUntilNextMinute();
        _currentTimeTimer.Start();
    }

    private void StopCurrentTimeTimer()
    {
        if (_currentTimeTimer is null)
        {
            return;
        }

        _currentTimeTimer.Stop();
        _currentTimeTimer.Tick -= CurrentTimeTimer_Tick;
        _currentTimeTimer = null;
    }

    private void CurrentTimeTimer_Tick(object? sender, object e)
    {
        if (_currentTimeTimer is not null)
        {
            _currentTimeTimer.Interval = TimeSpan.FromMinutes(1);
        }

        var today = GetLocalToday();
        if (today != _lastObservedToday)
        {
            _lastObservedToday = today;
            Rebuild();
            return;
        }

        UpdateCurrentTimeIndicator();
    }

    private void UpdateCurrentTimeIndicator()
    {
        CurrentTimeOverlayCanvas.Children.Clear();

        var localNow = _timeProvider.GetLocalNow().DateTime;
        if (!CalendarViewVisualStateCalculator.TryGetCurrentTimeIndicatorTop(ViewModel.CurrentDate, localNow, RowHeight * 24, out var topOffset))
        {
            return;
        }

        var overlayWidth = CurrentTimeOverlayCanvas.ActualWidth > 0
            ? CurrentTimeOverlayCanvas.ActualWidth
            : Math.Max(0, DayGrid.ActualWidth);
        if (overlayWidth <= TimeColumnWidth)
        {
            return;
        }

        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = CurrentTimeIndicatorBrush
        };
        var line = new Line
        {
            X1 = TimeColumnWidth,
            Y1 = topOffset,
            X2 = overlayWidth,
            Y2 = topOffset,
            Stroke = CurrentTimeIndicatorBrush,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(dot, TimeColumnWidth - 5);
        Canvas.SetTop(dot, topOffset - 5);
        CurrentTimeOverlayCanvas.Children.Add(line);
        CurrentTimeOverlayCanvas.Children.Add(dot);
    }

    private void ApplySelectionVisualState(string? selectedGcalEventId)
    {
        foreach (var (gcalEventId, registrations) in _eventBorders)
        {
            var isSelected = selectedGcalEventId is not null &&
                string.Equals(gcalEventId, selectedGcalEventId, StringComparison.Ordinal);

            foreach (var registration in registrations)
            {
                ApplySelectionState(registration.Border, registration, isSelected);
            }
        }
    }

    private void RegisterEventBorder(string gcalEventId, Border border)
    {
        if (!_eventBorders.TryGetValue(gcalEventId, out var registrations))
        {
            registrations = [];
            _eventBorders[gcalEventId] = registrations;
        }

        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness, border.Padding));
    }

    private static string BuildTooltipText(CalendarEventDisplayModel item, CultureInfo culture)
    {
        return item.IsAllDay
            ? $"{item.Title}\nAll day"
            : $"{item.Title}\n{item.StartLocal.ToString("g", culture)} - {item.EndLocal.ToString("g", culture)}";
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

    private static DateTime GetDisplayStartTime(CalendarEventDisplayModel item, VisibleTimedEventSegment segment)
    {
        return item.StartLocal.Date != item.EndLocal.Date
            ? item.StartLocal
            : segment.VisibleStart;
    }

    private static DateTime GetDisplayEndTime(CalendarEventDisplayModel item, VisibleTimedEventSegment segment)
    {
        return item.StartLocal.Date != item.EndLocal.Date
            ? item.EndLocal
            : segment.VisibleEnd;
    }

    private static void ApplySelectionState(Border border, EventBorderRegistration registration, bool isSelected)
    {
        var selectedThickness = new Thickness(2);
        border.BorderBrush = isSelected ? SelectedBorderBrush : registration.DefaultBorderBrush;
        border.BorderThickness = isSelected ? selectedThickness : registration.DefaultBorderThickness;
        border.Padding = isSelected
            ? AdjustPaddingForThickness(registration.DefaultPadding, registration.DefaultBorderThickness, selectedThickness)
            : registration.DefaultPadding;
    }

    private static Thickness AdjustPaddingForThickness(Thickness padding, Thickness fromThickness, Thickness toThickness)
    {
        return new Thickness(
            Math.Max(0, padding.Left - (toThickness.Left - fromThickness.Left)),
            Math.Max(0, padding.Top - (toThickness.Top - fromThickness.Top)),
            Math.Max(0, padding.Right - (toThickness.Right - fromThickness.Right)),
            Math.Max(0, padding.Bottom - (toThickness.Bottom - fromThickness.Bottom)));
    }

    private DateOnly GetLocalToday()
    {
        return DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
    }

    private TimeSpan GetDelayUntilNextMinute()
    {
        var now = _timeProvider.GetLocalNow().DateTime;
        var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind).AddMinutes(1);
        return nextMinute - now;
    }

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness,
        Thickness DefaultPadding);
}

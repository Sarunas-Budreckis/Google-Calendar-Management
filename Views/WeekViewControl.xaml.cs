using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class WeekViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];

    private const double TimeColumnWidth = 72;
    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 20;
    private const double WeekGridHorizontalPadding = 24.0; // Padding="12" in XAML → left(12) + right(12)
    private const double RowHeight = 72.0;
    private const double EventBottomGap = 3.0;
    private const double EventSideMargin = 4.0;
    private const double OverlapIndent = 10.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;

    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush OverlapOutlineBrush = new SolidColorBrush(Colors.Black);
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private DispatcherTimer? _resizeDebounceTimer;

    public WeekViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        InitializeComponent();
        WeekHeaderGrid.Background = TransparentPanelBrush;
        WeekHeaderGrid.Tapped += WeekGrid_Tapped;
        WeekGrid.Background = TransparentPanelBrush;
        WeekGrid.Tapped += WeekGrid_Tapped;
        Loaded += WeekViewControl_Loaded;
        Unloaded += WeekViewControl_Unloaded;
        SizeChanged += WeekViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void WeekViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<WeekViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<WeekViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());
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
        WeekHeaderGrid.Children.Clear();
        WeekHeaderGrid.RowDefinitions.Clear();
        WeekHeaderGrid.ColumnDefinitions.Clear();
        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();
        _eventBorders.Clear();

        var viewportWidth = Math.Max(0d, ActualWidth - HorizontalChromeAllowance);
        var minimumContentWidth = TimeColumnWidth + (MinimumDayColumnWidth * 7) + WeekGridHorizontalPadding;
        var contentWidth = Math.Max(minimumContentWidth, viewportWidth);
        // Subtract the shared horizontal padding so columns fill the area exactly.
        var availableDayWidth = (contentWidth - WeekGridHorizontalPadding - TimeColumnWidth) / 7d;

        // Both grids must be the same width so their columns align when the user
        // scrolls horizontally — the header stays in sync with the hourly content.
        WeekHeaderGrid.Width = contentWidth;
        WeekGrid.Width = contentWidth;

        // ── Shared column setup ───────────────────────────────────────────────
        void AddColumns(Grid g)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
            for (var c = 0; c < 7; c++)
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(availableDayWidth) });
        }
        AddColumns(WeekHeaderGrid);
        AddColumns(WeekGrid);

        // ── Header rows (frozen) ──────────────────────────────────────────────
        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 0: day name + dot
        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // row 1: all-day events

        // ── Hourly rows (scrollable) ──────────────────────────────────────────
        for (var hour = 0; hour < 24; hour++)
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });

        var culture = CultureInfo.CurrentCulture;
        var (weekStart, _) = GetWeekRange(ViewModel.CurrentDate);

        // Hour labels in the time column
        for (var hour = 0; hour < 24; hour++)
        {
            var label = new TextBlock
            {
                Text = $"{hour:00}:00",
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(label, hour);
            Grid.SetColumn(label, 0);
            WeekGrid.Children.Add(label);
        }

        for (var offset = 0; offset < 7; offset++)
        {
            var currentDay = weekStart.AddDays(offset);
            var dayStart = currentDay.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            var column = offset + 1;

            // ── Day header with sync dot (header row 0) ───────────────────────
            var isSynced = ViewModel.SyncStatusMap.TryGetValue(currentDay, out var syncStatus)
                           && syncStatus == SyncStatus.Synced;
            var dot = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(isSynced ? SyncedColor : NotSyncedColor),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            ToolTipService.SetToolTip(dot, ViewModel.LastSyncTooltip);

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4),
                Spacing = 2,
                Children =
                {
                    dot,
                    new TextBlock
                    {
                        Text = currentDay.ToDateTime(TimeOnly.MinValue).ToString("ddd d", culture),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    }
                }
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, column);
            WeekHeaderGrid.Children.Add(header);

            // ── All-day events (header row 1) ─────────────────────────────────
            var allDayPanel = new StackPanel { Spacing = 4, Margin = new Thickness(4) };
            foreach (var item in ViewModel.CurrentEvents
                         .Where(evt => evt.IsAllDay && DateOnly.FromDateTime(evt.StartLocal.Date) == currentDay)
                         .OrderBy(evt => evt.StartLocal))
            {
                allDayPanel.Children.Add(CreateEventChip(item, culture));
            }
            Grid.SetRow(allDayPanel, 1);
            Grid.SetColumn(allDayPanel, column);
            WeekHeaderGrid.Children.Add(allDayPanel);

            // ── Hourly slot borders ───────────────────────────────────────────
            for (var hour = 0; hour < 24; hour++)
            {
                var slotBorder = new Border
                {
                    BorderBrush = GridLineBrush,
                    BorderThickness = new Thickness(1, 1, column == 7 ? 1 : 0, hour == 23 ? 1 : 0)
                };
                Grid.SetRow(slotBorder, hour);
                Grid.SetColumn(slotBorder, column);
                WeekGrid.Children.Add(slotBorder);
            }

            // ── Timed events ──────────────────────────────────────────────────
            var timedSegments = BuildTimedEventSegments(
                ViewModel.CurrentEvents
                    .Where(evt => !evt.IsAllDay && evt.StartLocal < dayEnd && evt.EndLocal > dayStart)
                    .OrderBy(evt => evt.StartLocal)
                    .ThenBy(evt => evt.EndLocal));

            foreach (var segment in timedSegments)
            {
                var eventBlock = CreateTimedEventBlock(segment, culture);
                var startHour = segment.VisibleStart.Hour;
                var totalMinutesFromStartHour = segment.VisibleStart.Minute + (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
                var span = (int)Math.Ceiling(totalMinutesFromStartHour / 60.0);

                Grid.SetRow(eventBlock, startHour);
                Grid.SetColumn(eventBlock, column);
                Grid.SetRowSpan(eventBlock, Math.Max(1, Math.Min(span, 24 - startHour)));
                WeekGrid.Children.Add(eventBlock);
            }
        }

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private Border CreateEventChip(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var border = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = ElementCornerRadius,
            Background = ToBrush(item.ColorHex),
            BorderBrush = TransparentPanelBrush,
            BorderThickness = new Thickness(0),
            Child = new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        ToolTipService.SetToolTip(border, BuildTooltipText(item, culture));
        border.Tapped += (sender, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            e.Handled = true;
        };

        RegisterEventBorder(item.GcalEventId, border);
        return border;
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

        var border = new Border
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

        ToolTipService.SetToolTip(border, BuildTooltipText(segment.Item, culture));
        border.Tapped += (sender, e) =>
        {
            _selectionService.Select(segment.Item.GcalEventId);
            e.Handled = true;
        };

        RegisterEventBorder(segment.Item.GcalEventId, border);
        return border;
    }

    private void WeekGrid_Tapped(object sender, TappedRoutedEventArgs e)
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

    private void ApplySelectionVisualState(string? selectedGcalEventId)
    {
        foreach (var (gcalEventId, registrations) in _eventBorders)
        {
            var isSelected = selectedGcalEventId is not null &&
                string.Equals(gcalEventId, selectedGcalEventId, StringComparison.Ordinal);

            foreach (var registration in registrations)
            {
                registration.Border.BorderBrush = isSelected ? SelectedBorderBrush : registration.DefaultBorderBrush;
                registration.Border.BorderThickness = isSelected ? new Thickness(2) : registration.DefaultBorderThickness;
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

        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness));
    }

    private static string BuildTooltipText(CalendarEventDisplayModel item, CultureInfo culture)
    {
        return item.IsAllDay
            ? $"{item.Title}\nAll day"
            : $"{item.Title}\n{item.StartLocal.ToString("g", culture)} - {item.EndLocal.ToString("g", culture)}";
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

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness);
}

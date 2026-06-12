using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class DayViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];
    private static CornerRadius HalfElementCornerRadius { get { var r = ElementCornerRadius; return new CornerRadius(r.TopLeft / 2, r.TopRight / 2, r.BottomRight / 2, r.BottomLeft / 2); } }

    private const double EventBottomGap = 2.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;
    private const double DragThresholdPixels = 4.0;
    private static readonly Brush PendingDeleteBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C));
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush SelectedForPushBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4C, 0xAF, 0x50));
    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush TodayTextBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush CurrentTimeIndicatorBrush = new SolidColorBrush(Colors.Red);
    private static readonly InputSystemCursor ResizeVerticalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly ICalendarDaySelectionService _daySelectionService;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly EventDetailsPanelViewModel _eventDetailsViewModel;
    private readonly TimeProvider _timeProvider;
    private readonly EventColorPickerFlyoutController _eventColorPicker;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private readonly Dictionary<Border, TimedEventInteractionRegistration> _interactiveTimedEventBorders = new();
    private ColorPickerTarget? _activeColorTarget;
    private DispatcherTimer? _currentTimeTimer;
    private DateOnly _lastObservedToday;
    private EventInteractionState? _activeInteraction;
    private DraftCreationState? _activeDraftCreation;
    private bool _suppressSurfaceTapOnce;

    public DayViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        _daySelectionService = App.GetRequiredService<ICalendarDaySelectionService>();
        _pendingEventDraftService = App.GetRequiredService<IPendingEventDraftService>();
        _eventDetailsViewModel = App.GetRequiredService<EventDetailsPanelViewModel>();
        _timeProvider = App.GetRequiredService<TimeProvider>();
        _eventColorPicker = new EventColorPickerFlyoutController(
            _eventDetailsViewModel.AvailableColors,
            () => _activeColorTarget?.ColorKey,
            async colorKey =>
            {
                if (_activeColorTarget is null)
                {
                    return;
                }

                await _eventDetailsViewModel.ApplyColorToEventAsync(
                    _activeColorTarget.EventId,
                    _activeColorTarget.SourceKind,
                    colorKey);
                _activeColorTarget = _activeColorTarget with { ColorKey = colorKey };
            },
            () => new EventColorPickerMenuState(
                ShowRevert: _activeColorTarget?.IsPending == true,
                ShowPendingPublishSelectionToggle: _activeColorTarget?.IsPending == true,
                IsSelectedForPush: _activeColorTarget is not null && ViewModel.IsPendingEventSelectedForPush(_activeColorTarget.EventId),
                ShowDelete: _activeColorTarget is not null),
            async () =>
            {
                if (_activeColorTarget is null)
                {
                    return;
                }

                await _eventDetailsViewModel.RevertPendingChangesForEventAsync(
                    _activeColorTarget.EventId,
                    _activeColorTarget.SourceKind);
            },
            async () =>
            {
                if (_activeColorTarget is null)
                {
                    return;
                }

                await ViewModel.TogglePendingPublishSelectionForEventAsync(_activeColorTarget.EventId);
            },
            async () =>
            {
                if (_activeColorTarget is null)
                {
                    return;
                }

                await ViewModel.PushEventNowAsync(_activeColorTarget.EventId);
            },
            async () =>
            {
                if (_activeColorTarget is null)
                {
                    return;
                }

                await _eventDetailsViewModel.DeleteEventByIdAsync(
                    _activeColorTarget.EventId,
                    _activeColorTarget.SourceKind);
            });
        InitializeComponent();
        AllDayPanel.Background = TransparentPanelBrush;
        AllDayPanel.Tapped += CalendarSurface_Tapped;
        DayGrid.Background = TransparentPanelBrush;
        DayGrid.Tapped += CalendarSurface_Tapped;
        DayGrid.PointerPressed += DayGrid_PointerPressed;
        DayGrid.PointerMoved += DayGrid_PointerMoved;
        DayGrid.PointerReleased += DayGrid_PointerReleased;
        DayGrid.PointerCaptureLost += DayGrid_PointerCaptureLost;
        KeyDown += DayViewControl_KeyDown;
        Loaded += DayViewControl_Loaded;
        Unloaded += DayViewControl_Unloaded;
        SizeChanged += DayViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void DayViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _eventDetailsViewModel.PropertyChanged += EventDetailsViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<DayViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<DayViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());
        _daySelectionService.AutoSelectDay(ViewModel.CurrentDate);
        _lastObservedToday = GetLocalToday();
        StartCurrentTimeTimer();
        Rebuild();
    }

    private void DayViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _eventDetailsViewModel.PropertyChanged -= EventDetailsViewModel_PropertyChanged;
        StopCurrentTimeTimer();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _eventBorders.Clear();
        _interactiveTimedEventBorders.Clear();
        _activeInteraction = null;
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

    private void EventDetailsViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventDetailsPanelViewModel.IsEditMode))
        {
            RefreshInteractiveTimedEventBlocks();
        }
    }

    private void Rebuild()
    {
        AllDayPanel.Children.Clear();
        DayGrid.Children.Clear();
        DayGrid.RowDefinitions.Clear();
        DayGrid.ColumnDefinitions.Clear();
        CreationOverlayCanvas.Children.Clear();
        CurrentTimeOverlayCanvas.Children.Clear();
        _eventBorders.Clear();
        _interactiveTimedEventBorders.Clear();
        _activeInteraction = null;
        _activeDraftCreation = null;
        _suppressSurfaceTapOnce = false;

        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeFocusedViewLayoutMetrics.TimeColumnWidth) });
        DayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var hour = 0; hour < 24; hour++)
        {
            DayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TimeFocusedViewLayoutMetrics.HourRowHeight) });
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
                Tag = item.EventId,
                Padding = new Thickness(8),
                Opacity = item.Opacity,
                CornerRadius = HalfElementCornerRadius,
                Background = ToBrush(item.DisplayColorHex),
                BorderBrush = item.IsPendingDelete ? PendingDeleteBorderBrush : TransparentPanelBrush,
                BorderThickness = item.IsPendingDelete ? new Thickness(2) : new Thickness(0),
                Child = new TextBlock
                {
                    Text = GetDisplayTitle(item),
                    Foreground = new SolidColorBrush(Colors.Black)
                }
            };

            ToolTipService.SetToolTip(eventBorder, BuildTooltipText(item, culture));
            eventBorder.Tapped += (sender, e) =>
            {
                _selectionService.Select(item.EventId, item.SourceKind);
                e.Handled = true;
            };
            eventBorder.RightTapped += (_, e) =>
            {
                ShowEventColorPicker(eventBorder, item, e.GetPosition(eventBorder));
                e.Handled = true;
            };
            eventBorder.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(eventBorder).Properties.IsMiddleButtonPressed)
                {
                    e.Handled = true;
                    _ = _eventDetailsViewModel.DeleteEventByIdAsync(item.EventId, item.SourceKind);
                }
            };

            RegisterEventBorder(item.EventId, eventBorder);
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
                BorderBrush = GridLineBrush,
                BorderThickness = new Thickness(0, 1, 0, hour == 23 ? 1 : 0)
            };
            Grid.SetRow(slotBorder, hour);
            Grid.SetColumn(slotBorder, 1);
            DayGrid.Children.Add(slotBorder);
        }

        foreach (var timedItem in BuildDayTimedEventSegments(dayEvents.Where(evt => !evt.IsAllDay)))
        {
            var item = timedItem.Item;
            var segment = timedItem.Segment;

            var minutesFromDayStart = (segment.VisibleStart - ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue)).TotalMinutes;
            var durationMinutes = (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
            var topOffset = minutesFromDayStart / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
            var pixelHeight = durationMinutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
            var eventHeight = Math.Max(MinimumEventHeight, pixelHeight - EventBottomGap);
            var hasUsableOverlapWidth =
                timedItem.OverlapColumnCount > 1 &&
                DayGrid.ActualWidth > TimeFocusedViewLayoutMetrics.TimeColumnWidth + 16;
            var laneGap = hasUsableOverlapWidth ? 4.0 : 0.0;
            var dayColumnWidth = hasUsableOverlapWidth
                ? DayGrid.ActualWidth - TimeFocusedViewLayoutMetrics.TimeColumnWidth
                : 0;
            var laneWidth = hasUsableOverlapWidth
                ? Math.Max(
                    1,
                    (dayColumnWidth - 8 - (laneGap * (timedItem.OverlapColumnCount - 1))) / timedItem.OverlapColumnCount)
                : double.NaN;
            var leftOffset = hasUsableOverlapWidth
                ? 4 + ((laneWidth + laneGap) * timedItem.OverlapColumn)
                : 4;
            var black = new SolidColorBrush(Colors.Black);

            UIElement content;
            Thickness padding;

            if (durationMinutes < 45)
            {
                var centeredTopPadding = Math.Max(0, (eventHeight - ShortEventContentHeightEstimate) / 2);
                padding = TimeFocusedViewLayoutMetrics.CreateCompactTimedEventPadding(
                    Math.Min(StandardTopPadding, centeredTopPadding));
                content = new TextBlock
                {
                    Text = $"{GetDisplayTitle(item)}, {GetDisplayStartTime(item, segment).ToString("t", culture)}",
                    Foreground = black,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
            }
            else
            {
                padding = new Thickness(TimeFocusedViewLayoutMetrics.StandardTimedEventPadding);
                var durationInt = (int)durationMinutes;
                var summaryLineCount = 1 + Math.Max(0, (durationInt - 60) / 30);

                content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = GetDisplayTitle(item),
                            Foreground = black,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            FontSize = 12,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            TextWrapping = TextWrapping.Wrap,
                            MaxLines = summaryLineCount
                        },
                        new TextBlock
                        {
                            Text = $"{GetDisplayStartTime(item, segment).ToString("t", culture)} - {GetDisplayEndTime(item, segment).ToString("t", culture)}",
                            Foreground = black,
                            FontSize = 11
                        }
                    }
                };
            }

            var eventBlock = new Border
            {
                Tag = item.EventId,
                // Top margin encodes the sub-hour start offset so the block begins at the correct pixel.
                Margin = hasUsableOverlapWidth
                    ? new Thickness(leftOffset, topOffset, 0, 0)
                    : new Thickness(4, topOffset, 4, 0),
                Width = laneWidth,
                Height = eventHeight,
                HorizontalAlignment = hasUsableOverlapWidth ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
                Opacity = item.Opacity,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = padding,
                CornerRadius = HalfElementCornerRadius,
                Background = ToBrush(item.DisplayColorHex),
                BorderBrush = item.IsPendingDelete ? PendingDeleteBorderBrush : TransparentPanelBrush,
                BorderThickness = item.IsPendingDelete ? new Thickness(2) : new Thickness(0),
                Child = content
            };

            ToolTipService.SetToolTip(eventBlock, BuildTooltipText(item, culture));
            eventBlock.Tapped += (sender, e) =>
            {
                _selectionService.Select(item.EventId, item.SourceKind);
                e.Handled = true;
            };
            eventBlock.RightTapped += (_, e) =>
            {
                ShowEventColorPicker(eventBlock, item, e.GetPosition(eventBlock));
                e.Handled = true;
            };

            RegisterEventBorder(item.EventId, eventBlock);
            RegisterInteractiveTimedEventBorder(eventBlock, item);

            Grid.SetRow(eventBlock, 0);
            Grid.SetColumn(eventBlock, 1);
            Grid.SetRowSpan(eventBlock, 24);
            DayGrid.Children.Add(eventBlock);
        }

        CurrentTimeOverlayCanvas.Height = TimeFocusedViewLayoutMetrics.HourRowHeight * 24;
        _ = DispatcherQueue.TryEnqueue(UpdateCurrentTimeIndicator);
        ApplySelectionVisualState(_selectionService.SelectedEventId);
    }

    private void CalendarSurface_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_suppressSurfaceTapOnce)
        {
            _suppressSurfaceTapOnce = false;
            return;
        }

        _selectionService.ClearSelection();
    }

    private IReadOnlyList<DayTimedEventLayoutItem> BuildDayTimedEventSegments(IEnumerable<CalendarEventDisplayModel> events)
    {
        var candidates = events
            .Select(item =>
            {
                var hasSegment = CalendarViewVisualStateCalculator.TryClipTimedEventToDay(
                    item,
                    ViewModel.CurrentDate,
                    out var segment);
                return new { Item = item, HasSegment = hasSegment, Segment = segment };
            })
            .Where(item => item.HasSegment)
            .OrderBy(item => item.Segment.VisibleStart)
            .ThenBy(item => item.Segment.VisibleEnd)
            .ToList();

        var result = new List<DayTimedEventLayoutItem>(candidates.Count);
        var group = new List<(CalendarEventDisplayModel Item, VisibleTimedEventSegment Segment)>();
        DateTime? groupEnd = null;

        foreach (var candidate in candidates)
        {
            if (groupEnd is not null && candidate.Segment.VisibleStart >= groupEnd.Value)
            {
                AddOverlapGroup(group, result);
                group.Clear();
                groupEnd = null;
            }

            group.Add((candidate.Item, candidate.Segment));
            groupEnd = groupEnd is null || candidate.Segment.VisibleEnd > groupEnd.Value
                ? candidate.Segment.VisibleEnd
                : groupEnd;
        }

        if (group.Count > 0)
        {
            AddOverlapGroup(group, result);
        }

        return result
            .OrderBy(item => item.Segment.VisibleStart)
            .ThenBy(item => item.Segment.VisibleEnd)
            .ThenBy(item => item.Item.Title, StringComparer.CurrentCulture)
            .ToList();
    }

    private static void AddOverlapGroup(
        IReadOnlyList<(CalendarEventDisplayModel Item, VisibleTimedEventSegment Segment)> group,
        ICollection<DayTimedEventLayoutItem> result)
    {
        var activeColumns = new List<(int Column, DateTime End)>();
        var assignments = new List<(CalendarEventDisplayModel Item, VisibleTimedEventSegment Segment, int Column)>();
        var maxColumn = 0;

        foreach (var item in group.OrderBy(item => item.Segment.VisibleStart).ThenBy(item => item.Segment.VisibleEnd))
        {
            activeColumns.RemoveAll(active => active.End <= item.Segment.VisibleStart);
            var usedColumns = activeColumns.Select(active => active.Column).ToHashSet();
            var column = 0;
            while (usedColumns.Contains(column))
            {
                column++;
            }

            activeColumns.Add((column, item.Segment.VisibleEnd));
            assignments.Add((item.Item, item.Segment, column));
            maxColumn = Math.Max(maxColumn, column);
        }

        var columnCount = maxColumn + 1;
        foreach (var assignment in assignments)
        {
            result.Add(new DayTimedEventLayoutItem(
                assignment.Item,
                assignment.Segment,
                assignment.Column,
                columnCount));
        }
    }

    private void ShowEventColorPicker(FrameworkElement target, CalendarEventDisplayModel item)
    {
        ShowEventColorPicker(target, item, new Point(0, 0));
    }

    private void ShowEventColorPicker(FrameworkElement target, CalendarEventDisplayModel item, Point position)
    {
        _activeColorTarget = new ColorPickerTarget(item.EventId, item.SourceKind, item.ColorKey, item.IsPending, item.IsPendingDelete);
        _eventColorPicker.ShowAt(target, position);
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplySelectionVisualState(message.EventId);
            RefreshInteractiveTimedEventBlocks();
        });
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
        if (!CalendarViewVisualStateCalculator.TryGetCurrentTimeIndicatorTop(
                ViewModel.CurrentDate,
                localNow,
                TimeFocusedViewLayoutMetrics.HourRowHeight * 24,
                out var topOffset))
        {
            return;
        }

        var overlayWidth = CurrentTimeOverlayCanvas.ActualWidth > 0
            ? CurrentTimeOverlayCanvas.ActualWidth
            : Math.Max(0, DayGrid.ActualWidth);
        if (overlayWidth <= TimeFocusedViewLayoutMetrics.TimeColumnWidth)
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
            X1 = TimeFocusedViewLayoutMetrics.TimeColumnWidth,
            Y1 = topOffset,
            X2 = overlayWidth,
            Y2 = topOffset,
            Stroke = CurrentTimeIndicatorBrush,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(dot, TimeFocusedViewLayoutMetrics.TimeColumnWidth - TimeFocusedViewLayoutMetrics.CurrentTimeIndicatorDotOffset);
        Canvas.SetTop(dot, topOffset - TimeFocusedViewLayoutMetrics.CurrentTimeIndicatorDotOffset);
        CurrentTimeOverlayCanvas.Children.Add(line);
        CurrentTimeOverlayCanvas.Children.Add(dot);
    }

    private void ApplySelectionVisualState(string? selectedEventId)
    {
        foreach (var (eventId, registrations) in _eventBorders)
        {
            var isSelected = selectedEventId is not null &&
                string.Equals(eventId, selectedEventId, StringComparison.Ordinal);
            var isSelectedForPush = ViewModel.IsPendingEventSelectedForPush(eventId);

            foreach (var registration in registrations)
            {
                ApplySelectionState(registration.Border, registration, isSelected, isSelectedForPush);
            }
        }
    }

    private void RegisterEventBorder(string eventId, Border border)
    {
        if (!_eventBorders.TryGetValue(eventId, out var registrations))
        {
            registrations = [];
            _eventBorders[eventId] = registrations;
        }

        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness, border.Padding));
    }

    private void RegisterInteractiveTimedEventBorder(Border border, CalendarEventDisplayModel item)
    {
        border.PointerPressed -= TimedEventBlock_PointerPressed;
        border.PointerMoved -= TimedEventBlock_PointerMoved;
        border.PointerReleased -= TimedEventBlock_PointerReleased;
        border.PointerCaptureLost -= TimedEventBlock_PointerCaptureLost;
        border.PointerExited -= TimedEventBlock_PointerExited;

        string? defaultTimeText = null;
        if (border.Child is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock timeTextBlock)
            defaultTimeText = timeTextBlock.Text;

        _interactiveTimedEventBorders[border] = new TimedEventInteractionRegistration(
            item.EventId,
            item.SourceKind,
            item.StartLocal,
            item.EndLocal,
            border.Margin,
            border.Height,
            defaultTimeText);

        border.PointerPressed += TimedEventBlock_PointerPressed;
        border.PointerMoved += TimedEventBlock_PointerMoved;
        border.PointerReleased += TimedEventBlock_PointerReleased;
        border.PointerCaptureLost += TimedEventBlock_PointerCaptureLost;
        border.PointerExited += TimedEventBlock_PointerExited;

        RefreshInteractiveTimedEventBorder(border);
    }

    private void RefreshInteractiveTimedEventBlocks()
    {
        foreach (var border in _interactiveTimedEventBorders.Keys.ToList())
        {
            RefreshInteractiveTimedEventBorder(border);
        }
    }

    private void RefreshInteractiveTimedEventBorder(Border border)
    {
        if (!_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        ProtectedCursor = null;

        if (_activeInteraction is null || !ReferenceEquals(_activeInteraction.Border, border))
        {
            ResetInteractivePreview(border, registration);
        }
    }

    private void TimedEventBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        if (e.GetCurrentPoint(border).Properties.IsMiddleButtonPressed &&
            _interactiveTimedEventBorders.TryGetValue(border, out var midReg))
        {
            e.Handled = true;
            _ = _eventDetailsViewModel.DeleteEventByIdAsync(midReg.EventId, midReg.SourceKind);
            return;
        }

        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        var pointerMode = TimedEventDragMath.GetPointerMode(
            e.GetCurrentPoint(border).Position.Y,
            border.ActualHeight,
            TimeFocusedViewLayoutMetrics.ResizeBoundaryThickness);
        var mode = pointerMode == TimedEventPointerMode.Resize
            ? EventInteractionMode.Resize
            : EventInteractionMode.Move;
        var startLocal = registration.StartLocal;
        var endLocal = registration.EndLocal;
        var selectOnRelease = false;
        if (mode == EventInteractionMode.Resize)
        {
            if (_eventDetailsViewModel.TryGetEditableTimedRange(registration.EventId, registration.SourceKind, out var editStart, out var editEnd))
            {
                startLocal = editStart;
                endLocal = editEnd;
            }
            else
            {
                selectOnRelease = true;
            }
        }

        var transform = border.RenderTransform as TranslateTransform ?? new TranslateTransform();
        border.RenderTransform = transform;
        border.CapturePointer(e.Pointer);
        _activeInteraction = new EventInteractionState(
            border,
            registration.EventId,
            registration.SourceKind,
            mode,
            e.Pointer.PointerId,
            e.GetCurrentPoint(DayTimelineSurface).Position,
            startLocal,
            endLocal,
            registration.BaseHeight,
            transform,
            selectOnRelease);
        e.Handled = true;
    }

    private void TimedEventBlock_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border ||
            _activeInteraction is null ||
            !ReferenceEquals(_activeInteraction.Border, border) ||
            _activeInteraction.PointerId != e.Pointer.PointerId ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            if (sender is Border hoverBorder &&
                _interactiveTimedEventBorders.TryGetValue(hoverBorder, out _))
            {
                ProtectedCursor = IsPointerNearResizeBoundary(e.GetCurrentPoint(hoverBorder).Position, hoverBorder)
                    ? ResizeVerticalCursor
                    : null;
            }

            return;
        }

        ApplyInteractivePreview(_activeInteraction, registration, e.GetCurrentPoint(DayTimelineSurface).Position);
        ProtectedCursor = _activeInteraction.Mode == EventInteractionMode.Resize ? ResizeVerticalCursor : null;
        e.Handled = true;
    }

    private async void TimedEventBlock_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border ||
            _activeInteraction is null ||
            !ReferenceEquals(_activeInteraction.Border, border) ||
            _activeInteraction.PointerId != e.Pointer.PointerId ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        var pointerPosition = e.GetCurrentPoint(DayTimelineSurface).Position;
        var preview = GetPreviewRange(_activeInteraction, pointerPosition);
        if (_activeInteraction.Mode == EventInteractionMode.Move)
        {
            var dx = Math.Abs(pointerPosition.X - _activeInteraction.OriginPoint.X);
            var dy = Math.Abs(pointerPosition.Y - _activeInteraction.OriginPoint.Y);
            var isClickGesture = dx <= DragThresholdPixels && dy <= DragThresholdPixels;

            if (!isClickGesture && IsWithinTimedSurface(pointerPosition))
            {
                await _eventDetailsViewModel.ApplyDroppedTimeRangeAsync(
                    _activeInteraction.EventId,
                    _activeInteraction.SourceKind,
                    preview.StartLocal,
                    preview.EndLocal);
            }
            else
            {
                ResetInteractivePreview(border, registration);
                if (isClickGesture)
                {
                    _selectionService.Select(_activeInteraction.EventId, _activeInteraction.SourceKind);
                }
            }
        }
        else
        {
            if (_activeInteraction.SelectOnRelease)
            {
                var dy = Math.Abs(pointerPosition.Y - _activeInteraction.OriginPoint.Y);
                if (dy <= DragThresholdPixels)
                {
                    ResetInteractivePreview(border, registration);
                    _selectionService.Select(_activeInteraction.EventId, _activeInteraction.SourceKind, openInEditMode: true);
                }
                else
                {
                    var eventId = _activeInteraction.EventId;
                    var sourceKind = _activeInteraction.SourceKind;
                    var originalStart = _activeInteraction.OriginalStartLocal;
                    var succeeded = await _eventDetailsViewModel.ApplyDroppedTimeRangeAsync(
                        eventId,
                        sourceKind,
                        originalStart,
                        preview.EndLocal);
                    ResetInteractivePreview(border, registration);
                    if (succeeded)
                    {
                        _selectionService.Select(eventId, sourceKind, openInEditMode: true);
                    }
                }
            }
            else
            {
                _eventDetailsViewModel.ApplyResizedEndTime(_activeInteraction.EventId, preview.EndLocal);
            }
        }

        border.ReleasePointerCaptures();
        _activeInteraction = null;
        e.Handled = true;
    }

    private void TimedEventBlock_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border ||
            _activeInteraction is null ||
            !ReferenceEquals(_activeInteraction.Border, border) ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        ResetInteractivePreview(border, registration);
        _activeInteraction = null;
    }

    private void TimedEventBlock_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && (_activeInteraction is null || !ReferenceEquals(_activeInteraction.Border, border)))
        {
            ProtectedCursor = null;
        }
    }

    private void DayGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid ||
            IsPointerOnExistingEvent(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var pointerPoint = e.GetCurrentPoint(CreationOverlayCanvas);
        if (!pointerPoint.Properties.IsLeftButtonPressed || !IsWithinDayColumn(pointerPoint.Position.X))
        {
            return;
        }

        Focus(FocusState.Programmatic);
        var anchorLocal = GetLocalTimeFromPosition(pointerPoint.Position.Y);
        var (previewBorder, timeLabel) = CreateDraftPreviewElement();
        CreationOverlayCanvas.Children.Add(previewBorder);
        _activeDraftCreation = new DraftCreationState(e.Pointer.PointerId, anchorLocal, previewBorder, timeLabel);
        grid.CapturePointer(e.Pointer);
        UpdateDraftPreview(pointerPoint.Position);
        _suppressSurfaceTapOnce = true;
        e.Handled = true;
    }

    private void DayGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid ||
            _activeDraftCreation is null ||
            _activeDraftCreation.PointerId != e.Pointer.PointerId)
        {
            return;
        }

        UpdateDraftPreview(e.GetCurrentPoint(CreationOverlayCanvas).Position);
        e.Handled = true;
    }

    private async void DayGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid ||
            _activeDraftCreation is null ||
            _activeDraftCreation.PointerId != e.Pointer.PointerId)
        {
            return;
        }

        var position = e.GetCurrentPoint(CreationOverlayCanvas).Position;
        var shouldCancel = !IsWithinDayColumn(position.X);
        var draftRange = GetDraftRange(position.Y);
        ClearDraftPreview();
        grid.ReleasePointerCaptures();

        if (!shouldCancel)
        {
            var draft = await _pendingEventDraftService.CreateDraftAsync(draftRange.StartLocal, draftRange.EndLocal);
            _selectionService.Select(draft.PendingEventId, CalendarEventSourceKind.Pending, openInEditMode: true);
        }

        e.Handled = true;
    }

    private void DayGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ClearDraftPreview();
    }

    private void DayViewControl_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _activeInteraction is not null)
        {
            if (_interactiveTimedEventBorders.TryGetValue(_activeInteraction.Border, out var registration))
            {
                ResetInteractivePreview(_activeInteraction.Border, registration);
            }

            _activeInteraction.Border.ReleasePointerCaptures();
            _activeInteraction = null;
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape && _activeDraftCreation is not null)
        {
            ClearDraftPreview();
            e.Handled = true;
        }
    }

    private static string BuildTooltipText(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var title = GetDisplayTitle(item);
        var scheduleText = item.IsAllDay
            ? $"{title}\nAll day"
            : $"{title}\n{item.StartLocal.ToString("g", culture)} - {item.EndLocal.ToString("g", culture)}";

        return string.IsNullOrWhiteSpace(item.StatusLabel)
            ? scheduleText
            : $"{scheduleText}\n{item.StatusLabel}";
    }

    private static string GetDisplayTitle(CalendarEventDisplayModel item)
    {
        return item.SourceKind == CalendarEventSourceKind.Pending
            ? $"Draft: {item.Title}"
            : item.Title;
    }

    private bool IsPointerOnExistingEvent(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is Border border &&
                border.Tag is string &&
                (_interactiveTimedEventBorders.ContainsKey(border) || _eventBorders.Values.Any(registrations => registrations.Any(registration => ReferenceEquals(registration.Border, border)))))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool IsWithinDayColumn(double x)
    {
        return x >= TimeFocusedViewLayoutMetrics.TimeColumnWidth && x <= CreationOverlayCanvas.ActualWidth;
    }

    private bool IsWithinTimedSurface(Point point)
    {
        return IsWithinDayColumn(point.X) &&
            point.Y >= 0 &&
            point.Y <= TimeFocusedViewLayoutMetrics.HourRowHeight * 24;
    }

    private DateTime GetLocalTimeFromPosition(double y)
    {
        var clampedY = Math.Clamp(y, 0, TimeFocusedViewLayoutMetrics.HourRowHeight * 24);
        var minutes = clampedY / TimeFocusedViewLayoutMetrics.HourRowHeight * 60.0;
        return ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue).AddMinutes(minutes);
    }

    private (DateTime StartLocal, DateTime EndLocal) GetDraftRange(double currentY)
    {
        if (_activeDraftCreation is null)
        {
            return default;
        }

        var currentLocal = GetLocalTimeFromPosition(currentY);
        var (startLocal, endLocal) = CalendarDraftTiming.SnapDragRange(_activeDraftCreation.AnchorLocal, currentLocal);
        var day = ViewModel.CurrentDate;
        return (
            CalendarDraftTiming.ClampToDay(startLocal, day),
            CalendarDraftTiming.ClampToDay(endLocal, day));
    }

    private void UpdateDraftPreview(Point pointerPosition)
    {
        if (_activeDraftCreation is null)
        {
            return;
        }

        var (startLocal, endLocal) = GetDraftRange(pointerPosition.Y);
        var dayStart = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue);
        var top = (startLocal - dayStart).TotalMinutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
        var height = Math.Max(
            TimeFocusedViewLayoutMetrics.MinDraftPreviewHeight,
            (endLocal - startLocal).TotalMinutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight);
        _activeDraftCreation.PreviewBorder.Width = Math.Max(
            0,
            CreationOverlayCanvas.ActualWidth
                - TimeFocusedViewLayoutMetrics.TimeColumnWidth
                - (TimeFocusedViewLayoutMetrics.DraftOverlayInset * 2));
        _activeDraftCreation.PreviewBorder.Height = height;
        _activeDraftCreation.TimeLabel.Text = $"{startLocal:t} – {endLocal:t}";
        Canvas.SetLeft(
            _activeDraftCreation.PreviewBorder,
            TimeFocusedViewLayoutMetrics.TimeColumnWidth + TimeFocusedViewLayoutMetrics.DraftOverlayInset);
        Canvas.SetTop(_activeDraftCreation.PreviewBorder, top);
    }

    private static (Border PreviewBorder, TextBlock TimeLabel) CreateDraftPreviewElement()
    {
        var black = new SolidColorBrush(Colors.Black);
        var timeLabel = new TextBlock
        {
            Foreground = black,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(6, 4, 6, 4)
        };
        var border = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(ColorHelper.FromArgb(0x99, 0x00, 0x88, 0xCC)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xCC, 0x00, 0x88, 0xCC)),
            BorderThickness = new Thickness(1),
            Child = timeLabel
        };
        return (border, timeLabel);
    }

    private void ClearDraftPreview()
    {
        if (_activeDraftCreation is null)
        {
            return;
        }

        CreationOverlayCanvas.Children.Remove(_activeDraftCreation.PreviewBorder);
        _activeDraftCreation = null;
        _suppressSurfaceTapOnce = false;
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

    private static void ApplySelectionState(
        Border border,
        EventBorderRegistration registration,
        bool isSelected,
        bool isSelectedForPush)
    {
        var targetBrush = isSelected
            ? SelectedBorderBrush
            : isSelectedForPush
                ? SelectedForPushBorderBrush
                : registration.DefaultBorderBrush;
        var targetThickness = isSelected || isSelectedForPush ? new Thickness(2) : registration.DefaultBorderThickness;
        border.BorderBrush = targetBrush;
        border.BorderThickness = targetThickness;
        border.Padding = isSelected || isSelectedForPush
            ? AdjustPaddingForThickness(registration.DefaultPadding, registration.DefaultBorderThickness, targetThickness)
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

    private static bool IsPointerNearResizeBoundary(Point point, Border border)
    {
        return point.Y >= Math.Max(0, border.ActualHeight - TimeFocusedViewLayoutMetrics.ResizeBoundaryThickness);
    }

    private void ApplyInteractivePreview(
        EventInteractionState interaction,
        TimedEventInteractionRegistration registration,
        Point pointerPosition)
    {
        var preview = GetPreviewRange(interaction, pointerPosition);
        UpdateTimedEventTimeText(interaction.Border, preview.StartLocal, preview.EndLocal);
        if (interaction.Mode == EventInteractionMode.Move)
        {
            interaction.Transform.Y = MinutesToPixels(preview.MinuteDelta);
            interaction.Border.Height = registration.BaseHeight;
        }
        else
        {
            interaction.Transform.Y = 0;
            interaction.Border.Height = Math.Max(
                MinimumEventHeight,
                registration.BaseHeight + MinutesToPixels(preview.MinuteDelta));
        }
    }

    private static void UpdateTimedEventTimeText(Border border, DateTime startLocal, DateTime endLocal)
    {
        var timeText = $"{startLocal:t} – {endLocal:t}";
        if (border.Child is StackPanel sp &&
            sp.Children.Count >= 2 &&
            sp.Children[1] is TextBlock timeBlock)
        {
            timeBlock.Text = timeText;
        }
    }

    private static PreviewRangeResult GetPreviewRange(EventInteractionState interaction, Point pointerPosition)
    {
        var rawMinuteDelta = (pointerPosition.Y - interaction.OriginPoint.Y) / TimeFocusedViewLayoutMetrics.HourRowHeight * 60.0;
        if (interaction.Mode == EventInteractionMode.Move)
        {
            var preview = TimedEventDragMath.GetMovePreview(
                interaction.OriginalStartLocal,
                interaction.OriginalEndLocal,
                rawMinuteDelta);
            return new PreviewRangeResult(
                preview.StartLocal,
                preview.EndLocal,
                preview.VisualMinuteDelta);
        }

        var clampedEnd = TimedEventDragMath.GetResizeEndPreview(
            interaction.OriginalStartLocal,
            interaction.OriginalEndLocal,
            rawMinuteDelta);

        return new PreviewRangeResult(
            interaction.OriginalStartLocal,
            clampedEnd,
            (int)Math.Round((clampedEnd - interaction.OriginalEndLocal).TotalMinutes));
    }

    private static double MinutesToPixels(int minutes)
    {
        return minutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
    }

    private void ResetInteractivePreview(Border border, TimedEventInteractionRegistration registration)
    {
        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }

        ProtectedCursor = null;
        border.Margin = registration.BaseMargin;
        border.Height = registration.BaseHeight;

        if (registration.DefaultTimeText is not null &&
            border.Child is StackPanel sp &&
            sp.Children.Count >= 2 &&
            sp.Children[1] is TextBlock timeBlock)
        {
            timeBlock.Text = registration.DefaultTimeText;
        }
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

    private sealed record ColorPickerTarget(string EventId, CalendarEventSourceKind SourceKind, string ColorKey, bool IsPending, bool IsPendingDelete = false);

    private sealed record TimedEventInteractionRegistration(
        string EventId,
        CalendarEventSourceKind SourceKind,
        DateTime StartLocal,
        DateTime EndLocal,
        Thickness BaseMargin,
        double BaseHeight,
        string? DefaultTimeText);

    private sealed record DayTimedEventLayoutItem(
        CalendarEventDisplayModel Item,
        VisibleTimedEventSegment Segment,
        int OverlapColumn,
        int OverlapColumnCount);

    private sealed record EventInteractionState(
        Border Border,
        string EventId,
        CalendarEventSourceKind SourceKind,
        EventInteractionMode Mode,
        uint PointerId,
        Point OriginPoint,
        DateTime OriginalStartLocal,
        DateTime OriginalEndLocal,
        double BaseHeight,
        TranslateTransform Transform,
        bool SelectOnRelease = false);

    private sealed record DraftCreationState(
        uint PointerId,
        DateTime AnchorLocal,
        Border PreviewBorder,
        TextBlock TimeLabel);

    private sealed record PreviewRangeResult(DateTime StartLocal, DateTime EndLocal, int MinuteDelta);

    private enum EventInteractionMode
    {
        Move,
        Resize
    }
}

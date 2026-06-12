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

public sealed partial class WeekViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];
    private static CornerRadius HalfElementCornerRadius { get { var r = ElementCornerRadius; return new CornerRadius(r.TopLeft / 2, r.TopRight / 2, r.BottomRight / 2, r.BottomLeft / 2); } }

    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 20;
    private const double WeekGridHorizontalPadding = 24.0;
    private const double DragThresholdPixels = 4.0;

    private static readonly Brush PendingDeleteBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xC4, 0x2B, 0x1C));
    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush OverlapOutlineBrush = new SolidColorBrush(Colors.Black);
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush SelectedForPushBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4C, 0xAF, 0x50));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush SelectedDayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xB9, 0x32, 0x42));
    private static readonly Brush SelectedDayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xF0, 0x8A, 0x94));
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
    private readonly Dictionary<DateOnly, List<DayNumberRegistration>> _dayNumberRegistrations = [];
    private readonly Dictionary<Border, TimedEventInteractionRegistration> _interactiveTimedEventBorders = new();
    private ColorPickerTarget? _activeColorTarget;
    private IReadOnlyList<WeekTimedEventLayoutItem> _timedEventItems = [];
    private WeekTimedEventVirtualizingLayout _timedEventLayout = new();
    private DispatcherTimer? _currentTimeTimer;
    private DispatcherTimer? _resizeDebounceTimer;
    private DateOnly _lastObservedToday;
    private DateOnly _renderedWeekStart;
    private double _renderedDayColumnWidth;
    private EventInteractionState? _activeInteraction;
    private DraftCreationState? _activeDraftCreation;
    private bool _suppressSurfaceTapOnce;

    public WeekViewControl()
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

        WeekHeaderGrid.Background = TransparentPanelBrush;
        WeekHeaderGrid.Tapped += WeekGrid_Tapped;
        WeekGrid.Background = TransparentPanelBrush;
        WeekGrid.Tapped += WeekGrid_Tapped;
        WeekGrid.PointerPressed += WeekGrid_PointerPressed;
        WeekGrid.PointerMoved += WeekGrid_PointerMoved;
        WeekGrid.PointerReleased += WeekGrid_PointerReleased;
        WeekGrid.PointerCaptureLost += WeekGrid_PointerCaptureLost;
        KeyDown += WeekViewControl_KeyDown;

        TimedEventsRepeater.ItemTemplate = (DataTemplate)Resources["WeekTimedEventTemplate"];
        AttachFreshTimedEventsLayout();
        TimedEventsRepeater.ElementPrepared += TimedEventsRepeater_ElementPrepared;
        TimedEventsRepeater.ElementClearing += TimedEventsRepeater_ElementClearing;

        Loaded += WeekViewControl_Loaded;
        Unloaded += WeekViewControl_Unloaded;
        SizeChanged += WeekViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void WeekViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        _eventDetailsViewModel.PropertyChanged += EventDetailsViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<WeekViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<WeekViewControl, DaySelectedMessage>(this, static (recipient, message) => recipient.OnDaySelected(message));
        WeakReferenceMessenger.Default.Register<WeekViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());

        _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer?.Stop();
            Rebuild();
        };

        _lastObservedToday = GetLocalToday();
        StartCurrentTimeTimer();
        Rebuild();
    }

    private void WeekViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _eventDetailsViewModel.PropertyChanged -= EventDetailsViewModel_PropertyChanged;
        StopCurrentTimeTimer();
        _resizeDebounceTimer?.Stop();
        _resizeDebounceTimer = null;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _eventBorders.Clear();
        _dayNumberRegistrations.Clear();
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
            RefreshInteractiveTimedEventBorders();
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
        TimedEventsRepeater.ItemsSource = null;
        _timedEventItems = [];
        _eventBorders.Clear();
        _dayNumberRegistrations.Clear();
        _interactiveTimedEventBorders.Clear();
        _activeInteraction = null;
        _activeDraftCreation = null;
        _suppressSurfaceTapOnce = false;
        AttachFreshTimedEventsLayout();

        var viewportWidth = Math.Max(0d, ActualWidth - HorizontalChromeAllowance);
        var minimumContentWidth = TimeFocusedViewLayoutMetrics.TimeColumnWidth + (MinimumDayColumnWidth * 7) + WeekGridHorizontalPadding;
        var contentWidth = Math.Max(minimumContentWidth, viewportWidth);
        var availableDayWidth = (contentWidth - WeekGridHorizontalPadding - TimeFocusedViewLayoutMetrics.TimeColumnWidth) / 7d;

        WeekHeaderGrid.Width = contentWidth;
        WeekBodySurface.Width = contentWidth;
        WeekBodySurface.Height = TimeFocusedViewLayoutMetrics.HourRowHeight * 24;
        WeekGrid.Width = contentWidth;
        TimedEventsRepeater.Width = contentWidth;
        TimedEventsRepeater.Height = WeekBodySurface.Height;
        CurrentTimeOverlayCanvas.Width = contentWidth;
        CurrentTimeOverlayCanvas.Height = WeekBodySurface.Height;
        CreationOverlayCanvas.Width = contentWidth;
        CreationOverlayCanvas.Height = WeekBodySurface.Height;

        void AddColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeFocusedViewLayoutMetrics.TimeColumnWidth) });
            for (var column = 0; column < 7; column++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(availableDayWidth) });
            }
        }

        AddColumns(WeekHeaderGrid);
        AddColumns(WeekGrid);

        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WeekHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var hour = 0; hour < 24; hour++)
        {
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TimeFocusedViewLayoutMetrics.HourRowHeight) });
        }

        var culture = CultureInfo.CurrentCulture;
        var (weekStart, _) = GetWeekRange(ViewModel.CurrentDate);
        _renderedWeekStart = weekStart;
        _renderedDayColumnWidth = availableDayWidth;
        var today = GetLocalToday();

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
            var column = offset + 1;
            var isToday = CalendarViewVisualStateCalculator.IsToday(currentDay, today.ToDateTime(TimeOnly.MinValue));

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

            var dayHighlight = new Ellipse
            {
                Fill = isToday ? TodayHighlightBrush : TransparentPanelBrush,
                Stroke = isToday ? TodayHighlightStrokeBrush : TransparentPanelBrush,
                StrokeThickness = isToday ? 1.25 : 0,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var selectedOverlay = new Ellipse
            {
                Margin = new Thickness(4),
                Fill = TransparentPanelBrush,
                Stroke = TransparentPanelBrush,
                StrokeThickness = 0
            };
            var dayLabel = new TextBlock
            {
                Text = currentDay.Day.ToString(culture),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = isToday
                    ? TodayTextBrush
                    : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dayNumber = new Border
            {
                Width = 28,
                Height = 28,
                Background = TransparentPanelBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Grid
                {
                    Children =
                    {
                        dayHighlight,
                        selectedOverlay,
                        dayLabel
                    }
                }
            };
            dayNumber.Tapped += (_, e) =>
            {
                ToggleDaySelection(currentDay);
                e.Handled = true;
            };
            RegisterDayNumber(currentDay, dayHighlight, selectedOverlay, dayLabel, isToday);

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4),
                Background = TransparentPanelBrush,
                Spacing = 6,
                Children =
                {
                    dot,
                    new TextBlock
                    {
                        Text = currentDay.ToDateTime(TimeOnly.MinValue).ToString("ddd", culture),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center
                    },
                    dayNumber
                }
            };
            header.Tapped += (_, e) =>
            {
                ToggleDaySelection(currentDay);
                e.Handled = true;
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, column);
            WeekHeaderGrid.Children.Add(header);

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
        }

        _timedEventItems = WeekTimedEventProjectionBuilder.Build(
            weekStart,
            ViewModel.CurrentEvents,
            availableDayWidth,
            culture);
        TimedEventsRepeater.ItemsSource = _timedEventItems;
        UpdateCurrentTimeIndicator();

        ApplySelectionVisualState(_selectionService.SelectedEventId);
        ApplyDaySelectionVisualState(_daySelectionService.SelectedDay);
    }

    private Border CreateEventChip(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var border = new Border
        {
            Padding = new Thickness(4),
            Opacity = item.Opacity,
            CornerRadius = HalfElementCornerRadius,
            Background = ToBrush(item.DisplayColorHex),
            BorderBrush = item.IsPendingDelete ? PendingDeleteBorderBrush : TransparentPanelBrush,
            BorderThickness = item.IsPendingDelete ? new Thickness(2) : new Thickness(0),
            Child = new TextBlock
            {
                    Text = GetDisplayTitle(item),
                Foreground = new SolidColorBrush(Colors.Black),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };

        ToolTipService.SetToolTip(border, BuildTooltipText(item, culture));
        border.Tapped += (sender, e) =>
        {
            _selectionService.Select(item.EventId, item.SourceKind);
            e.Handled = true;
        };
        border.RightTapped += (sender, e) =>
        {
            ShowEventColorPicker(border, item, e.GetPosition(border));
            e.Handled = true;
        };
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsMiddleButtonPressed)
            {
                e.Handled = true;
                _ = _eventDetailsViewModel.DeleteEventByIdAsync(item.EventId, item.SourceKind);
            }
        };

        RegisterEventBorder(item.EventId, border);
        return border;
    }

    private void TimedEventsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Border border)
        {
            return;
        }

        var item = GetTimedEventLayoutItem(args.Index);
        if (item is null)
        {
            ResetTimedEventBorder(border);
            return;
        }

        if (border.Tag is string previousEventId &&
            !string.Equals(previousEventId, item.EventId, StringComparison.Ordinal))
        {
            UnregisterEventBorder(previousEventId, border);
        }

        ConfigureTimedEventBorder(border, item);
        RegisterEventBorder(item.EventId, border);
        RegisterInteractiveTimedEventBorder(border, item);

        if (string.Equals(_selectionService.SelectedEventId, item.EventId, StringComparison.Ordinal))
        {
            ApplySelectionState(
                border,
                _eventBorders[item.EventId].Last(),
                isSelected: true,
                isSelectedForPush: ViewModel.IsPendingEventSelectedForPush(item.EventId));
        }
    }

    private void TimedEventsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not Border border || border.Tag is not string eventId)
        {
            return;
        }

        UnregisterEventBorder(eventId, border);
        _interactiveTimedEventBorders.Remove(border);
        ResetTimedEventBorder(border);
    }

    private void ConfigureTimedEventBorder(Border border, WeekTimedEventLayoutItem item)
    {
        if (border.Child is not Grid layoutRoot)
        {
            return;
        }

        var compactTextBlock = (TextBlock)layoutRoot.Children[0];
        var detailedPanel = (StackPanel)layoutRoot.Children[1];
        var titleTextBlock = (TextBlock)detailedPanel.Children[0];
        var timeTextBlock = (TextBlock)detailedPanel.Children[1];

        border.Tag = item.EventId;
        border.Opacity = item.Opacity;
        border.Height = double.NaN;
        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 0;
        }
        border.CornerRadius = HalfElementCornerRadius;
        border.Background = ToBrush(item.ColorHex);
        border.BorderBrush = item.IsPendingDelete ? PendingDeleteBorderBrush : item.UseOverlapOutline ? OverlapOutlineBrush : null;
        border.BorderThickness = item.IsPendingDelete || item.UseOverlapOutline ? new Thickness(item.IsPendingDelete ? 2 : 1) : new Thickness(0);
        border.Padding = item.IsCompact
            ? TimeFocusedViewLayoutMetrics.CreateCompactTimedEventPadding(item.CompactTopPadding)
            : new Thickness(TimeFocusedViewLayoutMetrics.StandardTimedEventPadding);
        border.Tapped -= TimedEventBorder_Tapped;
        border.Tapped += TimedEventBorder_Tapped;
        border.RightTapped -= TimedEventBorder_RightTapped;
        border.RightTapped += TimedEventBorder_RightTapped;
        ToolTipService.SetToolTip(border, item.TooltipText);

        compactTextBlock.Visibility = item.IsCompact ? Visibility.Visible : Visibility.Collapsed;
        compactTextBlock.Text = item.PrimaryText;

        detailedPanel.Visibility = item.IsCompact ? Visibility.Collapsed : Visibility.Visible;
        titleTextBlock.Text = item.PrimaryText;
        titleTextBlock.MaxLines = item.MaxTitleLines;
        timeTextBlock.Text = item.SecondaryText ?? string.Empty;
    }

    private void TimedEventBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: string eventId } && sender is Border { DataContext: WeekTimedEventLayoutItem item })
        {
            _selectionService.Select(eventId, item.SourceKind);
            e.Handled = true;
        }
    }

    private void TimedEventBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is WeekTimedEventLayoutItem item)
        {
            ShowEventColorPicker(border, item.EventId, item.SourceKind, ResolveColorKey(item.EventId), e.GetPosition(border));
            e.Handled = true;
        }
    }

    private void WeekGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (_suppressSurfaceTapOnce)
        {
            _suppressSurfaceTapOnce = false;
            return;
        }

        _selectionService.ClearSelection();
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplySelectionVisualState(message.EventId);
            RefreshInteractiveTimedEventBorders();
        });
    }

    private void OnDaySelected(DaySelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplyDaySelectionVisualState(message.SelectedDay));
    }

    private void OnSyncCompleted()
    {
        _ = DispatcherQueue.TryEnqueue(Rebuild);
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

        if (_renderedDayColumnWidth <= 0 || WeekBodySurface.Height <= 0)
        {
            return;
        }

        var localNow = _timeProvider.GetLocalNow().DateTime;
        var today = DateOnly.FromDateTime(localNow);
        if (!CalendarViewVisualStateCalculator.TryGetCurrentTimeIndicatorTop(today, localNow, WeekBodySurface.Height, out var topOffset))
        {
            return;
        }

        var dayOffset = today.DayNumber - _renderedWeekStart.DayNumber;
        if (dayOffset < 0 || dayOffset > 6)
        {
            return;
        }

        var lineStart = (WeekGridHorizontalPadding / 2d) + TimeFocusedViewLayoutMetrics.TimeColumnWidth + (dayOffset * _renderedDayColumnWidth);
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = CurrentTimeIndicatorBrush
        };
        var line = new Line
        {
            X1 = lineStart,
            Y1 = topOffset,
            X2 = lineStart + _renderedDayColumnWidth,
            Y2 = topOffset,
            Stroke = CurrentTimeIndicatorBrush,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(dot, lineStart - TimeFocusedViewLayoutMetrics.CurrentTimeIndicatorDotOffset);
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

    private void RegisterDayNumber(DateOnly date, Ellipse highlight, Ellipse selectedOverlay, TextBlock label, bool isToday)
    {
        if (!_dayNumberRegistrations.TryGetValue(date, out var registrations))
        {
            registrations = [];
            _dayNumberRegistrations[date] = registrations;
        }

        registrations.Add(new DayNumberRegistration(highlight, selectedOverlay, label, isToday));
    }

    private void ToggleDaySelection(DateOnly date)
    {
        if (_daySelectionService.SelectedDay == date)
        {
            _daySelectionService.ClearSelection();
            return;
        }

        _daySelectionService.SelectDay(date);
    }

    private void ApplyDaySelectionVisualState(DateOnly? selectedDay)
    {
        foreach (var (date, registrations) in _dayNumberRegistrations)
        {
            var isSelected = selectedDay == date;
            foreach (var registration in registrations)
            {
                registration.Highlight.Fill = isSelected
                    ? registration.IsToday
                        ? TodayHighlightBrush
                        : SelectedDayHighlightBrush
                    : registration.IsToday
                        ? TodayHighlightBrush
                        : TransparentPanelBrush;
                registration.Highlight.Stroke = isSelected
                    ? registration.IsToday
                        ? TodayHighlightStrokeBrush
                        : SelectedDayHighlightStrokeBrush
                    : registration.IsToday
                        ? TodayHighlightStrokeBrush
                        : TransparentPanelBrush;
                registration.Highlight.StrokeThickness = isSelected || registration.IsToday ? 1.25 : 0;
                registration.Highlight.RenderTransform = registration.IsToday && isSelected
                    ? new ScaleTransform { ScaleX = 1.16, ScaleY = 1.16 }
                    : null;
                registration.SelectedOverlay.Fill = registration.IsToday && isSelected
                    ? SelectedDayHighlightBrush
                    : TransparentPanelBrush;
                registration.SelectedOverlay.Stroke = registration.IsToday && isSelected
                    ? SelectedDayHighlightStrokeBrush
                    : TransparentPanelBrush;
                registration.SelectedOverlay.StrokeThickness = registration.IsToday && isSelected ? 1 : 0;
                registration.Label.Foreground = isSelected || registration.IsToday
                    ? TodayTextBrush
                    : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
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

        registrations.RemoveAll(registration => ReferenceEquals(registration.Border, border));
        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness, border.Padding));
    }

    private void RegisterInteractiveTimedEventBorder(Border border, WeekTimedEventLayoutItem item)
    {
        border.PointerPressed -= TimedEventBorder_PointerPressed;
        border.PointerMoved -= TimedEventBorder_PointerMoved;
        border.PointerReleased -= TimedEventBorder_PointerReleased;
        border.PointerCaptureLost -= TimedEventBorder_PointerCaptureLost;
        border.PointerExited -= TimedEventBorder_PointerExited;

        var layoutRoot = border.Child as Grid;
        if (layoutRoot is null)
        {
            return;
        }

        _interactiveTimedEventBorders[border] = new TimedEventInteractionRegistration(
            item.EventId,
            item.SourceKind,
            item.StartLocal,
            item.EndLocal,
            item.DayOffset,
            item.Height,
            item.SecondaryText);

        border.PointerPressed += TimedEventBorder_PointerPressed;
        border.PointerMoved += TimedEventBorder_PointerMoved;
        border.PointerReleased += TimedEventBorder_PointerReleased;
        border.PointerCaptureLost += TimedEventBorder_PointerCaptureLost;
        border.PointerExited += TimedEventBorder_PointerExited;

        RefreshInteractiveTimedEventBorder(border);
    }

    private void RefreshInteractiveTimedEventBorders()
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

    private WeekTimedEventLayoutItem? GetTimedEventLayoutItem(int index)
    {
        return index >= 0 && index < _timedEventItems.Count
            ? _timedEventItems[index]
            : null;
    }

    private void AttachFreshTimedEventsLayout()
    {
        _timedEventLayout = new WeekTimedEventVirtualizingLayout();
        TimedEventsRepeater.Layout = _timedEventLayout;
    }

    private void TimedEventBorder_PointerPressed(object sender, PointerRoutedEventArgs e)
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
            e.GetCurrentPoint(WeekBodySurface).Position,
            startLocal,
            endLocal,
            registration.DayOffset,
            registration.BaseHeight,
            transform,
            selectOnRelease);
        e.Handled = true;
    }

    private void TimedEventBorder_PointerMoved(object sender, PointerRoutedEventArgs e)
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

        ApplyInteractivePreview(_activeInteraction, registration, e.GetCurrentPoint(WeekBodySurface).Position);
        ProtectedCursor = _activeInteraction.Mode == EventInteractionMode.Resize ? ResizeVerticalCursor : null;
        e.Handled = true;
    }

    private async void TimedEventBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border ||
            _activeInteraction is null ||
            !ReferenceEquals(_activeInteraction.Border, border) ||
            _activeInteraction.PointerId != e.Pointer.PointerId ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        var pointerPosition = e.GetCurrentPoint(WeekBodySurface).Position;
        var preview = GetPreviewRange(_activeInteraction, pointerPosition);
        if (_activeInteraction.Mode == EventInteractionMode.Move)
        {
            var dx = Math.Abs(pointerPosition.X - _activeInteraction.OriginPoint.X);
            var dy = Math.Abs(pointerPosition.Y - _activeInteraction.OriginPoint.Y);
            var isClickGesture = dx <= DragThresholdPixels && dy <= DragThresholdPixels;

            if (!isClickGesture && TryGetDayOffset(pointerPosition.X, out _))
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

    private void TimedEventBorder_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
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

    private void TimedEventBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border && (_activeInteraction is null || !ReferenceEquals(_activeInteraction.Border, border)))
        {
            ProtectedCursor = null;
        }
    }

    private void WeekGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid ||
            IsPointerOnExistingEvent(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var position = e.GetCurrentPoint(CreationOverlayCanvas).Position;
        if (!e.GetCurrentPoint(CreationOverlayCanvas).Properties.IsLeftButtonPressed ||
            !TryGetDayOffset(position.X, out var dayOffset))
        {
            return;
        }

        Focus(FocusState.Programmatic);
        var anchorDay = _renderedWeekStart.AddDays(dayOffset);
        var anchorLocal = GetLocalTimeFromPosition(position.Y, anchorDay);
        var (previewBorder, timeLabel) = CreateDraftPreviewElement();
        CreationOverlayCanvas.Children.Add(previewBorder);
        _activeDraftCreation = new DraftCreationState(e.Pointer.PointerId, dayOffset, anchorLocal, previewBorder, timeLabel);
        grid.CapturePointer(e.Pointer);
        UpdateDraftPreview(position);
        _suppressSurfaceTapOnce = true;
        e.Handled = true;
    }

    private void WeekGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
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

    private async void WeekGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid ||
            _activeDraftCreation is null ||
            _activeDraftCreation.PointerId != e.Pointer.PointerId)
        {
            return;
        }

        var position = e.GetCurrentPoint(CreationOverlayCanvas).Position;
        var shouldCancel = !TryGetDayOffset(position.X, out var releaseDayOffset) || releaseDayOffset != _activeDraftCreation.DayOffset;
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

    private void WeekGrid_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ClearDraftPreview();
    }

    private void WeekViewControl_KeyDown(object sender, KeyRoutedEventArgs e)
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
            interaction.Transform.X = preview.DayDelta * _renderedDayColumnWidth;
            interaction.Transform.Y = MinutesToPixels(preview.MinuteDelta);
            interaction.Border.Height = double.NaN;
        }
        else
        {
            interaction.Transform.X = 0;
            interaction.Transform.Y = 0;
            var newHeight = Math.Max(15.0, registration.BaseHeight + MinutesToPixels(preview.MinuteDelta));
            _timedEventLayout.DragEventId = interaction.EventId;
            _timedEventLayout.DragHeight = newHeight;
            TimedEventsRepeater.InvalidateMeasure();
        }
    }

    private static void UpdateTimedEventTimeText(Border border, DateTime startLocal, DateTime endLocal)
    {
        var timeText = $"{startLocal.ToString("t", CultureInfo.CurrentCulture)} – {endLocal.ToString("t", CultureInfo.CurrentCulture)}";
        if (border.Child is not Grid layoutRoot) return;

        if (layoutRoot.Children.Count > 1 &&
            layoutRoot.Children[1] is StackPanel stack &&
            stack.Children.Count > 1 &&
            stack.Children[1] is TextBlock detailedTime)
        {
            detailedTime.Text = timeText;
        }

        if (layoutRoot.Children.Count > 0 &&
            layoutRoot.Children[0] is TextBlock compact &&
            compact.Visibility == Visibility.Visible)
        {
            compact.Text = timeText;
        }
    }

    private PreviewRangeResult GetPreviewRange(EventInteractionState interaction, Point pointerPosition)
    {
        var rawMinuteDelta = (pointerPosition.Y - interaction.OriginPoint.Y) / TimeFocusedViewLayoutMetrics.HourRowHeight * 60.0;
        if (interaction.Mode == EventInteractionMode.Move)
        {
            var dayDelta = TryGetDayOffset(pointerPosition.X, out var pointerDayOffset)
                ? pointerDayOffset - interaction.OriginalDayOffset
                : 0;
            var preview = TimedEventDragMath.GetMovePreview(
                interaction.OriginalStartLocal,
                interaction.OriginalEndLocal,
                rawMinuteDelta,
                dayDelta);
            return new PreviewRangeResult(
                preview.StartLocal,
                preview.EndLocal,
                preview.VisualMinuteDelta,
                preview.VisualDayDelta);
        }

        var clampedEnd = TimedEventDragMath.GetResizeEndPreview(
            interaction.OriginalStartLocal,
            interaction.OriginalEndLocal,
            rawMinuteDelta);

        return new PreviewRangeResult(
            interaction.OriginalStartLocal,
            clampedEnd,
            (int)Math.Round((clampedEnd - interaction.OriginalEndLocal).TotalMinutes),
            0);
    }

    private static double MinutesToPixels(int minutes)
    {
        return minutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
    }

    private void ResetInteractivePreview(Border border, TimedEventInteractionRegistration registration)
    {
        if (border.Child is Grid layoutRoot &&
            layoutRoot.Children.Count > 1 &&
            layoutRoot.Children[1] is StackPanel stack &&
            stack.Children.Count > 1 &&
            stack.Children[1] is TextBlock timeText)
        {
            timeText.Text = registration.DefaultTimeText ?? string.Empty;
        }

        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }

        ProtectedCursor = null;
        border.Height = double.NaN;
        _timedEventLayout.DragEventId = null;
        _timedEventLayout.DragHeight = 0;
        TimedEventsRepeater.InvalidateMeasure();
    }

    private void ResetTimedEventBorder(Border border)
    {
        border.Tag = null;
        border.Background = null;
        border.BorderBrush = null;
        border.BorderThickness = new Thickness(0);
        border.Padding = new Thickness(0);
        border.Height = double.NaN;
        border.Tapped -= TimedEventBorder_Tapped;
        border.RightTapped -= TimedEventBorder_RightTapped;
        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 0;
        }
        ToolTipService.SetToolTip(border, null);

        if (border.Child is not Grid layoutRoot || layoutRoot.Children.Count < 2)
        {
            return;
        }

        if (layoutRoot.Children[0] is TextBlock compactTextBlock)
        {
            compactTextBlock.Text = string.Empty;
            compactTextBlock.Visibility = Visibility.Collapsed;
        }

        if (layoutRoot.Children[1] is StackPanel detailedPanel)
        {
            detailedPanel.Visibility = Visibility.Collapsed;

            if (detailedPanel.Children.Count > 0 && detailedPanel.Children[0] is TextBlock titleTextBlock)
            {
                titleTextBlock.Text = string.Empty;
            }

            if (detailedPanel.Children.Count > 1 && detailedPanel.Children[1] is TextBlock timeTextBlock)
            {
                timeTextBlock.Text = string.Empty;
            }
        }
    }

    private void UnregisterEventBorder(string eventId, Border border)
    {
        if (!_eventBorders.TryGetValue(eventId, out var registrations))
        {
            return;
        }

        registrations.RemoveAll(registration => ReferenceEquals(registration.Border, border));
        if (registrations.Count == 0)
        {
            _eventBorders.Remove(eventId);
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

    private bool TryGetDayOffset(double x, out int dayOffset)
    {
        var dayColumnStart = (WeekGridHorizontalPadding / 2d) + TimeFocusedViewLayoutMetrics.TimeColumnWidth;
        if (x < dayColumnStart || _renderedDayColumnWidth <= 0)
        {
            dayOffset = -1;
            return false;
        }

        dayOffset = (int)((x - dayColumnStart) / _renderedDayColumnWidth);
        return dayOffset >= 0 && dayOffset < 7;
    }

    private DateTime GetLocalTimeFromPosition(double y, DateOnly day)
    {
        var clampedY = Math.Clamp(y, 0, TimeFocusedViewLayoutMetrics.HourRowHeight * 24);
        var minutes = clampedY / TimeFocusedViewLayoutMetrics.HourRowHeight * 60.0;
        return day.ToDateTime(TimeOnly.MinValue).AddMinutes(minutes);
    }

    private (DateTime StartLocal, DateTime EndLocal) GetDraftRange(double currentY)
    {
        if (_activeDraftCreation is null)
        {
            return default;
        }

        var day = _renderedWeekStart.AddDays(_activeDraftCreation.DayOffset);
        var currentLocal = GetLocalTimeFromPosition(currentY, day);
        var (startLocal, endLocal) = CalendarDraftTiming.SnapDragRange(_activeDraftCreation.AnchorLocal, currentLocal);
        return (
            CalendarDraftTiming.ClampToDay(startLocal, day),
            CalendarDraftTiming.ClampToDay(endLocal, day));
    }

    private void UpdateDraftPreview(Point position)
    {
        if (_activeDraftCreation is null)
        {
            return;
        }

        var (startLocal, endLocal) = GetDraftRange(position.Y);
        var day = _renderedWeekStart.AddDays(_activeDraftCreation.DayOffset);
        var top = (startLocal - day.ToDateTime(TimeOnly.MinValue)).TotalMinutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight;
        var height = Math.Max(
            TimeFocusedViewLayoutMetrics.MinDraftPreviewHeight,
            (endLocal - startLocal).TotalMinutes / 60.0 * TimeFocusedViewLayoutMetrics.HourRowHeight);
        var left = (WeekGridHorizontalPadding / 2d)
            + TimeFocusedViewLayoutMetrics.TimeColumnWidth
            + (_activeDraftCreation.DayOffset * _renderedDayColumnWidth)
            + TimeFocusedViewLayoutMetrics.DraftOverlayInset;

        _activeDraftCreation.PreviewBorder.Width = Math.Max(0, _renderedDayColumnWidth - 8);
        _activeDraftCreation.PreviewBorder.Height = height;
        Canvas.SetLeft(_activeDraftCreation.PreviewBorder, left);
        Canvas.SetTop(_activeDraftCreation.PreviewBorder, top);

        var culture = CultureInfo.CurrentCulture;
        _activeDraftCreation.TimeLabel.Text = $"{startLocal.ToString("t", culture)} – {endLocal.ToString("t", culture)}";
    }

    private (Border PreviewBorder, TextBlock TimeLabel) CreateDraftPreviewElement()
    {
        var timeLabel = new TextBlock
        {
            Foreground = new SolidColorBrush(Colors.Black),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(4, 2, 4, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
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

    private void ShowEventColorPicker(FrameworkElement target, CalendarEventDisplayModel item)
    {
        ShowEventColorPicker(target, item, new Point(0, 0));
    }

    private void ShowEventColorPicker(FrameworkElement target, CalendarEventDisplayModel item, Point position)
    {
        ShowEventColorPicker(target, item.EventId, item.SourceKind, item.ColorKey, position);
    }

    private void ShowEventColorPicker(
        FrameworkElement target,
        string eventId,
        CalendarEventSourceKind sourceKind,
        string colorKey,
        Point position)
    {
        _activeColorTarget = new ColorPickerTarget(eventId, sourceKind, colorKey, ResolveIsPending(eventId), ResolveIsPendingDelete(eventId));
        _eventColorPicker.ShowAt(target, position);
    }

    private string ResolveColorKey(string eventId)
    {
        return ViewModel.CurrentEvents
            .FirstOrDefault(item => string.Equals(item.EventId, eventId, StringComparison.Ordinal))
            ?.ColorKey
            ?? "azure";
    }

    private bool ResolveIsPending(string eventId)
    {
        return ViewModel.CurrentEvents
            .FirstOrDefault(item => string.Equals(item.EventId, eventId, StringComparison.Ordinal))
            ?.IsPending
            ?? false;
    }

    private bool ResolveIsPendingDelete(string eventId)
    {
        return ViewModel.CurrentEvents
            .FirstOrDefault(item => string.Equals(item.EventId, eventId, StringComparison.Ordinal))
            ?.IsPendingDelete
            ?? false;
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        if (hex.Length == 7 && hex[0] == '#')
        {
            try
            {
                return new SolidColorBrush(ColorHelper.FromArgb(
                    0xFF,
                    Convert.ToByte(hex.Substring(1, 2), 16),
                    Convert.ToByte(hex.Substring(3, 2), 16),
                    Convert.ToByte(hex.Substring(5, 2), 16)));
            }
            catch (FormatException)
            {
            }
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

    private DateOnly GetLocalToday()
    {
        return DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
    }

    private TimeSpan GetDelayUntilNextMinute()
    {
        var now = _timeProvider.GetLocalNow().DateTime;
        var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind).AddMinutes(1);
        var delay = nextMinute - now;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness,
        Thickness DefaultPadding);

    private sealed record DayNumberRegistration(Ellipse Highlight, Ellipse SelectedOverlay, TextBlock Label, bool IsToday);

    private sealed record ColorPickerTarget(string EventId, CalendarEventSourceKind SourceKind, string ColorKey, bool IsPending, bool IsPendingDelete = false);

    private sealed record TimedEventInteractionRegistration(
        string EventId,
        CalendarEventSourceKind SourceKind,
        DateTime StartLocal,
        DateTime EndLocal,
        int DayOffset,
        double BaseHeight,
        string? DefaultTimeText);

    private sealed record EventInteractionState(
        Border Border,
        string EventId,
        CalendarEventSourceKind SourceKind,
        EventInteractionMode Mode,
        uint PointerId,
        Point OriginPoint,
        DateTime OriginalStartLocal,
        DateTime OriginalEndLocal,
        int OriginalDayOffset,
        double BaseHeight,
        TranslateTransform Transform,
        bool SelectOnRelease = false);

    private sealed record DraftCreationState(
        uint PointerId,
        int DayOffset,
        DateTime AnchorLocal,
        Border PreviewBorder,
        TextBlock TimeLabel);

    private sealed record PreviewRangeResult(DateTime StartLocal, DateTime EndLocal, int MinuteDelta, int DayDelta);

    private enum EventInteractionMode
    {
        Move,
        Resize
    }

}

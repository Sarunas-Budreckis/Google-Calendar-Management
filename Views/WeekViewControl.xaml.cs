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

    private const double TimeColumnWidth = 72;
    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 20;
    private const double WeekGridHorizontalPadding = 24.0;
    private const double RowHeight = 72.0;
    private const double ResizeBoundaryThickness = 5.0;

    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush OverlapOutlineBrush = new SolidColorBrush(Colors.Black);
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush TodayTextBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush CurrentTimeIndicatorBrush = new SolidColorBrush(Colors.Red);
    private static readonly InputSystemCursor ResizeVerticalCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly EventDetailsPanelViewModel _eventDetailsViewModel;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private readonly Dictionary<Border, TimedEventInteractionRegistration> _interactiveTimedEventBorders = new();
    private IReadOnlyList<WeekTimedEventLayoutItem> _timedEventItems = [];
    private WeekTimedEventVirtualizingLayout _timedEventLayout = new();
    private DispatcherTimer? _currentTimeTimer;
    private DispatcherTimer? _resizeDebounceTimer;
    private DateOnly _lastObservedToday;
    private DateOnly _renderedWeekStart;
    private double _renderedDayColumnWidth;
    private EventInteractionState? _activeInteraction;

    public WeekViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        _eventDetailsViewModel = App.GetRequiredService<EventDetailsPanelViewModel>();
        _timeProvider = App.GetRequiredService<TimeProvider>();
        InitializeComponent();

        WeekHeaderGrid.Background = TransparentPanelBrush;
        WeekHeaderGrid.Tapped += WeekGrid_Tapped;
        WeekGrid.Background = TransparentPanelBrush;
        WeekGrid.Tapped += WeekGrid_Tapped;

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
        _interactiveTimedEventBorders.Clear();
        _activeInteraction = null;
        AttachFreshTimedEventsLayout();

        var viewportWidth = Math.Max(0d, ActualWidth - HorizontalChromeAllowance);
        var minimumContentWidth = TimeColumnWidth + (MinimumDayColumnWidth * 7) + WeekGridHorizontalPadding;
        var contentWidth = Math.Max(minimumContentWidth, viewportWidth);
        var availableDayWidth = (contentWidth - WeekGridHorizontalPadding - TimeColumnWidth) / 7d;

        WeekHeaderGrid.Width = contentWidth;
        WeekBodySurface.Width = contentWidth;
        WeekBodySurface.Height = RowHeight * 24;
        WeekGrid.Width = contentWidth;
        TimedEventsRepeater.Width = contentWidth;
        TimedEventsRepeater.Height = WeekBodySurface.Height;
        CurrentTimeOverlayCanvas.Width = contentWidth;
        CurrentTimeOverlayCanvas.Height = WeekBodySurface.Height;

        void AddColumns(Grid grid)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
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
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
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

            var dayNumber = new Border
            {
                Width = 28,
                Height = 28,
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
                            Text = currentDay.Day.ToString(culture),
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            Foreground = isToday
                                ? TodayTextBrush
                                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };

            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(4),
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

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private Border CreateEventChip(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var border = new Border
        {
            Padding = new Thickness(4),
            Opacity = item.Opacity,
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

        if (border.Tag is string previousGcalEventId &&
            !string.Equals(previousGcalEventId, item.GcalEventId, StringComparison.Ordinal))
        {
            UnregisterEventBorder(previousGcalEventId, border);
        }

        ConfigureTimedEventBorder(border, item);
        RegisterEventBorder(item.GcalEventId, border);
        RegisterInteractiveTimedEventBorder(border, item);

        if (string.Equals(_selectionService.SelectedGcalEventId, item.GcalEventId, StringComparison.Ordinal))
        {
            ApplySelectionState(border, _eventBorders[item.GcalEventId].Last(), isSelected: true);
        }
    }

    private void TimedEventsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not Border border || border.Tag is not string gcalEventId)
        {
            return;
        }

        UnregisterEventBorder(gcalEventId, border);
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

        border.Tag = item.GcalEventId;
        border.Opacity = item.Opacity;
        border.Height = double.NaN;
        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 0;
        }
        border.CornerRadius = ElementCornerRadius;
        border.Background = ToBrush(item.ColorHex);
        border.BorderBrush = item.UseOverlapOutline ? OverlapOutlineBrush : null;
        border.BorderThickness = item.UseOverlapOutline ? new Thickness(1) : new Thickness(0);
        border.Padding = item.IsCompact
            ? new Thickness(4, item.CompactTopPadding, 4, 0)
            : new Thickness(6);
        border.Tapped -= TimedEventBorder_Tapped;
        border.Tapped += TimedEventBorder_Tapped;
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
        if (sender is Border { Tag: string gcalEventId })
        {
            _selectionService.Select(gcalEventId);
            e.Handled = true;
        }
    }

    private void WeekGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectionService.ClearSelection();
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplySelectionVisualState(message.GcalEventId);
            RefreshInteractiveTimedEventBorders();
        });
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

        var lineStart = (WeekGridHorizontalPadding / 2d) + TimeColumnWidth + (dayOffset * _renderedDayColumnWidth);
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

        Canvas.SetLeft(dot, lineStart - 5);
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
            item.GcalEventId,
            item.Height);

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

        var isInteractive = _eventDetailsViewModel.IsEditingSelectedTimedEvent(registration.GcalEventId);
        ProtectedCursor = null;

        if (!isInteractive &&
            (_activeInteraction is null || !ReferenceEquals(_activeInteraction.Border, border)))
        {
            ResetInteractivePreview(border, registration);
        }
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
        if (sender is not Border border ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration) ||
            !_eventDetailsViewModel.TryGetEditableTimedRange(registration.GcalEventId, out var startLocal, out var endLocal))
        {
            return;
        }

        var mode = IsPointerNearResizeBoundary(e.GetCurrentPoint(border).Position, border)
            ? EventInteractionMode.Resize
            : EventInteractionMode.Move;

        var transform = border.RenderTransform as TranslateTransform ?? new TranslateTransform();
        border.RenderTransform = transform;
        border.CapturePointer(e.Pointer);
        _activeInteraction = new EventInteractionState(
            border,
            registration.GcalEventId,
            mode,
            e.Pointer.PointerId,
            e.GetCurrentPoint(WeekBodySurface).Position,
            startLocal,
            endLocal,
            registration.BaseHeight,
            transform);
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
                _interactiveTimedEventBorders.TryGetValue(hoverBorder, out var hoverRegistration) &&
                _eventDetailsViewModel.IsEditingSelectedTimedEvent(hoverRegistration.GcalEventId))
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

    private void TimedEventBorder_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border ||
            _activeInteraction is null ||
            !ReferenceEquals(_activeInteraction.Border, border) ||
            _activeInteraction.PointerId != e.Pointer.PointerId ||
            !_interactiveTimedEventBorders.TryGetValue(border, out var registration))
        {
            return;
        }

        var preview = GetPreviewRange(_activeInteraction, e.GetCurrentPoint(WeekBodySurface).Position);
        if (_activeInteraction.Mode == EventInteractionMode.Move)
        {
            _eventDetailsViewModel.ApplyDraggedTimeRange(_activeInteraction.GcalEventId, preview.StartLocal, preview.EndLocal);
        }
        else
        {
            _eventDetailsViewModel.ApplyResizedEndTime(_activeInteraction.GcalEventId, preview.EndLocal);
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

    private static bool IsPointerNearResizeBoundary(Point point, Border border)
    {
        return point.Y >= Math.Max(0, border.ActualHeight - ResizeBoundaryThickness);
    }

    private void ApplyInteractivePreview(
        EventInteractionState interaction,
        TimedEventInteractionRegistration registration,
        Point pointerPosition)
    {
        var preview = GetPreviewRange(interaction, pointerPosition);
        if (interaction.Mode == EventInteractionMode.Move)
        {
            interaction.Transform.Y = MinutesToPixels(preview.MinuteDelta);
            interaction.Border.Height = double.NaN;
        }
        else
        {
            interaction.Transform.Y = 0;
            var newHeight = Math.Max(15.0, registration.BaseHeight + MinutesToPixels(preview.MinuteDelta));
            _timedEventLayout.DragGcalEventId = interaction.GcalEventId;
            _timedEventLayout.DragHeight = newHeight;
            TimedEventsRepeater.InvalidateMeasure();
        }
    }

    private static PreviewRangeResult GetPreviewRange(EventInteractionState interaction, Point pointerPosition)
    {
        var minuteDelta = SnapMinutes((pointerPosition.Y - interaction.OriginPoint.Y) / RowHeight * 60.0);
        if (interaction.Mode == EventInteractionMode.Move)
        {
            return new PreviewRangeResult(
                interaction.OriginalStartLocal.AddMinutes(minuteDelta),
                interaction.OriginalEndLocal.AddMinutes(minuteDelta),
                minuteDelta);
        }

        var candidateEnd = RoundToNearestQuarterHour(interaction.OriginalEndLocal.AddMinutes(minuteDelta));
        var minEnd = interaction.OriginalStartLocal.AddMinutes(15);
        var maxEnd = interaction.OriginalStartLocal.Date.AddDays(1).AddHours(2);
        var clampedEnd = candidateEnd < minEnd
            ? minEnd
            : candidateEnd > maxEnd
                ? maxEnd
                : candidateEnd;

        return new PreviewRangeResult(
            interaction.OriginalStartLocal,
            clampedEnd,
            (int)Math.Round((clampedEnd - interaction.OriginalEndLocal).TotalMinutes));
    }

    private static int SnapMinutes(double rawMinutes)
    {
        return (int)Math.Round(rawMinutes / 15.0) * 15;
    }

    private static DateTime RoundToNearestQuarterHour(DateTime value)
    {
        var totalMinutes = value.Hour * 60 + value.Minute + (value.Second / 60.0);
        var snappedMinutes = (int)Math.Round(totalMinutes / 15.0) * 15;
        var dayOffset = Math.DivRem(snappedMinutes, 24 * 60, out var minuteOfDay);
        if (minuteOfDay < 0)
        {
            minuteOfDay += 24 * 60;
            dayOffset--;
        }

        return value.Date.AddDays(dayOffset).AddMinutes(minuteOfDay);
    }

    private static double MinutesToPixels(int minutes)
    {
        return minutes / 60.0 * RowHeight;
    }

    private void ResetInteractivePreview(Border border, TimedEventInteractionRegistration registration)
    {
        if (border.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 0;
        }

        ProtectedCursor = null;
        border.Height = double.NaN;
        _timedEventLayout.DragGcalEventId = null;
        _timedEventLayout.DragHeight = 0;
        TimedEventsRepeater.InvalidateMeasure();
    }

    private static void ResetTimedEventBorder(Border border)
    {
        border.Tag = null;
        border.Background = null;
        border.BorderBrush = null;
        border.BorderThickness = new Thickness(0);
        border.Padding = new Thickness(0);
        border.Height = double.NaN;
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

    private void UnregisterEventBorder(string gcalEventId, Border border)
    {
        if (!_eventBorders.TryGetValue(gcalEventId, out var registrations))
        {
            return;
        }

        registrations.RemoveAll(registration => ReferenceEquals(registration.Border, border));
        if (registrations.Count == 0)
        {
            _eventBorders.Remove(gcalEventId);
        }
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

    private sealed record TimedEventInteractionRegistration(
        string GcalEventId,
        double BaseHeight);

    private sealed record EventInteractionState(
        Border Border,
        string GcalEventId,
        EventInteractionMode Mode,
        uint PointerId,
        Point OriginPoint,
        DateTime OriginalStartLocal,
        DateTime OriginalEndLocal,
        double BaseHeight,
        TranslateTransform Transform);

    private sealed record PreviewRangeResult(DateTime StartLocal, DateTime EndLocal, int MinuteDelta);

    private enum EventInteractionMode
    {
        Move,
        Resize
    }

}

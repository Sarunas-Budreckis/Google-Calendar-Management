using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class MonthViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];
    private static CornerRadius MediumCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusMedium"];
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush TodayTextBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private readonly List<DispatcherQueueTimer> _tooltipTimers = [];
    private DispatcherTimer? _todayRefreshTimer;
    private DateOnly _lastObservedToday;

    public MonthViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        _timeProvider = App.GetRequiredService<TimeProvider>();
        InitializeComponent();
        MonthGrid.Background = TransparentPanelBrush;
        MonthGrid.Tapped += MonthGrid_Tapped;
        MoreEventsPopup.Closed += (_, _) =>
        {
            StopTooltipTimers();
            MoreEventsPopup.Child = null;
        };
        Loaded += MonthViewControl_Loaded;
        Unloaded += MonthViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void MonthViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<MonthViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<MonthViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());
        _lastObservedToday = GetLocalToday();
        StartTodayRefreshTimer();
        Rebuild();
    }

    private void MonthViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        StopTodayRefreshTimer();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        CloseMoreEventsPopup();
        StopTooltipTimers();
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
        CloseMoreEventsPopup();
        StopTooltipTimers();
        MonthGrid.Children.Clear();
        MonthGrid.RowDefinitions.Clear();
        MonthGrid.ColumnDefinitions.Clear();
        _eventBorders.Clear();

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
            MonthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthViewLayoutMetrics.WeekRowHeight) });
        }

        var culture = CultureInfo.CurrentCulture;
        for (var row = 0; row < totalRows; row++)
        {
            var weekStart = gridStart.AddDays(row * 7);
            var weekLayout = MonthViewLayoutPlanner.BuildWeekLayout(weekStart, ViewModel.CurrentEvents);
            var weekGrid = BuildWeekRowGrid(weekStart, firstDay.Month, weekLayout, culture, ViewModel.SyncStatusMap);
            Grid.SetRow(weekGrid, row);
            Grid.SetColumnSpan(weekGrid, 7);
            MonthGrid.Children.Add(weekGrid);
        }

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private Grid BuildWeekRowGrid(
        DateOnly weekStart,
        int activeMonth,
        MonthWeekLayout weekLayout,
        CultureInfo culture,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap)
    {
        var grid = new Grid();
        var visibleTrackCount = weekLayout.VisibleAllDayTracks.Count == 0
            ? 0
            : weekLayout.VisibleAllDayTracks.Max(track => track.TrackIndex) + 1;
        var totalRows = visibleTrackCount + 2;

        for (var i = 0; i < 7; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthViewLayoutMetrics.DayHeaderHeight) });
        for (var trackIndex = 0; trackIndex < visibleTrackCount; trackIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthViewLayoutMetrics.AllDayTrackHeight) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (var col = 0; col < 7; col++)
        {
            var date = weekStart.AddDays(col);
            var dayCell = CreateDayCell(
                date,
                activeMonth,
                visibleTrackCount,
                totalRows,
                culture,
                syncStatusMap,
                weekLayout.DayLayouts[date]);
            Grid.SetColumn(dayCell, col);
            Grid.SetRowSpan(dayCell, totalRows);
            grid.Children.Add(dayCell);
        }

        foreach (var track in weekLayout.VisibleAllDayTracks)
        {
            var eventBlock = CreateAllDayEventBlock(
                track.Event,
                track.ContinuesFromLeft,
                track.ContinuesToRight,
                culture);
            Grid.SetColumn(eventBlock, track.ColumnStart);
            Grid.SetRow(eventBlock, track.TrackIndex + 1);
            Grid.SetColumnSpan(eventBlock, track.ColumnEnd - track.ColumnStart + 1);
            grid.Children.Add(eventBlock);
        }

        return grid;
    }

    private Grid CreateDayCell(
        DateOnly date,
        int activeMonth,
        int visibleTrackCount,
        int totalRows,
        CultureInfo culture,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap,
        MonthDayLayout dayLayout)
    {
        var isToday = CalendarViewVisualStateCalculator.IsToday(date, _timeProvider.GetLocalNow().DateTime);
        var isSynced = syncStatusMap.TryGetValue(date, out var syncStatus) && syncStatus == SyncStatus.Synced;
        var dayCell = new Grid
        {
            Background = TransparentPanelBrush
        };
        dayCell.Tapped += DayCellBackground_Tapped;

        dayCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthViewLayoutMetrics.DayHeaderHeight) });
        for (var trackIndex = 0; trackIndex < visibleTrackCount; trackIndex++)
        {
            dayCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(MonthViewLayoutMetrics.AllDayTrackHeight) });
        }

        dayCell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var background = new Border
        {
            Margin = new Thickness(4),
            CornerRadius = MediumCornerRadius,
            Opacity = date.Month == activeMonth ? 1.0 : 0.35,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1)
        };
        Grid.SetRowSpan(background, totalRows);
        dayCell.Children.Add(background);

        var headerGrid = new Grid
        {
            Margin = new Thickness(8, 6, 8, 0)
        };
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        var syncDot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(isSynced ? SyncedColor : NotSyncedColor),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(syncDot, ViewModel.LastSyncTooltip);
        headerPanel.Children.Add(syncDot);

        headerPanel.Children.Add(new Border
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
                        Text = date.Day.ToString(culture),
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
        headerGrid.Children.Add(headerPanel);

        Grid.SetRow(headerGrid, 0);
        dayCell.Children.Add(headerGrid);

        var contentGrid = new Grid
        {
            Margin = new Thickness(8, 4, 8, 8)
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var timedEventsRepeater = CreateTimedEventsRepeater(dayLayout.VisibleTimedEvents);
        Grid.SetRow(timedEventsRepeater, 0);
        contentGrid.Children.Add(timedEventsRepeater);

        if (dayLayout.OverflowCount > 0)
        {
            var moreButton = new HyperlinkButton
            {
                Content = $"+{dayLayout.OverflowCount} more",
                Padding = new Thickness(0),
                Height = MonthViewLayoutMetrics.MoreLinkHeight,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            moreButton.Click += (_, _) => ShowMoreEventsPopup(background, dayLayout, culture);
            Grid.SetRow(moreButton, 1);
            contentGrid.Children.Add(moreButton);
        }

        Grid.SetRow(contentGrid, totalRows - 1);
        dayCell.Children.Add(contentGrid);
        return dayCell;
    }

    private Border CreateAllDayEventBlock(
        CalendarEventDisplayModel item,
        bool continuesFromLeft,
        bool continuesToRight,
        CultureInfo culture)
    {
        var corner = ElementCornerRadius;
        var block = new Border
        {
            Height = 20,
            Opacity = item.Opacity,
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(continuesFromLeft ? 4 : 8, 2, continuesToRight ? 4 : 8, 2),
            CornerRadius = new CornerRadius(
                continuesFromLeft ? 0 : corner.TopLeft,
                continuesToRight ? 0 : corner.TopRight,
                continuesToRight ? 0 : corner.BottomRight,
                continuesFromLeft ? 0 : corner.BottomLeft),
            Background = ToBrush(item.ColorHex),
            BorderBrush = TransparentPanelBrush,
            BorderThickness = new Thickness(0),
            Child = new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            }
        };

        ToolTipService.SetToolTip(block, BuildTooltipText(item, culture));
        block.Tapped += (_, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            e.Handled = true;
        };

        RegisterEventBorder(item.GcalEventId, block);
        return block;
    }

    private ItemsRepeater CreateTimedEventsRepeater(IReadOnlyList<CalendarEventDisplayModel> timedEvents)
    {
        var repeater = new ItemsRepeater
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = (DataTemplate)Resources["MonthTimedEventTemplate"],
            ItemsSource = timedEvents,
            Layout = new StackLayout
            {
                Spacing = MonthViewLayoutMetrics.TimedRowSpacing
            }
        };

        repeater.ElementPrepared += MonthTimedEventsRepeater_ElementPrepared;
        repeater.ElementClearing += MonthTimedEventsRepeater_ElementClearing;
        return repeater;
    }

    private void MonthTimedEventsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Border border)
        {
            return;
        }

        var item = GetTimedEventDisplayModel(sender, args.Index);
        if (item is null)
        {
            ResetTimedEventRow(border);
            return;
        }

        if (border.Tag is string previousGcalEventId &&
            !string.Equals(previousGcalEventId, item.GcalEventId, StringComparison.Ordinal))
        {
            UnregisterEventBorder(previousGcalEventId, border);
        }

        ConfigureTimedEventRow(border, item, CultureInfo.CurrentCulture);
        RegisterEventBorder(item.GcalEventId, border);

        if (_selectionService.SelectedGcalEventId is string selectedGcalEventId &&
            string.Equals(selectedGcalEventId, item.GcalEventId, StringComparison.Ordinal))
        {
            ApplySelectionState(border, _eventBorders[item.GcalEventId].Last(), isSelected: true);
        }
    }

    private void MonthTimedEventsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not Border border || border.Tag is not string gcalEventId)
        {
            return;
        }

        UnregisterEventBorder(gcalEventId, border);
        ResetTimedEventRow(border);
    }

    private void ConfigureTimedEventRow(Border border, CalendarEventDisplayModel item, CultureInfo culture)
    {
        if (border.Child is not Grid layoutRoot || layoutRoot.Children.Count < 3)
        {
            return;
        }

        if (layoutRoot.Children[0] is not Ellipse dot ||
            layoutRoot.Children[1] is not TextBlock timeTextBlock ||
            layoutRoot.Children[2] is not TextBlock titleTextBlock)
        {
            return;
        }

        border.Tag = item.GcalEventId;
        border.Height = MonthViewLayoutMetrics.TimedRowHeight;
        border.Padding = new Thickness(4, 2, 4, 2);
        border.CornerRadius = ElementCornerRadius;
        border.Background = TransparentPanelBrush;
        border.BorderBrush = TransparentPanelBrush;
        border.BorderThickness = new Thickness(0);
        border.Tapped -= TimedEventRow_Tapped;
        border.Tapped += TimedEventRow_Tapped;
        ToolTipService.SetToolTip(border, BuildTooltipText(item, culture));

        dot.Fill = ToBrush(item.ColorHex);
        timeTextBlock.Text = item.StartLocal.ToString("h:mm tt", culture);
        titleTextBlock.Text = item.Title;
    }

    private void TimedEventRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: string gcalEventId })
        {
            _selectionService.Select(gcalEventId);
            e.Handled = true;
        }
    }

    private void ResetTimedEventRow(Border border)
    {
        border.Tag = null;
        border.Background = null;
        border.BorderBrush = null;
        border.BorderThickness = new Thickness(0);
        border.Padding = new Thickness(0);
        border.Tapped -= TimedEventRow_Tapped;
        ToolTipService.SetToolTip(border, null);

        if (border.Child is not Grid layoutRoot || layoutRoot.Children.Count < 3)
        {
            return;
        }

        if (layoutRoot.Children[0] is Ellipse dot)
        {
            dot.Fill = null;
        }

        if (layoutRoot.Children[1] is TextBlock timeTextBlock)
        {
            timeTextBlock.Text = string.Empty;
        }

        if (layoutRoot.Children[2] is TextBlock titleTextBlock)
        {
            titleTextBlock.Text = string.Empty;
        }
    }

    private void ShowMoreEventsPopup(Border dayCard, MonthDayLayout dayLayout, CultureInfo culture)
    {
        if (dayCard.XamlRoot is null || RootLayout.ActualWidth <= 0 || RootLayout.ActualHeight <= 0)
        {
            return;
        }

        var cardOrigin = dayCard.TransformToVisual(RootLayout).TransformPoint(new Point(0, 0));
        var popupWidth = Math.Max(180, dayCard.ActualWidth);
        var popupMinHeight = Math.Max(120, dayCard.ActualHeight);
        var availableHeight = Math.Max(180, RootLayout.ActualHeight - cardOrigin.Y - 12);

        MoreEventsPopup.Child = BuildMoreEventsPopupContent(
            dayLayout,
            culture,
            popupWidth,
            popupMinHeight,
            availableHeight);
        MoreEventsPopup.HorizontalOffset = Math.Max(0, Math.Min(cardOrigin.X, RootLayout.ActualWidth - popupWidth));
        MoreEventsPopup.VerticalOffset = Math.Max(0, cardOrigin.Y);
        MoreEventsPopup.IsOpen = true;
    }

    private FrameworkElement BuildMoreEventsPopupContent(
        MonthDayLayout dayLayout,
        CultureInfo culture,
        double popupWidth,
        double popupMinHeight,
        double popupMaxHeight)
    {
        var eventList = new StackPanel
        {
            Spacing = 2
        };

        foreach (var allDayEvent in dayLayout.AllDayEvents)
        {
            eventList.Children.Add(CreatePopupAllDayEventBlock(allDayEvent, culture));
        }

        foreach (var timedEvent in dayLayout.TimedEvents)
        {
            eventList.Children.Add(CreatePopupTimedEventRow(timedEvent, culture));
        }

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        contentGrid.Children.Add(new TextBlock
        {
            Margin = new Thickness(12, 10, 12, 6),
            Text = dayLayout.Date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d", culture),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var eventsScrollViewer = new ScrollViewer
        {
            Margin = new Thickness(8, 0, 8, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = eventList
        };
        Grid.SetRow(eventsScrollViewer, 1);
        contentGrid.Children.Add(eventsScrollViewer);

        return new Border
        {
            Width = popupWidth,
            MinHeight = popupMinHeight,
            MaxHeight = popupMaxHeight,
            CornerRadius = MediumCornerRadius,
            Background = GetOpaquePopupBackgroundBrush(),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = contentGrid
        };
    }

    private Border CreatePopupAllDayEventBlock(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var block = new Border
        {
            Padding = new Thickness(6, 4, 6, 4),
            CornerRadius = ElementCornerRadius,
            Background = ToBrush(item.ColorHex),
            Child = new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            }
        };

        AttachInstantTooltip(block, BuildTooltipText(item, culture));
        block.Tapped += (_, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            CloseMoreEventsPopup();
            e.Handled = true;
        };
        return block;
    }

    private Border CreatePopupTimedEventRow(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var row = new Border
        {
            Padding = new Thickness(4),
            CornerRadius = ElementCornerRadius,
            Background = TransparentPanelBrush
        };

        var layoutRoot = new Grid();
        layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        layoutRoot.Children.Add(new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = ToBrush(item.ColorHex),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        var timeText = new TextBlock
        {
            Text = item.StartLocal.ToString("h:mm tt", culture),
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeText, 1);
        layoutRoot.Children.Add(timeText);

        var titleText = new TextBlock
        {
            Text = item.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 2);
        layoutRoot.Children.Add(titleText);

        row.Child = layoutRoot;
        row.Opacity = item.Opacity;
        AttachInstantTooltip(row, BuildTooltipText(item, culture));
        row.Tapped += (_, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            CloseMoreEventsPopup();
            e.Handled = true;
        };
        return row;
    }

    private void CloseMoreEventsPopup()
    {
        if (!MoreEventsPopup.IsOpen && MoreEventsPopup.Child is null)
        {
            return;
        }

        MoreEventsPopup.IsOpen = false;
        MoreEventsPopup.Child = null;
    }

    private void AttachInstantTooltip(UIElement element, string content)
    {
        var tooltip = new ToolTip
        {
            Content = content
        };
        ToolTipService.SetToolTip(element, tooltip);
        ConfigureTooltipDelay(element, tooltip);
    }

    private void ConfigureTooltipDelay(UIElement element, ToolTip tooltip)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.Zero;
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tooltip.IsOpen = true;
        };
        _tooltipTimers.Add(timer);

        element.PointerEntered += (_, _) =>
        {
            tooltip.IsOpen = false;
            timer.Stop();
            timer.Start();
        };

        element.PointerExited += (_, _) =>
        {
            timer.Stop();
            tooltip.IsOpen = false;
        };

        element.PointerCanceled += (_, _) =>
        {
            timer.Stop();
            tooltip.IsOpen = false;
        };

        element.PointerCaptureLost += (_, _) =>
        {
            timer.Stop();
            tooltip.IsOpen = false;
        };
    }

    private void StopTooltipTimers()
    {
        foreach (var timer in _tooltipTimers)
        {
            timer.Stop();
        }

        _tooltipTimers.Clear();
    }

    private void StartTodayRefreshTimer()
    {
        if (_todayRefreshTimer is not null)
        {
            _todayRefreshTimer.Start();
            return;
        }

        _todayRefreshTimer = new DispatcherTimer();
        _todayRefreshTimer.Tick += TodayRefreshTimer_Tick;
        _todayRefreshTimer.Interval = GetDelayUntilNextMinute();
        _todayRefreshTimer.Start();
    }

    private void StopTodayRefreshTimer()
    {
        if (_todayRefreshTimer is null)
        {
            return;
        }

        _todayRefreshTimer.Stop();
        _todayRefreshTimer.Tick -= TodayRefreshTimer_Tick;
        _todayRefreshTimer = null;
    }

    private void TodayRefreshTimer_Tick(object? sender, object e)
    {
        if (_todayRefreshTimer is not null)
        {
            _todayRefreshTimer.Interval = TimeSpan.FromMinutes(1);
        }

        var today = GetLocalToday();
        if (today == _lastObservedToday)
        {
            return;
        }

        _lastObservedToday = today;
        Rebuild();
    }

    private void DayCellBackground_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private void MonthGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        CloseMoreEventsPopup();
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

    private static CalendarEventDisplayModel? GetTimedEventDisplayModel(ItemsRepeater repeater, int index)
    {
        return repeater.ItemsSourceView?.GetAt(index) as CalendarEventDisplayModel;
    }

    private static string BuildTooltipText(CalendarEventDisplayModel item, CultureInfo culture)
    {
        return item.IsAllDay
            ? $"{item.Title}\nAll day"
            : $"{item.Title}\n{item.StartLocal.ToString("g", culture)} - {item.EndLocal.ToString("g", culture)}";
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        return TryParseHexColor(hex, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x00, 0x88, 0xCC));
    }

    private Brush GetOpaquePopupBackgroundBrush()
    {
        return ActualTheme == ElementTheme.Dark
            ? new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x20, 0x20, 0x20))
            : new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = default;

        if (hex.Length != 7 || hex[0] != '#' ||
            !byte.TryParse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
        {
            return false;
        }

        color = ColorHelper.FromArgb(0xFF, red, green, blue);
        return true;
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

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness,
        Thickness DefaultPadding);
}

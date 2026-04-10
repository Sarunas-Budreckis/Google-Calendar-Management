using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class YearViewControl : Page
{
    private const int RenderPrewarmRadius = 5;
    private const int MaxProjectionCacheEntries = 16;
    private const int MaxRenderCacheEntries = 16;
    private static readonly CornerRadius YearViewCornerRadius = new(4);
    private const double PreviewBarHeight = 11;
    private const double PreviewBarFontSize = 8;
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TodayHighlightBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4E, 0x8F, 0xD8));
    private static readonly Brush TodayHighlightStrokeBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x73, 0xA8, 0xE4));
    private static readonly Brush TodayTextBrush = new SolidColorBrush(Colors.White);
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<YearViewControl> _logger;
    private readonly SharedTooltipManager _tooltipManager;
    private Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ProjectionCacheEntry> _projectionCache = [];
    private long _projectionCacheAccessSequence;
    private readonly Dictionary<int, RenderCacheEntry> _renderCache = [];
    private long _renderCacheAccessSequence;
    private RenderCacheEntry? _currentRenderState;
    private CancellationTokenSource? _renderPrewarmCts;
    private DispatcherTimer? _todayRefreshTimer;
    private DateOnly _lastObservedToday;

    public YearViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        _timeProvider = App.GetRequiredService<TimeProvider>();
        _logger = App.GetRequiredService<ILogger<YearViewControl>>();
        InitializeComponent();
        _tooltipManager = new SharedTooltipManager(DispatcherQueue);
        MonthsGrid.Background = TransparentPanelBrush;
        Loaded += YearViewControl_Loaded;
        Unloaded += YearViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void YearViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<YearViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<YearViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());
        _lastObservedToday = GetLocalToday();
        StartTodayRefreshTimer();
        Rebuild();
    }

    private void YearViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        StopTodayRefreshTimer();
        CancelRenderPrewarm();
        _tooltipManager.Reset();
        _eventBorders.Clear();
        _projectionCache.Clear();
        _renderCache.Clear();
        _currentRenderState = null;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentDate) && ShouldSkipCurrentDateRebuild())
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SyncStatusMap) && ShouldSkipSyncStatusRebuild())
        {
            _logger.LogDebug(
                "YearViewControl skipped intermediate SyncStatusMap rebuild for target year {TargetYear} while rendered year {RenderedYear} is still active.",
                ViewModel.CurrentDate.Year,
                _currentRenderState?.Year);
            return;
        }

        if (e.PropertyName is nameof(MainViewModel.CurrentDate)
            or nameof(MainViewModel.CurrentEvents)
            or nameof(MainViewModel.SyncStatusMap))
        {
            Rebuild();
        }
    }

    private void OnSyncCompleted()
    {
        _ = DispatcherQueue.TryEnqueue(Rebuild);
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplySelectionVisualState(message.GcalEventId));
    }

    private void Rebuild()
    {
        var totalTimer = Stopwatch.StartNew();
        CancelRenderPrewarm();
        EnsureMonthsGridStructure();

        if (TryActivateCachedRenderState(ViewModel.CurrentDate.Year))
        {
            totalTimer.Stop();
            _logger.LogInformation(
                "YearViewControl render cache hit for year {Year}. Reattach completed in {ElapsedMs}ms.",
                ViewModel.CurrentDate.Year,
                totalTimer.ElapsedMilliseconds);
            ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
            ScheduleRenderPrewarm();
            return;
        }

        var resetTimer = Stopwatch.StartNew();
        _tooltipManager.Reset();
        MonthsGrid.Children.Clear();
        _eventBorders = new Dictionary<string, List<EventBorderRegistration>>(StringComparer.Ordinal);
        resetTimer.Stop();

        var culture = CultureInfo.CurrentCulture;
        var projectionTimer = Stopwatch.StartNew();
        var (projection, projectionCacheHit) = GetProjectionForYear(
            ViewModel.CurrentDate.Year,
            ViewModel.CurrentEvents,
            ViewModel.SyncStatusMap);
        projectionTimer.Stop();

        var buildTimer = Stopwatch.StartNew();
        var renderState = CreateRenderState(
            ViewModel.CurrentDate.Year,
            ViewModel.CurrentEvents,
            ViewModel.SyncStatusMap,
            culture,
            projection,
            attachToGrid: true);
        buildTimer.Stop();

        CacheAndActivateRenderState(renderState);

        totalTimer.Stop();
        _logger.LogInformation(
            "YearViewControl rebuilt year {Year}. ProjectionCacheHit={ProjectionCacheHit}. Reset={ResetMs}ms Projection={ProjectionMs}ms Build={BuildMs}ms Total={TotalMs}ms.",
            ViewModel.CurrentDate.Year,
            projectionCacheHit,
            resetTimer.ElapsedMilliseconds,
            projectionTimer.ElapsedMilliseconds,
            buildTimer.ElapsedMilliseconds,
            totalTimer.ElapsedMilliseconds);

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
        ScheduleRenderPrewarm();
    }

    private bool ShouldSkipCurrentDateRebuild()
    {
        if (_currentRenderState is null)
        {
            return false;
        }

        if (_currentRenderState.Year == ViewModel.CurrentDate.Year)
        {
            return true;
        }

        return ReferenceEquals(_currentRenderState.Events, ViewModel.CurrentEvents) &&
               ReferenceEquals(_currentRenderState.SyncStatusMap, ViewModel.SyncStatusMap);
    }

    private bool ShouldSkipSyncStatusRebuild()
    {
        return _currentRenderState is not null &&
               _currentRenderState.Year != ViewModel.CurrentDate.Year;
    }

    private void EnsureMonthsGridStructure()
    {
        if (MonthsGrid.RowDefinitions.Count == 4 && MonthsGrid.ColumnDefinitions.Count == 3)
        {
            return;
        }

        MonthsGrid.RowDefinitions.Clear();
        MonthsGrid.ColumnDefinitions.Clear();

        for (var row = 0; row < 4; row++)
        {
            MonthsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var column = 0; column < 3; column++)
        {
            MonthsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
    }

    private bool TryActivateCachedRenderState(int year)
    {
        return TryGetRenderableState(year, ViewModel.CurrentEvents, ViewModel.SyncStatusMap, out var cachedState) &&
               ActivateCachedRenderState(cachedState);
    }

    private bool TryGetRenderableState(
        int year,
        IList<CalendarEventDisplayModel> events,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap,
        out RenderCacheEntry renderState)
    {
        if (!_renderCache.TryGetValue(year, out var cachedState) ||
            !ReferenceEquals(cachedState.Events, events) ||
            !ReferenceEquals(cachedState.SyncStatusMap, syncStatusMap))
        {
            renderState = null!;
            return false;
        }

        renderState = cachedState;
        return true;
    }

    private bool ActivateCachedRenderState(RenderCacheEntry cachedState)
    {
        cachedState.AccessSequence = ++_renderCacheAccessSequence;
        ActivateRenderState(cachedState);
        return true;
    }

    private void ActivateRenderState(RenderCacheEntry renderState)
    {
        _tooltipManager.Reset();
        MonthsGrid.Children.Clear();

        foreach (var monthPanel in renderState.MonthPanels)
        {
            MonthsGrid.Children.Add(monthPanel);
        }

        _currentRenderState = renderState;
        _eventBorders = renderState.EventBorders;
    }

    private void CacheAndActivateRenderState(RenderCacheEntry renderState)
    {
        CacheRenderState(renderState);
        ActivateRenderState(renderState);
    }

    private void CacheRenderState(RenderCacheEntry renderState)
    {
        _renderCache[renderState.Year] = renderState;
        TrimRenderCache();
    }

    private void TrimRenderCache()
    {
        while (_renderCache.Count > MaxRenderCacheEntries)
        {
            var oldestYear = _renderCache
                .OrderBy(static pair => pair.Value.AccessSequence)
                .Select(static pair => pair.Key)
                .First();

            _renderCache.Remove(oldestYear);
        }
    }

    private (YearViewProjectionResult Projection, bool CacheHit) GetProjectionForYear(
        int year,
        IList<CalendarEventDisplayModel> events,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap)
    {
        if (_projectionCache.TryGetValue(year, out var cachedEntry) &&
            ReferenceEquals(cachedEntry.Events, events) &&
            ReferenceEquals(cachedEntry.SyncStatusMap, syncStatusMap))
        {
            cachedEntry.AccessSequence = ++_projectionCacheAccessSequence;
            return (cachedEntry.Projection, true);
        }

        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var projection = YearViewDayProjectionBuilder.Build(
            EnumerateDates(yearStart, yearEnd),
            events,
            syncStatusMap);

        _projectionCache[year] = new ProjectionCacheEntry(
            events,
            syncStatusMap,
            projection,
            ++_projectionCacheAccessSequence);
        TrimProjectionCache();
        return (projection, false);
    }

    private RenderCacheEntry CreateRenderState(
        int year,
        IList<CalendarEventDisplayModel> events,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap,
        CultureInfo culture,
        YearViewProjectionResult projection,
        bool attachToGrid)
    {
        var eventBorders = new Dictionary<string, List<EventBorderRegistration>>(StringComparer.Ordinal);
        var renderContext = new RenderBuildContext(eventBorders);
        var monthPanels = new List<UIElement>(12);

        for (var month = 1; month <= 12; month++)
        {
            var monthBorder = BuildMonthPanel(new DateOnly(year, month, 1), culture, projection, renderContext);
            Grid.SetRow(monthBorder, (month - 1) / 3);
            Grid.SetColumn(monthBorder, (month - 1) % 3);

            if (attachToGrid)
            {
                MonthsGrid.Children.Add(monthBorder);
            }

            monthPanels.Add(monthBorder);
        }

        return new RenderCacheEntry(
            year,
            events,
            syncStatusMap,
            monthPanels,
            eventBorders,
            ++_renderCacheAccessSequence);
    }

    private void ScheduleRenderPrewarm()
    {
        CancelRenderPrewarm();
        _renderPrewarmCts = new CancellationTokenSource();
        _ = PrewarmRenderCacheAsync(ViewModel.CurrentDate.Year, _renderPrewarmCts.Token);
    }

    private void CancelRenderPrewarm()
    {
        if (_renderPrewarmCts is null)
        {
            return;
        }

        _renderPrewarmCts.Cancel();
        _renderPrewarmCts.Dispose();
        _renderPrewarmCts = null;
    }

    private async Task PrewarmRenderCacheAsync(int selectedYear, CancellationToken ct)
    {
        var actualCurrentYear = GetLocalToday().Year;
        var candidateYears = new HashSet<int>();

        for (var year = selectedYear - RenderPrewarmRadius; year <= selectedYear + RenderPrewarmRadius; year++)
        {
            candidateYears.Add(year);
        }

        candidateYears.Add(actualCurrentYear);
        candidateYears.Remove(selectedYear);

        foreach (var year in candidateYears
                     .OrderBy(year => Math.Abs(year - selectedYear))
                     .ThenBy(static year => year))
        {
            ct.ThrowIfCancellationRequested();

            var snapshot = await ViewModel.EnsureYearViewDataAsync(year, ct);
            ct.ThrowIfCancellationRequested();

            if (TryGetRenderableState(year, snapshot.Events, snapshot.SyncStatusMap, out _))
            {
                continue;
            }

            await WaitForLowPriorityTurnAsync(ct);
            ct.ThrowIfCancellationRequested();

            if (TryGetRenderableState(year, snapshot.Events, snapshot.SyncStatusMap, out _))
            {
                continue;
            }

            var timer = Stopwatch.StartNew();
            var culture = CultureInfo.CurrentCulture;
            var (projection, _) = GetProjectionForYear(year, snapshot.Events, snapshot.SyncStatusMap);
            var renderState = CreateRenderState(
                year,
                snapshot.Events,
                snapshot.SyncStatusMap,
                culture,
                projection,
                attachToGrid: false);
            CacheRenderState(renderState);
            timer.Stop();

            _logger.LogDebug(
                "YearViewControl prewarmed render cache for year {Year} in {ElapsedMs}ms.",
                year,
                timer.ElapsedMilliseconds);
        }
    }

    private Task WaitForLowPriorityTurnAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        if (!DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                registration.Dispose();
                tcs.TrySetResult();
            }))
        {
            registration.Dispose();
            tcs.TrySetResult();
        }

        return tcs.Task;
    }

    private void TrimProjectionCache()
    {
        while (_projectionCache.Count > MaxProjectionCacheEntries)
        {
            var oldestYear = _projectionCache
                .OrderBy(static pair => pair.Value.AccessSequence)
                .Select(static pair => pair.Key)
                .First();

            _projectionCache.Remove(oldestYear);
        }
    }

    private Border BuildMonthPanel(
        DateOnly firstDay,
        CultureInfo culture,
        YearViewProjectionResult projection,
        RenderBuildContext renderContext)
    {
        var lastDay = new DateOnly(firstDay.Year, firstDay.Month, DateTime.DaysInMonth(firstDay.Year, firstDay.Month));
        var gridStart = StartOfWeek(firstDay);
        var gridEnd = EndOfWeek(lastDay);

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = firstDay.ToDateTime(TimeOnly.MinValue).ToString("MMMM", culture),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"]
        });

        var monthGrid = new Grid();
        for (var column = 0; column < 7; column++)
        {
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var totalRows = ((gridEnd.DayNumber - gridStart.DayNumber) / 7) + 1;
        for (var row = 0; row < totalRows; row++)
        {
            monthGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var weekStart = gridStart.AddDays(row * 7);
            var weekGrid = BuildWeekRowGrid(weekStart, firstDay.Month, culture, projection, renderContext);
            Grid.SetRow(weekGrid, row);
            Grid.SetColumnSpan(weekGrid, 7);
            monthGrid.Children.Add(weekGrid);
        }

        panel.Children.Add(monthGrid);
        return new Border
        {
            Margin = new Thickness(0, 0, 12, 12),
            Padding = new Thickness(10),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = YearViewCornerRadius,
            Child = panel
        };
    }

    private Grid BuildWeekRowGrid(
        DateOnly weekStart,
        int activeMonth,
        CultureInfo culture,
        YearViewProjectionResult projection,
        RenderBuildContext renderContext)
    {
        var weekGrid = new Grid();
        for (var column = 0; column < 7; column++)
        {
            weekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var column = 0; column < 7; column++)
        {
            var date = weekStart.AddDays(column);
            var isInActiveMonth = date.Month == activeMonth;
            var dayLookup = projection.DayLookup;

            weekGrid.Children.Add(BuildDayBackground(date, activeMonth, column));

            if (!isInActiveMonth || !dayLookup.TryGetValue(date, out var displayModel))
            {
                continue;
            }

            var header = BuildDayHeader(date, culture, displayModel, CalendarViewVisualStateCalculator.IsToday(date, _timeProvider.GetLocalNow().DateTime));
            Grid.SetColumn(header, column);
            Grid.SetRow(header, 0);
            weekGrid.Children.Add(header);

            var singleDayBar = BuildPreviewBar(displayModel.SingleDayAllDayBar, renderContext);
            Grid.SetColumn(singleDayBar, column);
            Grid.SetRow(singleDayBar, 1);
            weekGrid.Children.Add(singleDayBar);
        }

        var row2Placeholder = new Border { Height = PreviewBarHeight, IsHitTestVisible = false };
        Grid.SetRow(row2Placeholder, 2);
        Grid.SetColumnSpan(row2Placeholder, 7);
        weekGrid.Children.Add(row2Placeholder);

        foreach (var segment in BuildWeekSegments(weekStart, activeMonth, projection.DayLookup))
        {
            var multiDayBar = BuildPreviewBar(segment.Bar, renderContext);
            Grid.SetColumn(multiDayBar, segment.StartColumn);
            Grid.SetRow(multiDayBar, 2);
            Grid.SetColumnSpan(multiDayBar, segment.ColumnSpan);
            weekGrid.Children.Add(multiDayBar);
        }

        return weekGrid;
    }

    private static Border BuildDayBackground(DateOnly date, int activeMonth, int column)
    {
        var isInActiveMonth = date.Month == activeMonth;
        var background = new Border
        {
            Background = isInActiveMonth
                ? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]
                : TransparentPanelBrush,
            BorderBrush = isInActiveMonth
                ? (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
                : TransparentPanelBrush,
            BorderThickness = isInActiveMonth ? new Thickness(1) : new Thickness(0),
            CornerRadius = YearViewCornerRadius,
            MinHeight = 48
        };

        Grid.SetColumn(background, column);
        Grid.SetRowSpan(background, 3);
        return background;
    }

    private static Grid BuildDayHeader(
        DateOnly date,
        CultureInfo culture,
        YearViewDayDisplayModel displayModel,
        bool isToday)
    {
        var header = new Grid
        {
            Margin = new Thickness(2, 2, 2, 1),
            IsHitTestVisible = false
        };

        if (displayModel.SyncDotPlacement == YearViewSyncDotPlacement.Trailing)
        {
            header.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(displayModel.SyncStatus == SyncStatus.Synced ? SyncedColor : NotSyncedColor),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        header.Children.Add(new Border
        {
            Width = 20,
            Height = 20,
            Background = TransparentPanelBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
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
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                        Foreground = isToday
                            ? TodayTextBrush
                            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 11
                    }
                }
            }
        });

        return header;
    }

    private Border BuildPreviewBar(YearViewPreviewBarDisplayModel bar, RenderBuildContext renderContext)
    {
        var previewBar = new Border
        {
            Height = PreviewBarHeight,
            Margin = new Thickness(1, 0, 1, 1),
            Padding = bar.HasContent ? new Thickness(3, 0, 3, 0) : new Thickness(0),
            CornerRadius = YearViewCornerRadius,
            Background = bar.HasContent && bar.ColorHex is not null
                ? ToBrush(bar.ColorHex)
                : TransparentPanelBrush,
            BorderBrush = TransparentPanelBrush,
            BorderThickness = new Thickness(0),
            IsHitTestVisible = bar.HasContent
        };

        if (bar.HasContent && !string.IsNullOrWhiteSpace(bar.SummaryText))
        {
            previewBar.Child = new TextBlock
            {
                Text = bar.SummaryText,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = PreviewBarFontSize,
                VerticalAlignment = VerticalAlignment.Center,
                MaxLines = 1,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        if (bar.HasContent && bar.GcalEventId is not null)
        {
            RegisterEventBorder(bar.GcalEventId, previewBar, renderContext);
            ConfigurePreviewBarInteractions(previewBar, bar, renderContext);
        }

        return previewBar;
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

    private static IEnumerable<DateOnly> EnumerateDates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static IReadOnlyList<YearViewMultiDaySegmentDisplayModel> BuildWeekSegments(
        DateOnly weekStart,
        int activeMonth,
        IReadOnlyDictionary<DateOnly, YearViewDayDisplayModel> dayLookup)
    {
        var segments = new List<YearViewMultiDaySegmentDisplayModel>();

        for (var column = 0; column < 7; column++)
        {
            var date = weekStart.AddDays(column);
            if (date.Month != activeMonth ||
                !dayLookup.TryGetValue(date, out var dayDisplay) ||
                !dayDisplay.MultiDayAllDayBar.HasContent ||
                dayDisplay.MultiDayAllDayBar.GcalEventId is null)
            {
                continue;
            }

            var segmentStartColumn = column;
            var bar = dayDisplay.MultiDayAllDayBar;
            while (column + 1 < 7)
            {
                var nextDate = weekStart.AddDays(column + 1);
                if (nextDate.Month != activeMonth ||
                    !dayLookup.TryGetValue(nextDate, out var nextDayDisplay) ||
                    !nextDayDisplay.MultiDayAllDayBar.HasContent ||
                    !string.Equals(nextDayDisplay.MultiDayAllDayBar.GcalEventId, bar.GcalEventId, StringComparison.Ordinal))
                {
                    break;
                }

                column++;
            }

            segments.Add(new YearViewMultiDaySegmentDisplayModel(
                bar.GcalEventId,
                weekStart.AddDays(segmentStartColumn),
                weekStart.AddDays(column),
                segmentStartColumn,
                column - segmentStartColumn + 1,
                bar));
        }

        return segments;
    }

    private void ConfigurePreviewBarInteractions(Border previewBar, YearViewPreviewBarDisplayModel bar, RenderBuildContext renderContext)
    {
        previewBar.Tapped += (_, e) =>
        {
            if (bar.GcalEventId is not null)
            {
                _selectionService.Select(bar.GcalEventId);
                e.Handled = true;
            }
        };

        var tooltip = new ToolTip
        {
            Content = bar.SummaryText ?? string.Empty
        };
        ToolTipService.SetToolTip(previewBar, tooltip);
        _tooltipManager.Attach(previewBar, tooltip);
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
        _renderCache.Clear();
        _currentRenderState = null;
        Rebuild();
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

    private static void RegisterEventBorder(string gcalEventId, Border border, RenderBuildContext renderContext)
    {
        if (!renderContext.EventBorders.TryGetValue(gcalEventId, out var registrations))
        {
            registrations = [];
            renderContext.EventBorders[gcalEventId] = registrations;
        }

        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness, border.Padding));
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
        var selectedThickness = new Thickness(1);
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

    private sealed class ProjectionCacheEntry
    {
        public ProjectionCacheEntry(
            IList<CalendarEventDisplayModel> events,
            IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap,
            YearViewProjectionResult projection,
            long accessSequence)
        {
            Events = events;
            SyncStatusMap = syncStatusMap;
            Projection = projection;
            AccessSequence = accessSequence;
        }

        public IList<CalendarEventDisplayModel> Events { get; }

        public IReadOnlyDictionary<DateOnly, SyncStatus> SyncStatusMap { get; }

        public YearViewProjectionResult Projection { get; }

        public long AccessSequence { get; set; }
    }

    private sealed class RenderCacheEntry
    {
        public RenderCacheEntry(
            int year,
            IList<CalendarEventDisplayModel> events,
            IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap,
            IReadOnlyList<UIElement> monthPanels,
            Dictionary<string, List<EventBorderRegistration>> eventBorders,
            long accessSequence)
        {
            Year = year;
            Events = events;
            SyncStatusMap = syncStatusMap;
            MonthPanels = monthPanels;
            EventBorders = eventBorders;
            AccessSequence = accessSequence;
        }

        public int Year { get; }

        public IList<CalendarEventDisplayModel> Events { get; }

        public IReadOnlyDictionary<DateOnly, SyncStatus> SyncStatusMap { get; }

        public IReadOnlyList<UIElement> MonthPanels { get; }

        public Dictionary<string, List<EventBorderRegistration>> EventBorders { get; }

        public long AccessSequence { get; set; }
    }

    private sealed record RenderBuildContext(
        Dictionary<string, List<EventBorderRegistration>> EventBorders);

    private sealed class SharedTooltipManager
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DispatcherQueueTimer _timer;
        private ToolTip? _pendingTooltip;
        private ToolTip? _activeTooltip;

        public SharedTooltipManager(DispatcherQueue dispatcherQueue)
        {
            _dispatcherQueue = dispatcherQueue;
            _timer = _dispatcherQueue.CreateTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.IsRepeating = false;
            _timer.Tick += OnTimerTick;
        }

        public void Attach(UIElement element, ToolTip tooltip)
        {
            element.PointerEntered += (_, _) => BeginHover(tooltip);
            element.PointerExited += (_, _) => EndHover(tooltip);
            element.PointerCanceled += (_, _) => EndHover(tooltip);
            element.PointerCaptureLost += (_, _) => EndHover(tooltip);
        }

        public void Reset()
        {
            _timer.Stop();
            _pendingTooltip = null;

            if (_activeTooltip is not null)
            {
                _activeTooltip.IsOpen = false;
                _activeTooltip = null;
            }
        }

        private void BeginHover(ToolTip tooltip)
        {
            if (_activeTooltip is not null && !ReferenceEquals(_activeTooltip, tooltip))
            {
                _activeTooltip.IsOpen = false;
                _activeTooltip = null;
            }

            _pendingTooltip = tooltip;
            tooltip.IsOpen = false;
            _timer.Stop();
            _timer.Start();
        }

        private void EndHover(ToolTip tooltip)
        {
            if (ReferenceEquals(_pendingTooltip, tooltip))
            {
                _pendingTooltip = null;
                _timer.Stop();
            }

            if (ReferenceEquals(_activeTooltip, tooltip))
            {
                tooltip.IsOpen = false;
                _activeTooltip = null;
            }
        }

        private void OnTimerTick(DispatcherQueueTimer sender, object args)
        {
            sender.Stop();

            if (_pendingTooltip is null)
            {
                return;
            }

            if (_activeTooltip is not null && !ReferenceEquals(_activeTooltip, _pendingTooltip))
            {
                _activeTooltip.IsOpen = false;
            }

            _activeTooltip = _pendingTooltip;
            _activeTooltip.IsOpen = true;
        }
    }
}

using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class YearViewControl : Page
{
    private static readonly CornerRadius YearViewCornerRadius = new(4);
    private const double PreviewBarHeight = 11;
    private const double PreviewBarFontSize = 8;
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private readonly List<DispatcherQueueTimer> _tooltipTimers = [];

    public YearViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        InitializeComponent();
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
        Rebuild();
    }

    private void YearViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        StopTooltipTimers();
        _eventBorders.Clear();
        WeakReferenceMessenger.Default.UnregisterAll(this);
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
        MonthsGrid.Children.Clear();
        MonthsGrid.RowDefinitions.Clear();
        MonthsGrid.ColumnDefinitions.Clear();
        StopTooltipTimers();
        _eventBorders.Clear();

        for (var row = 0; row < 4; row++)
        {
            MonthsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var column = 0; column < 3; column++)
        {
            MonthsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var culture = CultureInfo.CurrentCulture;
        var yearStart = new DateOnly(ViewModel.CurrentDate.Year, 1, 1);
        var yearEnd = new DateOnly(ViewModel.CurrentDate.Year, 12, 31);
        var projection = YearViewDayProjectionBuilder.Build(
            EnumerateDates(yearStart, yearEnd),
            ViewModel.CurrentEvents,
            ViewModel.SyncStatusMap);

        for (var month = 1; month <= 12; month++)
        {
            var monthBorder = BuildMonthPanel(new DateOnly(ViewModel.CurrentDate.Year, month, 1), culture, projection);
            Grid.SetRow(monthBorder, (month - 1) / 3);
            Grid.SetColumn(monthBorder, (month - 1) % 3);
            MonthsGrid.Children.Add(monthBorder);
        }

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private Border BuildMonthPanel(
        DateOnly firstDay,
        CultureInfo culture,
        YearViewProjectionResult projection)
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
            var weekGrid = BuildWeekRowGrid(weekStart, firstDay.Month, culture, projection);
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
        YearViewProjectionResult projection)
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

            var header = BuildDayHeader(date, culture, displayModel);
            Grid.SetColumn(header, column);
            Grid.SetRow(header, 0);
            weekGrid.Children.Add(header);

            var singleDayBar = BuildPreviewBar(displayModel.SingleDayAllDayBar, true);
            Grid.SetColumn(singleDayBar, column);
            Grid.SetRow(singleDayBar, 1);
            weekGrid.Children.Add(singleDayBar);
        }

        foreach (var segment in BuildWeekSegments(weekStart, activeMonth, projection.DayLookup))
        {
            var multiDayBar = BuildPreviewBar(segment.Bar, false);
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
        YearViewDayDisplayModel displayModel)
    {
        var header = new Grid
        {
            Margin = new Thickness(2, 2, 2, 1),
            IsHitTestVisible = false
        };

        header.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(culture),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        });

        if (displayModel.SyncDotPlacement == YearViewSyncDotPlacement.Trailing)
        {
            header.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(displayModel.SyncStatus == SyncStatus.Synced ? SyncedColor : NotSyncedColor),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return header;
    }

    private Border BuildPreviewBar(YearViewPreviewBarDisplayModel bar, bool isSingleDay)
    {
        var previewBar = new Border
        {
            Height = PreviewBarHeight,
            Margin = isSingleDay
                ? new Thickness(1, 0, 1, 1)
                : new Thickness(1, 0, 1, 1),
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
            RegisterEventBorder(bar.GcalEventId, previewBar);
            ConfigurePreviewBarInteractions(previewBar, bar);
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

    private void ConfigurePreviewBarInteractions(Border previewBar, YearViewPreviewBarDisplayModel bar)
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
        ConfigureTooltipDelay(previewBar, tooltip);
    }

    private void ConfigureTooltipDelay(UIElement element, ToolTip tooltip)
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
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

    private void ApplySelectionVisualState(string? selectedGcalEventId)
    {
        foreach (var (gcalEventId, registrations) in _eventBorders)
        {
            var isSelected = selectedGcalEventId is not null &&
                string.Equals(gcalEventId, selectedGcalEventId, StringComparison.Ordinal);

            foreach (var registration in registrations)
            {
                registration.Border.BorderBrush = isSelected ? SelectedBorderBrush : registration.DefaultBorderBrush;
                registration.Border.BorderThickness = isSelected ? new Thickness(1) : registration.DefaultBorderThickness;
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

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness);
}

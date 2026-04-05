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

namespace GoogleCalendarManagement.Views;

public sealed partial class MonthViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];
    private static CornerRadius MediumCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusMedium"];
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);

    // Maximum number of multi-day spanning event tracks shown per week row.
    private const int MaxSpanTracks = 2;
    // Maximum number of single-day event chips shown per day column.
    private const int MaxSingleDayChips = 2;

    private readonly ICalendarSelectionService _selectionService;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);

    public MonthViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        _selectionService = App.GetRequiredService<ICalendarSelectionService>();
        InitializeComponent();
        MonthGrid.Background = TransparentPanelBrush;
        MonthGrid.Tapped += MonthGrid_Tapped;
        Loaded += MonthViewControl_Loaded;
        Unloaded += MonthViewControl_Unloaded;
    }

    public MainViewModel ViewModel { get; }

    private void MonthViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.Register<MonthViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        Rebuild();
    }

    private void MonthViewControl_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _eventBorders.Clear();
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
            MonthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
        }

        // Pre-process events: convert Google's exclusive all-day end to an inclusive end day,
        // and record whether the event visually spans more than one calendar day.
        var eventSpans = ViewModel.CurrentEvents
            .Select(evt =>
            {
                var startDay = DateOnly.FromDateTime(evt.StartLocal.Date);
                var rawEndDay = DateOnly.FromDateTime(evt.EndLocal.Date);
                // Google stores all-day end as midnight of the next day (exclusive).
                var endDay = (evt.IsAllDay && rawEndDay > startDay)
                    ? rawEndDay.AddDays(-1)
                    : rawEndDay;
                return (evt, startDay, endDay);
            })
            .ToList();

        var culture = CultureInfo.CurrentCulture;
        for (var row = 0; row < totalRows; row++)
        {
            var weekStart = gridStart.AddDays(row * 7);
            var weekGrid = BuildWeekRowGrid(weekStart, firstDay.Month, eventSpans, culture);
            Grid.SetRow(weekGrid, row);
            Grid.SetColumnSpan(weekGrid, 7);
            MonthGrid.Children.Add(weekGrid);
        }

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    /// <summary>
    /// Builds a nested Grid that represents one calendar week row.
    /// Multi-day events are rendered as ColumnSpan blocks; single-day events
    /// appear below them as individual chips, mirroring Google Calendar's layout.
    /// </summary>
    private Grid BuildWeekRowGrid(
        DateOnly weekStart,
        int activeMonth,
        List<(CalendarEventDisplayModel evt, DateOnly startDay, DateOnly endDay)> allSpans,
        CultureInfo culture)
    {
        var weekEnd = weekStart.AddDays(6);
        var grid = new Grid();

        for (var i = 0; i < 7; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Row 0: day-number headers (auto height)
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Collect multi-day segments visible in this week.
        var multiDaySegments = allSpans
            .Where(e => e.startDay != e.endDay && e.startDay <= weekEnd && e.endDay >= weekStart)
            .OrderBy(e => e.startDay)
            .ThenByDescending(e => e.endDay.DayNumber - e.startDay.DayNumber)
            .ToList();

        // Greedy track assignment: pack non-overlapping events into as few rows as possible.
        var tracks = new List<List<(CalendarEventDisplayModel evt, int colStart, int colEnd)>>();
        var overflowMultiDayCount = 0;

        foreach (var segment in multiDaySegments)
        {
            var colStart = Math.Max(0, segment.startDay.DayNumber - weekStart.DayNumber);
            var colEnd = Math.Min(6, segment.endDay.DayNumber - weekStart.DayNumber);
            var placed = false;

            foreach (var track in tracks)
            {
                if (track[track.Count - 1].colEnd < colStart)
                {
                    track.Add((segment.evt, colStart, colEnd));
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                if (tracks.Count < MaxSpanTracks)
                    tracks.Add(new List<(CalendarEventDisplayModel evt, int colStart, int colEnd)> { (segment.evt, colStart, colEnd) });
                else
                    overflowMultiDayCount++;
            }
        }

        // Add a row per track, then the single-day events row.
        foreach (var _ in tracks)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var singleDayRow = 1 + tracks.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var totalGridRows = singleDayRow + 1;

        // Day-background Borders — one per column, span all sub-rows for visual cohesion.
        for (var col = 0; col < 7; col++)
        {
            var date = weekStart.AddDays(col);
            var bg = new Border
            {
                Margin = new Thickness(4),
                CornerRadius = MediumCornerRadius,
                Opacity = date.Month == activeMonth ? 1.0 : 0.35,
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
            };
            Grid.SetColumn(bg, col);
            Grid.SetRowSpan(bg, totalGridRows);
            grid.Children.Add(bg);
        }

        // Day-number TextBlocks (row 0)
        for (var col = 0; col < 7; col++)
        {
            var date = weekStart.AddDays(col);
            var dayText = new TextBlock
            {
                Text = date.Day.ToString(culture),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(8, 6, 8, 2),
            };
            Grid.SetColumn(dayText, col);
            Grid.SetRow(dayText, 0);
            grid.Children.Add(dayText);
        }

        // Multi-day spanning event chips (rows 1..tracks.Count)
        for (var trackIdx = 0; trackIdx < tracks.Count; trackIdx++)
        {
            foreach (var (evt, colStart, colEnd) in tracks[trackIdx])
            {
                var continuesLeft = colStart == 0 && (evt.StartLocal.Date < weekStart.ToDateTime(TimeOnly.MinValue));
                var continuesRight = colEnd == 6 && (evt.EndLocal.Date > weekEnd.ToDateTime(TimeOnly.MinValue));

                var chip = CreateSpanEventChip(evt, continuesLeft, continuesRight, culture);
                Grid.SetColumn(chip, colStart);
                Grid.SetRow(chip, trackIdx + 1);
                Grid.SetColumnSpan(chip, colEnd - colStart + 1);
                grid.Children.Add(chip);
            }
        }

        // Single-day event chips per column (singleDayRow)
        for (var col = 0; col < 7; col++)
        {
            var date = weekStart.AddDays(col);
            var stackPanel = new StackPanel { Spacing = 4, Margin = new Thickness(4, 2, 4, 4) };

            var singleDayEvents = allSpans
                .Where(e => e.startDay == e.endDay && e.startDay == date)
                .Select(e => e.evt)
                .OrderBy(e => e.StartLocal)
                .ToList();

            foreach (var evt in singleDayEvents.Take(MaxSingleDayChips))
            {
                stackPanel.Children.Add(CreateEventChip(evt, culture));
            }

            // Overflow from single-day events on this column plus unshown multi-day events
            // (shown on the leftmost column of the week so the user knows events are hidden).
            var overflowSingleDay = Math.Max(0, singleDayEvents.Count - MaxSingleDayChips);
            var overflowExtra = col == 0 ? overflowMultiDayCount : 0;
            var totalOverflow = overflowSingleDay + overflowExtra;

            if (totalOverflow > 0)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"+{totalOverflow} more",
                    FontSize = 12,
                    Margin = new Thickness(4, 0, 4, 0),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }

            Grid.SetColumn(stackPanel, col);
            Grid.SetRow(stackPanel, singleDayRow);
            grid.Children.Add(stackPanel);
        }

        return grid;
    }

    /// <summary>Creates a chip that spans one or more day columns for a multi-day event.</summary>
    private Border CreateSpanEventChip(
        CalendarEventDisplayModel item,
        bool continuesFromLeft,
        bool continuesToRight,
        CultureInfo culture)
    {
        var corner = ElementCornerRadius;
        var chip = new Border
        {
            Padding = new Thickness(4),
            Margin = new Thickness(continuesFromLeft ? 0 : 4, 2, continuesToRight ? 0 : 4, 2),
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
            }
        };

        ToolTipService.SetToolTip(chip, BuildTooltipText(item, culture));
        chip.Tapped += (_, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            e.Handled = true;
        };

        RegisterEventBorder(item.GcalEventId, chip);
        return chip;
    }

    /// <summary>Creates a standard chip for a single-day event.</summary>
    private Border CreateEventChip(CalendarEventDisplayModel item, CultureInfo culture)
    {
        var chip = new Border
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
                TextTrimming = TextTrimming.CharacterEllipsis,
            }
        };

        ToolTipService.SetToolTip(chip, BuildTooltipText(item, culture));
        chip.Tapped += (_, e) =>
        {
            _selectionService.Select(item.GcalEventId);
            e.Handled = true;
        };

        RegisterEventBorder(item.GcalEventId, chip);
        return chip;
    }

    private void MonthGrid_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectionService.ClearSelection();
    }

    private void OnEventSelected(EventSelectedMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplySelectionVisualState(message.GcalEventId));
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

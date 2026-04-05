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

        // Expand multi-day events so they appear in every day they cover, not just their start day.
        var eventsByDay = new Dictionary<DateOnly, List<CalendarEventDisplayModel>>();
        foreach (var evt in ViewModel.CurrentEvents)
        {
            var startDay = DateOnly.FromDateTime(evt.StartLocal.Date);
            var endDay = DateOnly.FromDateTime(evt.EndLocal.Date);
            for (var d = startDay; d <= endDay; d = d.AddDays(1))
            {
                if (!eventsByDay.TryGetValue(d, out var list))
                {
                    list = [];
                    eventsByDay[d] = list;
                }

                list.Add(evt);
            }
        }

        foreach (var list in eventsByDay.Values)
        {
            list.Sort((a, b) => a.StartLocal.CompareTo(b.StartLocal));
        }

        var currentDay = gridStart;
        for (var row = 0; row < totalRows; row++)
        {
            for (var column = 0; column < 7; column++)
            {
                eventsByDay.TryGetValue(currentDay, out var dayEvents);
                var cell = BuildDayCell(currentDay, firstDay.Month, dayEvents ?? [], CultureInfo.CurrentCulture);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                MonthGrid.Children.Add(cell);
                currentDay = currentDay.AddDays(1);
            }
        }

        ApplySelectionVisualState(_selectionService.SelectedGcalEventId);
    }

    private Border BuildDayCell(
        DateOnly date,
        int activeMonth,
        IReadOnlyList<CalendarEventDisplayModel> dayEvents,
        CultureInfo culture)
    {
        var stackPanel = new StackPanel { Spacing = 6 };
        stackPanel.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(culture),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        foreach (var item in dayEvents.Take(3))
        {
            var eventBorder = new Border
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

            ToolTipService.SetToolTip(eventBorder, BuildTooltipText(item, culture));
            eventBorder.Tapped += (sender, e) =>
            {
                _selectionService.Select(item.GcalEventId);
                e.Handled = true;
            };

            RegisterEventBorder(item.GcalEventId, eventBorder);
            stackPanel.Children.Add(eventBorder);
        }

        var overflowCount = Math.Max(0, dayEvents.Count - 3);
        if (overflowCount > 0)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Text = $"+{overflowCount} more",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }

        return new Border
        {
            Margin = new Thickness(4),
            Padding = new Thickness(8),
            CornerRadius = MediumCornerRadius,
            Opacity = date.Month == activeMonth ? 1.0 : 0.35,
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Child = stackPanel
        };
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

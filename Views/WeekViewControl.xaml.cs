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

public sealed partial class WeekViewControl : Page
{
    private static CornerRadius ElementCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusElement"];

    private const double TimeColumnWidth = 72;
    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 20;
    private const double WeekGridHorizontalPadding = 24.0;
    private const double RowHeight = 72.0;

    private static readonly Brush GridLineBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4A, 0x4A, 0x4A));
    private static readonly Brush OverlapOutlineBrush = new SolidColorBrush(Colors.Black);
    private static readonly Brush SelectedBorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
    private static readonly Brush TransparentPanelBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Color SyncedColor = Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly Color NotSyncedColor = Color.FromArgb(0xFF, 0xA0, 0xA0, 0xA0);

    private readonly ICalendarSelectionService _selectionService;
    private readonly Dictionary<string, List<EventBorderRegistration>> _eventBorders = new(StringComparer.Ordinal);
    private IReadOnlyList<WeekTimedEventLayoutItem> _timedEventItems = [];
    private WeekTimedEventVirtualizingLayout _timedEventLayout = new();
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
        WeakReferenceMessenger.Default.Register<WeekViewControl, EventSelectedMessage>(this, static (recipient, message) => recipient.OnEventSelected(message));
        WeakReferenceMessenger.Default.Register<WeekViewControl, SyncCompletedMessage>(this, static (recipient, _) => recipient.OnSyncCompleted());

        _resizeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer?.Stop();
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
        TimedEventsRepeater.ItemsSource = null;
        _timedEventItems = [];
        _eventBorders.Clear();
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

        if (string.Equals(_selectionService.SelectedGcalEventId, item.GcalEventId, StringComparison.Ordinal))
        {
            border.BorderBrush = SelectedBorderBrush;
            border.BorderThickness = new Thickness(2);
        }
    }

    private void TimedEventsRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not Border border || border.Tag is not string gcalEventId)
        {
            return;
        }

        UnregisterEventBorder(gcalEventId, border);
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

        registrations.RemoveAll(registration => ReferenceEquals(registration.Border, border));
        registrations.Add(new EventBorderRegistration(border, border.BorderBrush, border.BorderThickness));
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

    private static void ResetTimedEventBorder(Border border)
    {
        border.Tag = null;
        border.Background = null;
        border.BorderBrush = null;
        border.BorderThickness = new Thickness(0);
        border.Padding = new Thickness(0);
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

    private sealed record EventBorderRegistration(
        Border Border,
        Brush? DefaultBorderBrush,
        Thickness DefaultBorderThickness);

}

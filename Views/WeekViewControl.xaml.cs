using System.ComponentModel;
using System.Globalization;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Views;

public sealed partial class WeekViewControl : Page
{
    private const double TimeColumnWidth = 72;
    private const double MinimumDayColumnWidth = 100;
    private const double HorizontalChromeAllowance = 48;

    private DispatcherTimer? _resizeDebounceTimer;

    public WeekViewControl()
    {
        ViewModel = App.GetRequiredService<MainViewModel>();
        InitializeComponent();
        Loaded += WeekViewControl_Loaded;
        Unloaded += WeekViewControl_Unloaded;
        SizeChanged += WeekViewControl_SizeChanged;
    }

    public MainViewModel ViewModel { get; }

    private void WeekViewControl_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.CurrentDate) or nameof(MainViewModel.CurrentEvents))
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
        WeekGrid.Children.Clear();
        WeekGrid.RowDefinitions.Clear();
        WeekGrid.ColumnDefinitions.Clear();

        var viewportWidth = Math.Max(0d, ActualWidth - HorizontalChromeAllowance);
        var minimumContentWidth = TimeColumnWidth + (MinimumDayColumnWidth * 7);
        var contentWidth = Math.Max(minimumContentWidth, viewportWidth);
        var availableDayWidth = (contentWidth - TimeColumnWidth) / 7d;

        WeekGrid.Width = contentWidth;

        WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TimeColumnWidth) });
        for (var column = 0; column < 7; column++)
        {
            WeekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(availableDayWidth) });
        }

        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        WeekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var hour = 0; hour < 24; hour++)
        {
            WeekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
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
            Grid.SetRow(label, hour + 2);
            Grid.SetColumn(label, 0);
            WeekGrid.Children.Add(label);
        }

        for (var offset = 0; offset < 7; offset++)
        {
            var currentDay = weekStart.AddDays(offset);
            var column = offset + 1;

            var header = new TextBlock
            {
                Text = currentDay.ToDateTime(TimeOnly.MinValue).ToString("ddd d", culture),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(4)
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, column);
            WeekGrid.Children.Add(header);

            var allDayPanel = new StackPanel
            {
                Spacing = 4,
                Margin = new Thickness(4)
            };
            foreach (var item in ViewModel.CurrentEvents
                         .Where(evt => DateOnly.FromDateTime(evt.StartLocal.Date) == currentDay && evt.IsAllDay)
                         .OrderBy(evt => evt.StartLocal))
            {
                allDayPanel.Children.Add(CreateEventChip(item.Title, item.ColorHex));
            }

            Grid.SetRow(allDayPanel, 1);
            Grid.SetColumn(allDayPanel, column);
            WeekGrid.Children.Add(allDayPanel);

            for (var hour = 0; hour < 24; hour++)
            {
                var slotBorder = new Border
                {
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                Grid.SetRow(slotBorder, hour + 2);
                Grid.SetColumn(slotBorder, column);
                WeekGrid.Children.Add(slotBorder);
            }

            foreach (var item in ViewModel.CurrentEvents
                         .Where(evt => !evt.IsAllDay &&
                                       DateOnly.FromDateTime(evt.StartLocal.Date) <= currentDay &&
                                       DateOnly.FromDateTime(evt.EndLocal.Date) >= currentDay)
                         .OrderBy(evt => evt.StartLocal))
            {
                var eventBlock = CreateTimedEventBlock(item, culture);
                var startRow = item.StartLocal.Hour + 2;
                var span = (int)Math.Ceiling((item.EndLocal - item.StartLocal).TotalHours);
                Grid.SetRow(eventBlock, startRow);
                Grid.SetColumn(eventBlock, column);
                Grid.SetRowSpan(eventBlock, Math.Max(1, Math.Min(span, 24 - item.StartLocal.Hour)));
                // TODO Story 3.x (WeekView virtualization): replace this imperative Grid + Children.Add approach
                // with ItemsRepeater + RecyclingElementFactory as specified. The current approach rebuilds all
                // event blocks synchronously on every Rebuild() call (including SizeChanged), which will be slow
                // at 200+ events. See review finding F21 in story 3-1.
                WeekGrid.Children.Add(eventBlock);
            }
        }
    }

    private static Border CreateEventChip(string title, string hexColor)
    {
        return new Border
        {
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(8),
            Background = ToBrush(hexColor),
            Child = new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    private static Border CreateTimedEventBlock(CalendarEventDisplayModel item, CultureInfo culture)
    {
        return new Border
        {
            Margin = new Thickness(4, 1, 4, 1),
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            Background = ToBrush(item.ColorHex),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = item.Title,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        FontSize = 12,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    new TextBlock
                    {
                        Text = $"{item.StartLocal.ToString("t", culture)} - {item.EndLocal.ToString("t", culture)}",
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 11
                    }
                }
            }
        };
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
}

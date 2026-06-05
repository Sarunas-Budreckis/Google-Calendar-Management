using GoogleCalendarManagement.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace GoogleCalendarManagement.Views;

public sealed partial class VerticalDotTimelineControl : UserControl
{
    private const double HoursTotal = 24.0;
    private const double CanvasHeight = 960.0;
    private const double PixelsPerHour = CanvasHeight / HoursTotal;   // 40px
    private const double DotSize = 8.0;

    public VerticalDotTimelineControl()
    {
        InitializeComponent();
        DrawHourLabels();
    }

    public void SetItems(IEnumerable<VerticalDotItem> items)
    {
        DotCanvas.Children.Clear();

        foreach (var item in items)
        {
            var localTime = item.Timestamp.Kind == DateTimeKind.Utc
                ? item.Timestamp.ToLocalTime()
                : item.Timestamp;

            var top = (localTime.Hour + localTime.Minute / 60.0 + localTime.Second / 3600.0) * PixelsPerHour - DotSize / 2.0;
            top = Math.Clamp(top, 0, CanvasHeight - DotSize);

            var dot = new Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                Fill = item.IsPartial
                    ? (Brush)Application.Current.Resources["SystemAccentColorBrush"]
                    : new SolidColorBrush(Colors.LightGray)
            };

            var tooltipLines = new List<string> { item.PrimaryLabel };
            if (item.SecondaryLabel is not null) tooltipLines.Add(item.SecondaryLabel);
            if (item.TertiaryLabel is not null) tooltipLines.Add(item.TertiaryLabel);
            ToolTipService.SetToolTip(dot, string.Join("\n", tooltipLines));

            Canvas.SetTop(dot, top);
            Canvas.SetLeft(dot, 8);
            DotCanvas.Children.Add(dot);
        }
    }

    private void DrawHourLabels()
    {
        for (var hour = 0; hour < 24; hour += 3)
        {
            var top = hour * PixelsPerHour;

            var line = new Line
            {
                X1 = 30, Y1 = top,
                X2 = 36, Y2 = top,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                Opacity = 0.4
            };
            Canvas.SetTop(line, 0);
            HourCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = $"{hour:D2}",
                FontSize = 9,
                Opacity = 0.5,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetTop(label, top - 6);
            Canvas.SetLeft(label, 0);
            HourCanvas.Children.Add(label);
        }

        // Faint horizontal lines across the dot area every 3 hours
        for (var hour = 0; hour < 24; hour += 3)
        {
            var top = hour * PixelsPerHour;
            var gridLine = new Line
            {
                X1 = 0, Y1 = top,
                X2 = 160, Y2 = top,
                Stroke = new SolidColorBrush(Colors.Gray),
                StrokeThickness = 1,
                Opacity = 0.15
            };
            DotCanvas.Children.Add(gridLine);
        }
    }
}

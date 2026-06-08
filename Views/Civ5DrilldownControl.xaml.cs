using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class Civ5DrilldownControl : UserControl
{
    private const double TimelineHeight = 480.0;
    private const double HourHeight = TimelineHeight / 24.0;
    private bool _suppressRebuild;

    public Civ5DrilldownControl(Civ5DrilldownViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        viewModel.SessionPoints.CollectionChanged += (_, _) =>
        {
            if (!_suppressRebuild) BuildTimeline();
        };
    }

    public Civ5DrilldownViewModel ViewModel { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _suppressRebuild = true;
        await ViewModel.LoadAsync(date, ct);
        _suppressRebuild = false;
        BuildTimeline();
    }

    private void BuildTimeline()
    {
        TimelineCanvas.Children.Clear();

        // Vertical guide line
        var guideLine = new Line
        {
            X1 = 36, Y1 = 0,
            X2 = 36, Y2 = TimelineHeight,
            Stroke = new SolidColorBrush(Colors.Gray),
            Opacity = 0.25,
            StrokeThickness = 1
        };
        TimelineCanvas.Children.Add(guideLine);

        // Hour labels and tick marks
        for (var h = 0; h < 24; h++)
        {
            var y = h * HourHeight;

            var label = new TextBlock
            {
                Text = $"{h:00}",
                FontSize = 10,
                Opacity = 0.5
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, Math.Max(0, y - 6));
            TimelineCanvas.Children.Add(label);

            var tick = new Line
            {
                X1 = 28, Y1 = y,
                X2 = 36, Y2 = y,
                Stroke = new SolidColorBrush(Colors.Gray),
                Opacity = 0.3,
                StrokeThickness = 1
            };
            TimelineCanvas.Children.Add(tick);
        }

        // Session point dots
        foreach (var point in ViewModel.SessionPoints)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(220, 255, 180, 0))
            };
            ToolTipService.SetToolTip(dot, point.TooltipText);
            Canvas.SetLeft(dot, 32);
            Canvas.SetTop(dot, point.CanvasTop - 4);
            TimelineCanvas.Children.Add(dot);
        }
    }
}

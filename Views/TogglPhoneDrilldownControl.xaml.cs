using System.Runtime.InteropServices;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class TogglPhoneDrilldownControl : UserControl
{
    private const double CanvasHeight = 480.0;
    private const double HoursInDay = 24.0;

    public TogglPhoneDrilldownControl(TogglPhoneDrilldownViewModel viewModel, TogglPhoneRulesViewModel rulesViewModel)
    {
        ViewModel = viewModel;
        _rulesViewModel = rulesViewModel;
        InitializeComponent();
        DataContext = viewModel;
        BuildHourLabels();
    }

    public TogglPhoneDrilldownViewModel ViewModel { get; }
    private readonly TogglPhoneRulesViewModel _rulesViewModel;
    private bool _rulesDialogOpen;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        await ViewModel.LoadAsync(date, ct);
        RebuildDots();
    }

    private void RebuildDots()
    {
        TimelineCanvas.Children.Clear();

        var dotBrush = new SolidColorBrush(Color.FromArgb(255, 234, 179, 8)); // yellow-500

        foreach (var dot in ViewModel.TimelineDots)
        {
            var ellipse = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = dotBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };

            Canvas.SetLeft(ellipse, 10);
            Canvas.SetTop(ellipse, dot.DotTopOffset - 5); // center the dot vertically

            ToolTipService.SetToolTip(ellipse, new ToolTip { Content = dot.Tooltip });

            TimelineCanvas.Children.Add(ellipse);
        }
    }

    private void BuildHourLabels()
    {
        var labelBrush = new SolidColorBrush(Colors.Gray);
        for (var hour = 0; hour < 24; hour += 6)
        {
            var label = new TextBlock
            {
                Text = hour == 0 ? "12a" : hour < 12 ? $"{hour}a" : hour == 12 ? "12p" : $"{hour - 12}p",
                FontSize = 9,
                Foreground = labelBrush,
                Opacity = 0.6
            };
            var top = hour / HoursInDay * CanvasHeight;
            Canvas.SetTop(label, Math.Max(0, top - 7));
            Canvas.SetLeft(label, 0);
            HourLabelCanvas.Children.Add(label);
        }
    }

    private async void ManageRulesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_rulesDialogOpen) return;
        _rulesDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = "Manage Phone Classification Rules",
                CloseButtonText = "Done",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
                Content = new TogglPhoneRulesControl(_rulesViewModel)
            };

            await _rulesViewModel.LoadAsync();
            await dialog.ShowAsync();
        }
        catch (Exception ex) when (ex is COMException or TaskCanceledException)
        {
        }
        finally
        {
            _rulesDialogOpen = false;
        }
    }
}

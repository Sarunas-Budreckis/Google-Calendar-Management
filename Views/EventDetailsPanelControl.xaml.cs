using System.ComponentModel;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class EventDetailsPanelControl : UserControl
{
    private const double PanelWidth = 380.0;
    private Storyboard? _openStoryboard;
    private Storyboard? _closeStoryboard;

    public EventDetailsPanelControl(EventDetailsPanelViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public EventDetailsPanelViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        // Sync initial state without animation
        SyncVisibility(animate: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _openStoryboard?.Stop();
        _closeStoryboard?.Stop();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventDetailsPanelViewModel.IsPanelVisible))
            SyncVisibility(animate: true);

        if (e.PropertyName == nameof(EventDetailsPanelViewModel.ColorHex))
            UpdateColorSwatch();
    }

    private void SyncVisibility(bool animate)
    {
        if (ViewModel.IsPanelVisible)
            OpenPanel(animate);
        else
            ClosePanel(animate);
    }

    private void OpenPanel(bool animate)
    {
        _closeStoryboard?.Stop();
        // Make the UserControl visible first so the column allocates space,
        // then animate the content sliding in from the right.
        Visibility = Visibility.Visible;
        UpdateColorSwatch();

        if (!animate)
        {
            PanelTranslate.X = 0;
            return;
        }

        PanelTranslate.X = PanelWidth;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation
        {
            From = PanelWidth,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = easing
        };
        Storyboard.SetTarget(animation, PanelTranslate);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.X));

        _openStoryboard = new Storyboard();
        _openStoryboard.Children.Add(animation);
        _openStoryboard.Begin();
    }

    private void ClosePanel(bool animate)
    {
        _openStoryboard?.Stop();

        if (!animate)
        {
            PanelTranslate.X = PanelWidth;
            Visibility = Visibility.Collapsed;
            return;
        }

        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var animation = new DoubleAnimation
        {
            From = 0,
            To = PanelWidth,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = easing
        };
        Storyboard.SetTarget(animation, PanelTranslate);
        Storyboard.SetTargetProperty(animation, nameof(TranslateTransform.X));

        _closeStoryboard = new Storyboard();
        _closeStoryboard.Completed += (_, _) =>
        {
            Visibility = Visibility.Collapsed;
            PanelTranslate.X = PanelWidth;
        };
        _closeStoryboard.Children.Add(animation);
        _closeStoryboard.Begin();
    }

    private void UpdateColorSwatch()
    {
        if (string.IsNullOrEmpty(ViewModel.ColorHex))
        {
            ColorSwatch.Background = null;
            return;
        }

        try
        {
            var hex = ViewModel.ColorHex.TrimStart('#');
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                ColorSwatch.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, r, g, b));
            }
        }
        catch (FormatException)
        {
            ColorSwatch.Background = null;
        }
    }
}

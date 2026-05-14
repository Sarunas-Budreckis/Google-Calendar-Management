using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace GoogleCalendarManagement.Views;

public sealed partial class DataSourcePanelControl : UserControl
{
    private static readonly InputSystemCursor ResizeEastWestCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private const double MinPanelWidth = 160.0;
    private const double MaxPanelWidth = 600.0;

    private bool _isResizing;
    private double _resizeStartX;
    private double _resizeStartWidth;

    public DataSourcePanelControl(DataSourcePanelViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DataSourcePanelViewModel ViewModel { get; }

    private async void DataSourcePanelControl_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMinimized = true;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMinimized = false;
    }

    private async void DayHeader_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel.OpenDayNameHeaderCommand.CanExecute(null))
        {
            await ViewModel.OpenDayNameHeaderCommand.ExecuteAsync(null);
        }
    }

    private void ResizeHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = ResizeEastWestCursor;
    }

    private void ResizeHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing)
        {
            ProtectedCursor = null;
        }
    }

    private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _resizeStartX = e.GetCurrentPoint(null).Position.X;
        _resizeStartWidth = double.IsNaN(ViewModel.PanelWidth) ? ActualWidth : ViewModel.PanelWidth;
        _isResizing = true;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        var dx = e.GetCurrentPoint(null).Position.X - _resizeStartX;
        ViewModel.PanelWidth = Math.Clamp(_resizeStartWidth + dx, MinPanelWidth, MaxPanelWidth);
        e.Handled = true;
    }

    private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        ((UIElement)sender).ReleasePointerCaptures();
        _isResizing = false;
        e.Handled = true;
    }

    private void ResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isResizing = false;
        ProtectedCursor = null;
    }
}

using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace GoogleCalendarManagement.Views;

public sealed partial class DataSourcePanelControl : UserControl
{
    private static readonly InputSystemCursor ResizeEastWestCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
    private const double MinPanelWidth = 160.0;
    private const double MaxPanelWidth = 600.0;

    private bool _isResizing;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private DataSourceDayCardViewModel? _draggedDayCard;
    private UIElement? _dragHandle;
    private bool _isDraggingDayCard;
    private Point _dragStartPoint;
    private const double ReorderDragThreshold = 6.0;

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

    private void DayCardDragHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DataSourceDayCardViewModel dayCard } handle)
        {
            return;
        }

        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedDayCard = dayCard;
        _dragHandle = handle;
        _isDraggingDayCard = false;
        _dragStartPoint = e.GetCurrentPoint(DayCardsListView).Position;
        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void DayCardDragHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_draggedDayCard is null || _dragHandle is null)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(DayCardsListView);
        if (!currentPoint.Properties.IsLeftButtonPressed)
        {
            EndDayCardDrag();
            return;
        }

        var position = currentPoint.Position;
        if (!_isDraggingDayCard && Math.Abs(position.Y - _dragStartPoint.Y) < ReorderDragThreshold)
        {
            return;
        }

        _isDraggingDayCard = true;
        var oldIndex = ViewModel.DayCards.IndexOf(_draggedDayCard);
        var newIndex = GetDayCardDropIndex(position);
        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
        {
            ViewModel.MoveDayCard(oldIndex, newIndex);
        }

        e.Handled = true;
    }

    private void DayCardDragHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragHandle is not null)
        {
            _dragHandle.ReleasePointerCaptures();
        }

        EndDayCardDrag();
        e.Handled = true;
    }

    private void DayCardDragHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDayCardDrag();
    }

    private int GetDayCardDropIndex(Point position)
    {
        if (ViewModel.DayCards.Count == 0)
        {
            return -1;
        }

        for (var index = 0; index < ViewModel.DayCards.Count; index++)
        {
            if (DayCardsListView.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            var topLeft = container.TransformToVisual(DayCardsListView).TransformPoint(new Point(0, 0));
            var midpointY = topLeft.Y + container.ActualHeight / 2.0;
            if (position.Y < midpointY)
            {
                return index;
            }
        }

        return ViewModel.DayCards.Count - 1;
    }

    private void EndDayCardDrag()
    {
        _draggedDayCard = null;
        _dragHandle = null;
        _isDraggingDayCard = false;
    }
}

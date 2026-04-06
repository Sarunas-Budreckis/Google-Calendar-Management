using System.ComponentModel;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using Windows.Foundation;

namespace GoogleCalendarManagement.Views;

public sealed partial class MainPage : Page
{
    private static CornerRadius MediumCornerRadius => (CornerRadius)Application.Current.Resources["AppCornerRadiusMedium"];

    private readonly Dictionary<ViewMode, Button> _viewModeButtons;
    private readonly Brush _selectorBackground = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorBorderBrush = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorHoverBackground;
    private readonly Brush _selectorSelectedIndicatorBackground;
    private readonly Brush _selectorSelectedForeground;
    private readonly Brush _selectorUnselectedForeground;
    private readonly ICalendarSelectionService _selectionService;
    private readonly TranslateTransform _selectionIndicatorTransform = new();
    private Storyboard? _selectionIndicatorStoryboard;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _lastSyncRefreshTimer;
    private bool _isLoaded;
    private bool _hasSelectionIndicatorPosition;
    private bool _isUpdatingPicker;
    private ViewMode? _pendingViewMode;
    private ViewMode _selectionIndicatorMode;

    public MainPage(MainViewModel viewModel, ICalendarSelectionService selectionService, EventDetailsPanelControl eventDetailsPanel)
    {
        ViewModel = viewModel;
        _selectionService = selectionService;
        InitializeComponent();
        EventDetailsPanel.Content = eventDetailsPanel;
        _selectorHoverBackground = (Brush)Application.Current.Resources["CalendarSelectorHoverBrush"];
        _selectorSelectedIndicatorBackground = (Brush)Application.Current.Resources["CalendarSelectionHoverBrush"];
        _selectorSelectedForeground = (Brush)Application.Current.Resources["WhiteBrush"];
        _selectorUnselectedForeground = new SolidColorBrush(ColorHelper.FromArgb(0xE0, 0xFF, 0xFF, 0xFF));
        _viewModeButtons = new Dictionary<ViewMode, Button>
        {
            [ViewMode.Year] = YearViewButton,
            [ViewMode.Month] = MonthViewButton,
            [ViewMode.Week] = WeekViewButton,
            [ViewMode.Day] = DayViewButton
        };
        ViewModeSelectionIndicator.RenderTransform = _selectionIndicatorTransform;
        ViewModeSelectorGrid.SizeChanged += ViewModeSelectorGrid_SizeChanged;

        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
        KeyDown += MainPage_KeyDown;
    }

    public MainViewModel ViewModel { get; }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        StartLastSyncRefreshTimer();
        ViewModel.RefreshRelativeSyncPresentation();
        _selectionIndicatorMode = ViewModel.CurrentViewMode;
        UpdateViewModeButtons();
        UpdateSelectionIndicator(_selectionIndicatorMode, animate: false);
        NavigateToCurrentView(force: true);
        _isUpdatingPicker = true;
        JumpToDatePicker.Date = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue);
        _isUpdatingPicker = false;
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        StopLastSyncRefreshTimer();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentViewMode))
        {
            _pendingViewMode = null;
            UpdateViewModeButtons();
            if (_selectionIndicatorMode != ViewModel.CurrentViewMode)
            {
                UpdateSelectionIndicator(ViewModel.CurrentViewMode);
            }
            _ = NavigateToCurrentViewDeferredAsync();
        }

        if (e.PropertyName == nameof(MainViewModel.CurrentDate))
        {
            _isUpdatingPicker = true;
            JumpToDatePicker.Date = ViewModel.CurrentDate.ToDateTime(TimeOnly.MinValue);
            _isUpdatingPicker = false;
        }

        if (e.PropertyName == nameof(MainViewModel.SyncFlyoutOpenRequestId))
        {
            ShowSyncFlyout();
        }
    }

    private async void ViewModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } ||
            !Enum.TryParse<ViewMode>(tag, out var mode))
        {
            return;
        }

        await SwitchViewModeAsync(mode);
    }

    private void ViewModeSelectorGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSelectionIndicator(GetDisplayedViewMode(), animate: false);
    }

    private async void MainPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Left or VirtualKey.Right && ShouldSuppressShellArrowNavigation())
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.P:
            case VirtualKey.K:
            case VirtualKey.Left:
                e.Handled = true;
                await ViewModel.NavigatePreviousCommand.ExecuteAsync(null);
                break;
            case VirtualKey.N:
            case VirtualKey.J:
            case VirtualKey.Right:
                e.Handled = true;
                await ViewModel.NavigateNextCommand.ExecuteAsync(null);
                break;
            case VirtualKey.T:
                e.Handled = true;
                await ViewModel.NavigateTodayCommand.ExecuteAsync(null);
                break;
            case VirtualKey.Escape:
                e.Handled = true;
                _selectionService.ClearSelection();
                break;
            case VirtualKey.G:
                e.Handled = true;
                _ = OpenJumpToDatePicker();
                break;
            case VirtualKey.Number1:
            case VirtualKey.Y:
                e.Handled = true;
                await SwitchViewModeAsync(ViewMode.Year);
                break;
            case VirtualKey.Number2:
            case VirtualKey.M:
                e.Handled = true;
                await SwitchViewModeAsync(ViewMode.Month);
                break;
            case VirtualKey.Number3:
            case VirtualKey.W:
                e.Handled = true;
                await SwitchViewModeAsync(ViewMode.Week);
                break;
            case VirtualKey.Number4:
            case VirtualKey.D:
                e.Handled = true;
                await SwitchViewModeAsync(ViewMode.Day);
                break;
            case VirtualKey.Up:
                e.Handled = true;
                await CycleViewModeAsync(up: true);
                break;
            case VirtualKey.Down:
                e.Handled = true;
                await CycleViewModeAsync(up: false);
                break;
        }
    }

    private Task CycleViewModeAsync(bool up)
    {
        // Up = increase period length (Day → Week → Month → Year), no wrap
        ViewMode[] order = [ViewMode.Day, ViewMode.Week, ViewMode.Month, ViewMode.Year];
        var currentIndex = Array.IndexOf(order, GetDisplayedViewMode());
        var newIndex = up ? currentIndex + 1 : currentIndex - 1;
        if (newIndex < 0 || newIndex >= order.Length)
            return Task.CompletedTask;
        return SwitchViewModeAsync(order[newIndex]);
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NavigatePreviousCommand.ExecuteAsync(null);
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NavigateNextCommand.ExecuteAsync(null);
    }

    private async void JumpToDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is null || _isUpdatingPicker)
        {
            return;
        }

        var date = DateOnly.FromDateTime(args.NewDate.Value.DateTime);
        await ViewModel.JumpToDateCommand.ExecuteAsync(date);
    }

    private Task OpenJumpToDatePicker()
    {
        JumpToDatePicker.Focus(FocusState.Programmatic);
        JumpToDatePicker.IsCalendarOpen = true;
        return Task.CompletedTask;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = App.GetRequiredService<SettingsPage>(),
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestSyncFlyout();
    }

    private void EmptyStateSyncPrompt_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RequestSyncFlyoutForVisibleRange();
    }

    private async void ConfirmSyncButton_Click(object sender, RoutedEventArgs e)
    {
        GetSyncFlyout().Hide();
        await ViewModel.ConfirmSyncAsync();
    }

    private void NavigateToCurrentView(bool force = false)
    {
        var pageType = ViewModel.CurrentViewMode switch
        {
            ViewMode.Year => typeof(YearViewControl),
            ViewMode.Month => typeof(MonthViewControl),
            ViewMode.Week => typeof(WeekViewControl),
            ViewMode.Day => typeof(DayViewControl),
            _ => typeof(YearViewControl)
        };

        if (!force && CalendarFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        CalendarFrame.Navigate(pageType, null, new SlideNavigationTransitionInfo());
    }

    private void UpdateViewModeButtons()
    {
        var displayedMode = GetDisplayedViewMode();

        foreach (var (mode, button) in _viewModeButtons)
        {
            var isSelected = mode == displayedMode;

            button.Background = _selectorBackground;
            button.Foreground = isSelected ? _selectorSelectedForeground : _selectorUnselectedForeground;
            button.BorderThickness = new Thickness(0);
            button.CornerRadius = MediumCornerRadius;
            button.FontWeight = isSelected
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;

            button.Resources["ButtonBackgroundPointerOver"] = isSelected ? _selectorSelectedIndicatorBackground : _selectorHoverBackground;
            button.Resources["ButtonBackgroundPressed"] = isSelected ? _selectorSelectedIndicatorBackground : _selectorHoverBackground;
            button.Resources["ButtonBorderBrushPointerOver"] = _selectorBorderBrush;
            button.Resources["ButtonBorderBrushPressed"] = _selectorBorderBrush;
            button.Resources["ButtonForegroundPointerOver"] = _selectorSelectedForeground;
            button.Resources["ButtonForegroundPressed"] = _selectorSelectedForeground;
        }
    }

    private void UpdateSelectionIndicator(ViewMode targetMode, bool animate = true)
    {
        if (!_isLoaded ||
            !_viewModeButtons.TryGetValue(targetMode, out var selectedButton) ||
            selectedButton.ActualWidth <= 0 ||
            ViewModeSelectorGrid.ActualWidth <= 0)
        {
            return;
        }

        ViewModeSelectionIndicator.Background = _selectorSelectedIndicatorBackground;

        var position = selectedButton.TransformToVisual(ViewModeSelectorGrid).TransformPoint(new Point(0, 0)).X;
        var targetWidth = selectedButton.ActualWidth;

        _selectionIndicatorStoryboard?.Stop();

        if (!animate || !_hasSelectionIndicatorPosition)
        {
            ViewModeSelectionIndicator.Width = targetWidth;
            _selectionIndicatorTransform.X = position;
            _selectionIndicatorMode = targetMode;
            _hasSelectionIndicatorPosition = true;
            return;
        }

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();

        var moveAnimation = new DoubleAnimation
        {
            To = position,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = easing
        };
        Storyboard.SetTarget(moveAnimation, _selectionIndicatorTransform);
        Storyboard.SetTargetProperty(moveAnimation, nameof(TranslateTransform.X));

        var widthAnimation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = easing
        };
        Storyboard.SetTarget(widthAnimation, ViewModeSelectionIndicator);
        Storyboard.SetTargetProperty(widthAnimation, nameof(Width));

        storyboard.Children.Add(moveAnimation);
        storyboard.Children.Add(widthAnimation);
        _selectionIndicatorStoryboard = storyboard;
        _selectionIndicatorMode = targetMode;
        _hasSelectionIndicatorPosition = true;
        storyboard.Begin();
    }

    private ViewMode GetDisplayedViewMode()
    {
        return _pendingViewMode ?? ViewModel.CurrentViewMode;
    }

    private async Task NavigateToCurrentViewDeferredAsync()
    {
        await Task.Yield();
        NavigateToCurrentView();
    }

    private async Task SwitchViewModeAsync(ViewMode mode)
    {
        if (mode == GetDisplayedViewMode())
        {
            return;
        }

        _pendingViewMode = mode;
        UpdateViewModeButtons();
        UpdateSelectionIndicator(mode);
        await Task.Yield();

        // When switching to week or day view, navigate to the selected event's date
        // so the user lands on the period containing the event they just tapped.
        var selectedId = _selectionService.SelectedGcalEventId;
        if (selectedId is not null && mode is ViewMode.Week or ViewMode.Day)
        {
            var selectedEvent = ViewModel.CurrentEvents
                .FirstOrDefault(e => string.Equals(e.GcalEventId, selectedId, StringComparison.Ordinal));
            if (selectedEvent is not null)
            {
                await ViewModel.NavigateToAsync(DateOnly.FromDateTime(selectedEvent.StartLocal.Date), mode);
                return;
            }
        }

        await ViewModel.SwitchViewModeCommand.ExecuteAsync(mode);
    }

    private void StartLastSyncRefreshTimer()
    {
        if (_lastSyncRefreshTimer is not null)
        {
            _lastSyncRefreshTimer.Start();
            return;
        }

        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _lastSyncRefreshTimer = dispatcherQueue.CreateTimer();
        _lastSyncRefreshTimer.Interval = TimeSpan.FromSeconds(30);
        _lastSyncRefreshTimer.Tick += LastSyncRefreshTimer_Tick;
        _lastSyncRefreshTimer.Start();
    }

    private void StopLastSyncRefreshTimer()
    {
        if (_lastSyncRefreshTimer is null)
        {
            return;
        }

        _lastSyncRefreshTimer.Stop();
        _lastSyncRefreshTimer.Tick -= LastSyncRefreshTimer_Tick;
        _lastSyncRefreshTimer = null;
    }

    private void LastSyncRefreshTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        ViewModel.RefreshRelativeSyncPresentation();
    }

    private void ShowSyncFlyout()
    {
        if (ViewModel.IsSyncing)
        {
            return;
        }

        var flyout = GetSyncFlyout();
        flyout.Hide();
        flyout.ShowAt(SyncButton);
    }

    private Flyout GetSyncFlyout()
    {
        return (Flyout)SyncButton.Flyout;
    }

    private bool ShouldSuppressShellArrowNavigation()
    {
        if (JumpToDatePicker.IsCalendarOpen ||
            GetSyncFlyout().IsOpen)
        {
            return true;
        }

        var focusedElement = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        return IsEditableControlFocused(focusedElement);
    }

    private static bool IsEditableControlFocused(DependencyObject? focusedElement)
    {
        return FindAncestorOrSelf<TextBox>(focusedElement) is not null ||
               FindAncestorOrSelf<RichEditBox>(focusedElement) is not null ||
               FindAncestorOrSelf<PasswordBox>(focusedElement) is not null ||
               FindAncestorOrSelf<AutoSuggestBox>(focusedElement) is not null ||
               FindAncestorOrSelf<NumberBox>(focusedElement) is not null;
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}

using System.ComponentModel;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using Windows.Foundation;

namespace GoogleCalendarManagement.Views;

public sealed partial class MainPage : Page
{
    private readonly Dictionary<ViewMode, Button> _viewModeButtons;
    private readonly Brush _selectorBackground = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorBorderBrush = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _selectorHoverBackground;
    private readonly Brush _selectorSelectedIndicatorBackground;
    private readonly Brush _selectorSelectedForeground;
    private readonly Brush _selectorUnselectedForeground;
    private readonly TranslateTransform _selectionIndicatorTransform = new();
    private Storyboard? _selectionIndicatorStoryboard;
    private bool _isLoaded;
    private bool _hasSelectionIndicatorPosition;
    private bool _isUpdatingPicker;
    private ViewMode? _pendingViewMode;
    private ViewMode _selectionIndicatorMode;

    public MainPage(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
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
        InitializeKeyboardAccelerators();
    }

    public MainViewModel ViewModel { get; }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
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

    private void InitializeKeyboardAccelerators()
    {
        AddKeyboardAccelerator(VirtualKey.P, () => ViewModel.NavigatePreviousCommand.ExecuteAsync(null));
        AddKeyboardAccelerator(VirtualKey.K, () => ViewModel.NavigatePreviousCommand.ExecuteAsync(null));
        AddKeyboardAccelerator(VirtualKey.N, () => ViewModel.NavigateNextCommand.ExecuteAsync(null));
        AddKeyboardAccelerator(VirtualKey.J, () => ViewModel.NavigateNextCommand.ExecuteAsync(null));
        AddKeyboardAccelerator(VirtualKey.T, () => ViewModel.NavigateTodayCommand.ExecuteAsync(null));
        AddKeyboardAccelerator(VirtualKey.G, OpenJumpToDatePicker);
        AddKeyboardAccelerator(VirtualKey.Number1, () => SwitchViewModeAsync(ViewMode.Year));
        AddKeyboardAccelerator(VirtualKey.Y, () => SwitchViewModeAsync(ViewMode.Year));
        AddKeyboardAccelerator(VirtualKey.Number2, () => SwitchViewModeAsync(ViewMode.Month));
        AddKeyboardAccelerator(VirtualKey.M, () => SwitchViewModeAsync(ViewMode.Month));
        AddKeyboardAccelerator(VirtualKey.Number3, () => SwitchViewModeAsync(ViewMode.Week));
        AddKeyboardAccelerator(VirtualKey.W, () => SwitchViewModeAsync(ViewMode.Week));
        AddKeyboardAccelerator(VirtualKey.Number4, () => SwitchViewModeAsync(ViewMode.Day));
        AddKeyboardAccelerator(VirtualKey.D, () => SwitchViewModeAsync(ViewMode.Day));
    }

    private void AddKeyboardAccelerator(VirtualKey key, Func<Task> action)
    {
        var accelerator = new KeyboardAccelerator
        {
            Key = key
        };
        accelerator.Invoked += (sender, e) =>
        {
            e.Handled = true;
            _ = action();
        };

        KeyboardAccelerators.Add(accelerator);
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
            button.CornerRadius = new CornerRadius(12);
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
        await ViewModel.SwitchViewModeCommand.ExecuteAsync(mode);
    }
}

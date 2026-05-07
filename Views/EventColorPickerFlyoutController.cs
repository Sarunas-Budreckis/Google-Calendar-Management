using GoogleCalendarManagement.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed class EventColorPickerFlyoutController
{
    private const int Columns = 6;
    private const double SwatchSize = 16;
    private const double ButtonSize = 20;
    private const double CursorOffset = 6;

    private readonly Func<string?> _getSelectedColorId;
    private readonly Func<EventColorPickerMenuState> _getMenuState;
    private readonly Func<string, Task> _onColorSelectedAsync;
    private readonly Func<Task> _onRevertAsync;
    private readonly Func<Task> _onTogglePendingPublishSelectionAsync;
    private readonly Dictionary<string, ColorOptionVisual> _optionVisuals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Flyout _flyout;
    private readonly StackPanel _actionPanel;
    private readonly Button _togglePendingPublishSelectionButton;
    private readonly Button _revertButton;

    public EventColorPickerFlyoutController(
        IReadOnlyList<CalendarColorOption> colorOptions,
        Func<string?> getSelectedColorId,
        Func<string, Task> onColorSelectedAsync,
        Func<EventColorPickerMenuState>? getMenuState = null,
        Func<Task>? onRevertAsync = null,
        Func<Task>? onTogglePendingPublishSelectionAsync = null)
    {
        _getSelectedColorId = getSelectedColorId;
        _getMenuState = getMenuState ?? (() => new EventColorPickerMenuState());
        _onColorSelectedAsync = onColorSelectedAsync;
        _onRevertAsync = onRevertAsync ?? (() => Task.CompletedTask);
        _onTogglePendingPublishSelectionAsync = onTogglePendingPublishSelectionAsync ?? (() => Task.CompletedTask);
        _togglePendingPublishSelectionButton = CreateActionButton("Select for Push", TogglePendingPublishSelectionButton_Click);
        _revertButton = CreateActionButton("Revert", RevertButton_Click);
        _actionPanel = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                _togglePendingPublishSelectionButton,
                _revertButton,
                CreateDeleteButton()
            }
        };
        _flyout = new Flyout
        {
            AreOpenCloseAnimationsEnabled = false,
            Content = BuildContent(colorOptions)
        };
        _flyout.Opened += Flyout_Opened;
    }

    public void ShowAt(FrameworkElement target)
    {
        RefreshVisualState();
        _flyout.ShowAt(target);
    }

    public void ShowAt(FrameworkElement target, Point position)
    {
        RefreshVisualState();
        _flyout.ShowAt(target, new FlyoutShowOptions
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            Position = new Point(position.X + CursorOffset, position.Y + CursorOffset)
        });
    }

    public void Hide()
    {
        _flyout.Hide();
    }

    public void RefreshVisualState()
    {
        var menuState = _getMenuState();
        _togglePendingPublishSelectionButton.Visibility = menuState.ShowPendingPublishSelectionToggle
            ? Visibility.Visible
            : Visibility.Collapsed;
        _togglePendingPublishSelectionButton.Content = menuState.IsSelectedForPush
            ? "Deselect from Push"
            : "Select for Push";
        _revertButton.Visibility = menuState.ShowRevert ? Visibility.Visible : Visibility.Collapsed;
        var selectedColorId = _getSelectedColorId() ?? string.Empty;
        foreach (var (key, visual) in _optionVisuals)
        {
            var isSelected = string.Equals(key, selectedColorId, StringComparison.OrdinalIgnoreCase);
            visual.CheckIcon.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private FrameworkElement BuildContent(IReadOnlyList<CalendarColorOption> colorOptions)
    {
        var rows = Math.Max(2, (int)Math.Ceiling(colorOptions.Count / (double)Columns));
        var slotCount = rows * Columns;
        var grid = new Grid
        {
            ColumnSpacing = 2,
            RowSpacing = 2,
            XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled
        };

        for (var column = 0; column < Columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        _optionVisuals.Clear();

        for (var index = 0; index < slotCount; index++)
        {
            if (index < colorOptions.Count)
            {
                var option = colorOptions[index];
                var button = CreateColorOptionButton(option);
                Grid.SetRow(button, index / Columns);
                Grid.SetColumn(button, index % Columns);
                grid.Children.Add(button);
            }
            else
            {
                var spacer = new Border
                {
                    Width = ButtonSize,
                    Height = ButtonSize,
                    IsHitTestVisible = false
                };
                Grid.SetRow(spacer, index / Columns);
                Grid.SetColumn(spacer, index % Columns);
                grid.Children.Add(spacer);
            }
        }

        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                _actionPanel,
                new Border
                {
                    Padding = new Thickness(2),
                    Child = grid
                }
            }
        };
    }

    private Button CreateColorOptionButton(CalendarColorOption option)
    {
        var checkIcon = new FontIcon
        {
            Glyph = "\uE73E",
            FontSize = 10,
            Foreground = CreateBrush(option.ContrastTextHex),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var swatch = new Border
        {
            Width = SwatchSize,
            Height = SwatchSize,
            CornerRadius = new CornerRadius(999),
            Background = CreateBrush(option.Hex),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = checkIcon
        };

        var hostBorder = new Border
        {
            Width = ButtonSize,
            Height = ButtonSize,
            CornerRadius = new CornerRadius(999),
            Child = swatch
        };

        var button = new Button
        {
            Tag = option.Key,
            Width = ButtonSize,
            Height = ButtonSize,
            Padding = new Thickness(0),
            Background = null,
            BorderBrush = null,
            BorderThickness = new Thickness(0),
            Content = hostBorder
        };
        button.Resources["ButtonBackground"] = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBorderBrush"] = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Colors.Transparent);

        ToolTipService.SetToolTip(button, option.DisplayName);
        AutomationProperties.SetName(button, $"{option.DisplayName} ({option.Hex})");
        button.Click += ColorOptionButton_Click;
        _optionVisuals[option.Key] = new ColorOptionVisual(button, hostBorder, checkIcon);
        return button;
    }

    private Button CreateActionButton(string label, RoutedEventHandler clickHandler)
    {
        var button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(6, 3, 6, 3),
            MinWidth = 96
        };
        button.Click += clickHandler;
        return button;
    }

    private Button CreateDeleteButton()
    {
        var button = CreateActionButton("Delete", static (_, _) => { });
        button.IsEnabled = false;
        return button;
    }

    private async void ColorOptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string colorKey)
        {
            return;
        }

        await _onColorSelectedAsync(colorKey);
        Hide();
    }

    private async void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        await _onRevertAsync();
        Hide();
    }

    private async void TogglePendingPublishSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        await _onTogglePendingPublishSelectionAsync();
        RefreshVisualState();
        Hide();
    }

    private void Flyout_Opened(object? sender, object e)
    {
        RefreshVisualState();
        var selectedColorId = _getSelectedColorId() ?? string.Empty;
        if (_optionVisuals.TryGetValue(selectedColorId, out var visual))
        {
            _ = visual.Button.DispatcherQueue.TryEnqueue(() => visual.Button.Focus(FocusState.Programmatic));
        }
    }

    private static Brush CreateBrush(string hex)
    {
        var normalizedHex = hex.TrimStart('#');
        var red = Convert.ToByte(normalizedHex.Substring(0, 2), 16);
        var green = Convert.ToByte(normalizedHex.Substring(2, 2), 16);
        var blue = Convert.ToByte(normalizedHex.Substring(4, 2), 16);
        return new SolidColorBrush(ColorHelper.FromArgb(0xFF, red, green, blue));
    }

    private sealed record ColorOptionVisual(Button Button, Border HostBorder, FontIcon CheckIcon);
}

public sealed record EventColorPickerMenuState(
    bool ShowRevert = false,
    bool ShowPendingPublishSelectionToggle = false,
    bool IsSelectedForPush = false);

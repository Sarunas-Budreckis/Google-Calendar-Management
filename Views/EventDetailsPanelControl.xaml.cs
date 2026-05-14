using System.ComponentModel;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI.Core;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class EventDetailsPanelControl : UserControl
{
    private bool _isSyncingEditors;
    private StackPanel? _editPanel;
    private TextBlock? _saveStatusTextBlock;
    private TextBox? _editTitleTextBox;
    private TextBlock? _titleErrorTextBlock;
    private StackPanel? _editSingleDatePanel;
    private DatePicker? _editSingleDatePicker;
    private Grid? _editDateGrid;
    private DatePicker? _editStartDatePicker;
    private TimePicker? _editStartTimePicker;
    private DatePicker? _editEndDatePicker;
    private TimePicker? _editEndTimePicker;
    private TextBlock? _dateTimeErrorTextBlock;
    private TextBox? _editDescriptionTextBox;
    private Button? _editColorButton;
    private Border? _editColorSwatch;
    private TextBlock? _editColorTextBlock;
    private Flyout? _colorPickerFlyout;
    private TextBlock? _editSourceTextBlock;
    private TextBlock? _editLastSavedTextBlock;
    private Button? _editSaveButton;
    private Button? _editRevertButton;
    private Button? _editDeleteButton;
    private TextBlock? _pendingDeleteStatusTextBlock;
    private readonly Dictionary<string, Button> _colorOptionButtons = new(StringComparer.OrdinalIgnoreCase);

    public EventDetailsPanelControl(EventDetailsPanelViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnPanelKeyDown;
    }

    public EventDetailsPanelViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncVisibility(animate: false);
        SyncEditMode();
        UpdateColorSwatches();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EventDetailsPanelViewModel.IsPanelVisible))
        {
            SyncVisibility(animate: true);
        }

        if (e.PropertyName == nameof(EventDetailsPanelViewModel.IsEditMode))
        {
            SyncEditMode();
        }

        if (e.PropertyName is nameof(EventDetailsPanelViewModel.ColorHex)
            or nameof(EventDetailsPanelViewModel.ColorName)
            or nameof(EventDetailsPanelViewModel.EditColorHex)
            or nameof(EventDetailsPanelViewModel.EditColorName)
            or nameof(EventDetailsPanelViewModel.EditColorId))
        {
            UpdateColorSwatches();
        }

        if (_editPanel is not null && e.PropertyName is nameof(EventDetailsPanelViewModel.EditTitle)
            or nameof(EventDetailsPanelViewModel.EditSingleDate)
            or nameof(EventDetailsPanelViewModel.UsesSingleDateEditor)
            or nameof(EventDetailsPanelViewModel.EditStartDate)
            or nameof(EventDetailsPanelViewModel.EditStartTime)
            or nameof(EventDetailsPanelViewModel.EditEndDate)
            or nameof(EventDetailsPanelViewModel.EditEndTime)
            or nameof(EventDetailsPanelViewModel.EditDescription)
            or nameof(EventDetailsPanelViewModel.TitleError)
            or nameof(EventDetailsPanelViewModel.DateTimeError)
            or nameof(EventDetailsPanelViewModel.SaveStatusText)
            or nameof(EventDetailsPanelViewModel.RevertButtonVisibility)
            or nameof(EventDetailsPanelViewModel.IsPendingDeleteEvent)
            or nameof(EventDetailsPanelViewModel.EditColorHex)
            or nameof(EventDetailsPanelViewModel.EditColorName)
            or nameof(EventDetailsPanelViewModel.EditColorId)
            or nameof(EventDetailsPanelViewModel.SourceDisplay)
            or nameof(EventDetailsPanelViewModel.LastSavedLocallyDisplay))
        {
            SyncEditPanelFromViewModel();
        }
    }

    private void SyncVisibility(bool animate)
    {
        VisualStateManager.GoToState(this, ViewModel.IsPanelVisible ? "EditMode" : "EmptyState", false);
    }

    private void SyncEditMode()
    {
        if (!ViewModel.IsEditMode)
        {
            return;
        }

        EnsureEditPanel();
        SyncEditPanelFromViewModel();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (_editTitleTextBox is not null)
            {
                _editTitleTextBox.Focus(FocusState.Programmatic);
                if (ViewModel.IsNewUneditedDraft)
                {
                    _editTitleTextBox.SelectAll();
                }
            }
        });
    }

    private void EnsureEditPanel()
    {
        if (_editPanel is not null)
        {
            return;
        }

        _saveStatusTextBlock = new TextBlock();
        _editTitleTextBox = new TextBox
        {
            PlaceholderText = "New event"
        };
        _editTitleTextBox.TextChanged += EditTitleTextBox_TextChanged;
        _titleErrorTextBlock = CreateErrorTextBlock();
        _editSingleDatePicker = new DatePicker();
        _editSingleDatePicker.DateChanged += EditSingleDatePicker_DateChanged;
        _editSingleDatePanel = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                CreateFieldLabelTextBlock("Date"),
                _editSingleDatePicker
            }
        };

        _editStartDatePicker = new DatePicker { Header = "Start date" };
        _editStartDatePicker.DateChanged += EditStartDatePicker_DateChanged;
        _editEndDatePicker = new DatePicker { Header = "End date" };
        _editEndDatePicker.DateChanged += EditEndDatePicker_DateChanged;
        _editDateGrid = new Grid
        {
            RowSpacing = 8
        };
        _editDateGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _editDateGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_editStartDatePicker, 0);
        Grid.SetRow(_editEndDatePicker, 1);
        _editDateGrid.Children.Add(_editStartDatePicker);
        _editDateGrid.Children.Add(_editEndDatePicker);

        _editStartTimePicker = new TimePicker { Header = "Start time", MinuteIncrement = 15 };
        _editStartTimePicker.TimeChanged += EditStartTimePicker_TimeChanged;
        _editEndTimePicker = new TimePicker { Header = "End time", MinuteIncrement = 15 };
        _editEndTimePicker.TimeChanged += EditEndTimePicker_TimeChanged;
        _dateTimeErrorTextBlock = CreateErrorTextBlock();
        _editDescriptionTextBox = new TextBox
        {
            AcceptsReturn = true,
            MinHeight = 120,
            TextWrapping = TextWrapping.Wrap
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_editDescriptionTextBox, ScrollBarVisibility.Auto);
        _editDescriptionTextBox.TextChanged += EditDescriptionTextBox_TextChanged;
        _editColorSwatch = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(3) };
        _editColorTextBlock = new TextBlock();
        _editColorButton = CreateColorButton();
        _colorPickerFlyout = CreateColorPickerFlyout();
        _editColorButton.Flyout = _colorPickerFlyout;
        _editSourceTextBlock = new TextBlock();
        _editLastSavedTextBlock = new TextBlock();
        _editSaveButton = new Button
        {
            Content = "Save"
        };
        _editSaveButton.Click += EditSaveButton_Click;
        _editRevertButton = new Button
        {
            Content = "Revert"
        };
        // Use AddHandler so the button captures PointerPressed even when the
        // ScrollViewer or a focused TextBox has already marked the event as handled.
        _editRevertButton.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((s, _) => ((UIElement)s).Focus(FocusState.Pointer)),
            handledEventsToo: true);
        _editRevertButton.Click += EditRevertButton_Click;
        _editDeleteButton = new Button
        {
            Content = "Delete"
        };
        if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBrush", out var criticalBrush) &&
            criticalBrush is Microsoft.UI.Xaml.Media.Brush dangerBrush)
        {
            _editDeleteButton.Foreground = dangerBrush;
        }
        _editDeleteButton.Click += EditDeleteButton_Click;
        _pendingDeleteStatusTextBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out var cautionBrush) &&
            cautionBrush is Microsoft.UI.Xaml.Media.Brush warningBrush)
        {
            _pendingDeleteStatusTextBlock.Foreground = warningBrush;
        }

        var timeGrid = new Grid
        {
            ColumnSpacing = 12
        };
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var startStack = new StackPanel { Spacing = 8 };
        startStack.Children.Add(_editStartTimePicker);
        Grid.SetColumn(startStack, 0);
        timeGrid.Children.Add(startStack);

        var endStack = new StackPanel { Spacing = 8 };
        endStack.Children.Add(_editEndTimePicker);
        Grid.SetColumn(endStack, 1);
        timeGrid.Children.Add(endStack);

        var rightButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                _editRevertButton,
                _editSaveButton
            }
        };
        var actionButtons = new Grid();
        actionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(_editDeleteButton!, 0);
        actionButtons.Children.Add(_editDeleteButton);
        Grid.SetColumn(rightButtons, 2);
        actionButtons.Children.Add(rightButtons);

        _editPanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                _saveStatusTextBlock,
                CreateFieldLabelTextBlock("Title"),
                _editTitleTextBox,
                _titleErrorTextBlock,
                _editSingleDatePanel,
                _editDateGrid,
                timeGrid,
                _dateTimeErrorTextBlock,
                CreateFieldLabelTextBlock("Description"),
                _editDescriptionTextBox,
                CreateFieldLabelTextBlock("Color"),
                _editColorButton,
                CreateFieldLabelTextBlock("Source"),
                _editSourceTextBlock,
                _pendingDeleteStatusTextBlock,
                CreateFieldLabelTextBlock("Last Saved Locally"),
                _editLastSavedTextBlock,
                actionButtons
            }
        };

        EditPanelHost.Children.Add(_editPanel);
    }

    private static TextBlock CreateErrorTextBlock()
    {
        return new TextBlock
        {
            Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemFillColorCritical"]),
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock CreateFieldLabelTextBlock(string text)
    {
        var label = new TextBlock
        {
            Text = text
        };

        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush) &&
            brush is Brush secondaryBrush)
        {
            label.Foreground = secondaryBrush;
        }

        if (Application.Current.Resources.TryGetValue("CaptionTextBlockStyle", out var style) &&
            style is Style textStyle)
        {
            label.Style = textStyle;
        }

        return label;
    }

    private Button CreateColorButton()
    {
        var chevron = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var contentGrid = new Grid
        {
            ColumnSpacing = 8
        };
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(_editColorSwatch!, 0);
        contentGrid.Children.Add(_editColorSwatch);

        Grid.SetColumn(_editColorTextBlock!, 1);
        contentGrid.Children.Add(_editColorTextBlock);

        Grid.SetColumn(chevron, 2);
        contentGrid.Children.Add(chevron);

        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = contentGrid
        };

        AutomationProperties.SetName(button, "Choose event color");
        return button;
    }

    private Flyout CreateColorPickerFlyout()
    {
        var grid = new Grid
        {
            ColumnSpacing = 2,
            RowSpacing = 2,
            XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled
        };

        for (var column = 0; column < 6; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        for (var row = 0; row < 2; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        _colorOptionButtons.Clear();

        for (var index = 0; index < 12; index++)
        {
            if (index < ViewModel.AvailableColors.Count)
            {
                var option = ViewModel.AvailableColors[index];
                var optionButton = CreateColorOptionButton(option);
                _colorOptionButtons[option.Key] = optionButton;
                Grid.SetRow(optionButton, index / 6);
                Grid.SetColumn(optionButton, index % 6);
                grid.Children.Add(optionButton);
            }
            else
            {
                var spacer = new Border
                {
                    Width = 20,
                    Height = 20,
                    IsHitTestVisible = false
                };
                Grid.SetRow(spacer, index / 6);
                Grid.SetColumn(spacer, index % 6);
                grid.Children.Add(spacer);
            }
        }

        var flyout = new Flyout
        {
            Content = new Border
            {
                Padding = new Thickness(4),
                Child = grid
            }
        };

        flyout.Opened += ColorPickerFlyout_Opened;
        return flyout;
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
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(999),
            Background = CreateBrush(option.DisplayHex),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = checkIcon
        };

        var hostBorder = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(999),
            Child = swatch
        };

        var button = new Button
        {
            Tag = option.Key,
            Padding = new Thickness(0),
            Background = null,
            BorderBrush = null,
            BorderThickness = new Thickness(0),
            Content = hostBorder,
            Width = 20,
            Height = 20
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
        return button;
    }

    private void SyncEditPanelFromViewModel()
    {
        if (_editPanel is null ||
            _editTitleTextBox is null ||
            _editSingleDatePicker is null ||
            _editSingleDatePanel is null ||
            _editDateGrid is null ||
            _editStartDatePicker is null ||
            _editStartTimePicker is null ||
            _editEndDatePicker is null ||
            _editEndTimePicker is null ||
            _editDescriptionTextBox is null ||
            _saveStatusTextBlock is null ||
            _titleErrorTextBlock is null ||
            _dateTimeErrorTextBlock is null ||
            _editColorButton is null ||
            _editColorTextBlock is null ||
            _editSourceTextBlock is null ||
            _editLastSavedTextBlock is null ||
            _editRevertButton is null ||
            _editDeleteButton is null ||
            _pendingDeleteStatusTextBlock is null)
        {
            return;
        }

        _isSyncingEditors = true;
        _editTitleTextBox.Text = ViewModel.EditTitle;
        _editSingleDatePicker.Date = ToDateTimeOffset(ViewModel.EditSingleDate);
        _editStartDatePicker.Date = ToDateTimeOffset(ViewModel.EditStartDate);
        _editStartTimePicker.Time = ViewModel.EditStartTime.ToTimeSpan();
        _editEndDatePicker.Date = ToDateTimeOffset(ViewModel.EditEndDate);
        _editEndTimePicker.Time = ViewModel.EditEndTime.ToTimeSpan();
        _editDescriptionTextBox.Text = ViewModel.EditDescription;
        _saveStatusTextBlock.Text = ViewModel.SaveStatusText;
        _titleErrorTextBlock.Text = ViewModel.TitleError;
        _dateTimeErrorTextBlock.Text = ViewModel.DateTimeError;
        _editColorTextBlock.Text = ViewModel.EditColorName;
        AutomationProperties.SetName(_editColorButton, $"Choose event color, current selection {ViewModel.EditColorName}");
        _editSourceTextBlock.Text = ViewModel.SourceDisplay;
        _editLastSavedTextBlock.Text = ViewModel.LastSavedLocallyDisplay;
        _editSingleDatePanel.Visibility = ViewModel.UsesSingleDateEditor ? Visibility.Visible : Visibility.Collapsed;
        _editDateGrid.Visibility = ViewModel.UsesSingleDateEditor ? Visibility.Collapsed : Visibility.Visible;
        _editRevertButton.Visibility = ViewModel.RevertButtonVisibility;
        if (ViewModel.IsPendingDeleteEvent)
        {
            _pendingDeleteStatusTextBlock.Text = ViewModel.SourceDisplay;
            _pendingDeleteStatusTextBlock.Visibility = Visibility.Visible;
        }
        else
        {
            _pendingDeleteStatusTextBlock.Visibility = Visibility.Collapsed;
        }
        UpdateColorSwatches();
        UpdateColorPickerSelection();
        _isSyncingEditors = false;
    }

    private async void OnPanelKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            if (await ViewModel.HandleEscapeAsync())
            {
                e.Handled = true;
            }

            return;
        }

        var ctrlPressed = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
        if (ctrlPressed && e.Key == VirtualKey.Z && ViewModel.IsEditMode)
        {
            await ViewModel.UndoLastInteractiveChangeAsync();
            e.Handled = true;
        }
    }

    private void EditTitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncingEditors && _editTitleTextBox is not null)
        {
            ViewModel.EditTitle = _editTitleTextBox.Text;
        }
    }

    private void EditSingleDatePicker_DateChanged(object? sender, DatePickerValueChangedEventArgs args)
    {
        if (!_isSyncingEditors && sender is DatePicker datePicker)
        {
            ViewModel.EditSingleDate = DateOnly.FromDateTime(datePicker.Date.DateTime);
        }
    }

    private void EditStartDatePicker_DateChanged(object? sender, DatePickerValueChangedEventArgs args)
    {
        if (!_isSyncingEditors && sender is DatePicker datePicker)
        {
            ViewModel.EditStartDate = DateOnly.FromDateTime(datePicker.Date.DateTime);
        }
    }

    private void EditStartTimePicker_TimeChanged(object? sender, TimePickerValueChangedEventArgs args)
    {
        if (!_isSyncingEditors && sender is TimePicker timePicker)
        {
            ViewModel.EditStartTime = TimeOnly.FromTimeSpan(timePicker.Time);
        }
    }

    private void EditEndDatePicker_DateChanged(object? sender, DatePickerValueChangedEventArgs args)
    {
        if (!_isSyncingEditors && sender is DatePicker datePicker)
        {
            ViewModel.EditEndDate = DateOnly.FromDateTime(datePicker.Date.DateTime);
        }
    }

    private void EditEndTimePicker_TimeChanged(object? sender, TimePickerValueChangedEventArgs args)
    {
        if (!_isSyncingEditors && sender is TimePicker timePicker)
        {
            ViewModel.EditEndTime = TimeOnly.FromTimeSpan(timePicker.Time);
        }
    }

    private void EditDescriptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isSyncingEditors && _editDescriptionTextBox is not null)
        {
            ViewModel.EditDescription = _editDescriptionTextBox.Text;
        }
    }

    private async void EditSaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveAndExitEditModeAsync();
    }

    private async void EditRevertButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RevertPendingChangesAsync();
        // After the revert the Revert button becomes Collapsed (IsPendingEvent → false),
        // which causes WinUI 3 to auto-move focus back to the title TextBox.
        // Explicitly land focus on the Save button so it doesn't return to a TextBox.
        _editSaveButton?.Focus(FocusState.Programmatic);
    }

    private async void EditDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.DeleteEventAsync();
    }

    private void ColorPickerFlyout_Opened(object? sender, object e)
    {
        UpdateColorPickerSelection();
        if (_colorOptionButtons.TryGetValue(ViewModel.EditColorId, out var selectedButton))
        {
            _ = DispatcherQueue.TryEnqueue(() => selectedButton.Focus(FocusState.Programmatic));
        }
    }

    private async void ColorOptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string colorKey)
        {
            return;
        }

        await ViewModel.SelectColorAsync(colorKey);
        _colorPickerFlyout?.Hide();
    }

    private void UpdateColorSwatches()
    {
        if (_editColorSwatch is not null)
        {
            UpdateColorSwatch(_editColorSwatch, ViewModel.EditColorHex);
        }
    }

    private void UpdateColorPickerSelection()
    {
        foreach (var (key, button) in _colorOptionButtons)
        {
            if (button.Content is not Border hostBorder ||
                hostBorder.Child is not Border swatchBorder ||
                swatchBorder.Child is not FontIcon checkIcon)
            {
                continue;
            }

            var isSelected = string.Equals(key, ViewModel.EditColorId, StringComparison.OrdinalIgnoreCase);
            checkIcon.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void UpdateColorSwatch(Border swatch, string hex)
    {
        swatch.Background = CreateBrush(hex);
    }

    private static Brush? CreateBrush(string hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return null;
        }

        try
        {
            var normalizedHex = hex.TrimStart('#');
            if (normalizedHex.Length == 6)
            {
                var red = Convert.ToByte(normalizedHex.Substring(0, 2), 16);
                var green = Convert.ToByte(normalizedHex.Substring(2, 2), 16);
                var blue = Convert.ToByte(normalizedHex.Substring(4, 2), 16);
                return new SolidColorBrush(ColorHelper.FromArgb(0xFF, red, green, blue));
            }
        }
        catch (FormatException)
        {
        }

        return null;
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date)
    {
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return new DateTimeOffset(localDateTime);
    }
}

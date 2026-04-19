using System.ComponentModel;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI;

namespace GoogleCalendarManagement.Views;

public sealed partial class EventDetailsPanelControl : UserControl
{
    private const double PanelWidth = 380.0;
    private Storyboard? _openStoryboard;
    private Storyboard? _closeStoryboard;
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
    private Border? _editColorSwatch;
    private TextBlock? _editColorTextBlock;
    private TextBlock? _editSourceTextBlock;
    private TextBlock? _editLastSavedTextBlock;
    private Button? _editSaveButton;
    private Button? _editRevertButton;

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
        _openStoryboard?.Stop();
        _closeStoryboard?.Stop();
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
            or nameof(EventDetailsPanelViewModel.ColorName))
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
            or nameof(EventDetailsPanelViewModel.SourceDisplay)
            or nameof(EventDetailsPanelViewModel.LastSavedLocallyDisplay))
        {
            SyncEditPanelFromViewModel();
        }
    }

    private void SyncVisibility(bool animate)
    {
        if (ViewModel.IsPanelVisible)
        {
            OpenPanel(animate);
        }
        else
        {
            ClosePanel(animate);
        }
    }

    private void SyncEditMode()
    {
        VisualStateManager.GoToState(this, ViewModel.IsEditMode ? "EditMode" : "ReadOnlyMode", false);

        if (ViewModel.IsEditMode)
        {
            EnsureEditPanel();
            SyncEditPanelFromViewModel();
            _ = DispatcherQueue.TryEnqueue(() => _editTitleTextBox?.Focus(FocusState.Programmatic));
        }
    }

    private void EnsureEditPanel()
    {
        if (_editPanel is not null)
        {
            return;
        }

        _saveStatusTextBlock = new TextBlock();
        _editTitleTextBox = new TextBox();
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
        ToolTipService.SetToolTip(_editColorSwatch, "Coming soon");
        _editColorTextBlock = new TextBlock();
        ToolTipService.SetToolTip(_editColorTextBlock, "Coming soon");
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
        _editRevertButton.Click += EditRevertButton_Click;

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

        var colorStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                _editColorSwatch,
                _editColorTextBlock
            }
        };

        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children =
            {
                _editRevertButton,
                _editSaveButton
            }
        };

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
                colorStack,
                CreateFieldLabelTextBlock("Source"),
                _editSourceTextBlock,
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
            _editColorTextBlock is null ||
            _editSourceTextBlock is null ||
            _editLastSavedTextBlock is null ||
            _editRevertButton is null)
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
        _editColorTextBlock.Text = ViewModel.ColorName;
        _editSourceTextBlock.Text = ViewModel.SourceDisplay;
        _editLastSavedTextBlock.Text = ViewModel.LastSavedLocallyDisplay;
        _editSingleDatePanel.Visibility = ViewModel.UsesSingleDateEditor ? Visibility.Visible : Visibility.Collapsed;
        _editDateGrid.Visibility = ViewModel.UsesSingleDateEditor ? Visibility.Collapsed : Visibility.Visible;
        _editRevertButton.Visibility = ViewModel.RevertButtonVisibility;
        UpdateColorSwatches();
        _isSyncingEditors = false;
    }

    private void OpenPanel(bool animate)
    {
        _closeStoryboard?.Stop();
        Visibility = Visibility.Visible;
        UpdateColorSwatches();

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
            ViewModel.UndoLastChange();
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
    }

    private void UpdateColorSwatches()
    {
        UpdateColorSwatch(ColorSwatch);

        if (_editColorSwatch is not null)
        {
            UpdateColorSwatch(_editColorSwatch);
        }
    }

    private void UpdateColorSwatch(Border swatch)
    {
        if (string.IsNullOrEmpty(ViewModel.ColorHex))
        {
            swatch.Background = null;
            return;
        }

        try
        {
            var hex = ViewModel.ColorHex.TrimStart('#');
            if (hex.Length == 6)
            {
                var red = Convert.ToByte(hex.Substring(0, 2), 16);
                var green = Convert.ToByte(hex.Substring(2, 2), 16);
                var blue = Convert.ToByte(hex.Substring(4, 2), 16);
                swatch.Background = new SolidColorBrush(ColorHelper.FromArgb(0xFF, red, green, blue));
                return;
            }
        }
        catch (FormatException)
        {
        }

        swatch.Background = null;
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date)
    {
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        return new DateTimeOffset(localDateTime);
    }
}

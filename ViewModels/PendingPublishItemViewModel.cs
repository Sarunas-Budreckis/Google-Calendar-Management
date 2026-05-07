using GoogleCalendarManagement.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GoogleCalendarManagement.ViewModels;

public sealed partial class PendingPublishItemViewModel : ObservableObject
{
    private static readonly Brush DefaultBackgroundBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x2B, 0x2B, 0x2B));
    private static readonly Brush SelectedBackgroundBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1E, 0x4A, 0x29));
    private static readonly Brush DefaultBorderBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x44, 0x44, 0x44));
    private static readonly Brush SelectedBorderBrush =
        new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x4C, 0xAF, 0x50));

    public PendingPublishItemViewModel(
        string pendingEventId,
        string displayEventId,
        CalendarEventSourceKind eventSourceKind,
        string title,
        string dateTimeSummary,
        string sourceLabel,
        string colorKey,
        string colorHex,
        bool isRecurringInstance,
        string? publishError)
    {
        PendingEventId = pendingEventId;
        DisplayEventId = displayEventId;
        EventSourceKind = eventSourceKind;
        Title = title;
        DateTimeSummary = dateTimeSummary;
        SourceLabel = sourceLabel;
        ColorKey = colorKey;
        ColorHex = colorHex;
        IsRecurringInstance = isRecurringInstance;
        PublishErrorDetails = publishError;
    }

    public string PendingEventId { get; }

    public string DisplayEventId { get; }

    public CalendarEventSourceKind EventSourceKind { get; }

    public string Title { get; }

    public string DateTimeSummary { get; }

    public string SourceLabel { get; }

    public string ColorKey { get; }

    public string ColorHex { get; }

    public Brush ColorBrush => new SolidColorBrush(ParseHexColor(ColorHex));

    public bool IsRecurringInstance { get; }

    public string? PublishErrorDetails { get; }

    public string? PublishErrorSummary =>
        string.IsNullOrWhiteSpace(PublishErrorDetails)
            ? null
            : PublishErrorDetails
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
                ?.Trim();

    public Visibility PublishErrorVisibility =>
        string.IsNullOrWhiteSpace(PublishErrorSummary) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PublishErrorDetailsVisibility =>
        string.IsNullOrWhiteSpace(PublishErrorDetails) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RecurringScopeVisibility =>
        IsRecurringInstance ? Visibility.Visible : Visibility.Collapsed;

    public Brush CardBackgroundBrush => IsSelected ? SelectedBackgroundBrush : DefaultBackgroundBrush;

    public Brush CardBorderBrush => IsSelected ? SelectedBorderBrush : DefaultBorderBrush;

    public Thickness CardBorderThickness => IsSelected ? new Thickness(1.5) : new Thickness(1);

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CardBackgroundBrush));
            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
        }
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        var normalizedHex = hex.TrimStart('#');
        if (normalizedHex.Length != 6)
        {
            return ColorHelper.FromArgb(0xFF, 0x00, 0x88, 0xCC);
        }

        return ColorHelper.FromArgb(
            0xFF,
            Convert.ToByte(normalizedHex.Substring(0, 2), 16),
            Convert.ToByte(normalizedHex.Substring(2, 2), 16),
            Convert.ToByte(normalizedHex.Substring(4, 2), 16));
    }
}

using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Views;

internal static class TimeFocusedViewLayoutMetrics
{
    public const double TimeColumnWidth = 72.0;
    public const double HourRowHeight = 72.0;
    public const double ResizeBoundaryThickness = 5.0;
    public const double CompactTimedEventHorizontalPadding = 4.0;
    public const double StandardTimedEventPadding = 6.0;
    public const double DraftOverlayInset = 4.0;
    public const double MinDraftPreviewHeight = 18.0;
    public const double CurrentTimeIndicatorDotOffset = 5.0;

    public static Thickness CreateCompactTimedEventPadding(double topPadding)
    {
        return new Thickness(
            CompactTimedEventHorizontalPadding,
            topPadding,
            CompactTimedEventHorizontalPadding,
            0);
    }
}

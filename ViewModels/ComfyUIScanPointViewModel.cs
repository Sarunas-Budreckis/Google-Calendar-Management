using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class ComfyUIScanPointViewModel
{
    private const double TimelineHeightPx = 480.0;

    public ComfyUIScanPointViewModel(ComfyUIScanPoint point)
    {
        var localModified = NormalizeUtc(point.Timestamp).ToLocalTime();
        ModifiedTimeLabel = localModified.ToString("h:mm tt");
        TooltipText = $"Modified: {ModifiedTimeLabel}";

        var minutesFromMidnight = localModified.Hour * 60.0 + localModified.Minute + localModified.Second / 60.0;
        CanvasTop = minutesFromMidnight / (24.0 * 60.0) * TimelineHeightPx;
    }

    public string ModifiedTimeLabel { get; }
    public string TooltipText { get; }
    public double CanvasTop { get; }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

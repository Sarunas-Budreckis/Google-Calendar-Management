using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class Civ5SessionPointViewModel
{
    private const double TimelineHeightPx = 480.0;

    public Civ5SessionPointViewModel(Civ5SessionPoint point)
    {
        var local = NormalizeUtc(point.FileModifiedAt).ToLocalTime();
        TimeLabel = local.ToString("HH:mm");
        GameMode = FormatMode(point.GameMode);
        TooltipText = $"{TimeLabel} — {GameMode}";

        var minutesFromMidnight = local.Hour * 60.0 + local.Minute + local.Second / 60.0;
        CanvasTop = minutesFromMidnight / (24.0 * 60.0) * TimelineHeightPx;
    }

    public string TimeLabel { get; }
    public string GameMode { get; }
    public string TooltipText { get; }
    public double CanvasTop { get; }

    private static string FormatMode(string gameMode) => gameMode switch
    {
        "single" => "single-player",
        "multi" => "multiplayer",
        "hotseat" => "hotseat",
        "pbem" => "play-by-email",
        "pitboss" => "Pitboss",
        _ => gameMode
    };

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}

using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MapsTimelineSegmentViewModel
{
    public MapsTimelineSegmentViewModel(MapsTimelineSegment segment)
    {
        var localStart = segment.StartTime.ToLocalTime();
        var localEnd = segment.EndTime.ToLocalTime();

        StartLabel = localStart.ToString("HH:mm");
        EndLabel = localEnd.ToString("HH:mm");
        DurationLabel = FormatDuration(segment.EndTime - segment.StartTime);
        TypeLabel = segment.IsVisit
            ? segment.LocationName ?? "Visit"
            : FormatActivityType(segment.ActivityType);
    }

    public string StartLabel { get; }
    public string EndLabel { get; }
    public string DurationLabel { get; }
    public string TypeLabel { get; }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";
        }

        return $"{duration.Minutes}m";
    }

    private static string FormatActivityType(string? rawType)
    {
        return rawType switch
        {
            "IN_VEHICLE" => "Driving",
            "IN_CAR" => "Driving",
            "ON_BICYCLE" => "Cycling",
            "WALKING" => "Walking",
            "RUNNING" => "Running",
            "STILL" => "Stationary",
            "IN_TRAIN" => "Train",
            "IN_BUS" => "Bus",
            "IN_SUBWAY" => "Subway",
            "FLYING" => "Flight",
            null or "" => "Activity",
            _ => rawType
        };
    }
}

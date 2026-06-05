using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public sealed record Civ5CandidateWindow(
    DateTime WindowStart,
    DateTime WindowEnd,
    IReadOnlyList<string> GameModes);

public static class Civ5SessionCoalescer
{
    private static readonly TimeSpan CoalesceGap = TimeSpan.FromMinutes(30);

    public static IReadOnlyList<Civ5CandidateWindow> CoalesceIntoWindows(
        IReadOnlyList<Civ5SessionPoint> points,
        TimeSpan? coalesceGap = null)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var gap = coalesceGap ?? CoalesceGap;
        var sorted = points.OrderBy(p => p.FileModifiedAt).ToList();
        var windows = new List<Civ5CandidateWindow>();

        var windowStart = sorted[0].FileModifiedAt;
        var windowEnd = sorted[0].FileModifiedAt;
        var modes = new List<string> { sorted[0].GameMode };

        for (var i = 1; i < sorted.Count; i++)
        {
            var point = sorted[i];
            // "within 30 minutes" is exclusive — exactly 30 min = new window
            if (point.FileModifiedAt - windowEnd < gap)
            {
                if (point.FileModifiedAt > windowEnd)
                {
                    windowEnd = point.FileModifiedAt;
                }
                modes.Add(point.GameMode);
            }
            else
            {
                windows.Add(new Civ5CandidateWindow(windowStart, windowEnd, modes.Distinct().ToList()));
                windowStart = point.FileModifiedAt;
                windowEnd = point.FileModifiedAt;
                modes = [point.GameMode];
            }
        }

        windows.Add(new Civ5CandidateWindow(windowStart, windowEnd, modes.Distinct().ToList()));
        return windows;
    }

    public static string GetEventTitle(Civ5CandidateWindow window)
    {
        return window.GameModes.Count > 1 ? "Civ 5 (mixed)" : "Civ 5";
    }
}

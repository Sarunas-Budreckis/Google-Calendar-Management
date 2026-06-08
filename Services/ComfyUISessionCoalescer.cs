using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public sealed record ComfyUICandidateWindow(DateTime WindowStart, DateTime WindowEnd);

public static class ComfyUISessionCoalescer
{
    private static readonly TimeSpan CoalesceGap = TimeSpan.FromMinutes(15);

    public static IReadOnlyList<ComfyUICandidateWindow> CoalesceIntoWindows(
        IReadOnlyList<ComfyUIScanPoint> points,
        TimeSpan? coalesceGap = null)
    {
        if (points.Count == 0)
        {
            return [];
        }

        var gap = coalesceGap ?? CoalesceGap;
        var sorted = points.OrderBy(p => p.Timestamp).ToList();
        var windows = new List<ComfyUICandidateWindow>();

        var windowStart = sorted[0].Timestamp;
        var windowEnd = sorted[0].Timestamp;

        for (var i = 1; i < sorted.Count; i++)
        {
            var point = sorted[i];
            if (point.Timestamp - windowEnd < gap)
            {
                if (point.Timestamp > windowEnd)
                {
                    windowEnd = point.Timestamp;
                }
            }
            else
            {
                windows.Add(new ComfyUICandidateWindow(windowStart, windowEnd));
                windowStart = point.Timestamp;
                windowEnd = point.Timestamp;
            }
        }

        windows.Add(new ComfyUICandidateWindow(windowStart, windowEnd));
        return windows;
    }
}

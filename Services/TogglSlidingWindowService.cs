namespace GoogleCalendarManagement.Services;

public sealed class TogglSlidingWindowService
{
    public record SlidingWindowEntry(DateTime StartUtc, DateTime EndUtc);

    public record SlidingWindowResult(DateTime WindowStartUtc, DateTime WindowEndUtc);

    public IReadOnlyList<SlidingWindowResult> ComputeWindows(
        IEnumerable<SlidingWindowEntry> entries,
        TimeSpan gapThreshold,
        double qualityThreshold,
        TimeSpan minWindowDuration)
    {
        var sorted = entries.OrderBy(e => e.StartUtc).ToList();
        if (sorted.Count == 0)
        {
            return [];
        }

        var windows = BuildWindows(sorted, gapThreshold);
        var result = new List<SlidingWindowResult>();

        foreach (var (windowStart, windowEnd, coveredEntries) in windows)
        {
            var windowDuration = windowEnd - windowStart;
            if (windowDuration <= TimeSpan.Zero)
            {
                continue;
            }

            var coveredDuration = coveredEntries.Aggregate(TimeSpan.Zero,
                (acc, entry) => acc + (entry.EndUtc - entry.StartUtc));

            var coverage = coveredDuration.TotalSeconds / windowDuration.TotalSeconds;

            if (coverage >= qualityThreshold)
            {
                if (windowDuration >= minWindowDuration)
                {
                    result.Add(new SlidingWindowResult(windowStart, windowEnd));
                }
            }
            else
            {
                // Retry with tighter gap (5 minutes)
                var retryGap = TimeSpan.FromMinutes(5);
                var retryWindows = BuildWindows(coveredEntries, retryGap);
                foreach (var (rStart, rEnd, _) in retryWindows)
                {
                    var rDuration = rEnd - rStart;
                    if (rDuration >= minWindowDuration)
                    {
                        result.Add(new SlidingWindowResult(rStart, rEnd));
                    }
                }
            }
        }

        return result;
    }

    private static List<(DateTime Start, DateTime End, List<SlidingWindowEntry> Entries)> BuildWindows(
        List<SlidingWindowEntry> sorted, TimeSpan gap)
    {
        var windows = new List<(DateTime Start, DateTime End, List<SlidingWindowEntry> Entries)>();
        if (sorted.Count == 0)
        {
            return windows;
        }

        var windowStart = sorted[0].StartUtc;
        var windowEnd = sorted[0].EndUtc;
        var windowEntries = new List<SlidingWindowEntry> { sorted[0] };

        for (var i = 1; i < sorted.Count; i++)
        {
            var entry = sorted[i];
            if (entry.StartUtc <= windowEnd + gap)
            {
                if (entry.EndUtc > windowEnd)
                {
                    windowEnd = entry.EndUtc;
                }

                windowEntries.Add(entry);
            }
            else
            {
                windows.Add((windowStart, windowEnd, windowEntries));
                windowStart = entry.StartUtc;
                windowEnd = entry.EndUtc;
                windowEntries = [entry];
            }
        }

        windows.Add((windowStart, windowEnd, windowEntries));
        return windows;
    }
}

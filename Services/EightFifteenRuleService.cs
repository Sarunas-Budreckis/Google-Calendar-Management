namespace GoogleCalendarManagement.Services;

public sealed class EightFifteenRuleService
{
    private const int BlockMinutes = 15;
    private const int MinActivityMinutes = 8;

    public IReadOnlyList<(DateTime Start, DateTime End)> ApplyRule(DateTime tripStart, DateTime tripEnd)
    {
        if (tripEnd <= tripStart)
        {
            return [(RoundToNearestQuarterHour(tripStart), RoundToNearestQuarterHour(tripStart).AddMinutes(BlockMinutes))];
        }

        var totalDuration = tripEnd - tripStart;
        var blockCount = Math.Max(1, (int)Math.Ceiling(totalDuration.TotalMinutes / BlockMinutes));

        var kept = new List<(DateTime Start, DateTime End)>();
        for (var i = 0; i < blockCount; i++)
        {
            var blockStart = tripStart.AddMinutes(i * BlockMinutes);
            var blockEnd = i == blockCount - 1
                ? tripEnd
                : tripStart.AddMinutes((i + 1) * BlockMinutes);

            var blockDurationMinutes = (blockEnd - blockStart).TotalMinutes;
            var isLastBlock = i == blockCount - 1;

            if (blockDurationMinutes >= MinActivityMinutes || isLastBlock)
            {
                var roundedStart = RoundToNearestQuarterHour(blockStart);
                var roundedEnd = RoundToNearestQuarterHour(blockEnd);
                if (roundedEnd <= roundedStart)
                {
                    roundedEnd = roundedStart.AddMinutes(BlockMinutes);
                }

                kept.Add((roundedStart, roundedEnd));
            }
        }

        if (kept.Count == 0)
        {
            var start = RoundToNearestQuarterHour(tripStart);
            kept.Add((start, start.AddMinutes(BlockMinutes)));
        }

        return kept;
    }

    private static DateTime RoundToNearestQuarterHour(DateTime value)
    {
        return CalendarDraftTiming.RoundToNearestQuarterHour(value);
    }
}

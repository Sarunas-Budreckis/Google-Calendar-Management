using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public static class YearViewDayProjectionBuilder
{
    public static YearViewProjectionResult Build(
        IEnumerable<DateOnly> visibleDates,
        IEnumerable<CalendarEventDisplayModel> currentEvents,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap)
    {
        ArgumentNullException.ThrowIfNull(visibleDates);
        ArgumentNullException.ThrowIfNull(currentEvents);
        ArgumentNullException.ThrowIfNull(syncStatusMap);

        var orderedVisibleDates = visibleDates
            .Distinct()
            .OrderBy(static date => date)
            .ToList();

        if (orderedVisibleDates.Count == 0)
        {
            return new YearViewProjectionResult(
                new Dictionary<DateOnly, YearViewDayDisplayModel>(),
                new Dictionary<DateOnly, IReadOnlyList<YearViewMultiDaySegmentDisplayModel>>());
        }

        var dayLookup = orderedVisibleDates.ToDictionary(
            static date => date,
            date => CreateEmptyDay(date, syncStatusMap));

        var singleDayAssignments = new Dictionary<DateOnly, YearViewPreviewBarDisplayModel>();
        var multiDayCandidatesByDate = new Dictionary<DateOnly, List<AllDayEventSpan>>();
        var multiDayAssignments = new Dictionary<DateOnly, YearViewPreviewBarDisplayModel>();

        foreach (var span in BuildAllDaySpans(currentEvents))
        {
            foreach (var date in orderedVisibleDates.Where(span.Covers))
            {
                if (span.SpanDays == 1)
                {
                    singleDayAssignments.TryAdd(
                        date,
                        new YearViewPreviewBarDisplayModel(span.GcalEventId, span.ColorHex, span.Title, span.SpanDays, span.Opacity));
                    continue;
                }

                if (!multiDayCandidatesByDate.TryGetValue(date, out var candidates))
                {
                    candidates = [];
                    multiDayCandidatesByDate[date] = candidates;
                }

                candidates.Add(span);
            }
        }

        AllDayEventSpan? activeMultiDaySpan = null;
        foreach (var date in orderedVisibleDates)
        {
            if (activeMultiDaySpan is not null && !activeMultiDaySpan.Covers(date))
            {
                activeMultiDaySpan = null;
            }

            if (activeMultiDaySpan is null &&
                multiDayCandidatesByDate.TryGetValue(date, out var candidatesForDate))
            {
                activeMultiDaySpan = candidatesForDate
                    .OrderByDescending(static candidate => candidate.SpanDays)
                    .ThenBy(static candidate => candidate.StartDay)
                    .ThenBy(static candidate => candidate.InputOrder)
                    .ThenBy(static candidate => candidate.GcalEventId, StringComparer.Ordinal)
                    .FirstOrDefault();
            }

            var existing = dayLookup[date];
            var singleDayBar = singleDayAssignments.GetValueOrDefault(date, YearViewPreviewBarDisplayModel.Empty);
            var multiDayBar = activeMultiDaySpan is null
                ? YearViewPreviewBarDisplayModel.Empty
                : new YearViewPreviewBarDisplayModel(
                    activeMultiDaySpan.GcalEventId,
                    activeMultiDaySpan.ColorHex,
                    activeMultiDaySpan.Title,
                    activeMultiDaySpan.SpanDays,
                    activeMultiDaySpan.Opacity);

            if (multiDayBar.HasContent)
            {
                multiDayAssignments[date] = multiDayBar;
            }

            dayLookup[date] = existing with
            {
                SingleDayAllDayBar = singleDayBar,
                MultiDayAllDayBar = multiDayBar
            };
        }

        var multiDaySegmentsByWeekStart = BuildMultiDaySegments(orderedVisibleDates, multiDayAssignments);
        return new YearViewProjectionResult(dayLookup, multiDaySegmentsByWeekStart);
    }

    private static IReadOnlyDictionary<DateOnly, IReadOnlyList<YearViewMultiDaySegmentDisplayModel>> BuildMultiDaySegments(
        IReadOnlyList<DateOnly> orderedVisibleDates,
        IReadOnlyDictionary<DateOnly, YearViewPreviewBarDisplayModel> multiDayAssignments)
    {
        var result = new Dictionary<DateOnly, IReadOnlyList<YearViewMultiDaySegmentDisplayModel>>();
        var weekStarts = orderedVisibleDates
            .Select(StartOfWeek)
            .Distinct()
            .OrderBy(static date => date);

        foreach (var weekStart in weekStarts)
        {
            var weekDates = orderedVisibleDates
                .Where(date => date >= weekStart && date <= weekStart.AddDays(6))
                .ToList();
            var segments = new List<YearViewMultiDaySegmentDisplayModel>();

            for (var index = 0; index < weekDates.Count; index++)
            {
                var date = weekDates[index];
                if (!multiDayAssignments.TryGetValue(date, out var bar) || !bar.HasContent || bar.GcalEventId is null)
                {
                    continue;
                }

                var segmentStartIndex = index;
                while (index + 1 < weekDates.Count &&
                       multiDayAssignments.TryGetValue(weekDates[index + 1], out var nextBar) &&
                       nextBar.HasContent &&
                       string.Equals(nextBar.GcalEventId, bar.GcalEventId, StringComparison.Ordinal))
                {
                    index++;
                }

                segments.Add(new YearViewMultiDaySegmentDisplayModel(
                    bar.GcalEventId,
                    weekDates[segmentStartIndex],
                    weekDates[index],
                    GetColumnIndex(weekDates[segmentStartIndex]),
                    GetColumnIndex(weekDates[index]) - GetColumnIndex(weekDates[segmentStartIndex]) + 1,
                    bar));
            }

            result[weekStart] = segments;
        }

        return result;
    }

    private static IEnumerable<AllDayEventSpan> BuildAllDaySpans(IEnumerable<CalendarEventDisplayModel> currentEvents)
    {
        var inputOrder = 0;
        foreach (var evt in currentEvents)
        {
            if (!evt.IsAllDay)
            {
                continue;
            }

            var startDay = DateOnly.FromDateTime(evt.StartLocal.Date);
            var rawEndDay = DateOnly.FromDateTime(evt.EndLocal.Date);
            var endDay = rawEndDay > startDay
                ? rawEndDay.AddDays(-1)
                : rawEndDay;

            if (endDay < startDay)
            {
                endDay = startDay;
            }

            yield return new AllDayEventSpan(
                evt.GcalEventId,
                evt.Title,
                evt.ColorHex,
                startDay,
                endDay,
                endDay.DayNumber - startDay.DayNumber + 1,
                inputOrder++,
                evt.Opacity);
        }
    }

    private static YearViewDayDisplayModel CreateEmptyDay(
        DateOnly date,
        IReadOnlyDictionary<DateOnly, SyncStatus> syncStatusMap)
    {
        var syncStatus = syncStatusMap.TryGetValue(date, out var status)
            ? status
            : SyncStatus.NotSynced;

        return new YearViewDayDisplayModel(
            date,
            syncStatus,
            YearViewSyncDotPlacement.Trailing,
            YearViewPreviewBarDisplayModel.Empty,
            YearViewPreviewBarDisplayModel.Empty);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }

    private static int GetColumnIndex(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        return dayOfWeek == 0 ? 6 : dayOfWeek - 1;
    }

    private sealed record AllDayEventSpan(
        string GcalEventId,
        string Title,
        string ColorHex,
        DateOnly StartDay,
        DateOnly EndDay,
        int SpanDays,
        int InputOrder,
        double Opacity)
    {
        public bool Covers(DateOnly date) => date >= StartDay && date <= EndDay;
    }
}

using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Models;

public enum YearViewSyncDotPlacement
{
    Trailing
}

public sealed record YearViewPreviewBarDisplayModel(
    string? GcalEventId,
    string? ColorHex,
    string? SummaryText,
    int SpanDays,
    double Opacity = 1.0)
{
    public static YearViewPreviewBarDisplayModel Empty { get; } = new(null, null, null, 0);

    public bool HasContent => !string.IsNullOrWhiteSpace(ColorHex);
}

public sealed record YearViewDayDisplayModel(
    DateOnly Date,
    SyncStatus SyncStatus,
    YearViewSyncDotPlacement SyncDotPlacement,
    YearViewPreviewBarDisplayModel SingleDayAllDayBar,
    YearViewPreviewBarDisplayModel MultiDayAllDayBar);

public sealed record YearViewMultiDaySegmentDisplayModel(
    string GcalEventId,
    DateOnly StartDate,
    DateOnly EndDate,
    int StartColumn,
    int ColumnSpan,
    YearViewPreviewBarDisplayModel Bar);

public sealed record YearViewProjectionResult(
    IReadOnlyDictionary<DateOnly, YearViewDayDisplayModel> DayLookup,
    IReadOnlyDictionary<DateOnly, IReadOnlyList<YearViewMultiDaySegmentDisplayModel>> MultiDaySegmentsByWeekStart);

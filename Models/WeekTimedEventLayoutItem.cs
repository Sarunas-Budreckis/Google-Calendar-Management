namespace GoogleCalendarManagement.Models;

public sealed record WeekTimedEventLayoutItem(
    string GcalEventId,
    string Title,
    string PrimaryText,
    string? SecondaryText,
    string TooltipText,
    string ColorHex,
    int DayOffset,
    int GridRow,
    int GridRowSpan,
    double Left,
    double Top,
    double Width,
    double Height,
    double CompactTopPadding,
    bool IsCompact,
    bool UseOverlapOutline,
    int MaxTitleLines);

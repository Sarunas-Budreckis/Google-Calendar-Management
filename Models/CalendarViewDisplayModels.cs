using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Models;

public sealed record CalendarEventChipModel(
    string GcalEventId,
    string Title,
    string TimeText,
    SolidColorBrush BackgroundBrush);

public sealed record CalendarDayCellModel(
    DateOnly Date,
    string DayLabel,
    double Opacity,
    IReadOnlyList<CalendarEventChipModel> VisibleEvents,
    string OverflowText);

public sealed record CalendarMonthSectionModel(
    string Title,
    IReadOnlyList<CalendarDayCellModel> DayCells);

public sealed record CalendarTimelineEventModel(
    string GcalEventId,
    string Title,
    string TimeText,
    SolidColorBrush BackgroundBrush,
    double TopOffset,
    double Height);

public sealed record CalendarTimelineDayModel(
    string Header,
    IReadOnlyList<CalendarEventChipModel> AllDayEvents,
    IReadOnlyList<CalendarTimelineEventModel> TimedEvents);

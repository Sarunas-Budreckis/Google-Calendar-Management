namespace GoogleCalendarManagement.Models;

public sealed record CalendarEventDisplayModel(
    string EventId,
    CalendarEventSourceKind SourceKind,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,
    DateTime EndLocal,
    bool IsAllDay,
    string ColorHex,
    string ColorName,
    bool IsRecurringInstance,
    string? Description,
    DateTime? LastSyncedAt,
    bool IsPending = false,
    bool IsSelectedForPush = false,
    double Opacity = 1.0,
    DateTime? PendingUpdatedAt = null,
    string StatusLabel = "",
    string ColorKey = "azure",
    bool IsPendingDelete = false)
{
    public string DisplayColorHex => ColorHex;
}

namespace GoogleCalendarManagement.Models;

public sealed record CalendarEventDisplayModel(
    string GcalEventId,
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
    DateTime? LastSyncedAt);

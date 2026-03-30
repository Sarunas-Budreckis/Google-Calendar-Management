namespace GoogleCalendarManagement.Services;

public sealed record GcalEventDto(
    string GcalEventId,
    string CalendarId,
    string? Summary,
    string? Description,
    DateTime? StartDateTimeUtc,
    DateTime? EndDateTimeUtc,
    bool IsAllDay,
    string? ColorId,
    string? GcalEtag,
    DateTime? GcalUpdatedAtUtc,
    bool IsDeleted,
    string? RecurringEventId,
    bool IsRecurringInstance);

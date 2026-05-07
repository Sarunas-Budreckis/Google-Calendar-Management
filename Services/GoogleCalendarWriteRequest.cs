namespace GoogleCalendarManagement.Services;

public sealed record GoogleCalendarWriteRequest(
    string CalendarId,
    string? Summary,
    string? Description,
    DateTime? StartDateTimeUtc,
    DateTime? EndDateTimeUtc,
    bool IsAllDay,
    string? ColorId);

using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Messages;

public sealed record EventSelectedMessage(
    string? EventId,
    CalendarEventSourceKind? SourceKind = null,
    bool OpenInEditMode = false);

using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Messages;

public sealed record EventUpdatedMessage(
    string EventId,
    CalendarEventDisplayModel? PreviewEvent = null,
    string? PreviousEventId = null,
    bool AnimateOpacityTransition = false);

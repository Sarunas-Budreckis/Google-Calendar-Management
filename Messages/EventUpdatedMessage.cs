using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Messages;

public sealed record EventUpdatedMessage(string GcalEventId, CalendarEventDisplayModel? PreviewEvent = null);

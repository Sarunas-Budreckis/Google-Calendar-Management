namespace GoogleCalendarManagement.Messages;

public sealed record CalendarViewRangeChangedMessage(DateOnly From, DateOnly To);

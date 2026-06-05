namespace GoogleCalendarManagement.Messages;

public sealed record DataSourceDayOpenRequestedMessage(DateOnly Date, string SourceKey);

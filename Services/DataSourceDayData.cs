namespace GoogleCalendarManagement.Services;

public sealed record DataSourceDayData(DateOnly Date, bool HasData, int? Count = null);

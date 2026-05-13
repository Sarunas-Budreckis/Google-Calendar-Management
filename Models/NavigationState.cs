namespace GoogleCalendarManagement.Models;

public sealed record NavigationState(ViewMode ViewMode, DateOnly CurrentDate, DateOnly? SelectedDay = null);

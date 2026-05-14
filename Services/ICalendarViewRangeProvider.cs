namespace GoogleCalendarManagement.Services;

public interface ICalendarViewRangeProvider
{
    (DateOnly From, DateOnly To) GetCurrentViewDisplayRange();
}

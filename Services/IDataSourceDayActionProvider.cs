namespace GoogleCalendarManagement.Services;

public interface IDataSourceDayActionProvider
{
    Task AddForDayAsync(DateOnly date, CancellationToken ct = default);
}

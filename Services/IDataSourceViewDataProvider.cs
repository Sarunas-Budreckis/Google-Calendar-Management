namespace GoogleCalendarManagement.Services;

public interface IDataSourceViewDataProvider
{
    Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

namespace GoogleCalendarManagement.Services;

public interface IDataSourceCardProviderPreloader
{
    Task PreloadAsync(DateOnly date, CancellationToken ct = default);
}

namespace GoogleCalendarManagement.Services;

public interface ITogglSleepQualityRepository
{
    Task<int?> GetQualityForDateAsync(DateOnly date, CancellationToken ct = default);
    Task UpsertQualityAsync(DateOnly date, int? quality, CancellationToken ct = default);
}

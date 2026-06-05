using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ISpotifyStreamRepository
{
    Task<IReadOnlyList<SpotifyStream>> GetStreamsForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetStreamCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}

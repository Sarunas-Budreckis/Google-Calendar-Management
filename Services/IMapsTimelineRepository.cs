using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IMapsTimelineRepository
{
    Task<MapsTimelineRaw?> GetLatestAsync(CancellationToken ct = default);
    Task SaveAsync(MapsTimelineRaw record, CancellationToken ct = default);
    Task DeleteAllAsync(CancellationToken ct = default);
}

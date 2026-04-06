namespace GoogleCalendarManagement.Services;

public interface ISyncStatusService
{
    Task<Dictionary<DateOnly, SyncStatus>> GetSyncStatusAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<DateTime?> GetLastSyncTimeAsync(CancellationToken ct = default);
}

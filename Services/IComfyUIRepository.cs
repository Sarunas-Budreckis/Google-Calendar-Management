using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IComfyUIRepository
{
    Task<IReadOnlyList<ComfyUIFolder>> GetActiveFoldersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ComfyUIFolder>> GetAllFoldersAsync(CancellationToken ct = default);
    Task AddFolderAsync(string folderPath, DateTime addedAt, CancellationToken ct = default);
    Task DeactivateFolderAsync(int folderId, CancellationToken ct = default);

    Task<IReadOnlyList<ComfyUIScanPoint>> GetPointsForDateAsync(DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, int>> GetCreatedEventCountsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<HashSet<(DateTime Timestamp, string EventType)>> GetExistingDedupKeysAsync(IReadOnlyList<(DateTime Timestamp, string EventType)> candidates, CancellationToken ct = default);
    Task InsertPointsAsync(IReadOnlyList<ComfyUIScanPoint> points, CancellationToken ct = default);
    Task<int> CountPointsAsync(CancellationToken ct = default);
}

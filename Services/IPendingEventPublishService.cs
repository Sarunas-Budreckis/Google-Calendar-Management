namespace GoogleCalendarManagement.Services;

public interface IPendingEventPublishService
{
    Task<IReadOnlyList<PendingPublishListItem>> GetPendingItemsAsync(CancellationToken ct = default);

    Task<PendingPublishBatchResult> PublishAsync(
        IReadOnlyCollection<string> pendingEventIds,
        IProgress<PendingPublishProgress>? progress = null,
        CancellationToken ct = default);

    Task RevertAsync(string pendingEventId, CancellationToken ct = default);

    Task UpdateColorAsync(string pendingEventId, string colorKey, CancellationToken ct = default);
}

public sealed record PendingPublishListItem(
    string PendingEventId,
    string? GcalEventId,
    string Title,
    DateTime? StartDateTimeUtc,
    DateTime? EndDateTimeUtc,
    bool IsAllDay,
    bool IsRecurringInstance,
    string SourceLabel,
    string ColorKey,
    string ColorHex,
    string? PublishError);

public sealed record PendingPublishProgress(int CompletedCount, int TotalCount);

public sealed record PendingPublishBatchResult(
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<PendingPublishItemResult> ItemResults);

public sealed record PendingPublishItemResult(
    string PendingEventId,
    string? PublishedEventId,
    bool Success,
    string? ErrorMessage,
    string? ErrorDetails = null);

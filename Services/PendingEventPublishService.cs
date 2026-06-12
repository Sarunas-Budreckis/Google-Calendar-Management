using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// STUB (Story 8.2). The publish pipeline read/wrote pending_event + gcal_event, both removed by
/// the unified `event` table. Publish state (ReadyToPublish / has_unpublished_changes / version
/// snapshots on publish) is rebuilt against `event` in Story 8.3. Until then the queue is empty
/// and publish/revert/recolor are no-ops so the app stays stable. Do NOT build new behavior here.
/// </summary>
public sealed class PendingEventPublishService : IPendingEventPublishService
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IColorMappingService _colorMappingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PendingEventPublishService> _logger;

    public PendingEventPublishService(
        IDbContextFactory<CalendarDbContext> dbContextFactory,
        IGoogleCalendarService googleCalendarService,
        IColorMappingService colorMappingService,
        TimeProvider timeProvider,
        ILogger<PendingEventPublishService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _googleCalendarService = googleCalendarService;
        _colorMappingService = colorMappingService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // TODO 8.3: surface events with has_unpublished_changes from the unified `event` table.
    public Task<IReadOnlyList<PendingPublishListItem>> GetPendingItemsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PendingPublishListItem>>([]);

    // TODO 8.3: publish unpublished `event` rows to Google Calendar.
    public Task<PendingPublishBatchResult> PublishAsync(
        IReadOnlyCollection<string> pendingEventIds,
        IProgress<PendingPublishProgress>? progress = null,
        CancellationToken ct = default)
        => Task.FromResult(new PendingPublishBatchResult(0, 0, 0, []));

    public Task RevertAsync(string pendingEventId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task UpdateColorAsync(string pendingEventId, string colorKey, CancellationToken ct = default)
        => Task.CompletedTask;
}

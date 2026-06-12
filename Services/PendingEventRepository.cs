using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// STUB (Story 8.2). The pending_event table was merged into the unified `event` table
/// (has_unpublished_changes replaces "a pending row exists"). The overlay/draft write path is
/// rebuilt in Stories 8.3 (repository) and 8.5 (editing). Until then reads return empty and
/// writes are no-ops so the app stays stable (no crash) while event editing is non-functional.
/// Do NOT build new behavior on this.
/// </summary>
public sealed class PendingEventRepository : IPendingEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public PendingEventRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    // TODO 8.3: resolve overlays/drafts from the unified `event` table.
    public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
        => Task.FromResult<PendingEvent?>(null);

    public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
        => Task.FromResult<PendingEvent?>(null);

    public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<PendingEvent?>(null);

    public Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
        => Task.FromResult<PendingEvent?>(null);

    // TODO 8.3: write overlays/drafts onto the unified `event` table.
    public Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
        => Task.CompletedTask;
}

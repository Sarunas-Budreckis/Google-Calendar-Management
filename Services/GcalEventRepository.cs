using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// STUB (Story 8.2). The gcal_event table was merged into the unified `event` table; the real
/// read path is rebuilt as the EventRepository + identity service in Story 8.3 and rendering in
/// Story 8.5. Until then these methods return empty results so the app stays stable (no crash)
/// while curated-event reads are non-functional. Do NOT build new behavior on this.
/// </summary>
public sealed class GcalEventRepository : IGcalEventRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public GcalEventRepository(IDbContextFactory<CalendarDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    // TODO 8.3: query the unified `event` table.
    public Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return Task.FromResult<IList<GcalEvent>>(new List<GcalEvent>());
    }

    // TODO 8.3: query the unified `event` table.
    public Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        return Task.FromResult<GcalEvent?>(null);
    }

    // TODO 8.3: derive the stored range from the unified `event` table.
    public Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
    {
        return Task.FromResult<(DateOnly From, DateOnly To)?>(null);
    }
}

using GoogleCalendarManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class SyncStatusService : ISyncStatusService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<SyncStatusService> _logger;

    public SyncStatusService(IDbContextFactory<CalendarDbContext> contextFactory, ILogger<SyncStatusService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Dictionary<DateOnly, SyncStatus>> GetSyncStatusAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var result = new Dictionary<DateOnly, SyncStatus>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            result[d] = SyncStatus.NotSynced;
        }

        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

            // 8.2: gcal_event merged into the unified `event` table. Published events carry the
            // synced-from-GCal status the old query keyed on. TODO 8.4: revisit once the sync
            // reconciler writes lifecycle/last_synced semantics onto `event`.
            var events = await ctx.Events
                .AsNoTracking()
                .Where(e => e.Publish == "published" &&
                            e.StartDatetime < toDt &&
                            e.EndDatetime > fromDt)
                .Select(e => new { e.StartDatetime, e.EndDatetime, e.IsAllDay })
                .ToListAsync(ct);

            foreach (var evt in events)
            {
                if (evt.StartDatetime is null || evt.EndDatetime is null)
                    continue;

                var startDate = DateOnly.FromDateTime(evt.StartDatetime.Value.Date);
                DateOnly endDate;

                if (evt.IsAllDay == true)
                {
                    // All-day events store EndDatetime at midnight of the next day (exclusive).
                    var rawEnd = DateOnly.FromDateTime(evt.EndDatetime.Value.Date);
                    endDate = rawEnd > startDate ? rawEnd.AddDays(-1) : rawEnd;
                }
                else
                {
                    endDate = DateOnly.FromDateTime(evt.EndDatetime.Value.Date);
                }

                for (var d = startDate; d <= endDate; d = d.AddDays(1))
                {
                    if (result.ContainsKey(d))
                        result[d] = SyncStatus.Synced;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to query sync status for range {From} to {To}.", from, to);
        }

        return result;
    }

    public async Task<DateTime?> GetLastSyncTimeAsync(CancellationToken ct = default)
    {
        try
        {
            await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

            return await ctx.DataSourceRefreshes
                .AsNoTracking()
                .Where(r => r.SourceName == "gcal" && r.Success == true)
                .OrderByDescending(r => r.LastRefreshedAt)
                .Select(r => r.LastRefreshedAt)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to query last sync time.");
            return null;
        }
    }
}

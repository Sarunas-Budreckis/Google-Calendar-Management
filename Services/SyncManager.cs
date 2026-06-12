using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class SyncManager : ISyncManager
{
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<SyncManager> _logger;

    public SyncManager(
        IGoogleCalendarService googleCalendarService,
        IDbContextFactory<CalendarDbContext> contextFactory,
        ILogger<SyncManager> logger)
    {
        _googleCalendarService = googleCalendarService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAsync(
        string calendarId = "primary",
        DateTime? rangeStart = null,
        DateTime? rangeEnd = null,
        IProgress<SyncProgress>? progress = null,
        CancellationToken ct = default)
    {
        var effectiveStart = DateTime.SpecifyKind(rangeStart ?? DateTime.UtcNow.AddMonths(-6), DateTimeKind.Utc);
        var effectiveEnd = DateTime.SpecifyKind(rangeEnd ?? DateTime.UtcNow.AddMonths(1), DateTimeKind.Utc);

        _logger.LogInformation(
            "Starting Google Calendar sync for {CalendarId} from {RangeStart:o} to {RangeEnd:o}",
            calendarId,
            effectiveStart,
            effectiveEnd);

        var pagesFetched = 0;
        progress?.Report(new SyncProgress(0, 0, "Fetching events from Google Calendar..."));

        var fetchProgress = new CallbackProgress<int>(value =>
        {
            pagesFetched = value;
            progress?.Report(new SyncProgress(pagesFetched, 0, $"Fetched {pagesFetched} page(s) from Google Calendar..."));
        });

        var fetchResult = await _googleCalendarService.FetchAllEventsAsync(
            calendarId,
            effectiveStart,
            effectiveEnd,
            fetchProgress,
            ct);

        if (!fetchResult.Success)
        {
            var failureMessage = fetchResult.ErrorMessage ?? "Unable to sync Google Calendar.";
            _logger.LogWarning("Google Calendar sync failed before persistence: {Message}", failureMessage);

            await PersistSyncMetadataAsync(
                effectiveStart,
                effectiveEnd,
                recordsFetched: 0,
                success: false,
                syncToken: null,
                errorMessage: failureMessage,
                added: 0,
                updated: 0,
                deleted: 0);

            return new SyncResult(false, 0, 0, 0, null, failureMessage);
        }

        var fetchedEvents = fetchResult.Data.Events;
        var fetchedEventList = fetchedEvents as FetchAllEventsResultList;
        var fetchWasCancelled = fetchedEventList?.WasCancelled == true;

        var eventsAdded = 0;
        var eventsUpdated = 0;
        var eventsDeleted = 0;
        var eventsProcessed = 0;
        var cancellationRequestedDuringPersistence = false;
        var syncedAt = DateTime.UtcNow;

        await using var context = await _contextFactory.CreateDbContextAsync();

        // TODO 8.4: persist fetched GCal events into the unified `event` table (match on
        // gcal_event_id, snapshot version history, reconcile deletes). The gcal_event table was
        // removed in Story 8.2, so the reconciler is stubbed here — sync still fetches from GCal
        // and records its metadata (refresh token, audit log), but event rows are not written
        // until the Story 8.4 reconciler lands. Counts reflect events fetched, not persisted.
        eventsProcessed = fetchedEvents.Count;
        _ = syncedAt;

        var wasCancelled = fetchWasCancelled || cancellationRequestedDuringPersistence;
        var success = !wasCancelled;
        var syncTokenToPersist = success ? fetchResult.Data.SyncToken : null;
        var statusMessage = wasCancelled ? "Sync cancelled by user." : null;

        await TryPersistSyncMetadataAsync(
            effectiveStart,
            effectiveEnd,
            eventsProcessed,
            success,
            syncTokenToPersist,
            statusMessage,
            eventsAdded,
            eventsUpdated,
            eventsDeleted,
            context);

        _logger.LogInformation(
            "Google Calendar sync completed. Success={Success}, Cancelled={WasCancelled}, Added={Added}, Updated={Updated}, Deleted={Deleted}",
            success,
            wasCancelled,
            eventsAdded,
            eventsUpdated,
            eventsDeleted);

        return new SyncResult(
            success,
            eventsAdded,
            eventsUpdated,
            eventsDeleted,
            syncTokenToPersist,
            statusMessage,
            wasCancelled);
    }

    private async Task PersistSyncMetadataAsync(
        DateTime effectiveStart,
        DateTime effectiveEnd,
        int recordsFetched,
        bool success,
        string? syncToken,
        string? errorMessage,
        int added,
        int updated,
        int deleted,
        CalendarDbContext? existingContext = null)
    {
        var ownsContext = existingContext is null;
        var context = existingContext ?? await _contextFactory.CreateDbContextAsync();

        try
        {
            var refresh = await context.DataSourceRefreshes
                .SingleOrDefaultAsync(item => item.SourceName == "gcal");

            if (refresh is null)
            {
                refresh = new DataSourceRefresh { SourceName = "gcal" };
                context.DataSourceRefreshes.Add(refresh);
            }

            refresh.StartDate = effectiveStart;
            refresh.EndDate = effectiveEnd;
            refresh.LastRefreshedAt = DateTime.UtcNow;
            refresh.RecordsFetched = recordsFetched;
            refresh.Success = success;
            refresh.ErrorMessage = errorMessage;
            refresh.SyncToken = syncToken;

            context.AuditLogs.Add(new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                OperationType = "gcal_sync",
                UserAction = true,
                Success = success,
                ErrorMessage = errorMessage,
                OperationDetails = $"added={added};updated={updated};deleted={deleted};records={recordsFetched}"
            });

            await SaveChangesWithRetryAsync(context, CancellationToken.None);
        }
        finally
        {
            if (ownsContext)
            {
                await context.DisposeAsync();
            }
        }
    }

    private async Task TryPersistSyncMetadataAsync(
        DateTime effectiveStart,
        DateTime effectiveEnd,
        int recordsFetched,
        bool success,
        string? syncToken,
        string? errorMessage,
        int added,
        int updated,
        int deleted,
        CalendarDbContext? existingContext = null)
    {
        try
        {
            await PersistSyncMetadataAsync(
                effectiveStart,
                effectiveEnd,
                recordsFetched,
                success,
                syncToken,
                errorMessage,
                added,
                updated,
                deleted,
                existingContext);
        }
        catch (Exception ex) when (IsDatabaseLockException(ex))
        {
            _logger.LogWarning(ex, "Unable to persist sync metadata because the SQLite database is locked.");
        }
    }

    // NOTE: the GcalEvent/GcalEventVersion mapping + reconciliation helpers were removed in
    // Story 8.2 (gcal_event table dropped). They are reintroduced against the unified `event`
    // table by the Story 8.4 sync reconciler.

    private static async Task SaveChangesWithRetryAsync(CalendarDbContext context, CancellationToken ct)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.SaveChangesAsync(CancellationToken.None);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsDatabaseLockException(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct);
            }
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    private static bool IsDatabaseLockException(Exception ex)
    {
        return ex switch
        {
            SqliteException sqliteEx when sqliteEx.SqliteErrorCode is 5 or 6 => true,
            DbUpdateException dbUpdateException when dbUpdateException.InnerException is not null =>
                IsDatabaseLockException(dbUpdateException.InnerException),
            _ => ex.Message.Contains("database is locked", StringComparison.OrdinalIgnoreCase)
        };
    }

    private sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public CallbackProgress(Action<T> callback)
        {
            _callback = callback;
        }

        public void Report(T value)
        {
            _callback(value);
        }
    }
}

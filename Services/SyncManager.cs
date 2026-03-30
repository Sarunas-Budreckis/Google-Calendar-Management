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

        try
        {
            foreach (var incomingEvent in fetchedEvents)
            {
                if (!fetchWasCancelled && ct.IsCancellationRequested)
                {
                    cancellationRequestedDuringPersistence = true;
                    break;
                }

                var existingEvent = await context.GcalEvents.FindAsync([incomingEvent.GcalEventId], CancellationToken.None);
                if (existingEvent is null)
                {
                    context.GcalEvents.Add(CreateEntity(incomingEvent, syncedAt));
                    if (incomingEvent.IsDeleted)
                    {
                        eventsDeleted++;
                    }
                    else
                    {
                        eventsAdded++;
                    }
                }
                else
                {
                    var wasDeleted = existingEvent.IsDeleted;
                    if (incomingEvent.IsDeleted)
                    {
                        if (!wasDeleted)
                        {
                            context.GcalEventVersions.Add(CreateVersionSnapshot(existingEvent, "deleted", syncedAt));
                            eventsDeleted++;
                        }

                        ApplyDeletedValues(existingEvent, incomingEvent, syncedAt);
                    }
                    else
                    {
                        if (ShouldSnapshotForUpdate(existingEvent, incomingEvent))
                        {
                            context.GcalEventVersions.Add(CreateVersionSnapshot(existingEvent, "updated", syncedAt));
                        }

                        var changed = ApplyIncomingValues(existingEvent, incomingEvent, syncedAt);
                        if (changed)
                        {
                            eventsUpdated++;
                        }
                    }
                }

                await SaveChangesWithRetryAsync(context, ct);
                eventsProcessed++;
                progress?.Report(new SyncProgress(
                    pagesFetched,
                    eventsProcessed,
                    $"Persisted {eventsProcessed} of {fetchedEvents.Count} event(s)..."));

                if (!fetchWasCancelled && ct.IsCancellationRequested)
                {
                    cancellationRequestedDuringPersistence = true;
                    break;
                }
            }
        }
        catch (Exception ex) when (IsDatabaseLockException(ex))
        {
            const string databaseBusyMessage =
                "The local database is busy or locked. Close SQLite Browser or other tools using the database, then try syncing again.";

            _logger.LogWarning(ex, "Google Calendar sync failed because the SQLite database is locked.");

            await TryPersistSyncMetadataAsync(
                effectiveStart,
                effectiveEnd,
                eventsProcessed,
                success: false,
                syncToken: null,
                errorMessage: databaseBusyMessage,
                added: eventsAdded,
                updated: eventsUpdated,
                deleted: eventsDeleted);

            return new SyncResult(
                false,
                eventsAdded,
                eventsUpdated,
                eventsDeleted,
                null,
                databaseBusyMessage);
        }

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

    private static GcalEvent CreateEntity(GcalEventDto incomingEvent, DateTime syncedAt)
    {
        return new GcalEvent
        {
            GcalEventId = incomingEvent.GcalEventId,
            CalendarId = incomingEvent.CalendarId,
            Summary = incomingEvent.Summary,
            Description = incomingEvent.Description,
            StartDatetime = incomingEvent.StartDateTimeUtc,
            EndDatetime = incomingEvent.EndDateTimeUtc,
            IsAllDay = incomingEvent.IsAllDay,
            ColorId = incomingEvent.ColorId,
            GcalEtag = incomingEvent.GcalEtag,
            GcalUpdatedAt = incomingEvent.GcalUpdatedAtUtc,
            IsDeleted = incomingEvent.IsDeleted,
            AppCreated = false,
            SourceSystem = null,
            AppPublished = false,
            RecurringEventId = incomingEvent.RecurringEventId,
            IsRecurringInstance = incomingEvent.IsRecurringInstance,
            LastSyncedAt = syncedAt,
            CreatedAt = syncedAt,
            UpdatedAt = syncedAt
        };
    }

    private static GcalEventVersion CreateVersionSnapshot(GcalEvent existingEvent, string changeReason, DateTime createdAt)
    {
        return new GcalEventVersion
        {
            GcalEventId = existingEvent.GcalEventId,
            GcalEtag = existingEvent.GcalEtag,
            Summary = existingEvent.Summary,
            Description = existingEvent.Description,
            StartDatetime = existingEvent.StartDatetime,
            EndDatetime = existingEvent.EndDatetime,
            IsAllDay = existingEvent.IsAllDay,
            ColorId = existingEvent.ColorId,
            ChangedBy = "gcal_sync",
            ChangeReason = changeReason,
            CreatedAt = createdAt
        };
    }

    private static bool ShouldSnapshotForUpdate(GcalEvent existingEvent, GcalEventDto incomingEvent)
    {
        if (existingEvent.IsDeleted)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(incomingEvent.GcalEtag) &&
            !string.IsNullOrWhiteSpace(existingEvent.GcalEtag) &&
            !string.Equals(existingEvent.GcalEtag, incomingEvent.GcalEtag, StringComparison.Ordinal))
        {
            return true;
        }

        return existingEvent.Summary != incomingEvent.Summary ||
               existingEvent.Description != incomingEvent.Description ||
               existingEvent.StartDatetime != incomingEvent.StartDateTimeUtc ||
               existingEvent.EndDatetime != incomingEvent.EndDateTimeUtc ||
               existingEvent.IsAllDay != incomingEvent.IsAllDay ||
               existingEvent.ColorId != incomingEvent.ColorId ||
               existingEvent.GcalEtag != incomingEvent.GcalEtag;
    }

    private static bool ApplyIncomingValues(GcalEvent existingEvent, GcalEventDto incomingEvent, DateTime syncedAt)
    {
        var changed =
            existingEvent.CalendarId != incomingEvent.CalendarId ||
            existingEvent.Summary != incomingEvent.Summary ||
            existingEvent.Description != incomingEvent.Description ||
            existingEvent.StartDatetime != incomingEvent.StartDateTimeUtc ||
            existingEvent.EndDatetime != incomingEvent.EndDateTimeUtc ||
            existingEvent.IsAllDay != incomingEvent.IsAllDay ||
            existingEvent.ColorId != incomingEvent.ColorId ||
            existingEvent.GcalEtag != incomingEvent.GcalEtag ||
            existingEvent.GcalUpdatedAt != incomingEvent.GcalUpdatedAtUtc ||
            existingEvent.IsDeleted != incomingEvent.IsDeleted ||
            existingEvent.RecurringEventId != incomingEvent.RecurringEventId ||
            existingEvent.IsRecurringInstance != incomingEvent.IsRecurringInstance ||
            existingEvent.AppCreated ||
            existingEvent.AppPublished ||
            existingEvent.SourceSystem is not null;

        existingEvent.CalendarId = incomingEvent.CalendarId;
        existingEvent.Summary = incomingEvent.Summary;
        existingEvent.Description = incomingEvent.Description;
        existingEvent.StartDatetime = incomingEvent.StartDateTimeUtc;
        existingEvent.EndDatetime = incomingEvent.EndDateTimeUtc;
        existingEvent.IsAllDay = incomingEvent.IsAllDay;
        existingEvent.ColorId = incomingEvent.ColorId;
        existingEvent.GcalEtag = incomingEvent.GcalEtag;
        existingEvent.GcalUpdatedAt = incomingEvent.GcalUpdatedAtUtc;
        existingEvent.IsDeleted = incomingEvent.IsDeleted;
        existingEvent.AppCreated = false;
        existingEvent.SourceSystem = null;
        existingEvent.AppPublished = false;
        existingEvent.RecurringEventId = incomingEvent.RecurringEventId;
        existingEvent.IsRecurringInstance = incomingEvent.IsRecurringInstance;
        existingEvent.LastSyncedAt = syncedAt;
        existingEvent.UpdatedAt = syncedAt;

        return changed;
    }

    private static void ApplyDeletedValues(GcalEvent existingEvent, GcalEventDto incomingEvent, DateTime syncedAt)
    {
        existingEvent.CalendarId = incomingEvent.CalendarId;
        existingEvent.GcalEtag = incomingEvent.GcalEtag ?? existingEvent.GcalEtag;
        existingEvent.GcalUpdatedAt = incomingEvent.GcalUpdatedAtUtc;
        existingEvent.IsDeleted = true;
        existingEvent.AppCreated = false;
        existingEvent.SourceSystem = null;
        existingEvent.AppPublished = false;
        existingEvent.RecurringEventId = incomingEvent.RecurringEventId;
        existingEvent.IsRecurringInstance = incomingEvent.IsRecurringInstance;
        existingEvent.LastSyncedAt = syncedAt;
        existingEvent.UpdatedAt = syncedAt;
    }

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

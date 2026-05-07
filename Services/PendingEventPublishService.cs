using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

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

    public async Task<IReadOnlyList<PendingPublishListItem>> GetPendingItemsAsync(CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var items = await (
            from pendingEvent in context.PendingEvents.AsNoTracking()
            join gcalEvent in context.GcalEvents.AsNoTracking()
                on pendingEvent.GcalEventId equals gcalEvent.GcalEventId into gcalEvents
            from gcalEvent in gcalEvents.DefaultIfEmpty()
            orderby pendingEvent.StartDatetime ?? pendingEvent.CreatedAt, pendingEvent.PendingEventId
            select new PendingPublishListItem(
                pendingEvent.PendingEventId,
                pendingEvent.GcalEventId,
                string.IsNullOrWhiteSpace(pendingEvent.Summary) ? "Untitled event" : pendingEvent.Summary,
                pendingEvent.StartDatetime,
                pendingEvent.EndDatetime,
                pendingEvent.IsAllDay ?? false,
                gcalEvent != null && gcalEvent.IsRecurringInstance,
                pendingEvent.GcalEventId == null ? "new draft" : "edited event",
                _colorMappingService.NormalizeColorKey(pendingEvent.ColorId),
                _colorMappingService.GetHexColor(pendingEvent.ColorId),
                pendingEvent.PublishError))
            .ToListAsync(ct);

        return items;
    }

    public async Task<PendingPublishBatchResult> PublishAsync(
        IReadOnlyCollection<string> pendingEventIds,
        IProgress<PendingPublishProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (pendingEventIds.Count == 0)
        {
            return new PendingPublishBatchResult(0, 0, 0, []);
        }

        var orderedPendingIds = await GetOrderedPendingIdsAsync(pendingEventIds, ct);
        var totalCount = orderedPendingIds.Count;
        var itemResults = new List<PendingPublishItemResult>(totalCount);
        var successCount = 0;
        var failureCount = 0;

        for (var index = 0; index < orderedPendingIds.Count; index++)
        {
            ct.ThrowIfCancellationRequested();

            var pendingEventId = orderedPendingIds[index];
            var itemResult = await PublishSingleAsync(pendingEventId, ct);
            itemResults.Add(itemResult);

            if (itemResult.Success)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }

            progress?.Report(new PendingPublishProgress(index + 1, totalCount));
        }

        return new PendingPublishBatchResult(totalCount, successCount, failureCount, itemResults);
    }

    public async Task RevertAsync(string pendingEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var pendingEvent = await context.PendingEvents
            .SingleOrDefaultAsync(item => item.PendingEventId == pendingEventId, ct);

        if (pendingEvent is null)
        {
            return;
        }

        var gcalEventId = pendingEvent.GcalEventId;
        context.PendingEvents.Remove(pendingEvent);
        await context.SaveChangesAsync(ct);

        if (gcalEventId is not null)
        {
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(gcalEventId, PreviousEventId: pendingEventId));
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(pendingEventId));
        }
    }

    public async Task UpdateColorAsync(string pendingEventId, string colorKey, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var pendingEvent = await context.PendingEvents
            .SingleOrDefaultAsync(item => item.PendingEventId == pendingEventId, ct);

        if (pendingEvent is null)
        {
            return;
        }

        pendingEvent.ColorId = colorKey;
        pendingEvent.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);

        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(pendingEventId));
    }

    private async Task<IReadOnlyList<string>> GetOrderedPendingIdsAsync(
        IReadOnlyCollection<string> pendingEventIds,
        CancellationToken ct)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await context.PendingEvents
            .AsNoTracking()
            .Where(pendingEvent => pendingEventIds.Contains(pendingEvent.PendingEventId))
            .OrderBy(pendingEvent => pendingEvent.StartDatetime ?? pendingEvent.CreatedAt)
            .ThenBy(pendingEvent => pendingEvent.PendingEventId)
            .Select(pendingEvent => pendingEvent.PendingEventId)
            .ToListAsync(ct);
    }

    private async Task<PendingPublishItemResult> PublishSingleAsync(string pendingEventId, CancellationToken ct)
    {
        try
        {
            var publishAttemptedAt = _timeProvider.GetUtcNow().UtcDateTime;
            var publishContext = await LoadPendingPublishContextAsync(pendingEventId, ct);
            if (publishContext is null)
            {
                return new PendingPublishItemResult(
                    pendingEventId,
                    null,
                    false,
                    "The pending event could not be found.");
            }

            var writeRequest = CreateWriteRequest(publishContext.PendingEvent);
            if (publishContext.PendingEvent.GcalEventId is null)
            {
                var insertResult = await _googleCalendarService.InsertEventAsync(writeRequest, ct);
                if (!insertResult.Success || insertResult.Event is null)
                {
                    var errorMessage = insertResult.ErrorMessage ?? "Unable to publish the event to Google Calendar.";
                    var errorDetails = insertResult.ErrorDetails ?? errorMessage;
                    await PersistFailureAsync(pendingEventId, publishAttemptedAt, errorDetails, ct);
                    return new PendingPublishItemResult(pendingEventId, null, false, errorMessage, errorDetails);
                }

                await PersistInsertSuccessAsync(
                    publishContext.PendingEvent,
                    MergePublishedColor(insertResult.Event, publishContext.PendingEvent.ColorId),
                    publishAttemptedAt,
                    ct);

                WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(
                    insertResult.Event.GcalEventId,
                    PreviousEventId: publishContext.PendingEvent.PendingEventId,
                    AnimateOpacityTransition: true));

                return new PendingPublishItemResult(
                    pendingEventId,
                    insertResult.Event.GcalEventId,
                    true,
                    null);
            }

            if (publishContext.LiveEvent is null)
            {
                const string missingLiveEventMessage = "The local published event could not be found for this pending edit.";
                await PersistFailureAsync(pendingEventId, publishAttemptedAt, missingLiveEventMessage, ct);
                return new PendingPublishItemResult(
                    pendingEventId,
                    publishContext.PendingEvent.GcalEventId,
                    false,
                    missingLiveEventMessage,
                    missingLiveEventMessage);
            }

            var updateResult = await _googleCalendarService.UpdateEventAsync(
                publishContext.PendingEvent.GcalEventId,
                writeRequest,
                publishContext.LiveEvent.GcalEtag,
                ct);

            if (!updateResult.Success &&
                updateResult.FailureKind == GoogleCalendarWriteFailureKind.PreconditionFailed)
            {
                updateResult = await RetryConflictIfLocalWinsAsync(publishContext, writeRequest, ct);
            }

            if (!updateResult.Success || updateResult.Event is null)
            {
                var errorMessage = updateResult.ErrorMessage ??
                    "Unable to publish the event to Google Calendar.";
                var errorDetails = updateResult.ErrorDetails ?? errorMessage;
                await PersistFailureAsync(pendingEventId, publishAttemptedAt, errorDetails, ct);
                return new PendingPublishItemResult(
                    pendingEventId,
                    publishContext.PendingEvent.GcalEventId,
                    false,
                    errorMessage,
                    errorDetails);
            }

            await PersistUpdateSuccessAsync(
                publishContext.PendingEvent,
                publishContext.LiveEvent,
                MergePublishedColor(updateResult.Event, publishContext.PendingEvent.ColorId),
                publishAttemptedAt,
                ct);

            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(
                updateResult.Event.GcalEventId,
                AnimateOpacityTransition: true));

            return new PendingPublishItemResult(
                pendingEventId,
                updateResult.Event.GcalEventId,
                true,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while publishing pending event {PendingEventId}.", pendingEventId);
            var errorMessage = ex.Message;
            var errorDetails = ex.ToString();
            await PersistFailureAsync(pendingEventId, _timeProvider.GetUtcNow().UtcDateTime, errorDetails, ct);
            return new PendingPublishItemResult(
                pendingEventId,
                null,
                false,
                string.IsNullOrWhiteSpace(errorMessage) ? "Unexpected publish failure." : errorMessage,
                errorDetails);
        }
    }

    private async Task<PendingPublishContext?> LoadPendingPublishContextAsync(string pendingEventId, CancellationToken ct)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        return await (
            from pendingEvent in context.PendingEvents.AsNoTracking()
            join gcalEvent in context.GcalEvents.AsNoTracking()
                on pendingEvent.GcalEventId equals gcalEvent.GcalEventId into gcalEvents
            from gcalEvent in gcalEvents.DefaultIfEmpty()
            where pendingEvent.PendingEventId == pendingEventId
            select new PendingPublishContext(pendingEvent, gcalEvent))
            .SingleOrDefaultAsync(ct);
    }

    private GoogleCalendarWriteRequest CreateWriteRequest(PendingEvent pendingEvent)
    {
        return new GoogleCalendarWriteRequest(
            pendingEvent.CalendarId,
            pendingEvent.Summary,
            pendingEvent.Description,
            pendingEvent.StartDatetime,
            pendingEvent.EndDatetime,
            pendingEvent.IsAllDay ?? false,
            _colorMappingService.GetGoogleColorId(pendingEvent.ColorId));
    }

    private async Task<GoogleCalendarWriteResult> RetryConflictIfLocalWinsAsync(
        PendingPublishContext publishContext,
        GoogleCalendarWriteRequest writeRequest,
        CancellationToken ct)
    {
        if (!LocalPendingWins(publishContext.PendingEvent, publishContext.LiveEvent!))
        {
            return GoogleCalendarWriteResult.Failure(
                "The Google Calendar version is newer than this pending edit. The change remains pending for manual review.",
                GoogleCalendarWriteFailureKind.PreconditionFailed);
        }

        var latestEventResult = await _googleCalendarService.GetEventAsync(
            publishContext.PendingEvent.CalendarId,
            publishContext.PendingEvent.GcalEventId!,
            ct);

        if (!latestEventResult.Success || latestEventResult.Data is null)
        {
            return GoogleCalendarWriteResult.Failure(
                latestEventResult.ErrorMessage ?? "Unable to refresh the latest Google Calendar event before retrying.",
                GoogleCalendarWriteFailureKind.PreconditionFailed);
        }

        return await _googleCalendarService.UpdateEventAsync(
            publishContext.PendingEvent.GcalEventId!,
            writeRequest,
            latestEventResult.Data.GcalEtag,
            ct);
    }

    private static bool LocalPendingWins(PendingEvent pendingEvent, GcalEvent liveEvent)
    {
        if (!liveEvent.GcalUpdatedAt.HasValue)
        {
            return true;
        }

        return pendingEvent.UpdatedAt >= liveEvent.GcalUpdatedAt.Value;
    }

    private async Task PersistInsertSuccessAsync(
        PendingEvent pendingEvent,
        GcalEventDto insertedEvent,
        DateTime publishedAtUtc,
        CancellationToken ct)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var storedPendingEvent = await context.PendingEvents
            .SingleAsync(item => item.PendingEventId == pendingEvent.PendingEventId, ct);

        var liveEvent = await context.GcalEvents
            .SingleOrDefaultAsync(item => item.GcalEventId == insertedEvent.GcalEventId, ct);

        if (liveEvent is null)
        {
            liveEvent = CreatePublishedEvent(insertedEvent, publishedAtUtc);
            context.GcalEvents.Add(liveEvent);
        }
        else
        {
            ApplyPublishedValues(liveEvent, insertedEvent, publishedAtUtc);
            liveEvent.AppCreated = true;
            liveEvent.SourceSystem = "manual";
        }

        context.PendingEvents.Remove(storedPendingEvent);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task PersistUpdateSuccessAsync(
        PendingEvent pendingEvent,
        GcalEvent liveEventBeforePublish,
        GcalEventDto updatedEvent,
        DateTime publishedAtUtc,
        CancellationToken ct)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var storedPendingEvent = await context.PendingEvents
            .SingleAsync(item => item.PendingEventId == pendingEvent.PendingEventId, ct);
        var storedLiveEvent = await context.GcalEvents
            .SingleAsync(item => item.GcalEventId == liveEventBeforePublish.GcalEventId, ct);

        context.GcalEventVersions.Add(CreateVersionSnapshot(storedLiveEvent, publishedAtUtc));
        ApplyPublishedValues(storedLiveEvent, updatedEvent, publishedAtUtc);
        context.PendingEvents.Remove(storedPendingEvent);

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task PersistFailureAsync(
        string pendingEventId,
        DateTime publishAttemptedAtUtc,
        string publishError,
        CancellationToken ct)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var storedPendingEvent = await context.PendingEvents
            .SingleOrDefaultAsync(item => item.PendingEventId == pendingEventId, ct);
        if (storedPendingEvent is null)
        {
            return;
        }

        storedPendingEvent.PublishAttemptedAt = publishAttemptedAtUtc;
        storedPendingEvent.PublishError = publishError;
        storedPendingEvent.UpdatedAt = publishAttemptedAtUtc;
        await context.SaveChangesAsync(ct);
    }

    private static GcalEvent CreatePublishedEvent(GcalEventDto eventDto, DateTime publishedAtUtc)
    {
        return new GcalEvent
        {
            GcalEventId = eventDto.GcalEventId,
            CalendarId = eventDto.CalendarId,
            Summary = eventDto.Summary,
            Description = eventDto.Description,
            StartDatetime = eventDto.StartDateTimeUtc,
            EndDatetime = eventDto.EndDateTimeUtc,
            IsAllDay = eventDto.IsAllDay,
            ColorId = eventDto.ColorId,
            GcalEtag = eventDto.GcalEtag,
            GcalUpdatedAt = eventDto.GcalUpdatedAtUtc,
            IsDeleted = eventDto.IsDeleted,
            AppCreated = true,
            SourceSystem = "manual",
            AppPublished = true,
            AppPublishedAt = publishedAtUtc,
            AppLastModifiedAt = publishedAtUtc,
            RecurringEventId = eventDto.RecurringEventId,
            IsRecurringInstance = eventDto.IsRecurringInstance,
            LastSyncedAt = publishedAtUtc,
            CreatedAt = publishedAtUtc,
            UpdatedAt = publishedAtUtc
        };
    }

    private static void ApplyPublishedValues(GcalEvent liveEvent, GcalEventDto eventDto, DateTime publishedAtUtc)
    {
        liveEvent.CalendarId = eventDto.CalendarId;
        liveEvent.Summary = eventDto.Summary;
        liveEvent.Description = eventDto.Description;
        liveEvent.StartDatetime = eventDto.StartDateTimeUtc;
        liveEvent.EndDatetime = eventDto.EndDateTimeUtc;
        liveEvent.IsAllDay = eventDto.IsAllDay;
        liveEvent.ColorId = eventDto.ColorId;
        liveEvent.GcalEtag = eventDto.GcalEtag;
        liveEvent.GcalUpdatedAt = eventDto.GcalUpdatedAtUtc;
        liveEvent.IsDeleted = eventDto.IsDeleted;
        liveEvent.AppPublished = true;
        liveEvent.AppPublishedAt = publishedAtUtc;
        liveEvent.AppLastModifiedAt = publishedAtUtc;
        liveEvent.RecurringEventId = eventDto.RecurringEventId;
        liveEvent.IsRecurringInstance = eventDto.IsRecurringInstance;
        liveEvent.LastSyncedAt = publishedAtUtc;
        liveEvent.UpdatedAt = publishedAtUtc;
    }

    private static GcalEventDto MergePublishedColor(GcalEventDto eventDto, string? fallbackColorId)
    {
        if (!string.IsNullOrWhiteSpace(eventDto.ColorId))
        {
            return eventDto;
        }

        return eventDto with { ColorId = fallbackColorId };
    }

    private static GcalEventVersion CreateVersionSnapshot(GcalEvent liveEvent, DateTime createdAtUtc)
    {
        return new GcalEventVersion
        {
            GcalEventId = liveEvent.GcalEventId,
            GcalEtag = liveEvent.GcalEtag,
            Summary = liveEvent.Summary,
            Description = liveEvent.Description,
            StartDatetime = liveEvent.StartDatetime,
            EndDatetime = liveEvent.EndDatetime,
            IsAllDay = liveEvent.IsAllDay,
            ColorId = liveEvent.ColorId,
            GcalUpdatedAt = liveEvent.GcalUpdatedAt,
            RecurringEventId = liveEvent.RecurringEventId,
            IsRecurringInstance = liveEvent.IsRecurringInstance,
            ChangedBy = "manual_publish",
            ChangeReason = "updated",
            CreatedAt = createdAtUtc
        };
    }

    private sealed record PendingPublishContext(PendingEvent PendingEvent, GcalEvent? LiveEvent);
}

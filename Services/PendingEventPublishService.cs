using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
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
        var events = await context.Events
            .AsNoTracking()
            .Where(e => e.HasUnpublishedChanges || e.Publish == "local_only")
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.Summary)
            .ToListAsync(ct);

        return events.Select(MapPendingItem).ToList();
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

        var results = new List<PendingPublishItemResult>(pendingEventIds.Count);
        var completed = 0;
        foreach (var eventId in pendingEventIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
                var ev = await context.Events.SingleOrDefaultAsync(e => e.EventId == eventId, ct);
                if (ev is null)
                {
                    results.Add(new PendingPublishItemResult(eventId, null, false, "Event was not found."));
                }
                else
                {
                    var request = new GoogleCalendarWriteRequest(
                        ev.CalendarId,
                        ev.Summary,
                        ev.Description,
                        ev.StartDatetime,
                        ev.EndDatetime,
                        ev.IsAllDay ?? false,
                        _colorMappingService.GetGoogleColorId(ev.ColorId) ?? ev.ColorId);

                    var publishResult = string.IsNullOrWhiteSpace(ev.GcalEventId)
                        ? await _googleCalendarService.InsertEventAsync(request, ct)
                        : await _googleCalendarService.UpdateEventAsync(ev.GcalEventId, request, ev.GcalEtag, ct);

                    if (!publishResult.Success || publishResult.Event is null)
                    {
                        results.Add(new PendingPublishItemResult(
                            eventId,
                            ev.GcalEventId,
                            false,
                            publishResult.ErrorMessage ?? "Google Calendar publish failed.",
                            publishResult.ErrorDetails));
                    }
                    else
                    {
                        ApplyPublishedState(ev, publishResult.Event);
                        ev.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
                        await context.SaveChangesAsync(ct);
                        results.Add(new PendingPublishItemResult(eventId, ev.GcalEventId, true, null));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to publish event {EventId}.", eventId);
                results.Add(new PendingPublishItemResult(eventId, null, false, "Unable to publish the event.", ex.ToString()));
            }
            finally
            {
                completed++;
                progress?.Report(new PendingPublishProgress(completed, pendingEventIds.Count));
            }
        }

        var successCount = results.Count(r => r.Success);
        return new PendingPublishBatchResult(
            pendingEventIds.Count,
            successCount,
            pendingEventIds.Count - successCount,
            results);
    }

    public async Task RevertAsync(string pendingEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var ev = await context.Events.SingleOrDefaultAsync(e => e.EventId == pendingEventId, ct);
        if (ev is null)
        {
            return;
        }

        if (ev.Publish == "local_only")
        {
            context.Events.Remove(ev);
        }
        else
        {
            // TODO 8.4: improve RevertAsync for published events by re-fetching the GCal row.
            ev.HasUnpublishedChanges = false;
            ev.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateColorAsync(string pendingEventId, string colorKey, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var ev = await context.Events.SingleOrDefaultAsync(e => e.EventId == pendingEventId, ct);
        if (ev is null)
        {
            return;
        }

        ev.ColorId = _colorMappingService.NormalizeColorKey(colorKey);
        if (ev.Publish == "published")
        {
            ev.HasUnpublishedChanges = true;
        }

        ev.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await context.SaveChangesAsync(ct);
    }

    private PendingPublishListItem MapPendingItem(Event ev)
    {
        var colorKey = _colorMappingService.NormalizeColorKey(ev.ColorId);
        return new PendingPublishListItem(
            ev.EventId,
            ev.GcalEventId,
            string.IsNullOrWhiteSpace(ev.Summary) ? "(Untitled)" : ev.Summary,
            ev.StartDatetime,
            ev.EndDatetime,
            ev.IsAllDay ?? false,
            ev.IsRecurringInstance,
            GetSourceLabel(ev),
            colorKey,
            _colorMappingService.GetHexColor(colorKey),
            null);
    }

    private static string GetSourceLabel(Event ev)
    {
        if (ev.Publish == "local_only" && ev.Lifecycle == "approved")
        {
            return "new draft";
        }

        return ev.Publish == "published" && ev.HasUnpublishedChanges
            ? "edited event"
            : ev.SourceSystem ?? ev.Lifecycle;
    }

    private static void ApplyPublishedState(Event ev, GcalEventDto published)
    {
        ev.GcalEventId = published.GcalEventId;
        ev.CalendarId = published.CalendarId;
        ev.Summary = published.Summary;
        ev.Description = published.Description;
        ev.StartDatetime = published.StartDateTimeUtc;
        ev.EndDatetime = published.EndDateTimeUtc;
        ev.IsAllDay = published.IsAllDay;
        ev.ColorId = published.ColorId;
        ev.Publish = "published";
        ev.HasUnpublishedChanges = false;
        ev.GcalEtag = published.GcalEtag;
        ev.GcalUpdatedAt = published.GcalUpdatedAtUtc;
        ev.RecurringEventId = published.RecurringEventId;
        ev.IsRecurringInstance = published.IsRecurringInstance;
        ev.LastSyncedAt = DateTime.UtcNow;
    }
}

using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class EventPublishService : IEventPublishService
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IColorMappingService _colorMappingService;
    private readonly IEventRepository _eventRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EventPublishService> _logger;

    public EventPublishService(
        IDbContextFactory<CalendarDbContext> dbContextFactory,
        IGoogleCalendarService googleCalendarService,
        IColorMappingService colorMappingService,
        IEventRepository eventRepository,
        TimeProvider timeProvider,
        ILogger<EventPublishService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _googleCalendarService = googleCalendarService;
        _colorMappingService = colorMappingService;
        _eventRepository = eventRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PendingPublishListItem>> GetPendingItemsAsync(CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        // Includes candidate events (Lifecycle="candidate", Publish="local_only") intentionally —
        // candidates may eventually surface an "approve and publish" action directly from this panel.
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
        var ids = pendingEventIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new PendingPublishBatchResult(0, 0, 0, []);
        }

        var results = new List<PendingPublishItemResult>(ids.Count);
        var completed = 0;
        foreach (var eventId in ids)
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
                progress?.Report(new PendingPublishProgress(completed, ids.Count));
            }
        }

        var successCount = results.Count(r => r.Success);
        return new PendingPublishBatchResult(
            ids.Count,
            successCount,
            ids.Count - successCount,
            results);
    }

    public async Task RevertAsync(string pendingEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var ev = await context.Events.SingleOrDefaultAsync(e => e.EventId == pendingEventId, ct);
        if (ev is null)
            return;

        if (ev.Publish == "local_only")
        {
            context.Events.Remove(ev);
            await context.SaveChangesAsync(ct);
        }
        else if (ev.HasUnpublishedChanges)
        {
            await _eventRepository.RevertToLastSyncedAsync(pendingEventId, ct);
        }
    }

    public async Task UpdateColorAsync(string pendingEventId, string colorKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(colorKey))
            return;

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

    private void ApplyPublishedState(Event ev, GcalEventDto published)
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
        ev.LastSyncedAt = _timeProvider.GetUtcNow().UtcDateTime;
    }
}

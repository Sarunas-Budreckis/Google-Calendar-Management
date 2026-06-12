using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;

namespace GoogleCalendarManagement.Services;

public sealed class PendingEventDraftService : IPendingEventDraftService
{
    private readonly IEventRepository _eventRepository;
    private readonly IEventIdentityService _eventIdentityService;
    private readonly TimeProvider _timeProvider;

    public PendingEventDraftService(
        IEventRepository eventRepository,
        IEventIdentityService eventIdentityService,
        TimeProvider timeProvider)
    {
        _eventRepository = eventRepository;
        _eventIdentityService = eventIdentityService;
        _timeProvider = timeProvider;
    }

    public async Task<Event> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
    {
        return await CreateEventAsync(
            startLocal,
            endLocal,
            summary,
            lifecycle: "approved",
            sourceSystem: "manual",
            colorId: "azure",
            ct);
    }

    public async Task<Event> CreateCandidateAsync(
        DateTime startLocal,
        DateTime endLocal,
        string? summary = null,
        string? sourceSystem = null,
        string? colorId = null,
        CancellationToken ct = default)
    {
        return await CreateEventAsync(
            startLocal,
            endLocal,
            summary,
            lifecycle: "candidate",
            sourceSystem: sourceSystem,
            colorId: colorId,
            ct);
    }

    private async Task<Event> CreateEventAsync(
        DateTime startLocal,
        DateTime endLocal,
        string? summary,
        string lifecycle,
        string? sourceSystem,
        string? colorId,
        CancellationToken ct)
    {
        var normalizedStartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        var normalizedEndLocal = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var ev = new Event
        {
            EventId = _eventIdentityService.MintEventId(),
            CalendarId = "primary",
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            StartDatetime = normalizedStartLocal.ToUniversalTime(),
            EndDatetime = normalizedEndLocal.ToUniversalTime(),
            IsAllDay = false,
            ColorId = string.IsNullOrWhiteSpace(colorId) ? "azure" : colorId,
            Lifecycle = lifecycle,
            Publish = "local_only",
            HasUnpublishedChanges = false,
            SourceSystem = string.IsNullOrWhiteSpace(sourceSystem) ? "manual" : sourceSystem,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _eventRepository.UpsertAsync(ev, ct);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(ev.EventId));
        return ev;
    }
}

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
        var normalizedStartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        var normalizedEndLocal = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var draft = new Event
        {
            EventId = _eventIdentityService.MintEventId(),
            CalendarId = "primary",
            Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            StartDatetime = normalizedStartLocal.ToUniversalTime(),
            EndDatetime = normalizedEndLocal.ToUniversalTime(),
            IsAllDay = false,
            ColorId = "azure",
            Lifecycle = "approved",
            Publish = "local_only",
            HasUnpublishedChanges = false,
            SourceSystem = "manual",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _eventRepository.UpsertAsync(draft, ct);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.EventId));
        return draft;
    }
}

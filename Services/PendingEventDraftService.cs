using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;

namespace GoogleCalendarManagement.Services;

public sealed class PendingEventDraftService : IPendingEventDraftService
{
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly TimeProvider _timeProvider;

    public PendingEventDraftService(IPendingEventRepository pendingEventRepository, TimeProvider timeProvider)
    {
        _pendingEventRepository = pendingEventRepository;
        _timeProvider = timeProvider;
    }

    public async Task<PendingEvent> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
    {
        var normalizedStartLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        var normalizedEndLocal = DateTime.SpecifyKind(endLocal, DateTimeKind.Local);
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var draft = new PendingEvent
        {
            PendingEventId = $"pending_{Guid.NewGuid():N}",
            CalendarId = "primary",
            Summary = string.IsNullOrWhiteSpace(summary) ? "New event" : summary.Trim(),
            StartDatetime = normalizedStartLocal.ToUniversalTime(),
            EndDatetime = normalizedEndLocal.ToUniversalTime(),
            IsAllDay = false,
            ColorId = "azure",
            AppCreated = true,
            SourceSystem = "manual",
            ReadyToPublish = false,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _pendingEventRepository.UpsertAsync(draft, ct);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.PendingEventId));
        return draft;
    }
}

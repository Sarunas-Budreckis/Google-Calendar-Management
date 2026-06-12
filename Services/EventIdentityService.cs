namespace GoogleCalendarManagement.Services;

public sealed class EventIdentityService : IEventIdentityService
{
    private readonly IEventRepository _eventRepository;

    public EventIdentityService(IEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public string MintEventId()
    {
        return Guid.NewGuid().ToString("N");
    }

    public async Task<string?> ResolveEventIdAsync(string? eventId, string? gcalEventId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            var byEventId = await _eventRepository.GetByEventIdAsync(eventId, ct);
            if (byEventId is not null)
            {
                return byEventId.EventId;
            }
        }

        if (!string.IsNullOrWhiteSpace(gcalEventId))
        {
            var byGcalEventId = await _eventRepository.GetByGcalEventIdAsync(gcalEventId, ct);
            return byGcalEventId?.EventId;
        }

        return null;
    }
}

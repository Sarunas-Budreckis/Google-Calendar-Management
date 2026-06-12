namespace GoogleCalendarManagement.Services;

public interface IEventIdentityService
{
    string MintEventId();

    Task<string?> ResolveEventIdAsync(string? eventId, string? gcalEventId, CancellationToken ct = default);
}

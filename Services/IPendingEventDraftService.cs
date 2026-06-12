using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IPendingEventDraftService
{
    Task<Event> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default);
}

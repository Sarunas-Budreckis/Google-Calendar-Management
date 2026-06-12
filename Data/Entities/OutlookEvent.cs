namespace GoogleCalendarManagement.Data.Entities;

public class OutlookEvent
{
    public string OutlookEventId { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTime StartDatetime { get; set; }
    public DateTime EndDatetime { get; set; }
    public bool IsAllDay { get; set; }
    public string? Organizer { get; set; }
    public string? Location { get; set; }
    public string? BodyPreview { get; set; }
    public bool IsRecurring { get; set; }
    public string? SeriesMasterId { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public bool IsSuppressed { get; set; }
}

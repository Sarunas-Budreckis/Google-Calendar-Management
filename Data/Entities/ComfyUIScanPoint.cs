namespace GoogleCalendarManagement.Data.Entities;

public class ComfyUIScanPoint
{
    public int Id { get; set; }
    public DateTime ScannedAt { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string? LinkedEventId { get; set; }
    public string? LinkedEventType { get; set; }
}

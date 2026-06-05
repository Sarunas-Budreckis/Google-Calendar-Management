namespace GoogleCalendarManagement.Data.Entities;

public class Civ5SessionPoint
{
    public int Id { get; set; }
    public DateTime ScannedAt { get; set; }
    public DateTime FileModifiedAt { get; set; }
    public string GameMode { get; set; } = "unknown";
    public string? LinkedEventId { get; set; }
    public string? LinkedEventType { get; set; }
}

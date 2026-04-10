namespace GoogleCalendarManagement.Data.Entities;

public class PendingEvent
{
    public Guid Id { get; set; }
    public string GcalEventId { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartDatetime { get; set; }
    public DateTime EndDatetime { get; set; }
    public string ColorId { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public GcalEvent? GcalEvent { get; set; }
}

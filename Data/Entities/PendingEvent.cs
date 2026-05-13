namespace GoogleCalendarManagement.Data.Entities;

public class PendingEvent
{
    public string PendingEventId { get; set; } = "";
    public string? GcalEventId { get; set; }
    public string CalendarId { get; set; } = "primary";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public bool AppCreated { get; set; } = true;
    public string? SourceSystem { get; set; } = "manual";
    public bool ReadyToPublish { get; set; }
    public DateTime? PublishAttemptedAt { get; set; }
    public string? PublishError { get; set; }
    public string OperationType { get; set; } = "edit";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public GcalEvent? GcalEvent { get; set; }
}

namespace GoogleCalendarManagement.Data.Entities;

public class DeletedEvent
{
    public string GcalEventId { get; set; } = "";
    public string CalendarId { get; set; } = "primary";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public string? GcalEtag { get; set; }
    public string? RecurringEventId { get; set; }
    public bool? IsRecurringInstance { get; set; }
    public bool? AppCreated { get; set; }
    public string? SourceSystem { get; set; }
    public DateTime DeletedAt { get; set; }
    public string DeletionSource { get; set; } = "user";
    public DateTime? OriginalCreatedAt { get; set; }
    public DateTime? OriginalUpdatedAt { get; set; }
}

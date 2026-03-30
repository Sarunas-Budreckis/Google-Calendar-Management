namespace GoogleCalendarManagement.Data.Entities;

public class GcalEventVersion
{
    public int VersionId { get; set; }
    public string GcalEventId { get; set; } = "";
    public string? GcalEtag { get; set; }
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public DateTime? GcalUpdatedAt { get; set; }
    public string? RecurringEventId { get; set; }
    public bool IsRecurringInstance { get; set; }
    public string? ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public GcalEvent GcalEvent { get; set; } = null!;
}

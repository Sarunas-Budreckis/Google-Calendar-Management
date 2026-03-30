namespace GoogleCalendarManagement.Data.Entities;

public class GcalEvent
{
    public string GcalEventId { get; set; } = "";
    public string CalendarId { get; set; } = "";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public string? GcalEtag { get; set; }
    public DateTime? GcalUpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool AppCreated { get; set; }
    public string? SourceSystem { get; set; }
    public bool AppPublished { get; set; }
    public DateTime? AppPublishedAt { get; set; }
    public DateTime? AppLastModifiedAt { get; set; }
    public string? RecurringEventId { get; set; }
    public bool IsRecurringInstance { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<GcalEventVersion> Versions { get; set; } = new List<GcalEventVersion>();
}

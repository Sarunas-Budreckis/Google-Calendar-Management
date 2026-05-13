namespace GoogleCalendarManagement.Data.Entities;

public class TogglEntry
{
    public long TogglId { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int? DurationSeconds { get; set; }
    public string? ProjectName { get; set; }
    public string? Tags { get; set; }
    public bool VisibleAsEvent { get; set; } = true;
    public bool PublishedToGcal { get; set; }
    public string? PublishedGcalEventId { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public GcalEvent? PublishedGcalEvent { get; set; }
}

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

    public TogglDataType? TogglDataType { get; set; }
    public string? LinkedEventId { get; set; }
    public string? LinkedEventType { get; set; }

    // published_gcal_event_id stays as a plain scalar column (the GCal id once a Toggl entry is
    // published). The EF navigation to the curated event was dropped in Story 8.2: modeling it as
    // a relationship would force event.gcal_event_id to be a NOT NULL alternate key, conflicting
    // with the nullable, filtered-UNIQUE gcal_event_id required by the unified model. The link is
    // re-established through the data_point/link tables in Epic 8 (Stories 8.7+).
}

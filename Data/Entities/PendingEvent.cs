namespace GoogleCalendarManagement.Data.Entities;

/// <summary>
/// THROWAWAY POCO SHIM (Story 8.2). The unified <see cref="Event"/> table replaced both
/// gcal_event and pending_event in the database — this class is no longer an EF entity and
/// is NOT mapped to any table or exposed as a DbSet. It survives only as a plain DTO so the
/// deferred consumers (EventDetailsPanelViewModel, drilldowns, ICS, publish/sync services)
/// keep compiling until Stories 8.3–8.5 rewrite them against the new model, at which point
/// this file is deleted. Do NOT add new usages.
/// </summary>
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
}

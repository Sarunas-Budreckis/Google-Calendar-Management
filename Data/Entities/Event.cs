namespace GoogleCalendarManagement.Data.Entities;

/// <summary>
/// Unified event row (Epic 8 Phase 0). Replaces the old gcal_event + pending_event split.
/// Keyed by a stable local <see cref="EventId"/> that NEVER changes — links always reference
/// this id, so publishing a local event (which fills <see cref="GcalEventId"/>) never breaks a link.
/// </summary>
public class Event
{
    /// <summary>Stable local id, minted for every event, never changes. PK.</summary>
    public string EventId { get; set; } = "";

    /// <summary>Nullable, UNIQUE — filled on publish to GCal. GCal sync matches on this.</summary>
    public string? GcalEventId { get; set; }

    public string CalendarId { get; set; } = "primary";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }

    /// <summary>'candidate' | 'approved' (enforced by CHECK constraint, not a C# enum).</summary>
    public string Lifecycle { get; set; } = "approved";

    /// <summary>'local_only' | 'published' (enforced by CHECK constraint, not a C# enum).</summary>
    public string Publish { get; set; } = "local_only";

    /// <summary>Replaces the old "row exists in pending_event = dirty" heuristic.</summary>
    public bool HasUnpublishedChanges { get; set; }

    /// <summary>
    /// Soft-delete flag set by the GCal sync reconciler when the remote event is cancelled
    /// (Story 8.4). Story 8.6 relocates deleted rows into the deleted_event table.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>Origin: 'manual' | 'toggl' | 'outlook' | 'civ5' | ...</summary>
    public string? SourceSystem { get; set; }

    public string? RecurringEventId { get; set; }
    public bool IsRecurringInstance { get; set; }
    public string? GcalEtag { get; set; }
    public DateTime? GcalUpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime? AppLastModifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<GcalEventVersion> Versions { get; set; } = new List<GcalEventVersion>();
}

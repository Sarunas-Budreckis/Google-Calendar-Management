namespace GoogleCalendarManagement.Data.Entities;

/// <summary>
/// Records the resolution of a single <see cref="DataPoint"/> — either linked to an
/// <see cref="Event"/> or intentionally ignored. Each datapoint has at most one link row
/// (enforced by a unique index on <c>data_point_id</c>). Manual operations are grouped by
/// <see cref="ActionGroupId"/> so a whole clump can be undone as one batch (Story 8.12).
/// </summary>
public class Link
{
    public int LinkId { get; set; }
    public int DataPointId { get; set; }

    /// <summary>Set when <see cref="State"/> is 'linked'; null when 'ignored'.</summary>
    public string? EventId { get; set; }

    /// <summary>'linked' | 'ignored' (invariant enforced by LinkService, not the DB).</summary>
    public string State { get; set; } = "";

    /// <summary>'manual' | 'auto_rule'. Auto links never overwrite manual ones.</summary>
    public string Origin { get; set; } = "manual";

    /// <summary>Set only for auto_rule links — identifies the rule that wrote the row.</summary>
    public string? RuleId { get; set; }

    /// <summary>Groups rows written in one operation, for clump-level undo.</summary>
    public string ActionGroupId { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DataPoint DataPoint { get; set; } = null!;
    public Event? Event { get; set; }
}

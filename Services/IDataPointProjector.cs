using GoogleCalendarManagement.Data;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// Projects raw imported records for a single source into <see cref="DataPointSpec"/> rows.
/// Used by import (post-import incremental path) and by the reconciliation sweep to create
/// <c>data_point</c> registry rows. This is a distinct concern from
/// <see cref="ISourcePointerResolver"/> (which resolves display info) — do not combine them.
/// All <see cref="DateTime"/> values are UTC by convention; consumers must pass UTC.
/// </summary>
public interface IDataPointProjector
{
    /// <summary>Canonical source key; must match the owning handler's <c>SourceKey</c>.</summary>
    string SourceKey { get; }

    /// <summary>
    /// Find all raw records for this source that currently have no <c>data_point</c> row.
    /// Returns one <see cref="DataPointSpec"/> per orphaned raw record.
    /// Used by the reconciliation sweep (incremental + startup + rebuild).
    /// </summary>
    Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default);

    /// <summary>
    /// Project a specific set of source_refs (just inserted) into <see cref="DataPointSpec"/>.
    /// Used for the post-import incremental path: pass newly-inserted source_ref values.
    /// </summary>
    Task<IReadOnlyList<DataPointSpec>> ProjectSourceRefsAsync(
        CalendarDbContext ctx,
        IReadOnlyList<string> sourceRefs,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all source_refs currently present in the raw table for this source.
    /// Used by the reconciliation sweep to detect and delete stale <c>data_point</c> rows
    /// whose corresponding raw record was deleted on re-import.
    /// </summary>
    Task<IReadOnlyList<string>> GetAllRawSourceRefsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default);
}

public record DataPointSpec(string SourceKey, string SourceRef, DateTime StartUtc, DateTime EndUtc);

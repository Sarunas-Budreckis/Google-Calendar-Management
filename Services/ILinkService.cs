using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// Executes link / ignore / unlink operations over the <see cref="Link"/> table (Story 8.12).
/// Manual operations are grouped by an <c>action_group_id</c> so a clump can be undone as one
/// batch via <see cref="UndoActionGroupAsync"/>. Single-row auto writes are not undoable; batched
/// rule-engine writes are undoable as one pipeline run.
/// </summary>
public interface ILinkService
{
    // --- Manual operations (all return action_group_id for undo) ---
    Task<string> LinkAsync(int dataPointId, string eventId, CancellationToken ct = default);
    Task<string> LinkClumpAsync(IEnumerable<int> dataPointIds, string eventId, CancellationToken ct = default);
    Task<string> IgnoreAsync(int dataPointId, CancellationToken ct = default);
    Task<string> IgnoreClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);
    Task<string> UnlinkAsync(int dataPointId, CancellationToken ct = default);
    Task<string> UnlinkClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);

    // Undo a previously returned action_group_id (no-op if unknown)
    Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);

    // --- Rule engine operations ---
    // Single-row auto writes (NOT added to undo stack).
    Task WriteAutoLinkAsync(int dataPointId, string eventId, string ruleId, CancellationToken ct = default);
    Task WriteAutoIgnoreAsync(int dataPointId, string ruleId, CancellationToken ct = default);

    /// <summary>
    /// Applies a whole rule-pipeline run's auto writes in ONE transaction under a single shared
    /// <c>action_group_id</c> (returned), so the run is atomically undoable via
    /// <see cref="UndoActionGroupAsync"/> (Story 8.14). Manual rows are never overwritten, and
    /// writes that exactly match an existing auto row are skipped (idempotent — no net change).
    /// </summary>
    Task<string> WriteAutoBatchAsync(IReadOnlyList<AutoLinkWrite> writes, CancellationToken ct = default);

    /// <summary>
    /// Deletes only <c>origin = 'auto_rule'</c> link rows for the given datapoints, in one
    /// transaction, and returns snapshots of the deleted rows. Manual rows are never deleted.
    /// Used by the rule engine's reversal pass (Story 8.14).
    /// </summary>
    Task<IReadOnlyList<Link>> DeleteAutoLinksForDataPointsAsync(
        IReadOnlyList<int> dataPointIds, CancellationToken ct = default);

    // --- Queries ---
    Task<Link?> GetLinkAsync(int dataPointId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByEventAsync(string eventId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByActionGroupAsync(string actionGroupId, CancellationToken ct = default);
}

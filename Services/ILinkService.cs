using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// Executes link / ignore / unlink operations over the <see cref="Link"/> table (Story 8.12).
/// Manual operations are grouped by an <c>action_group_id</c> so a clump can be undone as one
/// batch via <see cref="UndoActionGroupAsync"/>. Auto (rule-engine) writes are NOT undoable and
/// never overwrite a manual decision.
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

    // --- Rule engine operations (NOT added to undo stack) ---
    Task WriteAutoLinkAsync(int dataPointId, string eventId, string ruleId, CancellationToken ct = default);
    Task WriteAutoIgnoreAsync(int dataPointId, string ruleId, CancellationToken ct = default);

    // --- Queries ---
    Task<Link?> GetLinkAsync(int dataPointId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByEventAsync(string eventId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByActionGroupAsync(string actionGroupId, CancellationToken ct = default);
}

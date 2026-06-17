using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

/// <summary>
/// Atomic link / ignore / unlink operations over the <see cref="Link"/> table, grouped by an
/// <c>action_group_id</c> for clump-level undo (Story 8.12). The state/event_id invariant is
/// enforced here (not the DB): 'linked' ⇒ event_id set, 'ignored' ⇒ event_id null.
/// </summary>
public sealed class LinkService : ILinkService
{
    private const string StateLinked = "linked";
    private const string StateIgnored = "ignored";
    private const string OriginManual = "manual";
    private const string OriginAutoRule = "auto_rule";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    // Session-scoped in-memory undo stack. Snapshots capture the pre-operation row (or null) per
    // datapoint so a whole action group can be reverted in one transaction.
    private readonly Dictionary<string, List<LinkSnapshot>> _undoStack = new();
    private readonly object _undoLock = new();

    public LinkService(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private record LinkSnapshot(int DataPointId, Link? PreviousRow);

    public Task<string> LinkAsync(int dataPointId, string eventId, CancellationToken ct = default) =>
        UpsertClumpAsync(new[] { dataPointId }, StateLinked, eventId, OriginManual, null, addToUndoStack: true, ct);

    public Task<string> LinkClumpAsync(IEnumerable<int> dataPointIds, string eventId, CancellationToken ct = default) =>
        UpsertClumpAsync(dataPointIds, StateLinked, eventId, OriginManual, null, addToUndoStack: true, ct);

    public Task<string> IgnoreAsync(int dataPointId, CancellationToken ct = default) =>
        UpsertClumpAsync(new[] { dataPointId }, StateIgnored, null, OriginManual, null, addToUndoStack: true, ct);

    public Task<string> IgnoreClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default) =>
        UpsertClumpAsync(dataPointIds, StateIgnored, null, OriginManual, null, addToUndoStack: true, ct);

    public Task<string> UnlinkAsync(int dataPointId, CancellationToken ct = default) =>
        DeleteClumpAsync(new[] { dataPointId }, ct);

    public Task<string> UnlinkClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default) =>
        DeleteClumpAsync(dataPointIds, ct);

    public async Task WriteAutoLinkAsync(int dataPointId, string eventId, string ruleId, CancellationToken ct = default) =>
        await UpsertClumpAsync(new[] { dataPointId }, StateLinked, eventId, OriginAutoRule, ruleId, addToUndoStack: false, ct);

    public async Task WriteAutoIgnoreAsync(int dataPointId, string ruleId, CancellationToken ct = default) =>
        await UpsertClumpAsync(new[] { dataPointId }, StateIgnored, null, OriginAutoRule, ruleId, addToUndoStack: false, ct);

    public async Task<string> WriteAutoBatchAsync(IReadOnlyList<AutoLinkWrite> writes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(writes);

        var actionGroupId = GenerateActionGroupId();
        if (writes.Count == 0)
        {
            // Empty run — nothing to apply or undo.
            return actionGroupId;
        }

        var now = DateTime.UtcNow;
        var snapshots = new List<LinkSnapshot>();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await using var tx = await context.Database.BeginTransactionAsync(ct);

        foreach (var write in writes)
        {
            var state = write.Ignore ? StateIgnored : StateLinked;
            var eventId = write.Ignore ? null : write.EventId;

            AssertInvariant(state, eventId);
            AssertAutoRuleId(OriginAutoRule, write.RuleId);
            AssertGeneratedEventMatchesWrite(write, eventId);

            var existing = await context.Links
                .SingleOrDefaultAsync(l => l.DataPointId == write.DataPointId, ct);

            // Manual decisions are sacred — never overwrite. The engine already excludes manual
            // rows from the eligible set; this is defense-in-depth against a race, and it skips
            // (rather than throwing) so one stray row can't abort an entire pipeline run.
            if (existing?.Origin == OriginManual)
            {
                continue;
            }

            if (write.GeneratedEvent is not null)
            {
                await UpsertGeneratedEventOnContextAsync(context, write.GeneratedEvent, ct);
            }

            // Idempotency: an identical auto row is left untouched (no link write, no undo snapshot),
            // so re-running a pipeline on unchanged data produces no net link change. A generated
            // candidate refresh may still have been applied above when source fields changed.
            if (existing is not null &&
                existing.Origin == OriginAutoRule &&
                existing.State == state &&
                existing.EventId == eventId &&
                existing.RuleId == write.RuleId)
            {
                continue;
            }

            snapshots.Add(new LinkSnapshot(write.DataPointId, CloneRow(existing)));
            UpsertOnContext(context, existing, write.DataPointId, eventId, state, OriginAutoRule, write.RuleId, actionGroupId, now);
        }

        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Pipeline runs are undoable (Story 8.14): register the changed rows under the shared group.
        if (snapshots.Count > 0)
        {
            PushUndo(actionGroupId, snapshots);
        }

        return actionGroupId;
    }

    public async Task<IReadOnlyList<Link>> DeleteAutoLinksForDataPointsAsync(
        IReadOnlyList<int> dataPointIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dataPointIds);

        var ids = dataPointIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<Link>();
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var rows = await context.Links
            .Where(l => ids.Contains(l.DataPointId) && l.Origin == OriginAutoRule)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return Array.Empty<Link>();
        }

        var deleted = rows.Select(r => CloneRow(r)!).ToList();
        context.Links.RemoveRange(rows);
        await context.SaveChangesAsync(ct);

        return deleted;
    }

    public async Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default)
    {
        List<LinkSnapshot>? snapshots;
        lock (_undoLock)
        {
            if (!_undoStack.Remove(actionGroupId, out snapshots))
            {
                // Group is unknown (never created or already superseded) — no-op.
                return;
            }
        }

        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        foreach (var snapshot in snapshots)
        {
            var existing = await context.Links
                .SingleOrDefaultAsync(l => l.DataPointId == snapshot.DataPointId, ct);

            if (snapshot.PreviousRow is null)
            {
                // Was unlinked before the operation — remove the current row, if any.
                if (existing is not null)
                {
                    context.Links.Remove(existing);
                }
            }
            else
            {
                // Was linked/ignored before — restore the previous row's fields.
                UpsertOnContext(context, existing, snapshot.DataPointId,
                    snapshot.PreviousRow.EventId, snapshot.PreviousRow.State,
                    snapshot.PreviousRow.Origin, snapshot.PreviousRow.RuleId,
                    snapshot.PreviousRow.ActionGroupId, now);
            }
        }

        await context.SaveChangesAsync(ct);

        lock (_undoLock)
        {
            _undoStack.Remove(actionGroupId);
        }
    }

    public async Task<Link?> GetLinkAsync(int dataPointId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Links.AsNoTracking()
            .SingleOrDefaultAsync(l => l.DataPointId == dataPointId, ct);
    }

    public async Task<IReadOnlyList<Link>> GetLinksByEventAsync(string eventId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Links.AsNoTracking()
            .Where(l => l.EventId == eventId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Link>> GetLinksByActionGroupAsync(string actionGroupId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Links.AsNoTracking()
            .Where(l => l.ActionGroupId == actionGroupId)
            .ToListAsync(ct);
    }

    // --- Internals ---

    private static string GenerateActionGroupId() => Guid.NewGuid().ToString("N");

    private async Task<string> UpsertClumpAsync(
        IEnumerable<int> dataPointIds, string state, string? eventId,
        string origin, string? ruleId, bool addToUndoStack, CancellationToken ct)
    {
        AssertInvariant(state, eventId);
        AssertAutoRuleId(origin, ruleId);

        var ids = NormalizeDataPointIds(dataPointIds);
        var actionGroupId = GenerateActionGroupId();
        var now = DateTime.UtcNow;
        var snapshots = new List<LinkSnapshot>();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        foreach (var dataPointId in ids)
        {
            var existing = await context.Links
                .SingleOrDefaultAsync(l => l.DataPointId == dataPointId, ct);

            if (origin == OriginAutoRule)
            {
                AssertNotManual(existing, state == StateLinked ? "auto-link" : "auto-ignore");
            }

            snapshots.Add(new LinkSnapshot(dataPointId, CloneRow(existing)));
            UpsertOnContext(context, existing, dataPointId, eventId, state, origin, ruleId, actionGroupId, now);
        }

        await context.SaveChangesAsync(ct);

        if (addToUndoStack)
        {
            PushUndo(actionGroupId, snapshots);
        }

        return actionGroupId;
    }

    private async Task<string> DeleteClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct)
    {
        var ids = NormalizeDataPointIds(dataPointIds);
        var actionGroupId = GenerateActionGroupId();
        var snapshots = new List<LinkSnapshot>();

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        foreach (var dataPointId in ids)
        {
            var existing = await context.Links
                .SingleOrDefaultAsync(l => l.DataPointId == dataPointId, ct);

            snapshots.Add(new LinkSnapshot(dataPointId, CloneRow(existing)));

            if (existing is not null)
            {
                context.Links.Remove(existing);
            }
            // No row → idempotent no-op (snapshot of null lets undo restore "nothing").
        }

        await context.SaveChangesAsync(ct);

        PushUndo(actionGroupId, snapshots);
        return actionGroupId;
    }

    private static Link UpsertOnContext(
        CalendarDbContext context, Link? existing, int dataPointId, string? eventId,
        string state, string origin, string? ruleId, string actionGroupId, DateTime now)
    {
        if (existing is null)
        {
            var row = new Link
            {
                DataPointId = dataPointId,
                EventId = eventId,
                State = state,
                Origin = origin,
                RuleId = ruleId,
                ActionGroupId = actionGroupId,
                CreatedAt = now,
                UpdatedAt = now
            };
            context.Links.Add(row);
            return row;
        }

        existing.EventId = eventId;
        existing.State = state;
        existing.Origin = origin;
        existing.RuleId = ruleId;
        existing.ActionGroupId = actionGroupId;
        existing.UpdatedAt = now;
        return existing;
    }

    private static async Task<bool> UpsertGeneratedEventOnContextAsync(
        CalendarDbContext context,
        Event generated,
        CancellationToken ct)
    {
        var existing = await context.Events
            .SingleOrDefaultAsync(e => e.EventId == generated.EventId, ct);
        if (existing is null)
        {
            context.Events.Add(generated);
            return true;
        }

        var changed = false;
        if (existing.CalendarId != generated.CalendarId)
        {
            existing.CalendarId = generated.CalendarId;
            changed = true;
        }
        if (existing.Summary != generated.Summary)
        {
            existing.Summary = generated.Summary;
            changed = true;
        }
        if (existing.Description != generated.Description)
        {
            existing.Description = generated.Description;
            changed = true;
        }
        if (existing.StartDatetime != generated.StartDatetime)
        {
            existing.StartDatetime = generated.StartDatetime;
            changed = true;
        }
        if (existing.EndDatetime != generated.EndDatetime)
        {
            existing.EndDatetime = generated.EndDatetime;
            changed = true;
        }
        if (existing.ColorId != generated.ColorId)
        {
            existing.ColorId = generated.ColorId;
            changed = true;
        }
        if (existing.Lifecycle != generated.Lifecycle)
        {
            existing.Lifecycle = generated.Lifecycle;
            changed = true;
        }
        if (existing.Publish != generated.Publish)
        {
            existing.Publish = generated.Publish;
            changed = true;
        }
        if (existing.SourceSystem != generated.SourceSystem)
        {
            existing.SourceSystem = generated.SourceSystem;
            changed = true;
        }
        if (existing.HasUnpublishedChanges != generated.HasUnpublishedChanges)
        {
            existing.HasUnpublishedChanges = generated.HasUnpublishedChanges;
            changed = true;
        }
        if (existing.IsDeleted != generated.IsDeleted)
        {
            existing.IsDeleted = generated.IsDeleted;
            changed = true;
        }

        if (changed)
        {
            existing.UpdatedAt = generated.UpdatedAt;
        }

        return changed;
    }

    private static IReadOnlyList<int> NormalizeDataPointIds(IEnumerable<int> dataPointIds)
    {
        ArgumentNullException.ThrowIfNull(dataPointIds);

        var ids = dataPointIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new ArgumentException("At least one datapoint id is required.", nameof(dataPointIds));
        }

        return ids;
    }

    private void PushUndo(string actionGroupId, List<LinkSnapshot> snapshots)
    {
        lock (_undoLock)
        {
            var touchedIds = snapshots.Select(s => s.DataPointId).ToHashSet();
            var supersededGroups = _undoStack
                .Where(kvp => kvp.Value.Any(s => touchedIds.Contains(s.DataPointId)))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var supersededGroup in supersededGroups)
            {
                _undoStack.Remove(supersededGroup);
            }

            _undoStack[actionGroupId] = snapshots;
        }
    }

    private static Link? CloneRow(Link? row) =>
        row is null
            ? null
            : new Link
            {
                LinkId = row.LinkId,
                DataPointId = row.DataPointId,
                EventId = row.EventId,
                State = row.State,
                Origin = row.Origin,
                RuleId = row.RuleId,
                ActionGroupId = row.ActionGroupId,
                CreatedAt = row.CreatedAt,
                UpdatedAt = row.UpdatedAt
            };

    private static void AssertInvariant(string state, string? eventId)
    {
        var hasEvent = !string.IsNullOrWhiteSpace(eventId);

        if (state != StateLinked && state != StateIgnored)
        {
            throw new InvalidOperationException(
                $"Invalid link state '{state}'. Only '{StateLinked}' and '{StateIgnored}' are allowed.");
        }

        if (state == StateLinked && !hasEvent)
        {
            throw new InvalidOperationException(
                "A 'linked' resolution requires a non-null event_id.");
        }

        if (state == StateIgnored && hasEvent)
        {
            throw new InvalidOperationException(
                "An 'ignored' resolution must not reference an event_id.");
        }
    }

    private static void AssertAutoRuleId(string origin, string? ruleId)
    {
        if (origin == OriginAutoRule && string.IsNullOrWhiteSpace(ruleId))
        {
            throw new InvalidOperationException("An auto_rule resolution requires a non-empty rule_id.");
        }
    }

    private static void AssertGeneratedEventMatchesWrite(AutoLinkWrite write, string? eventId)
    {
        if (write.GeneratedEvent is null)
        {
            return;
        }

        if (write.Ignore)
        {
            throw new InvalidOperationException("An ignored auto-rule write cannot include a generated event.");
        }

        if (string.IsNullOrWhiteSpace(eventId) || write.GeneratedEvent.EventId != eventId)
        {
            throw new InvalidOperationException("A generated event write must link to the generated event id.");
        }
    }

    private static void AssertNotManual(Link? existing, string operation)
    {
        if (existing?.Origin == OriginManual)
        {
            throw new InvalidOperationException(
                $"Cannot {operation} datapoint {existing.DataPointId}: it has a manual link. " +
                "Rule engine must not overwrite manual decisions.");
        }
    }
}

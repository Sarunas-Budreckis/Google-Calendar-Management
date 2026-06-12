# Story 8.4: Migrate Sync Reconciler to Unified Event Table

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
**Agent:** Opus · **Effort:** high
**Dependencies:** 8.3 (blocking — `IEventRepository`, `IEventIdentityService`, and the unified `event` table must exist)

---

## Story

As the GCal sync pipeline,
I want to reconcile incoming Google Calendar events against the unified `event` table (matching on `gcal_event_id`), using `has_unpublished_changes` instead of the old "row exists in pending_event" heuristic,
so that pull-sync updates published events without clobbering local unpublished edits, new locally-approved events bind their `gcal_event_id` on first publish-then-sync, and version-history snapshots are written correctly — with no double-counting or data loss.

---

## Acceptance Criteria

1. `SyncManager` no longer references `GcalEvent`, `context.GcalEvents`, or any `GcalEvent`-typed helpers. All reconciliation logic operates on `context.Events` (the unified `event` table) via `IEventRepository` or direct `IDbContextFactory<CalendarDbContext>`.
2. **Lookup by `gcal_event_id`:** For each incoming `GcalEventDto`, the reconciler finds the existing `event` row using `event.gcal_event_id = incomingEvent.GcalEventId` (not by any old PK).
3. **New GCal event (not in DB):** If no row has that `gcal_event_id`, a new `event` row is inserted with `lifecycle='approved'`, `publish='published'`, `has_unpublished_changes=false`, `gcal_event_id` set, and a freshly minted `event_id` via `IEventIdentityService.MintEventId()`.
4. **Incoming update, no local edits** (`has_unpublished_changes=false`): GCal field values (summary, description, start/end, color, etag, recurring metadata, `gcal_updated_at`) are applied; `last_synced_at` and `updated_at` are updated; `has_unpublished_changes` remains false.
5. **Incoming update, local unpublished edits** (`has_unpublished_changes=true`): GCal metadata fields (`gcal_etag`, `gcal_updated_at`, `last_synced_at`) are updated, but user-facing fields (summary, description, start/end, color_id, is_all_day) are **not overwritten**. `has_unpublished_changes` stays true. The local edit is preserved.
6. **Incoming delete** (`isDeleted=true`), no local edits: mark `is_deleted=true` (or use lifecycle-equivalent field) on the `event` row; version snapshot is written with `change_reason='deleted'`.
7. **Incoming delete**, local unpublished edits exist (`has_unpublished_changes=true`): the delete is still applied (GCal is authoritative for deletes), but a `// NOTE: local edit discarded by remote delete` log warning is emitted.
8. **Version history snapshot** (`gcal_event_version`): the `CreateVersionSnapshot` helper is updated to accept an `Event` entity (not `GcalEvent`) and write the snapshot with `event_id` (not `gcal_event_id`) as the FK. Snapshot logic (when to snapshot: etag change, field change) is preserved.
9. `ShouldSnapshotForUpdate`, `ApplyIncomingValues`, `ApplyDeletedValues`, and `CreateEntity` are rewritten (or renamed) to operate on `Event` instead of `GcalEvent`. The color-preservation logic (when GCal sends null `color_id`, keep local value) is preserved.
10. `IGcalEventRepository` / `GcalEventRepository` have their `// TODO 8.4: remove after sync reconciler is rewritten` comment resolved: the interface and implementation are **deleted** in this story (they are no longer needed by any path after this story).
11. The existing `GoogleCalendarSyncTests` integration test suite is **rewritten** to use `context.Events` instead of `context.GcalEvents`. All existing behavioral contracts (add, update, delete, snapshot, cancellation, database lock, color preservation, ownership metadata, recurring) are preserved and pass. Ownership fields `AppCreated`, `AppPublished`, `SourceSystem` on `Event` are preserved during update/delete (same logic as before).
12. The app compiles and `SyncManager.SyncAsync` completes without error on a real database that was migrated by 8.2 (i.e., `gcal_event` data is now in `event`).
13. No references to `GcalEvent` entity class remain in `SyncManager.cs` or `GoogleCalendarSyncTests.cs`.

---

## Tasks / Subtasks

- [ ] Task 1: Rewrite `SyncManager` entity helpers to use `Event` (AC: #1, #8, #9)
  - [ ] 1.1 Delete `CreateEntity(GcalEventDto, DateTime) → GcalEvent` and replace with `CreateEventEntity(GcalEventDto, string eventId, DateTime) → Event` that mints using the passed `eventId`, sets `lifecycle='approved'`, `publish='published'`, `has_unpublished_changes=false`, and maps all GCal fields
  - [ ] 1.2 Delete `CreateVersionSnapshot(GcalEvent, string, DateTime) → GcalEventVersion` and replace with `CreateVersionSnapshot(Event, string, DateTime) → GcalEventVersion` — write `EventId = existingEvent.EventId` (not `GcalEventId`); copy all snapshot fields from `Event` equivalents
  - [ ] 1.3 Rewrite `ShouldSnapshotForUpdate` to compare `Event` fields vs `GcalEventDto` (same field comparisons as before, just different source type)
  - [ ] 1.4 Rewrite `ApplyIncomingValues(Event, GcalEventDto, DateTime)`: update GCal fields unconditionally only when `has_unpublished_changes=false`; when true, update only `GcalEtag`, `GcalUpdatedAt`, `LastSyncedAt`, `UpdatedAt` — **never touch** summary, description, start/end, color, is_all_day
  - [ ] 1.5 Rewrite `ApplyDeletedValues(Event, GcalEventDto, DateTime)`: mark deleted; log warning if `has_unpublished_changes=true` before applying
  - [ ] 1.6 Preserve `ResolveIncomingColorId` helper unchanged (logic is the same)

- [ ] Task 2: Rewrite `SyncAsync` reconciliation loop (AC: #1, #2, #3, #4, #5, #6, #7)
  - [ ] 2.1 Replace `context.GcalEvents.FindAsync([incomingEvent.GcalEventId])` with `context.Events.SingleOrDefaultAsync(e => e.GcalEventId == incomingEvent.GcalEventId, CancellationToken.None)` — note: lookup is by `gcal_event_id` (nullable), not PK
  - [ ] 2.2 For the "new event" branch: call `IEventIdentityService.MintEventId()` and pass result to `CreateEventEntity`; add to `context.Events`
  - [ ] 2.3 For the "existing event / update" branch: call rewritten `ApplyIncomingValues` which respects `HasUnpublishedChanges`
  - [ ] 2.4 For the "existing event / delete" branch: call rewritten `ApplyDeletedValues`; snapshot before delete as before
  - [ ] 2.5 Inject `IEventIdentityService` into `SyncManager` constructor; update DI registration in `App.xaml.cs`
  - [ ] 2.6 Remove all `GcalEvent`-typed local variables and casts from `SyncAsync`

- [ ] Task 3: Remove `IGcalEventRepository` / `GcalEventRepository` (AC: #10)
  - [ ] 3.1 Delete `Services/IGcalEventRepository.cs`
  - [ ] 3.2 Delete `Services/GcalEventRepository.cs`
  - [ ] 3.3 Search for any remaining callers of `IGcalEventRepository` (use: `grep -rn "IGcalEventRepository\|GcalEventRepository" Services/ ViewModels/`) — fix or stub any remaining references
  - [ ] 3.4 Remove `IGcalEventRepository` DI registration from `App.xaml.cs`

- [ ] Task 4: Update `GcalEventVersion` entity to use `EventId` FK (AC: #8) — **only if not done in 8.2**
  - [ ] 4.1 Verify `GcalEventVersion.EventId` (not `GcalEventId`) is the FK after 8.2 migration. If the property was renamed in 8.2 (Task 2.1 of 8.2), this task is a no-op.
  - [ ] 4.2 If `GcalEventVersion.GcalEventId` still exists as the navigation property (8.2 left it in place during stub phase), rename to `EventId` and update the configuration `GcalEventVersionConfiguration.cs` to reference `event.event_id`
  - [ ] 4.3 Any remaining test helpers that access `version.GcalEventId` must be updated to `version.EventId`

- [ ] Task 5: Rewrite `GoogleCalendarSyncTests` (AC: #11, #13)
  - [ ] 5.1 Replace all `context.GcalEvents` references with `context.Events`
  - [ ] 5.2 Replace all `GcalEvent { ... }` seed objects with `Event { ... }` seed objects — map fields to the unified entity:
    - `GcalEventId` → `GcalEventId` (same)
    - `IsDeleted` → keep (field exists on `Event` — verify entity)
    - `AppCreated` / `AppPublished` / `SourceSystem` → keep (migrated from `gcal_event`)
    - Add required new fields: `EventId = Guid.NewGuid().ToString("N")`, `Lifecycle = "approved"`, `Publish = "published"`, `HasUnpublishedChanges = false`
  - [ ] 5.3 Replace all `context.GcalEventVersions` assertions that check `version.GcalEventId` with `version.EventId`
  - [ ] 5.4 Verify `LockingCalendarDbContext` still works (no `GcalEvent` references inside it)
  - [ ] 5.5 Add new test: `SyncAsync_UpdatedEvent_WithLocalUnpublishedEdits_DoesNotClobberLocalFields` — seed an `Event` with `HasUnpublishedChanges=true`, summary "Local edit", then sync an incoming update with summary "GCal update" → verify summary stays "Local edit", etag is updated, `HasUnpublishedChanges` stays true
  - [ ] 5.6 Add new test: `SyncAsync_DeletedEvent_WithLocalUnpublishedEdits_AppliesDelete` — seed an `Event` with `HasUnpublishedChanges=true`, sync incoming delete → verify `IsDeleted=true`, version snapshot written, log warning emitted (optional: just verify `IsDeleted`)

- [ ] Task 6: Update DI and compilation (AC: #12)
  - [ ] 6.1 In `App.xaml.cs`: add `services.AddSingleton<IEventIdentityService, EventIdentityService>()` if not already registered from 8.3; remove `IGcalEventRepository` registration
  - [ ] 6.2 Verify `SyncManager` constructor parameters compile with `IEventIdentityService` injected
  - [ ] 6.3 Run build; resolve any remaining `GcalEvent`-shaped compilation errors in `SyncManager.cs`

---

## Dev Notes

### What 8.3 left for this story

Story 8.3 created:
- `Services/IEventRepository.cs` + `Services/EventRepository.cs` — full CRUD over `context.Events`
- `Services/IEventIdentityService.cs` + `Services/EventIdentityService.cs` — `MintEventId()` + `ResolveEventIdAsync`
- Added `// TODO 8.4: remove after sync reconciler is rewritten` comment on `IGcalEventRepository`
- `context.GcalEvents` DbSet no longer exists (removed in 8.2); `context.Events` is the only event set

The unified `event` table has all fields needed by the sync reconciler:
- `gcal_event_id` (nullable, UNIQUE) — the GCal-assigned id; sync matches on this
- `event_id` (PK, stable) — never changes even after publish
- `has_unpublished_changes` (bool) — the guard flag that prevents clobbering local edits
- `lifecycle` (`'candidate'` | `'approved'`), `publish` (`'local_only'` | `'published'`)
- All GCal fields: summary, description, start/end datetime, is_all_day, color_id, gcal_etag, gcal_updated_at, recurring_event_id, is_recurring_instance, last_synced_at

### Critical: `has_unpublished_changes` guard in `ApplyIncomingValues`

This is the most important behavioral change. The old code called `ApplyIncomingValues` unconditionally — it worked because `pending_event` (the "dirty" indicator) was checked separately in `CalendarQueryService`. Now the dirty flag lives on the event row itself.

```csharp
private static bool ApplyIncomingValues(Event existingEvent, GcalEventDto incomingEvent, DateTime syncedAt)
{
    // Always update sync metadata
    existingEvent.GcalEtag = incomingEvent.GcalEtag ?? existingEvent.GcalEtag;
    existingEvent.GcalUpdatedAt = incomingEvent.GcalUpdatedAtUtc;
    existingEvent.LastSyncedAt = syncedAt;
    existingEvent.UpdatedAt = syncedAt;

    if (existingEvent.HasUnpublishedChanges)
    {
        // Local edit is sacred — do NOT update user-facing fields
        return false; // no "changed" for the purpose of eventsUpdated count
    }

    var resolvedIncomingColorId = ResolveIncomingColorId(existingEvent.ColorId, incomingEvent.ColorId);
    var changed =
        existingEvent.Summary != incomingEvent.Summary ||
        existingEvent.Description != incomingEvent.Description ||
        existingEvent.StartDatetime != incomingEvent.StartDateTimeUtc ||
        existingEvent.EndDatetime != incomingEvent.EndDateTimeUtc ||
        existingEvent.IsAllDay != incomingEvent.IsAllDay ||
        existingEvent.ColorId != resolvedIncomingColorId ||
        // ... etc
    
    existingEvent.Summary = incomingEvent.Summary;
    // ... apply other fields
    return changed;
}
```

### `CreateVersionSnapshot` — FK is now `EventId`

`GcalEventVersion` was updated in 8.2 (Task 2) to use `EventId` instead of `GcalEventId` as the FK. The snapshot factory method must set:

```csharp
private static GcalEventVersion CreateVersionSnapshot(Event existingEvent, string changeReason, DateTime createdAt)
{
    return new GcalEventVersion
    {
        EventId = existingEvent.EventId,   // NOT GcalEventId
        GcalEtag = existingEvent.GcalEtag,
        Summary = existingEvent.Summary,
        // ... other snapshot fields
        ChangedBy = "gcal_sync",
        ChangeReason = changeReason,
        CreatedAt = createdAt
    };
}
```

Verify `GcalEventVersion.EventId` property exists (renamed in 8.2 Task 2.1). If the entity still has `GcalEventId`, complete the rename here.

### Lookup pattern: `gcal_event_id` is nullable UNIQUE

The `event` table has a partial unique index on `gcal_event_id WHERE gcal_event_id IS NOT NULL`. Local-only events (`publish='local_only'`) have `gcal_event_id = NULL`. The sync loop only touches published events (GCal returns events that have a gcal id), so the `SingleOrDefaultAsync` lookup is safe:

```csharp
var existingEvent = await context.Events
    .SingleOrDefaultAsync(e => e.GcalEventId == incomingEvent.GcalEventId, CancellationToken.None);
```

This will never accidentally match a local-only event (its `gcal_event_id` is null; `null != "event-123"` in SQL).

### Ownership metadata fields

`GcalEvent` had `AppCreated`, `AppPublished`, `AppPublishedAt`, `SourceSystem` which were preserved by the sync reconciler (not overwritten on update/delete). Verify that the unified `Event` entity has these same fields (migrated from `gcal_event` in 8.2). If they exist, preserve them identically in the rewritten helpers:

- `ApplyIncomingValues` must NOT touch `AppCreated`, `AppPublished`, `AppPublishedAt`, `SourceSystem`, `EventId`
- `ApplyDeletedValues` must NOT touch the same fields

### `is_deleted` field on `Event`

`GcalEvent` had `IsDeleted`. Confirm `Event` entity has the same field (it should — migrated in 8.2). The delete branch sets `existingEvent.IsDeleted = true` (same as before, soft delete behavior preserved). Story 8.6 will repoint `deleted_event` and recurring references; do NOT try to do that here.

### `operation_type='delete'` rows from old `pending_event`

The old `pending_event` had `operation_type='delete'` rows (user staged a GCal event for deletion). These were merged into `event` rows with `has_unpublished_changes=true` during the 8.2 migration. When the GCal sync receives an update for such an event, the `has_unpublished_changes=true` guard will kick in and preserve the pending-delete state. This is correct behavior — the delete is still pending; it should not be silently overwritten by a GCal pull.

### Test rewrite: seed data shape

When rewriting `GoogleCalendarSyncTests`, the seed `Event` objects require additional fields versus the old `GcalEvent` seeds:

```csharp
// Old GcalEvent seed:
new GcalEvent { GcalEventId = "event-1", CalendarId = "primary", Summary = "...", ... }

// New Event seed:
new Event
{
    EventId = Guid.NewGuid().ToString("N"),   // REQUIRED — stable local PK
    GcalEventId = "event-1",                  // GCal id (nullable unique)
    CalendarId = "primary",
    Summary = "...",
    Lifecycle = "approved",                   // REQUIRED
    Publish = "published",                    // REQUIRED
    HasUnpublishedChanges = false,            // REQUIRED
    // ... rest of fields unchanged
}
```

The `_connection`/`TestDbContextFactory` pattern stays the same — in-memory SQLite + `context.Database.EnsureCreated()`.

### `FetchAllEventsAsync` signature

The existing `IGoogleCalendarService.FetchAllEventsAsync` signature in the tests uses `IProgress<int>?` (the old signature). Check the current interface signature in `Services/IGoogleCalendarService.cs` — if it changed in a prior story, update the mock setup accordingly. Do not change the interface itself in this story.

### What this story does NOT do

- Does NOT change `CalendarQueryService` further (8.3 owns the query rewrite)
- Does NOT repoint `deleted_event`, `gcal_event_version` by recurring series references — Story 8.6
- Does NOT change `CalendarEventSourceKind` enum or rendering — Story 8.5
- Does NOT create `data_point` table or any Phase 1 work — Story 8.7+
- Does NOT rename `linked_event_id` columns to `data_point_id` — Story 8.7+
- Does NOT rewrite `PendingEventPublishService` further — already done in 8.3

### Project structure

- Modified: `Services/SyncManager.cs` — full rewrite of entity helpers + reconciliation loop
- Deleted: `Services/IGcalEventRepository.cs`, `Services/GcalEventRepository.cs`
- Updated: `App.xaml.cs` — add `IEventIdentityService` DI, remove `IGcalEventRepository` DI
- Updated: `Data/Entities/GcalEventVersion.cs` — `EventId` FK (if not done in 8.2)
- Updated: `Data/Configurations/GcalEventVersionConfiguration.cs` — FK to `event.event_id` (if not done in 8.2)
- Rewritten: `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs` — all tests updated to `Event`
- All C# files: `namespace GoogleCalendarManagement.Services;` (or `.Tests.Integration`)

### Testing framework

xUnit + FluentAssertions + Moq. Integration tests use in-memory SQLite (same as `PendingEventRepositoryTests.cs`):
```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
_contextFactory = new TestDbContextFactory(options);
```

The `TestDbContextFactory` and `LockingDbContextFactory` helper classes in `GoogleCalendarSyncTests.cs` are private nested classes — update them in place, no shared test infrastructure needed.

### References

- Canonical event model: [concepts.md §3](../concepts.md)
- Epic overview story 8.4 spec: [epic-overview.md §Phase 0 Story 8.4](../epic-overview.md)
- Story 8.2 (migration): [8-2-unified-event-table-and-migration.md](./8-2-unified-event-table-and-migration.md)
- Story 8.3 (repository + identity service): [8-3-event-repository-and-identity-service.md](./8-3-event-repository-and-identity-service.md)
- Existing sync implementation to rewrite: `Services/SyncManager.cs`
- Existing sync tests to rewrite: `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs`
- Repository pattern reference: `Services/EventRepository.cs` (from 8.3)
- `GcalEventVersion` entity: `Data/Entities/GcalEventVersion.cs`
- DI registration: `App.xaml.cs` (~lines 268–310)

---

## Dev Agent Record

### Agent Model Used

Opus

### Debug Log References

### Completion Notes List

### File List

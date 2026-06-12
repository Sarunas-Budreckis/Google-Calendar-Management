# Story 8.4: Migrate Sync Reconciler to Unified Event Table

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
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

- [x] Task 1: Rewrite `SyncManager` entity helpers to use `Event` (AC: #1, #8, #9)
  - [x] 1.1 Delete `CreateEntity(GcalEventDto, DateTime) → GcalEvent` and replace with `CreateEventEntity(GcalEventDto, string eventId, DateTime) → Event` that mints using the passed `eventId`, sets `lifecycle='approved'`, `publish='published'`, `has_unpublished_changes=false`, and maps all GCal fields
  - [x] 1.2 Delete `CreateVersionSnapshot(GcalEvent, string, DateTime) → GcalEventVersion` and replace with `CreateVersionSnapshot(Event, string, DateTime) → GcalEventVersion` — write `EventId = existingEvent.EventId` (not `GcalEventId`); copy all snapshot fields from `Event` equivalents
  - [x] 1.3 Rewrite `ShouldSnapshotForUpdate` to compare `Event` fields vs `GcalEventDto` (same field comparisons as before, just different source type)
  - [x] 1.4 Rewrite `ApplyIncomingValues(Event, GcalEventDto, DateTime)`: update GCal fields unconditionally only when `has_unpublished_changes=false`; when true, update only `GcalEtag`, `GcalUpdatedAt`, `LastSyncedAt`, `UpdatedAt` — **never touch** summary, description, start/end, color, is_all_day
  - [x] 1.5 Rewrite `ApplyDeletedValues(Event, GcalEventDto, DateTime)`: mark deleted; log warning if `has_unpublished_changes=true` before applying
  - [x] 1.6 Preserve `ResolveIncomingColorId` helper unchanged (logic is the same)

- [x] Task 2: Rewrite `SyncAsync` reconciliation loop (AC: #1, #2, #3, #4, #5, #6, #7)
  - [x] 2.1 Replace `context.GcalEvents.FindAsync([incomingEvent.GcalEventId])` with `context.Events.SingleOrDefaultAsync(e => e.GcalEventId == incomingEvent.GcalEventId, CancellationToken.None)` — note: lookup is by `gcal_event_id` (nullable), not PK
  - [x] 2.2 For the "new event" branch: call `IEventIdentityService.MintEventId()` and pass result to `CreateEventEntity`; add to `context.Events`
  - [x] 2.3 For the "existing event / update" branch: call rewritten `ApplyIncomingValues` which respects `HasUnpublishedChanges`
  - [x] 2.4 For the "existing event / delete" branch: call rewritten `ApplyDeletedValues`; snapshot before delete as before
  - [x] 2.5 Inject `IEventIdentityService` into `SyncManager` constructor; update DI registration in `App.xaml.cs`
  - [x] 2.6 Remove all `GcalEvent`-typed local variables and casts from `SyncAsync`

- [x] Task 3: Remove `IGcalEventRepository` / `GcalEventRepository` (AC: #10) — **see Deviation 2**
  - [x] 3.1 Delete `Services/IGcalEventRepository.cs`
  - [x] 3.2 Delete `Services/GcalEventRepository.cs`
  - [x] 3.3 Search for any remaining callers of `IGcalEventRepository` — migrated all 4 production consumers (`IcsExportService`, `IcsExporter`, `DataSourcePanelViewModel`, `EventDetailsPanelViewModel`, `TogglSleepDrilldownViewModel`) to `IEventRepository`
  - [x] 3.4 Remove `IGcalEventRepository` DI registration from `App.xaml.cs`

- [x] Task 4: Update `GcalEventVersion` entity to use `EventId` FK (AC: #8) — **no-op (done in 8.2)**
  - [x] 4.1 Verified `GcalEventVersion.EventId` is the FK (renamed in 8.2). No change needed.
  - [x] 4.2 N/A — rename already complete in 8.2.
  - [x] 4.3 Test helpers updated to assert `version.EventId`.

- [x] Task 5: Rewrite `GoogleCalendarSyncTests` (AC: #11, #13)
  - [x] 5.1 Replace all `context.GcalEvents` references with `context.Events`
  - [x] 5.2 Replace all `GcalEvent { ... }` seed objects with `Event { ... }` seed objects (deterministic `EventId`, `Lifecycle="approved"`, `Publish="published"`, `HasUnpublishedChanges=false`). NOTE: `Event` does **not** carry `AppCreated`/`AppPublished` (those were not migrated in 8.2); ownership assertions now check `EventId`/`SourceSystem`/`Lifecycle`/`Publish` preservation instead.
  - [x] 5.3 Version assertions now check `version.EventId`
  - [x] 5.4 Verified `LockingCalendarDbContext` still works (no `GcalEvent` references inside it)
  - [x] 5.5 Added `SyncAsync_UpdatedEvent_WithLocalUnpublishedEdits_DoesNotClobberLocalFields`
  - [x] 5.6 Added `SyncAsync_DeletedEvent_WithLocalUnpublishedEdits_AppliesDelete`

- [x] Task 6: Update DI and compilation (AC: #12)
  - [x] 6.1 `IEventIdentityService` already registered (8.3); removed `IGcalEventRepository` registration
  - [x] 6.2 `SyncManager` constructor compiles with `IEventIdentityService` injected
  - [x] 6.3 Build clean; no remaining `GcalEvent`-shaped compilation errors

### Review Findings

- [x] [Review][Patch] Rewritten sync tests are still excluded from the test assembly [GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj:41]
- [x] [Review][Patch] Deleted unified events can still render in calendar queries [Services/CalendarQueryService.cs:40]

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

Opus (claude-opus-4-8)

### Debug Log References

- `dotnet build` — succeeded (main project and full solution).
- `dotnet test` — **455 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations add AddEventIsDeleted` — generated `event.is_deleted` column migration.

### Completion Notes List

The sync reconciler was fully reintroduced against the unified `event` table. Two spec/codebase conflicts were surfaced and resolved with the user before implementation:

**Deviation 1 — `is_deleted` column (AC #6, #7, Dev Notes "is_deleted field on Event").**
The story assumed `Event.IsDeleted` was migrated in 8.2. It was not — 8.2 deliberately deferred delete-modeling to Story 8.6 (see the `UnifyEventTable` migration comment, Step B). The unified `event` table had no delete representation and the `lifecycle` CHECK constraint only allows `candidate`/`approved`. **User decision: add an `is_deleted` column now.** Implemented via `Event.IsDeleted` + `EventConfiguration` (`is_deleted` column, default false) + migration `AddEventIsDeleted`. Story 8.6 will relocate these rows into `deleted_event`.

**Deviation 2 — deleting `IGcalEventRepository` (AC #10, Task 3).**
AC #10 claimed the repository had "no remaining callers." In fact it had 4 production consumers (`IcsExportService`/`IcsExporter`, `DataSourcePanelViewModel`, `EventDetailsPanelViewModel`, `TogglSleepDrilldownViewModel`) plus 4 test files — and the test project did **not compile** on this branch because those tests (and the old sync tests) still referenced the removed `context.GcalEvents` DbSet. **User decision: delete now and migrate all consumers to `IEventRepository`.** Done:
- `IEventRepository`/`EventRepository` gained `GetStoredDateRangeAsync` (ported from the pre-8.2 `GcalEventRepository`).
- `IcsExporter.GenerateIcs` now operates on `Event`; `IcsExportService.IntersectsRange` skips deleted **and** local-only rows (null `gcal_event_id`) so export stays limited to published GCal events (preserves old behavior + avoids the "cannot export without gcal_event_id" throw).
- The three ViewModels now take a required `IEventRepository` (the previously optional 8.3 field is now mandatory; redundant null-guards removed). All fields they read (`CalendarId`, `IsAllDay`, `SourceSystem`, `Summary`, …) exist on `Event`, so the swap was mechanical; the `PendingEvent` write-paths were intentionally left untouched (Story 8.5 scope).
- `IGcalEventRepository`/`GcalEventRepository` deleted; DI registration removed.

**Other notes:**
- `ApplyIncomingValues` now always refreshes sync metadata (`GcalEtag`, `GcalUpdatedAt`, `LastSyncedAt`, `UpdatedAt`) and, when `HasUnpublishedChanges=true`, returns early without touching user-facing fields (no snapshot, not counted as updated) — the core "don't clobber local edits" guard.
- `GcalEventVersion.EventId` FK was already in place from 8.2, so Task 4 was a no-op.
- The `GcalEvent` entity class file (`Data/Entities/GcalEvent.cs`) is now unreferenced by live code but left in place for 8.6/8.16 cleanup (out of scope here).
- `IcsExporterTests` (unit) also seeded `GcalEvent` and was migrated to `Event`.

### File List

**Production (modified):**
- `Data/Entities/Event.cs` — added `IsDeleted`
- `Data/Configurations/EventConfiguration.cs` — mapped `is_deleted` column
- `Services/SyncManager.cs` — full reconciler reintroduction over `event` + `IEventIdentityService` injection
- `Services/IEventRepository.cs` — added `GetStoredDateRangeAsync`
- `Services/EventRepository.cs` — implemented `GetStoredDateRangeAsync` + date helpers
- `Services/IcsExporter.cs` — `GcalEvent` → `Event`
- `Services/IcsExportService.cs` — `IEventRepository`; intersection skips deleted/local-only
- `ViewModels/DataSourcePanelViewModel.cs` — required `IEventRepository`, removed gcal repo
- `ViewModels/EventDetailsPanelViewModel.cs` — `IEventRepository`
- `ViewModels/TogglSleepDrilldownViewModel.cs` — required `IEventRepository`, removed gcal repo
- `App.xaml.cs` — removed `IGcalEventRepository` registration

**Production (added):**
- `Data/Migrations/20260612150857_AddEventIsDeleted.cs` (+ `.Designer.cs`, snapshot updated)

**Production (deleted):**
- `Services/IGcalEventRepository.cs`
- `Services/GcalEventRepository.cs`

**Tests (modified):**
- `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs` — full rewrite to `Event` + 2 new guard tests
- `GoogleCalendarManagement.Tests/Integration/IcsExportServiceTests.cs` — `Event` seeds + `EventRepository`
- `GoogleCalendarManagement.Tests/Unit/Services/IcsExporterTests.cs` — `Event` seeds
- `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` — `StubEventRepository`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs` — `Mock<IEventRepository>`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/TogglSleepDrilldownViewModelTests.cs` — `StubEventRepository`

### Change Log

- 2026-06-12: Implemented Story 8.4 — reintroduced GCal sync reconciler against the unified `event` table with the `has_unpublished_changes` guard; added `event.is_deleted` (Deviation 1); deleted `IGcalEventRepository`/`GcalEventRepository` and migrated all consumers to `IEventRepository` (Deviation 2). All 455 tests pass.

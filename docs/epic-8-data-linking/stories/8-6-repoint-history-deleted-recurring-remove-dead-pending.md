# Story 8.6: Repoint History / Deleted / Recurring; Remove Dead Pending Code

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Opus · **Effort:** medium
**Dependencies:** 8.4 (blocking — sync reconciler uses `event` table), 8.5 (blocking — rendering + drilldowns use `event` table; `IPendingEventRepository` removed from most consumers)

---

## Story

As a developer completing the Epic 8 Phase 0 cleanup,
I want to ensure `gcal_event_version`, `deleted_event`, and recurring-series references all resolve via `event_id`, and to delete every remaining `PendingEvent*` code path that became dead after 8.2–8.5,
so that the entire codebase uses only the stable `event_id` for history, deletion records, and recurring linkage, with no trace of the old `pending_event` table or its service layer remaining.

---

## Acceptance Criteria

1. **Version history queryable by `event_id`:** `gcal_event_version.event_id` (FK → `event.event_id`) is the definitive FK; `GcalEventVersion.EventId` is the C# property used by all callers. Any remaining `GcalEventId` property on `GcalEventVersion` is removed; the index `idx_version_event` covers `(event_id, created_at)`.
2. **Delete flow intact:** `deleted_event` has an `event_id` column (string, nullable initially, then non-null for new rows); deletion paths write `event_id`; the delete flow produces correct snapshots with the stable `event_id` preserved. `gcal_event_id` is retained on `deleted_event` as a read-only audit column — it is **not** removed.
3. **Recurring instances resolve:** `event.recurring_event_id` (GCal series string FK → `recurring_event_series.series_id`) is intact; an index exists on `event.recurring_event_id`. No code tries to repoint this to a local `event_id` — the GCal-id-based relationship is correct and deliberate.
4. **No `pending_event` references remain:** The `PendingEvent` entity class, `PendingEventConfiguration`, the `PendingEvents` DbSet, `PendingEventRepository`/`IPendingEventRepository`, and `PendingEventPublishService`/`IPendingEventPublishService` are all deleted. Their DI registrations and test classes are also deleted.
5. **`PendingEventDraftService` survives but is clean:** `PendingEventDraftService` and `IPendingEventDraftService` are kept (they now create `Event` rows and have no `PendingEvent` entity imports). The name is not changed in this story (cosmetic rename deferred to 8.16).
6. `CalendarQueryService` queries only `context.Events`; no join to `context.PendingEvents`.
7. The app compiles and the full test suite passes.

---

## Tasks / Subtasks

- [x] Task 1: Finalize `GcalEventVersion.EventId` FK (AC: #1)
  - [x] 1.1 Search `Data/Entities/GcalEventVersion.cs` for `GcalEventId` property — if it still exists, rename it to `EventId`; update `GcalEventVersionConfiguration.cs` FK to `HasForeignKey(v => v.EventId)` pointing at `event.event_id`
  - [x] 1.2 Search for any caller of `version.GcalEventId` (services, tests) and update to `version.EventId`
  - [x] 1.3 Verify or create migration to rename `gcal_event_id` column → `event_id` on `gcal_event_version` table and update the index `idx_version_event` to `(event_id, created_at)` — **only needed if 8.2/8.4 left the column rename to this story**; check migration history first to avoid duplicate migration

- [x] Task 2: Add `event_id` to `deleted_event` via EF migration (AC: #2)
  - [x] 2.1 Add nullable `string? EventId` property to `Data/Entities/DeletedEvent.cs`; configure `deleted_event.event_id` column in `DeletedEventConfiguration.cs` (no FK constraint — `deleted_event` is a tombstone table; cascades are undesirable)
  - [x] 2.2 Add EF migration: `ALTER TABLE deleted_event ADD COLUMN event_id TEXT`; backfill: `UPDATE deleted_event SET event_id = (SELECT event_id FROM event WHERE gcal_event_id = deleted_event.gcal_event_id)` — rows where the event no longer exists (already cascaded) will remain NULL; that is acceptable
  - [x] 2.3 Do **not** remove `gcal_event_id` from `deleted_event` — it is the GCal audit field, not redundant

- [x] Task 3: Update delete-flow write paths to set `event_id` (AC: #2)
  - [x] 3.1 Search for all code that writes to `deleted_event` / `context.DeletedEvents` (post-8.4 this is inside the `EventRepository` or a deletion helper written in 8.3/8.4 — verify the exact callsite)
  - [x] 3.2 Where a `DeletedEvent` row is constructed, set `EventId = event.EventId` (use the stable `event_id` from the `event` row being deleted)
  - [x] 3.3 Verify `DeletedAt` and `DeletionSource` fields are still populated as before (no behavioral regression)

- [x] Task 4: Verify recurring-series index and FK integrity (AC: #3)
  - [x] 4.1 Confirm `event.recurring_event_id` column exists (migrated from `gcal_event` in 8.2); confirm `RecurringEventSeries.SeriesId` is still the GCal-series-id PK
  - [x] 4.2 Confirm index `idx_gcal_recurring` (or equivalent) exists on `event.recurring_event_id` — add migration to create it if missing
  - [x] 4.3 **Do NOT** attempt to repoint `recurring_event_id` to local `event_id` — this FK is intentionally GCal-id-based; see Dev Notes §Recurring

- [x] Task 5: Search and remove all remaining `PendingEvent` entity usages (AC: #4)
  - [x] 5.1 Run: `grep -rn "PendingEvent\b\|pending_event\|IPendingEventRepository\|IPendingEventPublishService" --include="*.cs"` — collect every hit outside of `PendingEventDraftService.cs` and `IPendingEventDraftService.cs` (those survive)
  - [x] 5.2 For each hit: confirm it is truly dead (the operation is now handled by `IEventRepository` or another 8.3–8.5 service), then remove it

- [x] Task 6: Delete `PendingEventRepository` (AC: #4)
  - [x] 6.1 Delete `Services/PendingEventRepository.cs`
  - [x] 6.2 Delete `Services/IPendingEventRepository.cs`

- [x] Task 7: Delete `PendingEventPublishService` (AC: #4)
  - [x] 7.1 Delete `Services/PendingEventPublishService.cs`
  - [x] 7.2 Delete `Services/IPendingEventPublishService.cs`
  - [x] 7.3 If `PendingPublishItemViewModel.cs` depended exclusively on `IPendingEventPublishService`, delete it too; otherwise update it to use the new publish path from 8.3

- [x] Task 8: Delete `PendingEvent` entity and configuration (AC: #4)
  - [x] 8.1 Delete `Data/Entities/PendingEvent.cs`
  - [x] 8.2 Delete `Data/Configurations/PendingEventConfiguration.cs`
  - [x] 8.3 Remove `public DbSet<PendingEvent> PendingEvents { get; set; }` from `Data/CalendarDbContext.cs`

- [x] Task 9: Remove DI registrations (AC: #4)
  - [x] 9.1 In `App.xaml.cs`: remove `services.AddSingleton<IPendingEventRepository, PendingEventRepository>()`
  - [x] 9.2 In `App.xaml.cs`: remove `services.AddSingleton<IPendingEventPublishService, PendingEventPublishService>()`
  - [x] 9.3 Verify `IPendingEventDraftService` / `PendingEventDraftService` registration is **kept** (this service survived 8.5)

- [x] Task 10: Delete `PendingEvent` test classes (AC: #4, #7)
  - [x] 10.1 Delete `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs` (or its 8.5-successor name) — this file previously tested the old `PendingEvent` table CRUD and the old publish flow
  - [x] 10.2 Delete `GoogleCalendarManagement.Tests/Integration/PendingEventPublishServiceTests.cs`
  - [x] 10.3 Verify `PendingEventDraftServiceTests.cs` is **kept** (it was updated in 8.5 to test `CreateCandidateAsync` via `IEventRepository` — it is still valid)

- [x] Task 11: Compile and test (AC: #7)
  - [x] 11.1 `dotnet build` — fix every compile error; use the build error list as the complete checklist of remaining `PendingEvent` references
  - [x] 11.2 `dotnet test` — all tests must pass; a test failure here means a live code path was removed that should have been kept or migrated

---

## Dev Notes

### What prior stories left for 8.6

By the time this story starts:

- **8.2** created the unified `event` table, merged `gcal_event` + `pending_event` into it, and dropped both source tables. `GcalEventVersion.EventId` may have been renamed from `GcalEventId` in this migration, or it was marked TODO for 8.4.
- **8.3** created `IEventRepository` + `IEventIdentityService`; all event CRUD now goes through `event_id`. Rewrote the publish logic (previously in `PendingEventPublishService`) to operate on `Event` entities.
- **8.4** rewrote `SyncManager` to use `context.Events`; deleted `IGcalEventRepository` / `GcalEventRepository`; confirmed or completed `GcalEventVersion.EventId` rename (Task 4 — "only if not done in 8.2").
- **8.5** rewrote `CalendarQueryService` to read only from `context.Events`; removed `IPendingEventRepository` injection from `EventDetailsPanelViewModel` and most drilldown VMs; updated `PendingEventDraftService` to use `IEventRepository` (return type changed from `PendingEvent` to `Event`).

**What 8.6 is NOT responsible for:**
- Creating any new functionality — this is cleanup only.
- Renaming service/interface names that still contain "Pending" (`IPendingEventDraftService` → `IEventDraftService`) — that is 8.16's job.
- Adding `data_point` table or any Phase 1 work — that starts at 8.7.
- Creating any formal `link` rows for `TogglEntry.PublishedGcalEventId` / `TogglEntry.LinkedEventId` — those become `link` table rows in 8.12; for now the column rename to `PublishedEventId` (if needed after 8.2 dropped `gcal_event`) may be done here if the FK is broken, or deferred to 8.7 if it's tolerated as a nullable orphan.

### `GcalEventVersion.EventId` — may already be done

Task 4 of story 8.4 says "Update `GcalEventVersion` entity to use `EventId` FK — only if not done in 8.2." When you pick up 8.6:

1. Open `Data/Entities/GcalEventVersion.cs` — if the property is already `EventId` and the configuration already references `event.event_id`, Task 1 is a no-op.
2. If `GcalEventId` is still present, complete the rename now and create the EF migration (Task 1.3).

Do not create a migration that was already created. Check `Data/Migrations/` history first.

### `deleted_event` — tombstone semantics

`deleted_event` is a tombstone table: when an event is hard-deleted (or a GCal delete is received), a snapshot row is written. It is **not** a FK-constrained child of `event` — the `event` row itself may be gone by the time you query `deleted_event`. Therefore:

- `event_id` column has **no FK constraint** (do not add `HasForeignKey`).
- `gcal_event_id` is kept as-is — it was always denormalized for GCal audit purposes.
- New rows written after this story should set both `EventId` (the stable id) and `GcalEventId` (if the event had one).

### Recurring events — GCal-id FK is intentional

`event.recurring_event_id` holds the GCal id of the recurring series template (e.g. `"_6t1jce1m6os3ib9o6krjib9k6..."`) — not a local `event_id`. This is the same value Google Calendar uses to group instances. The FK `event.recurring_event_id → recurring_event_series.series_id` is correct and intentional.

Do **not** try to repoint this to a local `event_id`. The open design question ("recurring instances link independently or as a unit") is deferred per concepts.md §10 item 6 — it will be addressed when Story 4.9 (recurring event editing) is tackled.

### `PendingEventPublishService` was rewritten in 8.3

Story 8.3 rewrote the publish logic to operate on `Event` objects (publish fills `gcal_event_id`, sets `publish='published'`). By 8.6, `PendingEventPublishService` may:

- **Still contain `PendingEvent` references** if 8.3 only partially migrated it — delete those and the class if the publish path is fully inside `EventRepository` or another service.
- **Be completely rewritten** by 8.3 to only use `Event` — in that case, delete the class if it is now an empty wrapper.

The litmus test: does `PendingEventPublishService` still import `using ... .Entities.PendingEvent` or query `context.PendingEvents`? If yes, it is still dead code from the old model and must be cleaned up. If it was made clean in 8.3, just delete the file (its logic lives in `EventRepository` now).

### `TogglEntry.PublishedGcalEventId` — check FK integrity

`TogglEntry` has a `PublishedGcalEventId` column (FK → old `gcal_event.gcal_event_id`). After 8.2 dropped `gcal_event`, this FK is broken at the schema level. Depending on how 8.2 handled it:

- If 8.2 migration dropped the FK constraint but kept the column: rename the column to `published_event_id` in this story (since `event.event_id` is now the stable key), add a soft FK note in the configuration, and backfill `published_event_id = (SELECT event_id FROM event WHERE gcal_event_id = toggl_entry.published_gcal_event_id)`.
- If it was already renamed in 8.2 or 8.3: no action needed here.
- If neither: include a migration in this story.

This column will eventually be superseded by the formal `link` table (Epic 8.12), but for now it serves as a direct reference for Toggl drill-down "which event did I approve for this entry?"

### Test deletion scope

Delete the following test files **completely** — they tested the old `pending_event` table CRUD and the old publish pipeline:
- `PendingEventRepositoryTests.cs`
- `PendingEventPublishServiceTests.cs`

Keep:
- `PendingEventDraftServiceTests.cs` — this was updated in 8.5 to test `CreateDraftAsync` (→ `Event` with `lifecycle=approved`) and `CreateCandidateAsync`; it is still valid and live.

### Build errors as your checklist

After deleting the entity and configuration files in Tasks 6–8, run `dotnet build`. Every compile error is a caller that was missed in Task 5. Work through the full error list before running tests.

### Project structure notes

**Files confirmed deleted:**
- `Data/Entities/PendingEvent.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Services/PendingEventRepository.cs`, `Services/IPendingEventRepository.cs`
- `Services/PendingEventPublishService.cs`, `Services/IPendingEventPublishService.cs`
- `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs`
- `GoogleCalendarManagement.Tests/Integration/PendingEventPublishServiceTests.cs`

**Files that may be deleted (check first):**
- `ViewModels/PendingPublishItemViewModel.cs` — if it exclusively wraps `IPendingEventPublishService`; if it was updated to use the new publish path in 8.3, keep it

**Files modified:**
- `Data/CalendarDbContext.cs` — remove `DbSet<PendingEvent>`
- `App.xaml.cs` — remove DI registrations for `IPendingEventRepository`, `IPendingEventPublishService`
- `Data/Entities/GcalEventVersion.cs` — rename `GcalEventId` → `EventId` if not already done
- `Data/Configurations/GcalEventVersionConfiguration.cs` — update FK if not already done
- `Data/Entities/DeletedEvent.cs` — add `EventId` property
- `Data/Configurations/DeletedEventConfiguration.cs` — add `event_id` column config
- New migration in `Data/Migrations/` — covers: `deleted_event.event_id` column, any remaining `gcal_event_version` column rename, `event.recurring_event_id` index (if missing), optional `toggl_entry` column rename

### References

- Canonical event model: [docs/epic-8-data-linking/concepts.md §3](../concepts.md)
- Recurring / open decisions: [concepts.md §10](../concepts.md) item 6
- Epic 8 overview — Story 8.6 spec: [epic-overview.md §Phase 0 Story 8.6](../epic-overview.md)
- Prior story that confirmed `GcalEventVersion.EventId` may or may not be done: [8-4-migrate-sync-reconciler-to-unified-event-table.md](./8-4-migrate-sync-reconciler-to-unified-event-table.md) Task 4
- Current `GcalEventVersion` entity: `Data/Entities/GcalEventVersion.cs`
- Current `DeletedEvent` entity: `Data/Entities/DeletedEvent.cs`
- Current `PendingEvent` entity (to be deleted): `Data/Entities/PendingEvent.cs`
- `CalendarDbContext`: `Data/CalendarDbContext.cs`
- DI registrations: `App.xaml.cs` (search for `IPendingEvent`)
- `TogglEntryConfiguration.cs:58–61` — `PublishedGcalEventId` FK definition

---

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- 2026-06-12T12:21:48-05:00 — Started story 8.6; confirmed `GcalEventVersion.EventId`, `idx_version_event`, and `event.recurring_event_id` index were already in place.
- 2026-06-12T12:36:39-05:00 — Verified cleanup with `rg` for deleted pending-event symbols; `dotnet build -p:Platform=x64 -p:WarningsNotAsErrors=NU1900`; `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`.

### Completion Notes List

- Added `DeletedEvent.EventId`, EF configuration, model snapshot, and migration `20260612172148_AddDeletedEventEventId` with nullable backfill from `event.gcal_event_id`.
- Updated sync remote-delete and repository hard-delete paths to write `deleted_event` tombstones with stable `event_id` while retaining `gcal_event_id` as the audit key.
- Removed the dead `PendingEvent` entity shim, pending repository, pending publish service/interface, and old pending repository/publish integration tests.
- Replaced the old pending publish service with `IEventPublishService` / `EventPublishService` backed by the unified `event` table.
- Removed stale `IPendingEventRepository` injections from data-source card providers and drilldown/compact viewmodels; updated sleep quality title edits to modify unified `Event` rows.
- Updated affected unit/integration tests for unified `Event` behavior and tombstone `event_id` assertions.

### File List

- App.xaml.cs
- Data/Configurations/DeletedEventConfiguration.cs
- Data/Entities/DeletedEvent.cs
- Data/Entities/PendingEvent.cs (deleted)
- Data/Migrations/20260612172148_AddDeletedEventEventId.cs
- Data/Migrations/CalendarDbContextModelSnapshot.cs
- GoogleCalendarManagement.Tests/Integration/EventRepositoryTests.cs
- GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs
- GoogleCalendarManagement.Tests/Integration/PendingEventPublishServiceTests.cs (deleted)
- GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs (deleted)
- GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/TogglSleepDrilldownViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/TogglTransitDrilldownViewModelTests.cs
- Services/CallLogCardProvider.cs
- Services/EventPublishService.cs
- Services/EventRepository.cs
- Services/IEventPublishService.cs
- Services/IPendingEventPublishService.cs (deleted)
- Services/IPendingEventRepository.cs (deleted)
- Services/PendingEventPublishService.cs (deleted)
- Services/PendingEventRepository.cs (deleted)
- Services/SyncManager.cs
- Services/TogglSleepCardProvider.cs
- Services/TogglTransitCardProvider.cs
- ViewModels/Civ5DrilldownViewModel.cs
- ViewModels/ComfyUIDrilldownViewModel.cs
- ViewModels/DataSourcePanelViewModel.cs
- ViewModels/MainViewModel.cs
- ViewModels/TogglPhoneDrilldownViewModel.cs
- ViewModels/TogglSleepCompactCardViewModel.cs
- ViewModels/TogglSleepDrilldownViewModel.cs
- ViewModels/TogglTransitDrilldownViewModel.cs

### Change Log

- 2026-06-12 — Completed Story 8.6 cleanup: tombstone `event_id`, unified publish service naming, deleted dead pending entity/repository/publish code, and updated tests.

### Review Findings

- [x] [Review][Decision] Candidate events in publish queue — intentional; candidates appear alongside drafts in the publish panel. A comment added noting candidates may eventually get an "approve and publish" action. [`Services/EventPublishService.cs`]

- [x] [Review][Patch] `RevertAsync` does not restore field values — fixed: calls `_eventRepository.RevertToLastSyncedAsync` for published events with edits; removes local-only draft on revert. [`Services/EventPublishService.cs`]

- [x] [Review][Patch] `deleted_event` duplicate PK risk — dismissed (false positive): `SyncManager` already guards with `!existingEvent.IsDeleted` before `DeletedEvents.Add`; `DeleteByEventIdAsync` guards via null check on second call.

- [x] [Review][Patch] `ApplyPublishedState` uses `DateTime.UtcNow` instead of `_timeProvider` — fixed: method made non-static; uses `_timeProvider.GetUtcNow().UtcDateTime` for `LastSyncedAt`. [`Services/EventPublishService.cs`]

- [x] [Review][Patch] `PublishAsync` does not deduplicate input — fixed: `.Distinct().ToList()` applied at entry; progress and batch counts use the deduped list. [`Services/EventPublishService.cs`]

- [x] [Review][Patch] `RevertAsync` spurious write on already-clean published events — fixed: `else if (ev.HasUnpublishedChanges)` guard added; no-op when flag is already false. [`Services/EventPublishService.cs`]

- [x] [Review][Patch] `UpdateColorAsync` no null/empty `colorKey` guard — fixed: `if (string.IsNullOrWhiteSpace(colorKey)) return;` added. [`Services/EventPublishService.cs`]

- [x] [Review][Defer] `RevertToLastSyncedAsync` picks highest `VersionId` including "deleted" snapshots [`Services/EventRepository.cs`] — deferred, pre-existing issue from 8.3/8.4; should filter `ChangeReason != "deleted"` when selecting the last version

- [x] [Review][Defer] Backfill migration leaves `event_id` NULL for tombstones of pre-existing hard-deleted events [`Data/Migrations/20260612172148_AddDeletedEventEventId.cs`] — deferred, column is nullable by design; only affects historical databases with pre-migration hard deletes

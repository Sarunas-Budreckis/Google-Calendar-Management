# Story 2.3: Implement Version History on Calendar Sync

Status: ready-for-dev

## Story

As a **user**,
I want **Google Calendar re-syncs to overwrite my local mirror while preserving the prior state of changed events**,
so that **the app stays aligned with Google Calendar without losing historical context for updates and deletions**.

## Acceptance Criteria

1. **AC-2.3.1 - Updated Events Snapshot Before Overwrite:** Given an existing `GcalEvent` row is changed in Google Calendar, when sync processes the newer Google payload, the current local row is first copied into `gcal_event_version` with `ChangedBy = "gcal_sync"` and `ChangeReason = "updated"` before the local row is overwritten.

2. **AC-2.3.2 - New Events Do Not Create History:** Given a Google Calendar event does not exist locally yet, sync inserts it into `gcal_event` without creating a `gcal_event_version` row.

3. **AC-2.3.3 - Deleted Events Snapshot Before Soft Delete:** Given an existing local row is returned from Google as deleted or cancelled, sync writes a snapshot to `gcal_event_version` with `ChangedBy = "gcal_sync"` and `ChangeReason = "deleted"` before setting `GcalEvent.IsDeleted = true`.

4. **AC-2.3.4 - Unchanged Events Do Not Spam History:** Given sync sees an existing event whose Google-facing fields have not materially changed, no new `gcal_event_version` row is created. Updating bookkeeping fields such as `LastSyncedAt` alone must not generate version history.

5. **AC-2.3.5 - History Remains Retained And Queryable:** Given snapshots exist in `gcal_event_version`, they remain insert-only, ordered by `CreatedAt` / `VersionId`, and are never automatically deleted or rewritten by sync operations.

## Tasks / Subtasks

- [ ] **Task 1: Reuse the Story 2.2 sync path instead of adding a second implementation** (AC: 2.3.1, 2.3.2, 2.3.3, 2.3.4)
  - [ ] Confirm Story 2.2's `SyncManager`, `SyncResult`, `SyncProgress`, and `GcalEventDto` work is present on the branch before starting Story 2.3
  - [ ] Extend the existing update and delete branches in `SyncManager`; do not move version-history logic into `SettingsViewModel`, `SettingsPage`, or a second manual sync flow
  - [ ] Keep `GoogleCalendarService` responsible for Google API calls only; history writes belong in the local persistence/orchestration layer

- [ ] **Task 2: Implement snapshot creation with the existing `GcalEventVersion` schema** (AC: 2.3.1, 2.3.3, 2.3.5)
  - [ ] Add a private helper in `SyncManager` or a small dedicated service that maps a current `GcalEvent` row to a new `GcalEventVersion`
  - [ ] Populate the snapshot from the existing schema only:
    - `GcalEventId`, `GcalEtag`, `Summary`, `Description`
    - `StartDatetime`, `EndDatetime`, `IsAllDay`, `ColorId`
    - `ChangedBy = "gcal_sync"`
    - `ChangeReason = "updated"` or `"deleted"`
    - `CreatedAt = DateTime.UtcNow`
  - [ ] Preserve the current schema contract; do not add `EventDataJson`, `ChangeType`, or a second history table unless an actual schema gap is discovered and explicitly migrated

- [ ] **Task 3: Integrate snapshot writes into sync update and delete handling** (AC: 2.3.1, 2.3.2, 2.3.3, 2.3.4)
  - [ ] For an existing non-deleted event whose Google payload changed, create the snapshot first, then overwrite the local `GcalEvent` fields using the Tier 1 `GoogleWins` strategy
  - [ ] Detect meaningful change primarily via `GcalEtag` when available; if Google omits or reuses it unexpectedly, fall back to comparing the mapped Google-facing fields before deciding to snapshot
  - [ ] For a cancelled/deleted Google event, snapshot the current row first, then set `IsDeleted = true` and update `GcalUpdatedAt`, `LastSyncedAt`, and `UpdatedAt`
  - [ ] For new Google events, insert the `GcalEvent` row with no history row
  - [ ] For already-deleted or unchanged rows, avoid duplicate snapshot creation

- [ ] **Task 4: Preserve version history as insert-only data** (AC: 2.3.5)
  - [ ] Use the existing `gcal_event_version` table and `idx_version_event` index; do not add cleanup logic, hard deletes, or in-place edits for historical rows
  - [ ] Ensure tests and any helper query path order snapshots by newest first using `CreatedAt` and `VersionId` as the stable tie-breakers
  - [ ] Keep Google deletions as soft deletes on `gcal_event`; do not physically delete the event row because the history FK and future rollback flows depend on it

- [ ] **Task 5: Add automated coverage for version-history behavior** (AC: all)
  - [ ] Add integration tests around the sync pipeline:
    - updated event creates exactly one `GcalEventVersion` row before overwrite
    - deleted event creates exactly one `GcalEventVersion` row before `IsDeleted = true`
    - new event creates no `GcalEventVersion` row
    - unchanged event creates no `GcalEventVersion` row
    - repeated syncs preserve prior history rows and append new ones without mutating old rows
  - [ ] Assert `ChangedBy`, `ChangeReason`, and the copied snapshot field values match the pre-update local row, not the post-sync row
  - [ ] Reuse the SQLite-backed integration style already established in the test project; do not rely only on mocks for persistence verification

- [ ] **Task 6: Final validation** (AC: all)
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual or debugger-assisted verification:
    - sync an updated Google event and confirm a `gcal_event_version` row is inserted before overwrite
    - sync a deleted Google event and confirm a snapshot exists before `IsDeleted` becomes `true`
    - re-sync an unchanged event and confirm no extra history row is created

## Dev Notes

### Architecture Patterns and Constraints

**The current repo already has the right history table shape. Preserve it.**

- `GcalEventVersion` is already modeled as a denormalized snapshot table with:
  - `GcalEventId`
  - `GcalEtag`
  - `Summary`
  - `Description`
  - `StartDatetime`
  - `EndDatetime`
  - `IsAllDay`
  - `ColorId`
  - `ChangedBy`
  - `ChangeReason`
  - `CreatedAt`
- The schema does **not** currently use `EventDataJson` or a `ChangeType` enum. Story 2.3 should work with the existing entity/table unless a genuine schema gap is found.
- `GcalEvent.GcalEventId` is the primary key, and `GcalEventVersion.GcalEventId` is the FK. Google deletions therefore must remain soft deletes on `gcal_event`; hard deletes would break the intended history chain.
- Tier 1 conflict resolution remains `GoogleWins`. Story 2.3 adds history capture around that overwrite path; it does not change the resolution model.
- Version rows are append-only audit data. Sync must never edit or delete old snapshots.

**Where the logic belongs:**

- `GoogleCalendarService` handles Google API I/O and DTO mapping.
- The sync orchestration layer introduced in Story 2.2 should own:
  - change detection
  - snapshot creation
  - local overwrite / soft-delete behavior
  - sync result bookkeeping
- No new UI is required for this story. Version history remains a data-layer capability for now.

**Change detection guardrail:**

- Do not create history rows just because `LastSyncedAt` or other sync bookkeeping changed.
- Snapshot only when the Google-backed event state is changing:
  - `GcalEtag`
  - `Summary`
  - `Description`
  - `StartDatetime`
  - `EndDatetime`
  - `IsAllDay`
  - `ColorId`
  - delete state

### Project Structure Notes

**Files expected to change or be created:**

```text
GoogleCalendarManagement/
├── Services/
│   ├── SyncManager.cs                     # extend Story 2.2 sync orchestration
│   ├── SyncResult.cs                      # reuse from Story 2.2
│   ├── SyncProgress.cs                    # reuse from Story 2.2
│   ├── IGoogleCalendarService.cs          # reuse existing contract; no new auth path
│   └── GoogleCalendarService.cs           # may only need minor support changes, not history persistence
├── Data/
│   ├── Entities/
│   │   ├── GcalEvent.cs
│   │   └── GcalEventVersion.cs
│   └── Configurations/
│       ├── GcalEventConfiguration.cs
│       └── GcalEventVersionConfiguration.cs

GoogleCalendarManagement.Tests/
└── Integration/
    └── GoogleCalendarSyncTests.cs         # new or extended integration coverage for snapshots
```

**Do not introduce these changes in Story 2.3:**

- No second sync entry point or alternate sync manager
- No new version-history table or JSON snapshot column by default
- No background-sync timer work
- No version-history UI surface
- No change to the Tier 1 `GoogleWins` conflict strategy

### Previous Story Intelligence

- Story 2.2 is currently `ready-for-dev`, not done. Story 2.3 assumes its sync pipeline exists first. If the branch only contains Story 2.1 auth work, finish Story 2.2 before implementing this story.
- The current worktree already contains Story 2.1 auth artifacts in `Services/GoogleCalendarService.cs`, `Services/IGoogleCalendarService.cs`, `Services/OperationResult.cs`, `Services/DpapiTokenStorageService.cs`, `ViewModels/SettingsViewModel.cs`, and `Views/SettingsPage.xaml`. Reuse them; do not rebuild authentication while adding history.
- Story 1.6 established the DI/logging guardrails that still apply here:
  - `services.AddLogging(builder => builder.AddSerilog())`
  - constructor-injected `ILogger<T>`
  - friendly user-facing errors, not raw exception text

### References

- [Epic 2 tech spec](../tech-spec.md) - authoritative Story 2.3 acceptance criteria and sync-history responsibilities
- [Epic 2 tech spec](../tech-spec.md) - `Story 2.3 — Version History on Sync`
- [Epic 2 tech spec](../tech-spec.md) - traceability rows for AC #13-16 and version-history test guidance
- [Epic breakdown](../../epics.md) - original Story 2.3 definition and sequencing
- [Database schemas](../../_database-schemas.md) - `gcal_event_version` table contract
- [Architecture](../../architecture.md) - version-history rationale and Tier 1 `GoogleWins` strategy
- [Story 2.2](2-2-fetch-google-calendar-events-and-store-locally.md) - sync pipeline this story must extend
- [Current `GcalEvent` entity](../../../Data/Entities/GcalEvent.cs) - primary event fields and soft-delete state
- [Current `GcalEventVersion` entity](../../../Data/Entities/GcalEventVersion.cs) - actual snapshot schema already available
- [Current `GcalEvent` configuration](../../../Data/Configurations/GcalEventConfiguration.cs) - FK relationship from `gcal_event_version`
- [Current `GcalEventVersion` configuration](../../../Data/Configurations/GcalEventVersionConfiguration.cs) - existing table and index mapping
- [Current Google Calendar service contract](../../../Services/IGoogleCalendarService.cs) - API layer already defined
- [Current auth implementation](../../../Services/GoogleCalendarService.cs) - existing service shape to preserve

## Dev Agent Record

### Context Reference

- [Story Context XML](2-3-implement-version-history-on-calendar-sync.context.xml) - Generated 2026-03-30

### Agent Model Used

gpt-5

### Debug Log References

### Completion Notes List

### File List

# Story 2.3A: Harden Version History Schema and Sync Semantics

Status: done

## Story

As a **developer**,
I want **the version-history schema and sync overwrite semantics hardened immediately after Story 2.3**,
so that **later sync, status, and rollback work builds on the correct event-history contract instead of carrying avoidable data-loss and metadata bugs forward**.

## Acceptance Criteria

1. **AC-2.3A.1 - History Snapshots Capture Additional Restore-Relevant Fields:** Given `gcal_event_version` stores the prior local state before sync overwrite, when Story 2.3A is implemented, each snapshot row also stores `RecurringEventId`, `IsRecurringInstance`, and `GcalUpdatedAt`.

2. **AC-2.3A.2 - Sync Preserves Ownership Metadata:** Given an existing `gcal_event` row is updated or soft-deleted from Google, when sync applies the incoming state, it does not forcibly reset `AppCreated`, `AppPublished`, or `SourceSystem`.

3. **AC-2.3A.3 - History FK No Longer Cascades Away Audit Data:** Given `gcal_event_version` is intended to be retained indefinitely, when the relationship to `gcal_event` is configured, it uses `Restrict` / `NoAction` semantics rather than cascade delete so historical rows are not silently removed with the parent.

4. **AC-2.3A.4 - Documentation Uses The Implemented Snapshot Contract:** Given the planning docs still contain older `EventDataJson` language, when the story is completed, the active planning and implementation docs describe the current fielded `gcal_event_version` snapshot model instead.

5. **AC-2.3A.5 - Rollback Enhancements Are Explicitly Staged, Not Half-Implemented:** Given rollback itself belongs to Epic 8, when Story 2.3A is completed, the codebase does not add partial rollback behavior yet, but the story documents that rollback must snapshot current live state first and that optional full raw Google-payload archival is future Epic 8 work.

## Tasks / Subtasks

- [x] **Task 1: Verify Story 2.3 is the base and keep this story scoped to sync hardening** (AC: all)
  - [x] Confirm Story 2.3's `SyncManager` snapshot path and integration coverage are present on the working branch before starting
  - [x] Treat this as a follow-up hardening story on the same sync pipeline; do not fork a second history mechanism
  - [x] Keep rollback service implementation out of this story; this story only prepares the schema and sync semantics for Epic 8

- [x] **Task 2: Extend the `GcalEventVersion` schema with restore-relevant fields** (AC: 2.3A.1)
  - [x] Add nullable `GcalUpdatedAt` to `Data/Entities/GcalEventVersion.cs`
  - [x] Add nullable `RecurringEventId` to `Data/Entities/GcalEventVersion.cs`
  - [x] Add non-nullable `IsRecurringInstance` with a sensible default to `Data/Entities/GcalEventVersion.cs`
  - [x] Update `Data/Configurations/GcalEventVersionConfiguration.cs` to map:
    - [x] `gcal_updated_at`
    - [x] `recurring_event_id`
    - [x] `is_recurring_instance`
  - [x] Create an EF Core migration that adds the three columns without disturbing existing history rows
  - [x] Keep `gcal_event_version` a fielded snapshot table; do not add `EventDataJson`, `ChangeType`, or a raw payload column in this story

- [x] **Task 3: Update snapshot creation to include the new fields** (AC: 2.3A.1)
  - [x] Extend `SyncManager.CreateVersionSnapshot(...)` to copy `GcalUpdatedAt`, `RecurringEventId`, and `IsRecurringInstance` from the live `GcalEvent`
  - [x] Ensure update and delete snapshots both capture the pre-overwrite values
  - [x] Preserve the existing `ChangedBy = "gcal_sync"` and `ChangeReason = "updated" / "deleted"` behavior

- [x] **Task 4: Preserve local ownership metadata during Google overwrite and delete handling** (AC: 2.3A.2)
  - [x] Remove the forced resets of `AppCreated`, `AppPublished`, and `SourceSystem` from `SyncManager.ApplyIncomingValues(...)`
  - [x] Remove the forced resets of `AppCreated`, `AppPublished`, and `SourceSystem` from `SyncManager.ApplyDeletedValues(...)`
  - [x] Re-evaluate the `changed` detection logic so preserving those fields does not cause false-positive update counts
  - [x] Keep Google-facing fields authoritative for Tier 1 `GoogleWins`; this story only stops wiping unrelated local ownership metadata

- [x] **Task 5: Change history FK behavior from cascade delete to restrict/no-action** (AC: 2.3A.3)
  - [x] Update `Data/Configurations/GcalEventConfiguration.cs` so `HasForeignKey(v => v.GcalEventId)` does not cascade delete historical rows
  - [x] Generate and review the migration to ensure the FK is recreated with non-cascading delete behavior in SQLite
  - [x] Verify the new behavior does not break normal soft-delete sync semantics, since soft deletes remain the standard path

- [x] **Task 6: Clean up documentation drift around `EventDataJson`** (AC: 2.3A.4, 2.3A.5)
  - [x] Update `docs/epics.md` Story 1.3 / Story 2.3 planning text and example tests to describe the actual `gcal_event_version` snapshot columns instead of `EventDataJson`
  - [x] Update `docs/epic-1/stories/1-3-implement-core-database-schema-tier-1-tables.md` if it still implies JSON-based version rows as the active design
  - [x] Update any Epic 2 story/planning references that still assume `ChangeType` + `EventDataJson`
  - [x] Add an explicit note in this story that rollback implementation belongs to Epic 8
  - [x] Add an explicit note in this story that future full-fidelity Google-side archival, if desired, should be planned as a separate Epic 8 enhancement

- [x] **Task 7: Add automated coverage for the new schema and preserved metadata behavior** (AC: 2.3A.1, 2.3A.2, 2.3A.3)
  - [x] Extend `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs` to assert snapshots now copy:
    - [x] `GcalUpdatedAt`
    - [x] `RecurringEventId`
    - [x] `IsRecurringInstance`
  - [x] Add an integration test proving sync overwrite does not wipe `AppCreated`, `AppPublished`, or `SourceSystem`
  - [x] Add a schema/configuration test verifying the relationship no longer uses cascade delete semantics
  - [x] Keep the existing Story 2.3 insert-only and unchanged/no-spam assertions intact

- [x] **Task 8: Final validation** (AC: all)
  - [x] Run `dotnet build -p:Platform=x64`
  - [x] Run `dotnet test`
  - [x] Manual or debugger-assisted verification:
    - [x] update a recurring-instance event and confirm the version row contains recurring metadata plus `gcal_updated_at`
    - [x] sync an app-owned row and confirm overwrite does not clear `AppCreated`, `AppPublished`, or `SourceSystem`
    - [x] inspect the generated migration / schema and confirm the version FK no longer cascades deletes
    - [x] review the touched docs and confirm `EventDataJson` no longer describes the active version-history design

### Review Findings

- [x] [Review][Patch] Story-critical Data files are still gitignored and will not be committed [.gitignore:69]
- [x] [Review][Patch] Snapshot gating does not include the newly added restore-relevant fields [Services/SyncManager.cs:350]
- [x] [Review][Patch] Schema tests bypass migrations and do not validate the upgrade path [GoogleCalendarManagement.Tests/Integration/SchemaTests.cs:167]

## Dev Notes

### Architecture Patterns and Constraints

**This is a hardening story inserted before the remaining Epic 2 sync work.**

- Story 2.3 is already implemented in `Services/SyncManager.cs` and uses `gcal_event_version` as a denormalized fielded snapshot table, not a JSON blob history table.
- The current `GcalEvent` live entity already contains `GcalUpdatedAt`, `RecurringEventId`, and `IsRecurringInstance`; the history entity is the piece that lags behind.
- `gcal_event_version` is operational history and rollback input. It should capture Google-facing event state plus history metadata, but it should not become a perfect mirror of `gcal_event`.
- `AppCreated`, `AppPublished`, and `SourceSystem` are local ownership/workflow metadata. They may be important later for publish, approval, and conflict-resolution logic, so sync should not zero them out on every overwrite.
- Soft delete in `gcal_event` remains the normal behavior for Google deletions. Changing the FK away from cascade does not change the soft-delete strategy; it protects against accidental future hard deletes.

**Do not widen scope to rollback implementation yet.**

- Epic 2 tech spec keeps save/restore and rollback out of scope for Epic 2.
- This story only adds the schema and sync behavior needed so Epic 8 can implement rollback cleanly.

**Epic 8 staging notes (documented here, not implemented):**
- Rollback must snapshot the current live row *before* restoring an older version so that rollback itself is reversible (i.e., `gcal_event_version` receives a new row capturing the pre-rollback state).
- Optional full-fidelity archival of every observed Google payload change should be treated as a separate local capture strategy, not an expansion of the operational snapshot table. The Google Calendar API does not expose a true event revision-history endpoint; ETags and update timestamps can be used to detect changes but not to retrieve older payloads.

**Documentation guardrail:**

- Older planning docs still mention `EventDataJson`, `ChangeType`, and JSON-deserialized history examples.
- The implemented Story 2.3 and the current EF model are authoritative now. Update the planning docs to match the fielded table contract rather than reintroducing the old design through future story generation.

### Project Structure Notes

**Expected files to create or update:**

```text
GoogleCalendarManagement/
├── Data/
│   ├── Entities/
│   │   └── GcalEventVersion.cs
│   ├── Configurations/
│   │   ├── GcalEventConfiguration.cs
│   │   └── GcalEventVersionConfiguration.cs
│   └── Migrations/
│       └── <timestamp>_HardenGcalEventVersionHistory.cs
├── Services/
│   └── SyncManager.cs

GoogleCalendarManagement.Tests/
├── Integration/
│   └── GoogleCalendarSyncTests.cs
└── Integration or Unit/
    └── Schema/configuration coverage for FK delete behavior

docs/
├── epics.md
└── epic-1/
    └── stories/
        └── 1-3-implement-core-database-schema-tier-1-tables.md
```

### Previous Story Intelligence

- Story 2.2 introduced recurring-event mapping and `sync_token` persistence, so recurring metadata is already part of the live `gcal_event` shape.
- Story 2.3 intentionally preserved the existing `gcal_event_version` schema and added snapshot writes around update/delete paths. This story is the right place to add the now-identified schema corrections rather than waiting until rollback code depends on the incomplete snapshots.
- Stories 2.4 and 2.5 are already drafted as `ready-for-dev`, but both should inherit the hardened sync contract rather than advancing on top of the current metadata-reset and incomplete-snapshot behavior.

### References

- [Epic 2 tech spec](../tech-spec.md) - authoritative scope boundary showing save/restore is out of Epic 2
- [Story 2.3](./2-3-implement-version-history-on-calendar-sync.md) - existing snapshot contract and insert-only behavior
- [Current `GcalEvent` entity](../../../Data/Entities/GcalEvent.cs) - live event fields already available to snapshot
- [Current `GcalEventVersion` entity](../../../Data/Entities/GcalEventVersion.cs) - current history-table gap
- [Current `SyncManager`](../../../Services/SyncManager.cs) - current snapshot creation and ownership-metadata reset behavior
- [Current `GcalEvent` configuration](../../../Data/Configurations/GcalEventConfiguration.cs) - FK delete behavior currently set to cascade
- [Database schemas](../../_database-schemas.md) - current authoritative schema doc already aligned with fielded snapshots
- [Epic breakdown](../../epics.md) - older planning doc that still contains `EventDataJson` drift to clean up

## Dev Agent Record

### Context Reference

- [Story Context XML](2-3a-harden-version-history-schema-and-sync-semantics.context.xml) - Generated 2026-03-30

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Added `GcalUpdatedAt`, `RecurringEventId`, `IsRecurringInstance` to `GcalEventVersion` entity and configuration; mapped to `gcal_updated_at`, `recurring_event_id`, `is_recurring_instance` columns.
- Updated `SyncManager.CreateVersionSnapshot` to copy the three new fields from the live `GcalEvent` row. Both update and delete snapshot paths now capture recurring metadata and the Google-side update timestamp.
- Removed forced resets of `AppCreated`, `AppPublished`, and `SourceSystem` from `ApplyIncomingValues` and `ApplyDeletedValues`. Also removed the false-positive `changed` detection that counted any app-owned event as changed on every sync.
- Changed `GcalEventConfiguration` FK from `DeleteBehavior.Cascade` to `DeleteBehavior.Restrict`. Generated migration `20260330201850_HardenGcalEventVersionHistory` which drops/re-adds the FK and adds the three new columns.
- Updated `docs/epics.md` Story 1.3 schema section and Story 2.3 test example to reflect the fielded snapshot design; removed all active `EventDataJson`/`ChangeType` language.
- Updated `docs/epic-1/stories/1-3-implement-core-database-schema-tier-1-tables.md` completion note to reference the FK hardening in Story 2.3A.
- Added Epic 8 staging notes to Dev Notes section documenting snapshot-before-rollback requirement and optional full-payload archival strategy.
- Added 5 new tests (3 in `GoogleCalendarSyncTests.cs`, 2 in `SchemaTests.cs`). All 47 tests pass, 0 regressions.

### File List

- Data/Entities/GcalEventVersion.cs
- Data/Configurations/GcalEventVersionConfiguration.cs
- Data/Configurations/GcalEventConfiguration.cs
- Data/Migrations/20260330201850_HardenGcalEventVersionHistory.cs
- Data/Migrations/20260330201850_HardenGcalEventVersionHistory.Designer.cs
- Data/Migrations/CalendarDbContextModelSnapshot.cs
- Services/SyncManager.cs
- GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs
- GoogleCalendarManagement.Tests/Integration/SchemaTests.cs
- docs/epics.md
- docs/epic-1/stories/1-3-implement-core-database-schema-tier-1-tables.md
- docs/epic-2/stories/2-3a-harden-version-history-schema-and-sync-semantics.md

### Change Log

- 2026-03-30: Story 2.3A implemented — hardened GcalEventVersion schema (3 new columns), preserved ownership metadata in sync overwrite/delete, changed FK to Restrict, cleaned up EventDataJson doc drift, added 5 new tests. All 47 tests pass.

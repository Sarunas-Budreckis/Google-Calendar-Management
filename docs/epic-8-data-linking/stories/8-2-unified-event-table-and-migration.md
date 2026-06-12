# Story 8.2: Unified `event` Table + Stable Id + Atomic Migration

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
**Agent:** Opus · **Effort:** high
**Dependencies:** 8.1 recommended (terminology naming) — not blocking

---

## Story

As the application's data layer,
I want all event data consolidated into a single `event` table with a stable local `event_id`,
so that links always reference a permanent identifier that never changes even when an event is published to GCal.

---

## Acceptance Criteria

1. A new `event` table exists with the exact schema from concepts.md §3: `event_id` (PK, stable local string, never changes), `gcal_event_id` (nullable, UNIQUE — filled on publish), `calendar_id`, `summary`, `description`, `start_datetime`, `end_datetime`, `is_all_day`, `color_id`, `lifecycle` (`'candidate'` | `'approved'`), `publish` (`'local_only'` | `'published'`), `has_unpublished_changes` (bool), `source_system`, `recurring_event_id`, `is_recurring_instance`, `gcal_etag`, `gcal_updated_at`, `last_synced_at`, `app_last_modified_at`, `created_at`, `updated_at`.
2. Every row that existed in `gcal_event` appears exactly once in `event` as `lifecycle='approved', publish='published'` — all event content preserved (summary, description, color, dates, etc.).
3. `pending_event` rows that have a `gcal_event_id` (overlay/edit rows) are **merged into** their corresponding `gcal_event` row in `event`: the event content from `pending_event` (the local edits) is preserved; `has_unpublished_changes = 1`; no duplicate rows created.
4. `pending_event` rows where `gcal_event_id IS NULL` and `source_system = 'manual'` (or null) become `event(lifecycle='approved', publish='local_only')` — content preserved, `has_unpublished_changes = 0`.
5. `pending_event` rows where `gcal_event_id IS NULL` and `source_system != 'manual'` and `source_system IS NOT NULL` (machine-generated) become `event(lifecycle='candidate', publish='local_only')` — content preserved.
6. `gcal_event_version.gcal_event_id` is rewritten to reference `event.event_id` (the new stable id that replaced the corresponding `gcal_event` row). The FK name on the column changes from `gcal_event_id` to `event_id`.
7. All four `linked_event_id` columns (`toggl_data.linked_event_id`, `call_log_entry.linked_event_id`, `civ5_data.linked_event_id`, `comfyui_scan_point.linked_event_id`) are updated: any value that was a `gcal_event_id` string is rewritten to the corresponding `event_id`. Values that were a `pending_event_id` are rewritten to the corresponding merged `event_id`.
8. The `gcal_event` and `pending_event` tables are **dropped** after the transform. The `date_source_integration` table is **also dropped** (manual per-day checkbox, superseded by computed coverage in 8.10 — AC per concepts.md §3 migration notes).
9. The migration is atomic: the entire transform runs inside a single SQLite transaction; the existing Story 1.4 `CreateBackupAsync("pre-migration")` is called automatically by `MigrationService.ApplyMigrationsAsync()` before any migration runs — this is already wired.
10. The EF Core `Event` entity class and `EventConfiguration` exist with the correct column mappings; `CalendarDbContext` exposes `DbSet<Event> Events` and no longer exposes `DbSet<GcalEvent> GcalEvents` or `DbSet<PendingEvent> PendingEvents`.
11. Integration tests verify: (a) every prior `gcal_event` row appears exactly once in `event` after migration; (b) an overlay `pending_event` row correctly sets `has_unpublished_changes = 1` and does not produce a duplicate; (c) a manual `pending_event` (no gcal_id) becomes `approved/local_only`; (d) a machine `pending_event` becomes `candidate/local_only`; (e) `gcal_event_version` rows have their event reference intact; (f) `linked_event_id` values on `toggl_data` and at least one other source table are rewritten correctly.
12. The app compiles and `MigrationService.RunStartupAsync()` completes without error on a database that had rows in both old tables before migration.

---

## Tasks / Subtasks

- [ ] Task 1: Create `Event` entity + `EventConfiguration` (AC: #1, #10)
  - [ ] 1.1 Create `Data/Entities/Event.cs` — all columns from AC #1; use `string` PK for `event_id`; `lifecycle` and `publish` as `string` (enum values enforced by the migration check constraint, not C# enum, to avoid EF enum serialization complexity with SQLite)
  - [ ] 1.2 Create `Data/Configurations/EventConfiguration.cs` — table name `event`; map every column; add UNIQUE index on `gcal_event_id` (filter `WHERE gcal_event_id IS NOT NULL`); add index `idx_event_date` on `(start_datetime, end_datetime)`; add index `idx_event_lifecycle` on `lifecycle`; add index `idx_event_source` on `source_system`; add CHECK constraint `CK_event_lifecycle CHECK (lifecycle IN ('candidate','approved'))`; add CHECK constraint `CK_event_publish CHECK (publish IN ('local_only','published'))`
  - [ ] 1.3 Update `CalendarDbContext`: add `DbSet<Event> Events`; remove `DbSet<GcalEvent> GcalEvents`; remove `DbSet<PendingEvent> PendingEvents`

- [ ] Task 2: Update `GcalEventVersion` entity + configuration to point at `event_id` (AC: #6)
  - [ ] 2.1 In `Data/Entities/GcalEventVersion.cs`: rename navigation property `public GcalEvent GcalEvent` → `public Event Event`; rename FK property `GcalEventId` → `EventId`
  - [ ] 2.2 In `Data/Configurations/GcalEventVersionConfiguration.cs`: update column name `gcal_event_id` → `event_id`; update FK to reference `event`; keep index `idx_version_event` on `(event_id, created_at)`
  - [ ] 2.3 Remove `GcalEvent.Versions` navigation (entity being deleted) and the corresponding `HasMany`/`WithOne` relationship; `GcalEventVersion` now navigates to `Event` instead

- [ ] Task 3: Create the EF Core migration (AC: #1–#9, #12)
  - [ ] 3.1 Scaffold the migration: `dotnet ef migrations add UnifyEventTable` — then replace the generated body entirely with hand-written SQL executed via `migrationBuilder.Sql()` (same pattern used by `20260605021000_DropLegacyCiv5SessionPointTable` and `20260605030000_RenameSpotifyStreamToSpotifyData` in `MigrationService._directSqlMap`)
  - [ ] 3.2 Migration `Up` steps (all in a single transaction via `migrationBuilder.BeginTransaction()` / `migrationBuilder.CommitTransaction()` or using `migrationBuilder.Sql()` with `BEGIN`/`COMMIT`):
    - Step A: Create `event` table with all columns + indexes + check constraints
    - Step B: Insert `gcal_event` rows → `event` (`lifecycle='approved'`, `publish='published'`, `has_unpublished_changes=0`)
    - Step C: For overlay `pending_event` rows (where `gcal_event_id IS NOT NULL`): UPDATE those `event` rows to apply pending content (summary, description, start_datetime, end_datetime, color_id, is_all_day); set `has_unpublished_changes=1`
    - Step D: Insert manual `pending_event` rows (`gcal_event_id IS NULL AND (source_system='manual' OR source_system IS NULL)`): new `event_id` = `pending_event_id`; `lifecycle='approved'`, `publish='local_only'`, `has_unpublished_changes=0`
    - Step E: Insert machine `pending_event` rows (`gcal_event_id IS NULL AND source_system IS NOT NULL AND source_system != 'manual'`): new `event_id` = `pending_event_id`; `lifecycle='candidate'`, `publish='local_only'`, `has_unpublished_changes=0`
    - Step F: Add `event_id` column to `gcal_event_version`; populate it by joining `gcal_event_version.gcal_event_id` → `event.gcal_event_id`; drop old `gcal_event_id` column; drop & recreate index `idx_version_event` on new `event_id`
    - Step G: Update `linked_event_id` on all four source tables — rewrite gcal-id values to `event_id` by joining `event.gcal_event_id`; rewrite pending-id values to `event_id` (they already match since `event_id = pending_event_id` in steps D/E)
    - Step H: Drop `pending_event` table; drop `gcal_event` table; drop `date_source_integration` table
  - [ ] 3.3 Migration `Down`: only restore a `-- reversible by DB backup` comment (no automatic down since the old tables are dropped). Document this in the migration class.
  - [ ] 3.4 Register migration as a direct-SQL migration in `MigrationService._directSqlMap` if it hits the EF migration lock issue (same pattern as the two existing direct-SQL migrations — see `ApplyMigrationsAsync` in `MigrationService.cs`). Only needed if `MigrateAsync()` deadlocks; start with `MigrateAsync()` and observe.

- [ ] Task 4: Remove deleted entity classes + configurations (AC: #10)
  - [ ] 4.1 Delete `Data/Entities/GcalEvent.cs`
  - [ ] 4.2 Delete `Data/Entities/PendingEvent.cs`
  - [ ] 4.3 Delete `Data/Configurations/GcalEventConfiguration.cs`
  - [ ] 4.4 Delete `Data/Configurations/PendingEventConfiguration.cs`
  - [ ] 4.5 Delete `Data/Configurations/DateSourceIntegrationConfiguration.cs` (table is dropped in migration)
  - [ ] 4.6 Delete `Data/Entities/DateSourceIntegration.cs`

- [ ] Task 5: Stub out / fix compilation errors in consuming code (AC: #12)
  - [ ] 5.1 `CalendarQueryService.cs` — heavily uses `GcalEvents` + `PendingEvents` join. Replace the query body with a **stub** that queries `context.Events` and returns empty/placeholder data. Add `// TODO 8.3+: full event query rewrite` comment. Do NOT rewrite business logic — that is Stories 8.3, 8.4, 8.5.
  - [ ] 5.2 All services that reference `GcalEvent`, `PendingEvent`, or `DateSourceIntegration` entities: update type references and property names enough to compile. Use `// TODO 8.3+` stubs where full rewrite is out of scope.
  - [ ] 5.3 `TogglEntry.PublishedGcalEvent` navigation property: the FK `PublishedGcalEventId` (on toggl_data) pointed at `gcal_event.gcal_event_id`. Replace with `public Event? PublishedEvent { get; set; }` and update the configuration to reference `event.gcal_event_id` (the published gcal id is still the lookup key). The column name `published_gcal_event_id` stays the same in the DB for now.
  - [ ] 5.4 `Models/CalendarEventDisplayModel.cs` and `Models/CalendarEventSourceKind.cs`: Do NOT change in this story. These are updated in Story 8.5.

- [ ] Task 6: Integration tests (AC: #11)
  - [ ] 6.1 Add test class `MigrationUnifyEventTableTests` in `GoogleCalendarManagement.Tests/Integration/`
  - [ ] 6.2 Test: given a seeded DB with rows in `gcal_event` + `pending_event` (overlay case), after migration each `gcal_event` row appears exactly once in `event`
  - [ ] 6.3 Test: overlay `pending_event` (has `gcal_event_id`) → merged event has `has_unpublished_changes=1`, no duplicate row
  - [ ] 6.4 Test: manual `pending_event` (null `gcal_event_id`, `source_system='manual'`) → `lifecycle='approved'`, `publish='local_only'`
  - [ ] 6.5 Test: machine `pending_event` (null `gcal_event_id`, `source_system='toggl'`) → `lifecycle='candidate'`, `publish='local_only'`
  - [ ] 6.6 Test: `gcal_event_version` rows point to the correct `event_id` after migration
  - [ ] 6.7 Test: `toggl_data.linked_event_id` (gcal-id string) is rewritten to the stable `event_id`
  - [ ] 6.8 Use the same `CreateTempFileService()` / `CleanupTempDir()` pattern as `MigrationServiceTests.cs` — seed data into the temp DB with raw SQL before running `svc.ApplyMigrationsAsync()`

---

## Dev Notes

### This is the highest-risk change in the project

Read the epic overview note verbatim: **REVISIT (Sarunas): confirm migration on a copy of the live DB before running for real.** Do not skip this step. `MigrationService.ApplyMigrationsAsync()` already calls `CreateBackupAsync("pre-migration")` automatically — backups land in `{db-dir}/backups/calendar_backup_{timestamp}_pre-migration.db`. Up to 5 backups kept.

### Current schema state — exactly what to replace

**`gcal_event` table** (`Data/Entities/GcalEvent.cs`, `Data/Configurations/GcalEventConfiguration.cs`, DB table `gcal_event`):
- PK: `gcal_event_id` (string, the GCal-assigned id — this is NOT stable for links)
- Key fields to preserve: `calendar_id`, `summary`, `description`, `start_datetime`, `end_datetime`, `is_all_day`, `color_id`, `gcal_etag`, `gcal_updated_at`, `is_deleted`, `app_created`, `source_system`, `app_published`, `app_published_at`, `app_last_modified_at`, `recurring_event_id`, `is_recurring_instance`, `last_synced_at`, `created_at`, `updated_at`
- Has one-to-one with `PendingEvent` (FK on `pending_event.gcal_event_id`)
- Has one-to-many with `GcalEventVersion` (FK on `gcal_event_version.gcal_event_id`)
- Referenced by `TogglEntry.PublishedGcalEvent` via `published_gcal_event_id` column on `toggl_data`

**`pending_event` table** (`Data/Entities/PendingEvent.cs`, `Data/Configurations/PendingEventConfiguration.cs`, DB table `pending_event`):
- PK: `pending_event_id` (string, app-generated GUID)
- `gcal_event_id` nullable: when set = overlay/edit of a published event; when null = new draft or machine candidate
- `source_system`: `'manual'` (or null) = user-created; any other value = machine-generated (e.g. `'toggl'`)
- `operation_type`: `'edit'`, `'delete'` — the delete case must be accounted for (see below)
- Important: `operation_type = 'delete'` means the user staged a GCal event for deletion. These rows STILL have a `gcal_event_id`. Treat them as: `lifecycle='approved'`, `publish='published'`, `has_unpublished_changes=1` (they have a pending delete, which is a form of unpublished change). The delete operation tracking itself is handled in Stories 8.4+.
- Has `ReadyToPublish`, `PublishAttemptedAt`, `PublishError` fields — these do NOT map to the new `event` table. Drop them silently (they are publish-pipeline state, superseded in Story 8.3).

**`date_source_integration` table** (`DateSourceIntegration.cs`) — drop entirely. No data preservation needed. Replaced by computed coverage in Story 8.10.

### `event_id` generation strategy

For `gcal_event` rows: `event_id` = a new UUID generated per row. **In SQL**: `lower(hex(randomblob(16)))` generates a 32-char hex string in SQLite. Use this: `lower(hex(randomblob(16)))` as the `event_id` value in the INSERT.

For `pending_event` rows with no `gcal_event_id` (steps D and E): `event_id` = `pending_event_id` (already a GUID string from the app). This preserves referential integrity for any existing `linked_event_id` values that point to `pending_event_id`s.

For overlay `pending_event` rows (step C — merge, not insert): the `event_id` was already set in step B (from the `gcal_event` side). Step C UPDATEs the row; `event_id` does not change.

### `linked_event_id` rewrite — four tables to update

These source entity tables currently store GCal event ids (or pending event ids) in `linked_event_id`:

| Table | Entity class | Current `linked_event_id` meaning |
|-------|-------------|-----------------------------------|
| `toggl_data` | `TogglEntry` | gcal_event_id string |
| `call_log_entry` | `CallLogEntry` | gcal_event_id string |
| `civ5_data` | `Civ5SessionPoint` | gcal_event_id string |
| `comfyui_scan_point` | `ComfyUIScanPoint` | gcal_event_id string |

These also have `linked_event_type` columns (currently `'gcal'` or similar). Leave `linked_event_type` alone in this migration — it is superseded by the `link` table in Story 8.12.

The rewrite SQL (per table):
```sql
UPDATE toggl_data
SET linked_event_id = (
    SELECT e.event_id FROM event e WHERE e.gcal_event_id = toggl_data.linked_event_id
)
WHERE linked_event_id IS NOT NULL
  AND EXISTS (SELECT 1 FROM event e WHERE e.gcal_event_id = toggl_data.linked_event_id);
```
Rows where `linked_event_id` was a `pending_event_id` already resolve because `event_id = pending_event_id` for those rows (no join needed — the value is already correct after steps D/E).

### `gcal_event_version` FK rewrite

`gcal_event_version` currently has FK `gcal_event_id → gcal_event.gcal_event_id`. After migration it must have `event_id → event.event_id`. Since SQLite does not support column renames with FK changes, the approach:

1. Add column `event_id TEXT` (nullable initially)
2. `UPDATE gcal_event_version SET event_id = (SELECT e.event_id FROM event e WHERE e.gcal_event_id = gcal_event_version.gcal_event_id)`
3. (All rows should match — if any don't, they reference deleted/orphaned events; can set to NULL or skip)
4. Make `event_id NOT NULL` — requires recreating the table in SQLite. Use the standard SQLite table-rename + recreate pattern:
   - `ALTER TABLE gcal_event_version RENAME TO gcal_event_version_old`
   - `CREATE TABLE gcal_event_version (version_id INTEGER PK, event_id TEXT NOT NULL, ...)`
   - `INSERT INTO gcal_event_version SELECT version_id, event_id, ... FROM gcal_event_version_old`
   - `DROP TABLE gcal_event_version_old`
   - Recreate index `idx_version_event` on `(event_id, created_at)`

### `day_name` unique constraint

`gcal_event` has a partial unique index: `idx_gcal_event_day_name_unique` filtered `WHERE source_system='day_name' AND is_deleted=0`. Likewise `pending_event` has `idx_pending_event_day_name_unique`. The new `event` table must recreate the equivalent constraint on `(start_datetime, source_system)` filtered `WHERE source_system='day_name'`. Add this to `EventConfiguration`.

### EF Core migration scaffold vs hand-written SQL

After `dotnet ef migrations add UnifyEventTable` is run, the auto-generated migration will be incorrect (EF doesn't know about the complex data transforms). **Replace the `Up()` body entirely with `migrationBuilder.Sql()`** calls or a single large SQL block. This is the established pattern for this project's complex migrations (`AddCiv5DataTable`, `RenameSpotifyStreamToSpotifyData`, etc.).

The **snapshot file** (`CalendarDbContextModelSnapshot.cs`) is auto-regenerated by EF from the current model. It will correctly reflect the new `Event` entity as long as the entity and configuration are correct. Do not hand-edit the snapshot.

### MigrationService direct-SQL registration (if needed)

If the migration deadlocks on the EF migration lock (which has happened with complex migrations), add it to `_directSqlMap` in `MigrationService.cs` and add the migration id to the `directSqlMigrations` HashSet. All SQL from the `Up()` method is duplicated there. This is an existing known pattern — see lines 62–76 of `MigrationService.cs`.

### Stub pattern for consuming code

`CalendarQueryService.cs` is the heaviest consumer of `GcalEvent` + `PendingEvent`. Its `GetEventsForRangeAsync` does a complex JOIN. Do NOT rewrite this in story 8.2 — stub it to `return new List<CalendarEventDisplayModel>()` with a `// TODO 8.3+: rewrite against unified event table` comment. Story 8.3 (Event repository + identity service) and Story 8.5 (rendering rewrite) own the full rewrite. The goal of 8.2 is schema + migration correctness, not zero service disruption.

Services to stub (do not fully rewrite):
- `Services/CalendarQueryService.cs`
- `Services/PendingEventRepository.cs` — stub to no-op or throw `NotImplementedException("Replaced in 8.3")`
- `Services/PendingEventDraftService.cs` — same
- `Services/PendingEventPublishService.cs` — same
- `Services/SyncManager.cs` (or similar GCal sync service) — stub the parts that reference `GcalEvent` / `PendingEvent` entities

### Testing framework

Project uses **xUnit** + **FluentAssertions**. Tests under `GoogleCalendarManagement.Tests/Integration/` use real temp-file SQLite databases. Use `CreateTempFileService()` pattern from `MigrationServiceTests.cs`:
```csharp
var options = new DbContextOptionsBuilder<CalendarDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;
var ctx = new CalendarDbContext(options);
var svc = new MigrationService(ctx, dbOptions, NullLogger<MigrationService>.Instance);
```

For migration tests that need to seed data into the *old* schema (before migration runs), use raw SQL via `SqliteConnection` directly against the temp file before calling `svc.ApplyMigrationsAsync()`. This is the only way to insert into the old tables — EF entities for `GcalEvent` and `PendingEvent` will no longer exist.

### Project structure notes

- Entity files: `Data/Entities/`
- EF configuration files: `Data/Configurations/`
- Migration files: `Data/Migrations/`
- DbContext: `Data/CalendarDbContext.cs`
- Migration runner: `Services/MigrationService.cs`
- Integration tests: `GoogleCalendarManagement.Tests/Integration/`
- All C# files use `namespace GoogleCalendarManagement.*` — match the namespace of neighboring files.

### What this story does NOT do

- Does not rewrite `CalendarQueryService` event query logic (Story 8.5).
- Does not create the `EventRepository` or `IEventIdentityService` (Story 8.3).
- Does not rewrite the GCal sync reconciler (Story 8.4).
- Does not repoint `gcal_event_version`, `deleted_event`, recurring-series beyond what's in this story (Story 8.6 handles `deleted_event` + recurring).
- Does not change `CalendarEventSourceKind` enum or `CalendarEventDisplayModel` (Story 8.5).
- Does not rename `linked_event_id` columns to `data_point_id` (that happens after Story 8.7 `data_point` table lands).
- Does not touch `deleted_event` table (no FK to `gcal_event` currently — verify and note if it does have one).

### References

- Canonical data model: [concepts.md §3](../concepts.md) — event table schema + migration transform rules
- Epic overview story 8.2 spec: [epic-overview.md §Phase 0](../epic-overview.md)
- Migration service (backup + direct-SQL patterns): `Services/MigrationService.cs`
- Existing entity: `Data/Entities/GcalEvent.cs`
- Existing entity: `Data/Entities/PendingEvent.cs`
- Existing version entity: `Data/Entities/GcalEventVersion.cs`
- Existing migration test pattern: `GoogleCalendarManagement.Tests/Integration/MigrationServiceTests.cs`
- EF configuration pattern: `Data/Configurations/GcalEventConfiguration.cs`

---

## Dev Agent Record

### Agent Model Used

Opus

### Debug Log References

### Completion Notes List

### File List

# Story 5.1: Data Source Infrastructure

Status: review

## Story

As a **developer**,
I want **the core database tables and repository layer for data source registration, per-day coverage tracking, and import history**,
so that **all Epic 5 UI stories and the Toggl Sleep import have a typed, testable data contract to build on**.

## Acceptance Criteria

1. **AC-5.1.1 — `data_source` table exists and is seeded-ready:** A new `data_source` table is created with columns `data_source_id` (integer PK autoincrement), `source_key` (text unique, e.g. `"toggl_sleep"`), `display_name` (text), `description` (text nullable), `supports_no_data_hint` (boolean default false), `created_at` (datetime). Seed rows are NOT required in this story — Story 5.6 inserts the Toggl Sleep row.

2. **AC-5.1.2 — `date_source_integration` junction table exists:** A new `date_source_integration` table is created with columns `integration_id` (integer PK autoincrement), `date` (date, not null), `data_source_id` (integer FK → `data_source`, restrict delete), `integrated` (boolean default false), `integrated_at` (datetime nullable), `created_at` (datetime), `updated_at` (datetime). A unique index covers `(date, data_source_id)`.

3. **AC-5.1.3 — `data_source_import_log` table exists:** A new `data_source_import_log` table is created with columns `import_log_id` (integer PK autoincrement), `data_source_id` (integer FK → `data_source`, restrict delete), `covered_start_date` (date not null), `covered_end_date` (date not null), `imported_at` (datetime not null), `records_fetched` (integer nullable), `success` (boolean not null), `error_message` (text nullable). This is a separate table from the existing `data_source_refresh` which continues to serve GCal green/grey sync indicators unchanged.

4. **AC-5.1.4 — Single EF Core migration applies cleanly:** One migration (e.g. `AddDataSourceTier3Tables`) adds all three tables. `dotnet ef database update` applies without errors on a fresh database and on a database that has the previous Epic 4 migration applied.

5. **AC-5.1.5 — `IDataSourceRepository` exposes the contracts needed by Epic 5:** The interface includes at minimum:
   - `GetAllSourcesAsync(CancellationToken ct)` → `IReadOnlyList<DataSource>`
   - `GetSourceByKeyAsync(string sourceKey, CancellationToken ct)` → `DataSource?`
   - `UpsertSourceAsync(DataSource source, CancellationToken ct)` → `DataSource`
   - `GetIntegrationAsync(DateOnly date, int dataSourceId, CancellationToken ct)` → `DateSourceIntegration?`
   - `SetIntegrationAsync(DateOnly date, int dataSourceId, bool integrated, CancellationToken ct)` → `DateSourceIntegration`
   - `GetLastImportAsync(int dataSourceId, CancellationToken ct)` → `DataSourceImportLog?` (most recent success)
   - `AddImportLogAsync(DataSourceImportLog log, CancellationToken ct)` → `DataSourceImportLog`

6. **AC-5.1.6 — Implementation uses `IDbContextFactory` pattern:** `DataSourceRepository` is registered as a singleton and uses `IDbContextFactory<CalendarDbContext>` — matching the existing repository pattern in the codebase.

7. **AC-5.1.7 — Schema tests lock in table shapes:** Integration tests assert that after migration, all three tables exist with the correct columns, FKs, and unique indexes. Tests follow the existing pattern in `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs`.

---

## Tasks / Subtasks

- [x] **Task 1: Create EF Core entities and configurations**
  - [x] Add `Data/Entities/DataSource.cs` with properties matching AC-5.1.1
  - [x] Add `Data/Entities/DateSourceIntegration.cs` with properties matching AC-5.1.2
  - [x] Add `Data/Entities/DataSourceImportLog.cs` with properties matching AC-5.1.3
  - [x] Add `Data/Configurations/DataSourceConfiguration.cs` using Fluent API (no data annotations)
  - [x] Add `Data/Configurations/DateSourceIntegrationConfiguration.cs` with unique index on `(Date, DataSourceId)` and FK restrict
  - [x] Add `Data/Configurations/DataSourceImportLogConfiguration.cs` with FK restrict
  - [x] Register all three as `DbSet<T>` in `Data/CalendarDbContext.cs`

- [x] **Task 2: Generate and verify the migration**
  - [x] Run `dotnet ef migrations add AddDataSourceTier3Tables` and verify the generated migration includes all three tables
  - [x] Verify the migration does NOT modify any existing tables (gcal_event, pending_event, data_source_refresh, etc.)
  - [x] Run `dotnet ef database update` against a local database carrying the latest Epic 4 migration

- [x] **Task 3: Implement `IDataSourceRepository` and `DataSourceRepository`**
  - [x] Add `Services/IDataSourceRepository.cs` with the interface from AC-5.1.5
  - [x] Add `Services/DataSourceRepository.cs` implementing it using `IDbContextFactory<CalendarDbContext>`
  - [x] `SetIntegrationAsync`: upserts — if row exists update `integrated` + `integrated_at` + `updated_at`; if not, insert
  - [x] `GetLastImportAsync`: returns the most recent `DataSourceImportLog` row where `Success = true`, ordered by `ImportedAt DESC`
  - [x] All timestamps in UTC

- [x] **Task 4: Register in DI**
  - [x] Register `IDataSourceRepository` → `DataSourceRepository` as singleton in `App.xaml.cs`, following the existing repository registration pattern

- [x] **Task 5: Schema integration tests**
  - [x] Add tests to `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs` asserting `data_source`, `date_source_integration`, and `data_source_import_log` tables exist post-migration
  - [x] Assert `date_source_integration` has a unique index on `(date, data_source_id)`
  - [x] Assert `data_source_import_log` FK to `data_source` exists

---

## Dev Notes

### Design Decision: New Tables vs. Extending `data_source_refresh`

Do NOT repurpose or modify `data_source_refresh`. That table is used by the existing GCal green/grey sync status system (`SyncStatusService`). Adding a `data_source_id` FK to it would couple the Tier 3 import concept to the Tier 1 sync mechanism. The new `data_source_import_log` is clean and separately indexed for the query pattern needed by the left panel (most recent successful import per source).

### Entity Naming Convention

Follow the existing pattern:
- Entity class names: `DataSource`, `DateSourceIntegration`, `DataSourceImportLog`
- Table names (snake_case): `data_source`, `date_source_integration`, `data_source_import_log`
- PK property: `DataSourceId`, `IntegrationId`, `ImportLogId`
- Timestamps: suffix `At` in C# (e.g. `ImportedAt`, `CreatedAt`)

### Project Structure

New files for this story:

```text
Data/
├── Entities/
│   ├── DataSource.cs                           # new
│   ├── DateSourceIntegration.cs                # new
│   └── DataSourceImportLog.cs                  # new
├── Configurations/
│   ├── DataSourceConfiguration.cs              # new
│   ├── DateSourceIntegrationConfiguration.cs   # new
│   └── DataSourceImportLogConfiguration.cs     # new
├── CalendarDbContext.cs                         # add 3 DbSets
└── Migrations/
    └── <timestamp>_AddDataSourceTier3Tables.cs # new

Services/
├── IDataSourceRepository.cs                    # new
└── DataSourceRepository.cs                     # new

App.xaml.cs                                     # add DI registration

GoogleCalendarManagement.Tests/Integration/
└── SchemaTests.cs                              # extend with new table assertions
```

### References

- [Epic 5 overview](../epic-overview.md) — data model implications section
- [Database schemas](../../_database-schemas.md) — `data_source`, `date_source_integration`, canonical SQL definitions
- [Existing DataSourceRefresh entity](../../../Data/Entities/DataSourceRefresh.cs) — do not modify
- [Existing schema tests](../../../GoogleCalendarManagement.Tests/Integration/SchemaTests.cs) — pattern to follow
- [CalendarDbContext](../../../Data/CalendarDbContext.cs) — add DbSets here
- [App.xaml.cs](../../../App.xaml.cs) — DI registration location

---

## Dev Agent Record

### Completion Notes

Implemented all five tasks as specified. Migration `20260513161426_AddDataSourceTier3Tables` adds `data_source`, `date_source_integration`, and `data_source_import_log` tables and applied cleanly to the local database. `DataSourceRepository` uses `IDbContextFactory<CalendarDbContext>` per the singleton pattern. All timestamps written in UTC. `SetIntegrationAsync` performs a true upsert. `GetLastImportAsync` filters `Success = true` ordered by `ImportedAt DESC`. Fixed a pre-existing build error in `PendingEventPublishServiceTests.StubGoogleCalendarService` that was missing `DeleteEventAsync` (added from the Epic 4 story that introduced the method). Added `appsettings.secrets.json` (gitignored) for the Toggl API token.

### File List

- `Data/Entities/DataSource.cs` — new
- `Data/Entities/DateSourceIntegration.cs` — new
- `Data/Entities/DataSourceImportLog.cs` — new
- `Data/Configurations/DataSourceConfiguration.cs` — new
- `Data/Configurations/DateSourceIntegrationConfiguration.cs` — new
- `Data/Configurations/DataSourceImportLogConfiguration.cs` — new
- `Data/CalendarDbContext.cs` — added 3 DbSets
- `Data/Migrations/20260513161426_AddDataSourceTier3Tables.cs` — new
- `Data/Migrations/20260513161426_AddDataSourceTier3Tables.Designer.cs` — new
- `Data/Migrations/CalendarDbContextModelSnapshot.cs` — updated by EF tooling
- `Services/IDataSourceRepository.cs` — new
- `Services/DataSourceRepository.cs` — new
- `App.xaml.cs` — added IDataSourceRepository singleton registration
- `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs` — added 5 new schema tests
- `GoogleCalendarManagement.Tests/Integration/PendingEventPublishServiceTests.cs` — fixed pre-existing stub missing DeleteEventAsync
- `appsettings.secrets.json` — new (gitignored, holds Toggl API token placeholder)
- `.gitignore` — added appsettings.secrets.json entry

### Change Log

- 2026-05-13: Implemented Story 5.1 — data source infrastructure (3 entities, 3 configurations, EF migration, IDataSourceRepository + DataSourceRepository, DI registration, 5 schema integration tests, Toggl secrets file)

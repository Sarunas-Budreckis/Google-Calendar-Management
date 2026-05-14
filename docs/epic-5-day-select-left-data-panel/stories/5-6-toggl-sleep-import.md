# Story 5.6: Toggl Sleep Import

Status: review

## Story

As a **user**,
I want **to import my sleep entries from Toggl Track for a specified date range**,
so that **the app has the raw sleep data it needs for the Toggl Sleep left panel card and candidate event creation**.

## Acceptance Criteria

1. **AC-5.6.1 — Toggl API token can be configured:** The user can enter a Toggl Track API token in the Settings page. The token is stored encrypted (using existing DPAPI storage pattern) in the `config` table under key `"toggl_api_token"`. A "Test connection" button verifies the token by making a lightweight Toggl API call (`GET /me`) and shows a success or error message.

2. **AC-5.6.2 — Import is triggered from the left panel via `IDataSourceImportHandler`:** `TogglSleepImportHandler` implements `IDataSourceImportHandler` with `SourceKey = "toggl_sleep"`. When the user clicks the "Import…" button on the Toggl Sleep row in the left panel's global mode (Story 5.4), the handler opens a date-range picker dialog (same pattern as ICS export) and runs the import after confirmation. The handler is registered in `DataSourceImportHandlerRegistry` at startup.

3. **AC-5.6.3 — `toggl_data` table is created:** A new EF Core migration adds the `toggl_data` table with the schema from `_database-schemas.md`: `toggl_id` (integer PK — Toggl's own ID), `description`, `start_time`, `end_time`, `duration_seconds`, `project_name`, `tags` (JSON text), `visible_as_event` (boolean default true), `published_to_gcal` (boolean default false), `published_gcal_event_id` (nullable, FK to gcal_event), `last_synced_at`, `created_at`.

4. **AC-5.6.4 — Import fetches only sleep entries:** The import service calls Toggl Track API v9 `GET /me/time_entries?start_date={start}&end_date={end}` and filters to entries where `description` contains "sleep" (case-insensitive). Non-sleep entries are not stored.

5. **AC-5.6.5 — Imported entries are upserted into `toggl_data`:** Each sleep entry is inserted or updated by `toggl_id`. On re-import of an overlapping date range, existing rows are updated (not duplicated).

6. **AC-5.6.6 — "Toggl Sleep" data source is registered on first import:** Before the first import, if no `data_source` row with `source_key = "toggl_sleep"` exists, it is inserted: `display_name = "Toggl Sleep"`, `description = "Sleep entries from Toggl Track"`, `supports_no_data_hint = true`. This is idempotent — subsequent imports check first.

7. **AC-5.6.7 — Import log entry is written:** After each import completes, a `data_source_import_log` row is added with `data_source_id` (Toggl Sleep), `covered_start_date`, `covered_end_date`, `imported_at` (UTC now), `records_fetched` (count of sleep entries stored), `success` (true/false), `error_message` (null on success).

8. **AC-5.6.8 — `DataSourceImportCompletedMessage` is published after import:** Win or lose, the message is published so the left panel global mode refreshes.

9. **AC-5.6.9 — Import errors are surfaced to the user:** If the Toggl API returns an error (bad token, network failure, rate limit), a clear error message is shown via the existing notification/InfoBar system. The import log records the failure. No crash.

10. **AC-5.6.10 — Import progress is indicated:** While running, the Import button is disabled and shows a loading indicator. The operation runs on a background thread and does not freeze the UI.

---

## Tasks / Subtasks

- [x] **Task 1: Create `toggl_data` EF Core entity and migration**
  - [x] Add `Data/Entities/TogglEntry.cs` with properties matching AC-5.6.3
  - [x] Add `Data/Configurations/TogglEntryConfiguration.cs` (snake_case table name `toggl_data`, `toggl_id` as PK, FK to gcal_event nullable)
  - [x] Register `DbSet<TogglEntry> TogglEntries` in `Data/CalendarDbContext.cs`
  - [x] Generate migration `AddTogglDataTable`; verify it does not touch existing tables

- [x] **Task 2: Build `TogglApiClient`**
  - [x] Add `Services/TogglApiClient.cs` (or `Services/ITogglApiClient.cs` + implementation)
  - [x] Uses `HttpClient` with Basic auth (API token as username, `"api_token"` as password — Toggl's documented pattern)
  - [x] Methods:
    - `TestConnectionAsync(string apiToken, CancellationToken ct)` → `bool` (calls `GET https://api.track.toggl.com/api/v9/me`)
    - `GetTimeEntriesAsync(string apiToken, DateOnly start, DateOnly end, CancellationToken ct)` → `IReadOnlyList<TogglTimeEntryDto>`
  - [x] Register as singleton `HttpClient` with base address `https://api.track.toggl.com/`
  - [x] Handle 429 rate limit with a single retry after the `Retry-After` header delay

- [x] **Task 3: Add `TogglTimeEntryDto`**
  - [x] Add `Services/TogglTimeEntryDto.cs` — a deserialization record for the Toggl API v9 time entry JSON:
    - `id` (long), `description` (string?), `start` (string ISO8601), `stop` (string? ISO8601), `duration` (int — Toggl encodes negative for running entries), `project_id` (long?), `tags` (string[]?)

- [x] **Task 4: Implement `ITogglSleepImportService` / `TogglSleepImportService`**
  - [x] Add `Services/ITogglSleepImportService.cs`:
    ```csharp
    public interface ITogglSleepImportService
    {
        Task<TogglSleepImportResult> ImportAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
    }
    ```
  - [x] Add `Services/TogglSleepImportResult.cs`: `bool Success`, `int RecordsFetched`, `string? ErrorMessage`
  - [x] `TogglSleepImportService` implementation:
    - Reads API token from `IConfigRepository` (key `"toggl_api_token"`)
    - Calls `TogglApiClient.GetTimeEntriesAsync`
    - Filters: `description?.Contains("sleep", StringComparison.OrdinalIgnoreCase) == true` AND `duration >= 0` (skip running entries)
    - Upserts into `toggl_data` by `toggl_id`
    - Ensures `data_source` row exists for `"toggl_sleep"` (AC-5.6.6)
    - Writes `data_source_import_log`
    - Publishes `DataSourceImportCompletedMessage`
  - [x] Register in DI as singleton

- [x] **Task 5: Add Toggl API token setting to Settings page**
  - [x] Extend `ViewModels/SettingsViewModel.cs`:
    - `TogglApiToken string` property (masked — never show actual token in plain text, show only `•••••` after save)
    - `SaveTogglApiTokenCommand` — encrypts and stores via existing DPAPI/config pattern
    - `TestTogglConnectionCommand` — calls `TogglApiClient.TestConnectionAsync`
    - `IsTestingTogglConnection bool` property
    - `TogglConnectionTestResult string?` property ("Connected" or error message)
  - [x] Extend `Views/SettingsPage.xaml`: add "Data Sources" section with Toggl token field and test button

- [x] **Task 6: Implement `TogglSleepImportHandler`**
  - [x] Add `Services/TogglSleepImportHandler.cs` implementing `IDataSourceImportHandler`:
    - `SourceKey` returns `"toggl_sleep"`
    - `TriggerImportAsync(CancellationToken ct)`: opens a date-range picker dialog (same content dialog pattern as ICS export), then calls `ITogglSleepImportService.ImportAsync(start, end, ct)` after the user confirms
    - Surfaces errors via the existing InfoBar/notification system (same as AC-5.6.9)
    - Shows progress in the dialog while running (disables confirm button, shows spinner)
  - [x] Register `TogglSleepImportHandler` in `DataSourceImportHandlerRegistry` in `App.xaml.cs`

- [x] **Task 7: Unit tests**
  - [x] Add `GoogleCalendarManagement.Tests/Unit/Services/TogglSleepImportServiceTests.cs`
  - [x] `ImportAsync_WhenDescriptionContainsSleep_StoresEntry`
  - [x] `ImportAsync_WhenDescriptionDoesNotContainSleep_SkipsEntry`
  - [x] `ImportAsync_WhenEntryIsRunning_SkipsEntry` (negative duration)
  - [x] `ImportAsync_WritesImportLogOnSuccess`
  - [x] `ImportAsync_WritesImportLogOnFailure`
  - [x] `ImportAsync_PublishesDataSourceImportCompletedMessage`
  - [x] `ImportAsync_UpsertsByTogglId_NoDuplicates`
  - [x] Mock `ITogglApiClient` and `IDbContextFactory` — do not call real Toggl API in tests

---

## Dev Notes

### Toggl API v9

The current Toggl public API is v9 (as of 2026). The base URL is `https://api.track.toggl.com/api/v9/`. Authentication is HTTP Basic with the API token as the username and the string `"api_token"` as the password. The time entries endpoint is `GET /me/time_entries?start_date=YYYY-MM-DD&end_date=YYYY-MM-DD`. Note: Toggl's `end_date` is exclusive; pass `end + 1 day` to include the last selected day.

### API Token Storage

Reuse the existing `IConfigRepository` or `config` table (key-value store already in the schema and codebase). For encryption, reuse the `DpapiTokenStorageService` pattern or a similar DPAPI wrapper — do not store the token in plaintext.

### Running Entries

Toggl encodes running (not-yet-stopped) time entries with a negative `duration` equal to the negative Unix timestamp of the start time. Filter these out during import (`duration >= 0` check). They are not useful as completed sleep records.

### Sleep Detection

Filter on `description?.Contains("sleep", StringComparison.OrdinalIgnoreCase)`. This catches "sleep", "Sleep", "SLEEP", "Deep sleep", etc. Do NOT use regex in this story — simple substring match is sufficient.

### `IConfigRepository`

Check whether `IConfigRepository` (or equivalent) already exists in the codebase (look for usage of the `config` table entity). If not, a minimal `GetConfigValueAsync(string key)` / `SetConfigValueAsync(string key, string value)` service is needed. Given the `config` table and `Config` entity already exist, this is likely a small addition.

### Project Structure

```text
Data/
├── Entities/TogglEntry.cs                          # new
├── Configurations/TogglEntryConfiguration.cs       # new
├── CalendarDbContext.cs                            # add DbSet<TogglEntry>
└── Migrations/<timestamp>_AddTogglDataTable.cs    # new

Services/
├── ITogglApiClient.cs / TogglApiClient.cs          # new
├── TogglTimeEntryDto.cs                            # new
├── ITogglSleepImportService.cs                     # new
├── TogglSleepImportService.cs                      # new
├── TogglSleepImportResult.cs                       # new
└── TogglSleepImportHandler.cs                      # new (IDataSourceImportHandler)

ViewModels/SettingsViewModel.cs                     # extend: Toggl token + test only
Views/SettingsPage.xaml                             # extend: Data Sources section (token + test)

App.xaml.cs                                         # DI registration + handler registry

GoogleCalendarManagement.Tests/Unit/Services/
└── TogglSleepImportServiceTests.cs                 # new
```

### Prerequisites

- Story 5.1 must be complete (`data_source`, `data_source_import_log` tables, `IDataSourceRepository`)

### References

- [Epic 5 overview](../epic-overview.md) — Toggl Track Sleep import section
- [Story 5.1](./5-1-data-source-infrastructure.md) — `IDataSourceRepository`, `data_source_import_log`
- [Database schemas](../../_database-schemas.md) — `toggl_data` table definition
- [DpapiTokenStorageService.cs](../../../Services/DpapiTokenStorageService.cs) — token encryption pattern
- [SettingsViewModel.cs](../../../ViewModels/SettingsViewModel.cs) — existing settings VM to extend
- [SettingsPage.xaml.cs](../../../Views/SettingsPage.xaml.cs) — existing settings page
- [IcsExportService.cs](../../../Services/IcsExportService.cs) — async import/export pattern to follow
- Toggl Track API v9 reference: https://engineering.toggl.com/docs/api/time_entries

---

## Dev Agent Record

### Implementation Plan

- Follow the existing EF configuration-per-entity pattern and generate a focused `toggl_data` migration.
- Introduce a small `IConfigRepository` because the story requires token storage in the `config` table and no generic repository existed.
- Keep external Toggl calls behind `ITogglApiClient` so import-service tests never call the real API.
- Place import orchestration in `TogglSleepImportService` and keep UI-specific date selection/progress in `TogglSleepImportHandler`.

### Debug Log

- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --no-restore --filter TogglSleepImportServiceTests` initially hit a WinUI PRI generated-file move failure before compilation.
- Cleaned only the workspace Debug `bin`/`obj` output folders and reran focused tests.
- `dotnet ef migrations add AddTogglDataTable -p GoogleCalendarManagement.csproj -s GoogleCalendarManagement.csproj -- --platform x64`
- `dotnet build -p:Platform=x64 -p:WarningsNotAsErrors=NU1900` passed.
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --no-restore` passed: 299 tests.

### Completion Notes

- Added `toggl_data` entity, EF configuration, `DbSet`, and migration with only the new table, indexes, and nullable FK to `gcal_event`.
- Added encrypted config repository support and Settings UI for saving/testing the Toggl API token under `toggl_api_token`.
- Added Toggl API client using Basic auth, API v9 endpoints, inclusive date range handling, and one 429 retry.
- Added Toggl Sleep import service with sleep filtering, running-entry exclusion, upsert-by-`toggl_id`, data source registration, import logging, and completion message publication.
- Added Toggl Sleep import handler with date-range dialog, in-dialog progress, user-facing success/error messages, and startup registry wiring.
- Added left-panel import in-progress state so the row disables and shows loading while an import handler is running.
- Added a handler-backed virtual global-panel source row so Toggl Sleep can be imported before the first `data_source` row exists; the real row is created idempotently during import.

## File List

- `App.xaml.cs`
- `Data/CalendarDbContext.cs`
- `Data/Configurations/TogglEntryConfiguration.cs`
- `Data/Entities/TogglEntry.cs`
- `Data/Migrations/20260513175452_AddTogglDataTable.cs`
- `Data/Migrations/20260513175452_AddTogglDataTable.Designer.cs`
- `Data/Migrations/CalendarDbContextModelSnapshot.cs`
- `Services/ConfigRepository.cs`
- `Services/DataSourceImportHandlerRegistry.cs`
- `Services/IConfigRepository.cs`
- `Services/ITogglApiClient.cs`
- `Services/ITogglSleepImportService.cs`
- `Services/TogglApiClient.cs`
- `Services/TogglApiException.cs`
- `Services/TogglSleepImportHandler.cs`
- `Services/TogglSleepImportResult.cs`
- `Services/TogglSleepImportService.cs`
- `Services/TogglTimeEntryDto.cs`
- `ViewModels/DataSourceSummaryViewModel.cs`
- `ViewModels/DataSourcePanelViewModel.cs`
- `ViewModels/SettingsViewModel.cs`
- `Views/DataSourcePanelControl.xaml`
- `Views/SettingsPage.xaml`
- `Views/SettingsPage.xaml.cs`
- `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/ConfigRepositoryTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/TogglSleepImportServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs`
- `docs/epic-5-day-select-left-data-panel/stories/5-6-toggl-sleep-import.md`
- `docs/sprint-status.yaml`

## Change Log

- 2026-05-13: Implemented Toggl Sleep import, encrypted token settings, EF schema migration, import handler registration, progress UI, and automated coverage.

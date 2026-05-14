---
title: 'DB, Logs, and Credentials Relocate to Project Root; Extract Threshold Config'
type: 'refactor'
created: '2026-05-14'
status: 'ready-for-dev'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The SQLite database, log files, and OAuth credentials all live under `%LocalAppData%\GoogleCalendarManagement\` (hard to access during development), and 6 threshold constants are seeded into the DB `config` table but are never read at runtime — dead DB data rather than live configuration.

**Approach:** Move the database and backups to `database/`, logs to `logs/`, and credentials to `credentials/` — all at the project root, located at runtime by a shared `ProjectPaths.GetProjectRoot()` helper that walks up from `AppContext.BaseDirectory` until a `*.csproj` file is found. Extract the 6 constants into a static C# class, remove the seed data, and add a migration to delete the orphaned rows.

## Boundaries & Constraints

**Always:**
- Project root is found by walking up from `AppContext.BaseDirectory` checking each directory for a `*.csproj` file; fall back to `AppContext.BaseDirectory` if none is found.
- `ProjectPaths.GetProjectRoot()` must be a shared static helper (not private to `App.xaml.cs`) because `LoggingService.Configure()` is called before DI at app startup.
- Backups go to `database/backups/` (subdirectory of `database/`, not co-mingled with `calendar.db`).
- `Directory.CreateDirectory` must be called for `database/`, `database/backups/`, `logs/`, and `credentials/` before use.
- Migration `Down()` must re-insert the 6 deleted config rows to keep migrations reversible.
- The `config` table and `IConfigRepository` stay intact; only the 6 threshold seed rows are removed.

**Ask First:**
- If the project-root walk-up approach fails at runtime (e.g., published build), ask before choosing an alternative path strategy.

**Never:**
- Do not modify existing migration files.
- Do not create new `IConfigRepository` call sites for threshold keys — thresholds are code-only after this change.
- Do not use `Directory.GetCurrentDirectory()` — unreliable for WinUI3 apps.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh launch | No `database/`, `logs/`, `credentials/` folders exist | App creates all three (plus `database/backups/`) and opens `database/calendar.db` | Startup fails with clear error if folders cannot be created |
| Existing user | `calendar.db` in `%LocalAppData%` | App creates a new `database/calendar.db` (manual copy required — see Design Notes) | No automatic user data migration |
| Missing client secret | `credentials/client_secret.json` absent | Warning logged; app starts in disconnected state (existing behaviour) | N/A |
| Backup on migration | Migration runs on startup | Backup written to `database/backups/calendar_backup_*.db`; max 5 retained | N/A |
| EF tooling | `dotnet ef migrations add` from project root | Factory uses `database/design_time.db` at project root | N/A |
| Threshold migration | Existing DB has 6 threshold rows | `RemoveThresholdConfigRows` Up() deletes all 6 rows | N/A |

</frozen-after-approval>

## Code Map

- `Infrastructure/ProjectPaths.cs` — NEW shared static helper; used by LoggingService (pre-DI) and App.xaml.cs
- `Services/LoggingService.cs` — log folder path (~line 12); replace AppData construction with `ProjectPaths.GetProjectRoot()`
- `App.xaml.cs` — DB path (~lines 164–174), GoogleCalendarOptions construction, error dialog text (~line 117)
- `Services/GoogleCalendarOptions.cs` — constructor receives project root instead of AppData folder; `credentials/` and `client_secret.json` paths derive from it unchanged
- `Services/MigrationService.cs` — `_appDataDir` → `_backupDir = database/backups/` (~lines 17–113)
- `Data/CalendarDbContextFactory.cs` — design-time connection string (line 15)
- `Data/Configurations/ConfigConfiguration.cs` — 6 `HasData` seed entries (lines ~20–27)
- `Config/ImportThresholds.cs` — NEW static class with 6 const int fields
- `.gitignore` — replace `*.db`/`*.db-shm`/`*.db-wal` section with `database/`; add `logs/` entry

## Tasks & Acceptance

**Execution:**
- [ ] `Infrastructure/ProjectPaths.cs` *(new)* — Create `internal static class ProjectPaths` with a cached `public static string GetProjectRoot()` that walks up from `AppContext.BaseDirectory` looking for a directory containing a `*.csproj` file, caches the result in a `static string?` field, and falls back to `AppContext.BaseDirectory`
- [ ] `Services/LoggingService.cs` — Replace the `Environment.GetFolderPath` + `Path.Combine("GoogleCalendarManagement", "logs")` construction with `Path.Combine(ProjectPaths.GetProjectRoot(), "logs")`
- [ ] `App.xaml.cs` — Replace `localAppData` / `dbFolder` construction with `var projectRoot = ProjectPaths.GetProjectRoot()`; set `dbFolder = Path.Combine(projectRoot, "database")`; pass `projectRoot` (not `dbFolder`) to `new GoogleCalendarOptions(...)`; update error dialog `Content` string to reference `database/backups/`
- [ ] `Services/GoogleCalendarOptions.cs` — Rename constructor parameter from `appDataDirectory` to `projectRoot`; update `AppDataDirectory` property name to `ProjectRoot`; `CredentialsDirectoryPath` and `ClientSecretPath` derivations remain unchanged (they still append `credentials/` and `client_secret.json`)
- [ ] `Services/MigrationService.cs` — Change `_appDataDir` field to `_backupDir`; assign it `Path.Combine(Path.GetDirectoryName(_dbPath)!, "backups")`; add `Directory.CreateDirectory(_backupDir)` at the start of `CreateBackupAsync`; update all `_appDataDir` usages to `_backupDir`
- [ ] `Data/CalendarDbContextFactory.cs` — Change connection string to `"Data Source=database/design_time.db"`
- [ ] `Config/ImportThresholds.cs` *(new)* — Create `public static class ImportThresholds` with `public const int` fields: `MinEventDurationMinutes = 5`, `PhoneCoalesceGapMinutes = 15`, `YoutubeCoalesceGapMinutes = 30`, `CallMinDurationMinutes = 3`, `YoutubeCharLimitShort = 40`, `EightFifteenThreshold = 8`
- [ ] `Data/Configurations/ConfigConfiguration.cs` — Remove the entire `builder.HasData(...)` block containing all 6 threshold entries
- [ ] New EF migration `RemoveThresholdConfigRows` — Run `dotnet ef migrations add RemoveThresholdConfigRows`; write `Up()` with raw SQL `DELETE FROM config WHERE config_key IN ('min_event_duration_minutes','phone_coalesce_gap_minutes','youtube_coalesce_gap_minutes','call_min_duration_minutes','youtube_char_limit_short','eight_fifteen_threshold')`; write `Down()` re-inserting all 6 rows with original values and `updated_at = '2026-01-01 00:00:00'`
- [ ] `.gitignore` — Replace the `*.db`/`*.db-shm`/`*.db-wal` comment block with a `database/` entry and updated comment; add a `logs/` entry in the runtime artifacts section (note: `credentials/` is already covered by the existing `credentials/` and `**/credentials/` entries)

**Acceptance Criteria:**
- Given the app launches fresh, when startup completes, then `database/calendar.db`, `logs/app-*.txt`, and `credentials/` all exist at the project root
- Given a migration triggers a backup, when `CreateBackupAsync` runs, then the backup lands in `database/backups/` with at most 5 files retained
- Given `credentials/client_secret.json` is absent, when the app starts, then a warning is logged and the app runs in disconnected state
- Given an existing DB with the 6 threshold rows, when `RemoveThresholdConfigRows` runs, then those 6 rows are gone
- Given `dotnet ef migrations add` runs from the project root, when `CalendarDbContextFactory.CreateDbContext` is called, then it targets `database/design_time.db`
- Given `database/`, `logs/`, and `credentials/` exist, when `git status` runs, then none of them appear as untracked

## Design Notes

**Existing AppData data (one-time manual steps after implementation):**
- Copy `%LocalAppData%\GoogleCalendarManagement\calendar.db` → `[projectRoot]/database/calendar.db`
- Copy `%LocalAppData%\GoogleCalendarManagement\credentials\` → `[projectRoot]/credentials/`
- Old AppData folder can be deleted afterward.

**Column name in migration:** Verify the actual column name for `ConfigKey` from `CalendarDbContextModelSnapshot.cs` before writing the raw SQL — EF may generate it as `config_key` (snake_case).

## Verification

**Commands:**
- `dotnet build` — expected: 0 errors, 0 warnings
- `dotnet ef migrations script` — expected: script includes DELETE for the 6 config keys

**Manual checks:**
- Launch app from Visual Studio; confirm `database/calendar.db`, `logs/`, and `credentials/` appear at project root
- Confirm none of those folders appear in `git status`

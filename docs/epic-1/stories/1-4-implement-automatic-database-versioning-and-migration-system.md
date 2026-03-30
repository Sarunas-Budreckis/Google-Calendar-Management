# Story 1.4: Implement Automatic Database Versioning and Migration System

Status: done

## Story

As a **developer**,
I want **automatic database schema migrations applied on app startup with pre-migration backups and integrity checks**,
So that **schema updates are applied seamlessly when the app is updated, and the local database is protected from data loss**.

## Acceptance Criteria

**Given** a new or existing database
**When** the application starts
**Then** pending EF Core migrations are automatically detected and applied via `MigrationService`

**And** migration safety checks are performed:
- If pending migrations exist, a backup is created first: `calendar_backup_{yyyyMMdd_HHmmss}_pre-migration.db` in `%LOCALAPPDATA%\GoogleCalendarManagement\`
- Backup creation uses `File.Copy` (atomic at OS level for SQLite with WAL mode closed)
- Migration failures are caught, logged, and re-thrown — app surfaces error to user
- Old backups are cleaned up: only the 5 most recent `calendar_backup_*.db` files are kept

**And** database schema version is tracked:
- After successful migration, `system_state` row with `state_name = 'DatabaseSchemaVersion'` is upserted with the name of the last applied migration
- If no migrations are pending, the row is left unchanged (not overwritten)

**And** database integrity is validated on every startup:
- `PRAGMA integrity_check;` runs after migration step (whether or not migrations were applied)
- If result is not `"ok"`, integrity failure is logged and an `InvalidOperationException` is thrown
- App surfaces this error to user with a message to copy `calendar.db` for recovery

**And** migration history is auditable:
- EF Core `__EFMigrationsHistory` table tracks all applied migrations
- `MigrationService` logs: pending count, backup path, each applied migration name, and final "DatabaseSchemaVersion" state
- All log entries use structured `ILogger<MigrationService>` (not Console.Write)

**And** the service is integrated via dependency injection:
- `IMigrationService` interface and `MigrationService` implementation registered in `App.xaml.cs`
- `MigrationService.RunStartupAsync()` called explicitly in `App.OnLaunched` (not via `IHostedService` — WinUI 3 does not use Generic Host by default)
- Integration tested: migration applies, backup file created, `DatabaseSchemaVersion` row present

## Tasks / Subtasks

- [x] Create `IMigrationService` interface and `MigrationService` class (AC: 1, 2, 3, 4, 5)
  - [x] Create `Services/IMigrationService.cs` with `RunStartupAsync()`, `ApplyMigrationsAsync()`, `CheckDatabaseIntegrityAsync()`, `CreateBackupAsync(string reason)` methods
  - [x] Create `Services/MigrationService.cs` implementing `IMigrationService`
  - [x] Implement `ApplyMigrationsAsync()`: call `GetPendingMigrationsAsync()`, if any → `CreateBackupAsync("pre-migration")` → `MigrateAsync()` → upsert `DatabaseSchemaVersion` in `system_state`
  - [x] Implement `CheckDatabaseIntegrityAsync()`: open connection, run `PRAGMA integrity_check;`, return `result == "ok"`
  - [x] Implement `CreateBackupAsync(string reason)`: `File.Copy` to timestamped backup path, then delete backups beyond 5 most recent (ordered by `File.GetCreationTimeUtc` descending)
  - [x] Implement `RunStartupAsync()`: call `ApplyMigrationsAsync()` then `CheckDatabaseIntegrityAsync()`, throw `InvalidOperationException` if integrity check fails

- [x] Register `MigrationService` in DI and wire to app startup (AC: 5)
  - [x] Add `services.AddScoped<IMigrationService, MigrationService>()` in `App.xaml.cs` DI setup
  - [x] In `App.OnLaunched`, resolve `IMigrationService` from service provider and `await service.RunStartupAsync()`
  - [x] Wrap `RunStartupAsync()` call in try/catch — on failure, show error dialog with "Database error on startup. Please contact support or restore from backup." then exit app

- [x] Write integration tests in `GoogleCalendarManagement.Tests/Integration/MigrationServiceTests.cs` (AC: 1-5)
  - [x] Test: `ApplyMigrationsAsync_OnFreshDatabase_AppliesMigrationsSuccessfully` — use temp-file SQLite, assert `GetAppliedMigrationsAsync()` returns expected migrations
  - [x] Test: `ApplyMigrationsAsync_WhenPendingMigrations_CreatesBackupFile` — use real temp file `calendar.db`, assert backup file created with correct naming pattern
  - [x] Test: `ApplyMigrationsAsync_AfterMigration_DatabaseSchemaVersionRowPresent` — assert `system_state` row `state_name = 'DatabaseSchemaVersion'` exists with non-empty value
  - [x] Test: `CheckDatabaseIntegrityAsync_OnHealthyDatabase_ReturnsTrue` — run integrity check on valid temp-file SQLite, assert true
  - [x] Test: `CreateBackupAsync_WhenMoreThanFiveBackupsExist_DeletesOldest` — create 5 fake backup files + calendar.db in temp dir, call CreateBackupAsync, assert only 5 remain
  - [x] Test: `ApplyMigrationsAsync_WhenNoMigrationsPending_DoesNotCreateBackup` — apply migrations once, call again, assert no backup created
  - [x] Run all tests; verify 100% pass (20/20)

- [x] Final validation (All ACs)
  - [x] Build Debug — no errors (0 warnings, 0 errors)
  - [x] Run full test suite — 20/20 passed

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- Entity Framework Core 9.0.12 with SQLite provider (already installed from Story 1.2)
- `ILogger<MigrationService>` from `Microsoft.Extensions.Logging` (already in DI from Story 1.1)
- No new NuGet packages required

**Critical Architecture Decisions:**
- **No IHostedService:** WinUI 3 apps do not use the .NET Generic Host by default. `MigrationService` must be invoked explicitly in `App.OnLaunched`, not via `AddHostedService<T>()`. Confirm with Story 1.1 DI setup.
- **Backup uses File.Copy, not SQLite Online Backup API:** Simpler and sufficient for startup migrations where the database is not yet open for writes. WAL mode from Story 1.2 ensures the source file is consistent.
- **Integrity check uses raw PRAGMA:** EF Core does not expose `integrity_check` via managed API. Use `_context.Database.GetDbConnection().CreateCommand()` pattern.
- **SystemState upsert (not Config):** The schema version is stored in `system_state` (Story 1.3), not the `config` table. Use `SingleOrDefaultAsync` on `state_name = 'DatabaseSchemaVersion'` for upsert logic.
- **Startup error handling:** Migration or integrity failure must block app startup. Show a WinUI 3 ContentDialog or MessageDialog before calling `Application.Current.Exit()`.

**`IMigrationService` Interface:**
```csharp
namespace GoogleCalendarManagement.Services;

public interface IMigrationService
{
    Task RunStartupAsync();
    Task ApplyMigrationsAsync();
    Task<bool> CheckDatabaseIntegrityAsync();
    Task CreateBackupAsync(string backupReason);
}
```

**`MigrationService` Skeleton:**
```csharp
namespace GoogleCalendarManagement.Services;

public class MigrationService : IMigrationService
{
    private readonly CalendarDbContext _context;
    private readonly ILogger<MigrationService> _logger;

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoogleCalendarManagement");

    public MigrationService(CalendarDbContext context, ILogger<MigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RunStartupAsync()
    {
        await ApplyMigrationsAsync();
        var isHealthy = await CheckDatabaseIntegrityAsync();
        if (!isHealthy)
            throw new InvalidOperationException("Database integrity check failed on startup.");
    }

    public async Task ApplyMigrationsAsync()
    {
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToList();
        if (!pending.Any())
        {
            _logger.LogInformation("No pending migrations.");
            return;
        }
        _logger.LogInformation("Found {Count} pending migrations: {Names}", pending.Count, string.Join(", ", pending));
        await CreateBackupAsync("pre-migration");
        await _context.Database.MigrateAsync();
        _logger.LogInformation("Successfully applied {Count} migrations.", pending.Count);

        var latestApplied = (await _context.Database.GetAppliedMigrationsAsync()).Last();
        var state = await _context.SystemStates.SingleOrDefaultAsync(s => s.StateName == "DatabaseSchemaVersion");
        if (state == null)
            _context.SystemStates.Add(new SystemState { StateName = "DatabaseSchemaVersion", StateValue = latestApplied, UpdatedAt = DateTime.UtcNow });
        else
        {
            state.StateValue = latestApplied;
            state.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<bool> CheckDatabaseIntegrityAsync()
    {
        try
        {
            using var conn = _context.Database.GetDbConnection();
            await _context.Database.OpenConnectionAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = await cmd.ExecuteScalarAsync();
            var passed = result?.ToString() == "ok";
            _logger.LogInformation("Database integrity check: {Result}", passed ? "OK" : "FAILED");
            return passed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database integrity check threw an exception.");
            return false;
        }
    }

    public Task CreateBackupAsync(string backupReason)
    {
        var dbPath = Path.Combine(AppDataDir, "calendar.db");
        var backupName = $"calendar_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{backupReason}.db";
        var backupPath = Path.Combine(AppDataDir, backupName);

        File.Copy(dbPath, backupPath, overwrite: false);
        _logger.LogInformation("Created backup: {BackupPath}", backupPath);

        var oldBackups = Directory.GetFiles(AppDataDir, "calendar_backup_*.db")
            .OrderByDescending(File.GetCreationTimeUtc)
            .Skip(5);
        foreach (var old in oldBackups)
        {
            File.Delete(old);
            _logger.LogInformation("Deleted old backup: {Path}", old);
        }
        return Task.CompletedTask;
    }
}
```

**DI Registration in `App.xaml.cs`:**
```csharp
services.AddScoped<IMigrationService, MigrationService>();
```

**Startup Call in `App.OnLaunched`:**
```csharp
protected override async void OnLaunched(LaunchActivatedEventArgs args)
{
    // ... window setup ...
    using var scope = _serviceProvider.CreateScope();
    var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
    try
    {
        await migrationService.RunStartupAsync();
    }
    catch (Exception ex)
    {
        var dialog = new ContentDialog
        {
            Title = "Startup Error",
            Content = $"Database error: {ex.Message}\n\nPlease restore from a backup in {AppDataDir}.",
            CloseButtonText = "Exit"
        };
        dialog.XamlRoot = _window.Content.XamlRoot;
        await dialog.ShowAsync();
        Application.Current.Exit();
    }
}
```

### Project Structure After Story 1.4

```
GoogleCalendarManagement/
├── Services/
│   ├── IMigrationService.cs              # New: interface
│   └── MigrationService.cs              # New: implementation
├── App.xaml.cs                          # Updated: DI registration + OnLaunched startup call
├── Data/
│   ├── CalendarDbContext.cs             # Unchanged from Story 1.3
│   ├── Entities/                        # Unchanged from Story 1.3
│   └── Configurations/                  # Unchanged from Story 1.3
GoogleCalendarManagement.Tests/
└── Integration/
    ├── DatabaseInfrastructureTests.cs   # From Story 1.2
    ├── SchemaTests.cs                   # From Story 1.3
    └── MigrationServiceTests.cs        # New: Story 1.4 tests
```

### References

**Source Documents:**
- [Epic 1: Story 1.4 Definition](../../epics.md#story-14-implement-automatic-database-versioning-and-migration-system)
- [Epic 1 Tech Spec: MigrationService](../tech-spec.md#migrationservice)
- [Epic 1 Tech Spec: Startup Sequence](../tech-spec.md#database-migration-workflow-story-14)
- [Architecture: Data Integrity](../architecture.md)
- [PRD NFR-D1](../PRD.md) — Data loss prevention (auto-backup before migrations)
- [PRD NFR-D3](../PRD.md) — Database integrity check on startup

**Specific Technical Mandates:**
- **NFR-D1 (Data Loss Prevention):** Backup before any migration; keep last 5 backups
- **NFR-D3 (Database Integrity):** `PRAGMA integrity_check` on every startup
- **NFR-I3 (Error Handling):** Migration failure must be surfaced to user before exit

**Prerequisites:**
- Story 1.2 complete — WAL mode, FK enforcement, `CalendarDbContext` DI registered
- Story 1.3 complete — `system_state` table and `SystemState` entity exist (required for `DatabaseSchemaVersion` upsert)

### Common Troubleshooting

- If `File.Copy` fails on backup: ensure WAL mode checkpoint has completed; add `PRAGMA wal_checkpoint(FULL)` before copy if needed
- If `GetPendingMigrationsAsync` throws on fresh database: ensure `MigrateAsync()` is called at least once in Story 1.2 to create `__EFMigrationsHistory`
- If `SystemStates` DbSet is not found: ensure Story 1.3 is fully applied and `CalendarDbContext` has the `SystemStates` DbSet
- If ContentDialog crashes (XamlRoot null): window must be activated before showing dialog; call `_window.Activate()` before `RunStartupAsync()`
- If backup cleanup deletes wrong files: `File.GetCreationTimeUtc` is used (not last-write-time); on copy the OS sets creation time to copy time, which is the intended behaviour

### Testing Strategy

**Story 1.4 Testing Scope:**
- Integration tests use real temp-file SQLite (`Path.GetTempFileName()`) for backup and file I/O tests
- In-memory SQLite (`Data Source=:memory:`) for migration application and integrity tests
- No Moq needed — real `CalendarDbContext` and real file system for backup tests
- Each test cleans up temp files in `Dispose` or finally block

**Key Test Cases:**
1. `ApplyMigrationsAsync_OnFreshDatabase_AppliesMigrationsSuccessfully` — fresh temp db, call `ApplyMigrationsAsync()`, assert applied migrations count > 0
2. `ApplyMigrationsAsync_WhenPendingMigrations_CreatesBackupFile` — real temp `calendar.db`, assert `calendar_backup_*_pre-migration.db` exists after call
3. `ApplyMigrationsAsync_AfterMigration_DatabaseSchemaVersionRowPresent` — assert `SystemStates` contains row with `state_name = 'DatabaseSchemaVersion'`
4. `CheckDatabaseIntegrityAsync_OnHealthyDatabase_ReturnsTrue` — apply migrations, run check, assert `true`
5. `CreateBackupAsync_WhenMoreThanFiveBackupsExist_DeletesOldest` — pre-create 6 backup files, call `CreateBackupAsync`, assert directory contains exactly 6 (5 old + 1 new), oldest deleted

### Change Log

**Version 1.1 - Implementation Complete (2026-03-27)**
- All tasks and subtasks completed; 20/20 tests passing
- `IMigrationService` + `MigrationService` created in `Services/`
- `App.xaml.cs` updated: window activated before startup migration, ContentDialog on error
- 6 integration tests added to `MigrationServiceTests.cs`

**Version 1.0 - Initial Draft (2026-03-27)**
- Created from Epic 1, Story 1.4 definition in epics.md and tech-spec.md
- Dev Notes include full MigrationService implementation, DI wiring, and startup error handling
- Clarified IHostedService vs explicit call for WinUI 3 (no Generic Host)
- Aligned SystemState upsert with Story 1.3 entity (system_state table, state_name UNIQUE)

## Dev Agent Record

### Context Reference

- [Story Context XML](1-4-implement-automatic-database-versioning-and-migration-system.context.xml) - Generated 2026-03-27

### Completion Notes (2026-03-27)

**Implemented by:** Amelia (Dev Agent)

**Files created:**
- `Services/IMigrationService.cs` — Interface with `RunStartupAsync()`, `ApplyMigrationsAsync()`, `CheckDatabaseIntegrityAsync()`, `CreateBackupAsync(string)`
- `Services/MigrationService.cs` — Full implementation; derives db path and appDataDir from injected `DatabaseOptions` (enabling testability without hardcoded paths)
- `GoogleCalendarManagement.Tests/Integration/MigrationServiceTests.cs` — 6 integration tests; all passing

**Files modified:**
- `App.xaml.cs` — Registered `IMigrationService` as scoped; moved window creation/activation BEFORE `RunStartupAsync()` call (required for `ContentDialog.XamlRoot`); replaced direct `MigrateAsync()` with `RunStartupAsync()` in try/catch with `ContentDialog` + `Application.Current.Exit()` on failure

**Key decisions:**
- `DatabaseOptions` injected into `MigrationService` (instead of hardcoded AppDataDir) to make backup path configurable in integration tests
- `File.Exists` check in `CreateBackupAsync` handles fresh installs where no `.db` file exists yet (skip backup, not an error)
- Connection opened via `_context.Database.OpenConnectionAsync()` in `CheckDatabaseIntegrityAsync` to satisfy C-6 (raw PRAGMA requires explicit open)
- Window activated before `RunStartupAsync()` per constraint C-8 (ContentDialog requires `XamlRoot`)

**Test results:** Passed 20/20 (14 prior + 6 new, Debug build)

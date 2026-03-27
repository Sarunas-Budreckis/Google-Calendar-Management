# Story 1.2: Configure SQLite Database with Entity Framework Core

Status: Done

## Story

As a **developer**,
I want **Entity Framework Core configured with SQLite provider**,
So that **I have a robust ORM for local data persistence**.

## Acceptance Criteria

**Given** the .NET 9 WinUI 3 project from Story 1.1
**When** I configure Entity Framework Core with SQLite
**Then** the application creates and connects to a SQLite database on first launch

**And** the DbContext is properly configured:
- `CalendarDbContext` class created with dependency injection registration
- Database file stored in `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db`
- Connection string managed via configuration (not hardcoded)
- EF Core migrations system enabled

**And** database infrastructure is testable:
- `CalendarDbContext` can be instantiated in unit tests (using in-memory SQLite)
- Database file is created at the expected location on first launch
- Basic CRUD round-trip operations succeed
- Database schema version is tracked via EF Core migrations

## Tasks / Subtasks

- [x] Install required NuGet packages (AC: all)
  - [x] Add `Microsoft.EntityFrameworkCore.Sqlite` v9.0.12 to main project
  - [x] Add `Microsoft.EntityFrameworkCore.Design` v9.0.12 to main project
  - [x] Add `Microsoft.EntityFrameworkCore.Tools` v9.0.12 to main project
  - [x] Add `Microsoft.EntityFrameworkCore.Sqlite` to test project (for in-memory SQLite)
  - [x] Verify packages restore without conflicts

- [x] Create `CalendarDbContext` (AC: 2)
  - [x] Create `Data/CalendarDbContext.cs` in main project
  - [x] Inherit from `DbContext`; add constructor accepting `DbContextOptions<CalendarDbContext>`
  - [x] Override `OnModelCreating` with fluent API configuration placeholder
  - [x] Enable WAL mode via `OnConfiguring` / SQLite PRAGMA for crash recovery (NFR-D1)
  - [x] Enable foreign key enforcement: `PRAGMA foreign_keys = ON`

- [x] Configure database path and connection string (AC: 2)
  - [x] Resolve `%LOCALAPPDATA%\GoogleCalendarManagement\` at runtime in App.xaml.cs
  - [x] Ensure directory is created if it does not exist before opening connection
  - [x] Store resolved path in a typed `DatabaseOptions` configuration class (not magic string)

- [x] Register `CalendarDbContext` with DI (AC: 2)
  - [x] Add `services.AddDbContext<CalendarDbContext>(...)` in App.xaml.cs DI setup
  - [x] Pass computed connection string from `DatabaseOptions`
  - [x] Verify context can be resolved from service provider at startup

- [x] Apply initial migration on startup (AC: 2, 4)
  - [x] Create initial EF Core migration: `dotnet ef migrations add InitialCreate`
  - [x] Call `dbContext.Database.MigrateAsync()` during app startup (non-blocking)
  - [x] Verify migration creates the database file at the expected path

- [x] Write integration tests for database infrastructure (AC: 3)
  - [x] Create `DatabaseInfrastructureTests.cs` in `Tests/Integration/`
  - [x] Test: `CalendarDbContext_CanBeInstantiated_WithInMemorySqlite`
  - [x] Test: `Database_CreatesFile_AtExpectedPath` (verifies AppData path)
  - [x] Test: `Database_CanExecute_BasicCrudOperations` (insert/read/delete a test entity)
  - [x] Test: `Database_MigrationApplied_SchemaVersionTracked` (verifies `__EFMigrationsHistory` table exists)
  - [x] Run all tests; verify 100% pass (9/9 passed)

- [x] Validate final database setup (All ACs)
  - [x] Build in Debug and Release — no errors
  - [x] Launch app; verify `calendar.db` created at `%LOCALAPPDATA%\GoogleCalendarManagement\`
  - [x] Confirm `__EFMigrationsHistory` table exists with `InitialCreate` entry
  - [x] Run all tests and verify they pass

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- .NET 9.0.12 / C# 13
- Entity Framework Core 9.0.12 with SQLite provider
- SQLite embedded database (no server required)
- Target: `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db`

**Critical Architecture Decisions:**
- **Folder vs. Separate Project:** Architecture doc targets a separate `GoogleCalendarManagement.Data/` library, but Story 1.1 established a single project with a `/Data` folder. Place `CalendarDbContext.cs` in the existing `/Data` folder for now; refactoring to a separate library is a future concern.
- **WAL Mode:** Must be enabled — required by NFR-D1 (crash recovery). Set via SQLite PRAGMA after connection open.
- **Foreign Key Enforcement:** SQLite does not enforce FK constraints by default. Enable via `PRAGMA foreign_keys = ON` in `OnConfiguring`.
- **DI Scope:** `CalendarDbContext` should be registered as **scoped** (default for `AddDbContext`) — never singleton.
- **Database Factory for Tests:** Use `DbContextOptionsBuilder` with `UseInMemoryDatabase` (or SQLite in-memory `Data Source=:memory:`) to keep tests fast and isolated.

**NuGet Packages Required:**
```xml
<!-- Main Project -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.12" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.12" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.12" />

<!-- Test Project -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.12" />
```

**CalendarDbContext Skeleton:**
```csharp
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Data;

public class CalendarDbContext : DbContext
{
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options)
        : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) return;
        // WAL mode and FK enforcement applied via connection string or interceptor
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configuration — populated in Story 1.3
        base.OnModelCreating(modelBuilder);
    }
}
```

**App.xaml.cs DI registration snippet:**
```csharp
var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
var dbFolder = Path.Combine(localAppData, "GoogleCalendarManagement");
Directory.CreateDirectory(dbFolder);
var dbPath = Path.Combine(dbFolder, "calendar.db");

services.AddDbContext<CalendarDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath};Mode=WALJournal"));
```

**WAL Mode (alternative — via raw connection):**
```csharp
// Ensure WAL mode on first connection
using var connection = new SqliteConnection(connectionString);
connection.Open();
using var cmd = connection.CreateCommand();
cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
cmd.ExecuteNonQuery();
```

**Test Factory Pattern:**
```csharp
private static CalendarDbContext CreateInMemoryContext()
{
    var options = new DbContextOptionsBuilder<CalendarDbContext>()
        .UseSqlite("Data Source=:memory:")
        .Options;
    var ctx = new CalendarDbContext(options);
    ctx.Database.OpenConnection();
    ctx.Database.EnsureCreated();
    return ctx;
}
```

### Project Structure After Story 1.2

```
GoogleCalendarManagement/
├── Data/
│   ├── CalendarDbContext.cs          # Added in Story 1.2
│   └── Migrations/                   # Generated by EF Core
│       ├── <timestamp>_InitialCreate.cs
│       └── CalendarDbContextModelSnapshot.cs
├── App.xaml.cs                       # Updated: DI + DB path setup
GoogleCalendarManagement.Tests/
└── Integration/
    └── DatabaseInfrastructureTests.cs  # Added in Story 1.2
```

### References

**Source Documents:**
- [Epic 1: Story 1.2 Definition](../epics.md#story-12-configure-sqlite-database-with-entity-framework-core)
- [Architecture: Data Layer](../architecture.md#project-structure) — `CalendarDbContext` location
- [Architecture: Technology Stack](../architecture.md#technology-stack-details) — EF Core 9.0.12 versions
- [Architecture: Service Registration](../architecture.md#service-registration) — `AddDbContext` pattern
- [Architecture: Database Overview](../architecture.md#database-overview) — WAL mode, FK enforcement, migrations

**Specific Technical Mandates:**
- **NFR-D1 (Crash Recovery):** WAL mode required — [PRD §8 Non-Functional Requirements](../PRD.md#8-non-functional-requirements)
- **NFR-D3 (Database Integrity):** Foreign key constraints enforced
- **FR-8.1 (SQLite Storage):** Local database at AppData path — [PRD §4.8 Data & Configuration](../PRD.md#48-data--configuration)

**Prerequisites:**
- Story 1.1 complete — project structure, DI container, and test project must exist
- .NET EF Core CLI tools installed: `dotnet tool install --global dotnet-ef`

**Common Troubleshooting:**
- If `dotnet ef migrations add` fails: ensure `Microsoft.EntityFrameworkCore.Design` is installed in the startup project
- If WAL files don't appear: verify `journal_mode=WAL` PRAGMA is executing after connection open
- If in-memory SQLite tests fail: ensure `ctx.Database.OpenConnection()` is called before `EnsureCreated()`
- If FK violations aren't caught: verify `PRAGMA foreign_keys=ON` is set per-connection (SQLite resets per connection)

### Testing Strategy

**Story 1.2 Testing Scope:**
- Integration tests hit a real SQLite in-memory database (no mocks for DB layer)
- Tests verify infrastructure plumbing, not business logic (no entities yet beyond a stub)
- Story 1.3 will add entity-level tests once the schema is defined

**Key Test Cases:**
1. `CalendarDbContext_CanBeInstantiated_WithInMemorySqlite` — DI and ctor work
2. `Database_CreatesFile_AtExpectedPath` — AppData path resolution
3. `Database_CanExecute_BasicCrudOperations` — EF Core round-trip
4. `Database_MigrationApplied_SchemaVersionTracked` — migrations machinery works

### Change Log

**Version 1.0 - Initial Draft (2026-03-27)**
- Created from Epic 1, Story 1.2 definition in epics.md
- Extracted acceptance criteria and expanded into testable subtasks
- Added Dev Notes aligned with architecture.md EF Core patterns
- Included WAL mode, FK enforcement, and in-memory test factory guidance

## Dev Agent Record

### Context Reference

- [Story Context XML](1-2-configure-sqlite-database-with-entity-framework-core.context.xml) - Generated 2026-03-27

### Completion Notes (2026-03-27)

**Implemented by:** Amelia (Dev Agent)

**Files created:**
- `Data/CalendarDbContext.cs` — DbContext with no DbSets (Story 1.3 adds entities)
- `Data/DatabaseOptions.cs` — Typed config class holding the connection string
- `Data/SqliteConnectionInterceptor.cs` — Internal interceptor that sets `PRAGMA journal_mode=WAL` and `PRAGMA foreign_keys=ON` per connection (constraints C-1, C-2)
- `Data/CalendarDbContextFactory.cs` — IDesignTimeDbContextFactory required for `dotnet ef` with WinUI 3 WinExe projects
- `Data/Migrations/20260327202425_InitialCreate.cs` + Designer + Snapshot — Empty initial migration establishing migration infrastructure
- `GoogleCalendarManagement.Tests/Integration/DatabaseInfrastructureTests.cs` — 6 integration tests (9 total with smoke tests); all pass

**Files modified:**
- `GoogleCalendarManagement.csproj` — Added EF Core Sqlite, Design, Tools v9.0.12
- `GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj` — Updated TFM to `net9.0-windows10.0.19041.0`, added EF Core Sqlite, added ProjectReference to main project
- `App.xaml.cs` — `OnLaunched` made `async void`; `ConfigureServices` extended with DB path resolution, `DatabaseOptions` singleton, `AddDbContext` with `SqliteConnectionInterceptor`; `MigrateAsync()` called on startup

**Key decisions:**
- WAL mode: Set via `PRAGMA journal_mode=WAL` in the interceptor (not via connection string — `Journal Mode` is not a valid Microsoft.Data.Sqlite keyword)
- Test project TFM: Changed to `net9.0-windows10.0.19041.0` to enable ProjectReference from test to WinUI 3 main project
- Migration output dir: `Data/Migrations/` (moved from default `Migrations/`); namespace updated to `GoogleCalendarManagement.Data.Migrations`
- `SqliteConnection.ClearAllPools()` called in file-based test cleanup to release SQLite connection pool before deleting temp files

**Test results:** Passed 9/9 (Debug build)

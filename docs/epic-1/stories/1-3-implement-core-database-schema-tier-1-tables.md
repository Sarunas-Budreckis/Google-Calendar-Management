# Story 1.3: Implement Core Database Schema (Tier 1 Tables)

Status: Done

## Story

As a **developer**,
I want **the Tier 1 database schema defined as EF Core entities with fluent API configurations**,
So that **the application can persist and retrieve Google Calendar events, version history, save states, and app metadata locally**.

## Acceptance Criteria

**Given** Entity Framework Core configured from Story 1.2
**When** I create the Tier 1 entity model and apply the migration
**Then** the following 7 tables are created in SQLite with all columns and indexes from `_database-schemas.md`:
- `gcal_event` — Google Calendar event cache
- `gcal_event_version` — Full version history for rollback
- `save_state` — Snapshot points for rollback
- `audit_log` — Complete operation audit trail
- `config` — Application configuration key-value store
- `data_source_refresh` — API sync tracking and status indicators
- `system_state` — Application-level state storage

**And** the entity model is correctly structured:
- 7 C# entity classes created in `Data/Entities/`
- 7 fluent API configuration classes created in `Data/Configurations/` (no data annotations on entities)
- `CalendarDbContext` updated with 7 `DbSet<T>` properties
- `OnModelCreating` applies all 7 configurations via `modelBuilder.ApplyConfigurationsFromAssembly()`

**And** the migration applies cleanly:
- `dotnet ef migrations add Phase1Schema` completes without errors
- All 7 tables created with correct columns, types, and defaults
- All indexes from schema doc present: `idx_gcal_event_date`, `idx_gcal_recurring`, `idx_gcal_source`, `idx_gcal_app_created`, `idx_version_event`, `idx_audit_timestamp`, `idx_audit_operation`, `idx_refresh_source`, `idx_refresh_date`
- Foreign key constraint enforced: `gcal_event_version.gcal_event_id → gcal_event.gcal_event_id`
- Default `config` values seeded via `HasData` for 6 application settings

**And** the schema is integration-tested:
- All 7 tables exist after `EnsureCreated()` on in-memory SQLite
- CRUD round-trip succeeds for `GcalEvent` entity
- Version history insert succeeds with valid `gcal_event_id` foreign key
- Config seed data is present after migration

## Tasks / Subtasks

- [x] Create entity classes in `Data/Entities/` (AC: 1, 2)
  - [x] Create `Data/Entities/GcalEvent.cs` — all columns from schema including Tier 1-3 nullable fields
  - [x] Create `Data/Entities/GcalEventVersion.cs` — version snapshot + `gcal_event_id` FK
  - [x] Create `Data/Entities/SaveState.cs` — save/restore snapshot with JSON field
  - [x] Create `Data/Entities/AuditLog.cs` — operation audit trail
  - [x] Create `Data/Entities/Config.cs` — typed key-value configuration store
  - [x] Create `Data/Entities/DataSourceRefresh.cs` — sync tracking per source
  - [x] Create `Data/Entities/SystemState.cs` — app-level key-value state

- [x] Create fluent configuration classes in `Data/Configurations/` (AC: 2, 3)
  - [x] Create `GcalEventConfiguration.cs`: table name, PK, column names/types, defaults, all 4 indexes
  - [x] Create `GcalEventVersionConfiguration.cs`: table name, PK (autoincrement), FK to GcalEvent, index
  - [x] Create `SaveStateConfiguration.cs`: table name, PK (autoincrement), required fields
  - [x] Create `AuditLogConfiguration.cs`: table name, PK (autoincrement), 2 indexes
  - [x] Create `ConfigConfiguration.cs`: table name, PK, HasData seed for 6 default config values
  - [x] Create `DataSourceRefreshConfiguration.cs`: table name, PK (autoincrement), 2 indexes
  - [x] Create `SystemStateConfiguration.cs`: table name, PK (autoincrement), unique constraint on `state_name`

- [x] Update `CalendarDbContext.cs` (AC: 2)
  - [x] Add 7 `DbSet<T>` properties for all Tier 1 entities
  - [x] Replace `OnModelCreating` placeholder comment with `modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly)`

- [x] Create EF Core migration (AC: 3)
  - [x] Run `dotnet ef migrations add Phase1Schema --project GoogleCalendarManagement.csproj`
  - [x] Review generated migration SQL — verify all 7 tables, indexes, FK, and seed data are present
  - [x] Run `dotnet ef database update` to verify migration applies without errors

- [x] Write integration tests in `GoogleCalendarManagement.Tests/Integration/` (AC: 4)
  - [x] Create `SchemaTests.cs`
  - [x] Test: `AllPhase1Tables_ExistAfterEnsureCreated` — query `sqlite_master` for all 7 table names
  - [x] Test: `GcalEvent_CrudRoundTrip_Succeeds` — insert, read, update, delete a GcalEvent
  - [x] Test: `GcalEventVersion_ForeignKey_EnforcedOnInsert` — verify FK constraint prevents orphan version rows
  - [x] Test: `Config_SeedData_PresentAfterMigration` — verify 6 default config entries exist
  - [x] Run all tests; verify 100% pass

- [x] Final validation (All ACs)
  - [x] Build in Debug and Release — no errors
  - [x] Inspect `calendar.db` with DB Browser for SQLite — verify 7 tables, columns, and indexes
  - [x] Confirm seed data in `config` table
  - [x] Confirm `__EFMigrationsHistory` contains both `InitialCreate` and `Phase1Schema`
  - [x] Run full test suite and verify all pass

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- Entity Framework Core 9.0.12 with SQLite provider (already installed in Story 1.2)
- C# 13 / .NET 9 with nullable reference types enabled
- All entities in `Data/Entities/` folder; all configurations in `Data/Configurations/`

**Critical Architecture Decisions:**
- **Fluent API only:** No data annotations (`[Required]`, `[Column]`, etc.) on entity classes. All configuration via `IEntityTypeConfiguration<T>` implementations. Per architecture.md decision.
- **snake_case table/column names:** SQLite tables use `gcal_event` not `GcalEvent`. Every configuration class must call `ToTable("snake_case_name")` and `HasColumnName("snake_case_col")` for every property.
- **Phase gating:** ONLY 7 Tier 1 tables. Do NOT define `PendingEvent`, `TogglData`, `YouTubeData`, `CallLogData`, `GeneratedEventSource`, `DateState`, `TrackedGap`, or `WeeklyState` entities — those are Tier 2/3.
- **`source_system` on GcalEvent:** This is a Tier 3 field but is defined now (nullable TEXT) to match the schema document and avoid a future breaking migration.
- **Integer PKs as AUTOINCREMENT:** `GcalEventVersion`, `SaveState`, `AuditLog`, `DataSourceRefresh`, `SystemState` use `INTEGER PRIMARY KEY AUTOINCREMENT`. In EF Core, configure with `.ValueGeneratedOnAdd()`.
- **String PK:** `GcalEvent.GcalEventId` and `Config.ConfigKey` are string PKs. Mark as `.ValueGeneratedNever()` so EF Core does not attempt to generate values.
- **Config seeding:** Use `HasData` in `ConfigConfiguration` for the 6 default values. These are seeded in the migration so the app has defaults without manual setup.

**Entity Class Skeleton (GcalEvent):**
```csharp
namespace GoogleCalendarManagement.Data.Entities;

public class GcalEvent
{
    public string GcalEventId { get; set; } = "";
    public string CalendarId { get; set; } = "";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public string? GcalEtag { get; set; }
    public DateTime? GcalUpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool AppCreated { get; set; }
    public string? SourceSystem { get; set; }
    public bool AppPublished { get; set; }
    public DateTime? AppPublishedAt { get; set; }
    public DateTime? AppLastModifiedAt { get; set; }
    public string? RecurringEventId { get; set; }
    public bool IsRecurringInstance { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<GcalEventVersion> Versions { get; set; } = new List<GcalEventVersion>();
}
```

**Configuration Skeleton (GcalEventConfiguration):**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GoogleCalendarManagement.Data.Configurations;

public class GcalEventConfiguration : IEntityTypeConfiguration<GcalEvent>
{
    public void Configure(EntityTypeBuilder<GcalEvent> builder)
    {
        builder.ToTable("gcal_event");

        builder.HasKey(e => e.GcalEventId);
        builder.Property(e => e.GcalEventId).HasColumnName("gcal_event_id").ValueGeneratedNever();
        builder.Property(e => e.CalendarId).HasColumnName("calendar_id").IsRequired();
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(e => e.AppCreated).HasColumnName("app_created").HasDefaultValue(false);
        builder.Property(e => e.AppPublished).HasColumnName("app_published").HasDefaultValue(false);
        builder.Property(e => e.IsRecurringInstance).HasColumnName("is_recurring_instance").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        // ... remaining properties

        builder.HasIndex(e => new { e.StartDatetime, e.EndDatetime }).HasDatabaseName("idx_gcal_event_date");
        builder.HasIndex(e => e.RecurringEventId).HasDatabaseName("idx_gcal_recurring");
        builder.HasIndex(e => e.SourceSystem).HasDatabaseName("idx_gcal_source");
        builder.HasIndex(e => e.AppCreated).HasDatabaseName("idx_gcal_app_created");

        builder.HasMany(e => e.Versions)
               .WithOne(v => v.GcalEvent)
               .HasForeignKey(v => v.GcalEventId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Updated CalendarDbContext:**
```csharp
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Data;

public class CalendarDbContext : DbContext
{
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options)
        : base(options) { }

    public DbSet<GcalEvent> GcalEvents { get; set; }
    public DbSet<GcalEventVersion> GcalEventVersions { get; set; }
    public DbSet<SaveState> SaveStates { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Config> Configs { get; set; }
    public DbSet<DataSourceRefresh> DataSourceRefreshes { get; set; }
    public DbSet<SystemState> SystemStates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CalendarDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

**Config Seed Data:**
```csharp
builder.HasData(
    new Config { ConfigKey = "min_event_duration_minutes", ConfigValue = "5", ConfigType = "integer", Description = "Minimum duration to show events", UpdatedAt = new DateTime(2026, 1, 1) },
    new Config { ConfigKey = "phone_coalesce_gap_minutes", ConfigValue = "15", ConfigType = "integer", Description = "Max gap for phone coalescing", UpdatedAt = new DateTime(2026, 1, 1) },
    new Config { ConfigKey = "youtube_coalesce_gap_minutes", ConfigValue = "30", ConfigType = "integer", Description = "Gap after video duration for YouTube", UpdatedAt = new DateTime(2026, 1, 1) },
    new Config { ConfigKey = "call_min_duration_minutes", ConfigValue = "3", ConfigType = "integer", Description = "Minimum call duration to import", UpdatedAt = new DateTime(2026, 1, 1) },
    new Config { ConfigKey = "youtube_char_limit_short", ConfigValue = "40", ConfigType = "integer", Description = "Char limit for events <90min", UpdatedAt = new DateTime(2026, 1, 1) },
    new Config { ConfigKey = "eight_fifteen_threshold", ConfigValue = "8", ConfigType = "integer", Description = "Minutes required in 15-min block", UpdatedAt = new DateTime(2026, 1, 1) }
);
```

**Migration Commands:**
```bash
dotnet ef migrations add Phase1Schema
dotnet ef database update
```

### Project Structure After Story 1.3

```
GoogleCalendarManagement/
├── Data/
│   ├── CalendarDbContext.cs              # Updated: 7 DbSet properties + ApplyConfigurationsFromAssembly
│   ├── DatabaseOptions.cs                # Unchanged from Story 1.2
│   ├── SqliteConnectionInterceptor.cs    # Unchanged from Story 1.2
│   ├── CalendarDbContextFactory.cs       # Unchanged from Story 1.2
│   ├── Entities/                         # New folder
│   │   ├── GcalEvent.cs
│   │   ├── GcalEventVersion.cs
│   │   ├── SaveState.cs
│   │   ├── AuditLog.cs
│   │   ├── Config.cs
│   │   ├── DataSourceRefresh.cs
│   │   └── SystemState.cs
│   ├── Configurations/                   # New folder
│   │   ├── GcalEventConfiguration.cs
│   │   ├── GcalEventVersionConfiguration.cs
│   │   ├── SaveStateConfiguration.cs
│   │   ├── AuditLogConfiguration.cs
│   │   ├── ConfigConfiguration.cs
│   │   ├── DataSourceRefreshConfiguration.cs
│   │   └── SystemStateConfiguration.cs
│   └── Migrations/
│       ├── <timestamp>_InitialCreate.cs      # From Story 1.2 (empty schema)
│       ├── <timestamp>_Phase1Schema.cs       # Added in Story 1.3
│       └── CalendarDbContextModelSnapshot.cs
GoogleCalendarManagement.Tests/
└── Integration/
    ├── DatabaseInfrastructureTests.cs     # From Story 1.2
    └── SchemaTests.cs                     # Added in Story 1.3
```

### References

**Source Documents:**
- [Epic 1: Story 1.3 Definition](../../epics.md#story-13-implement-core-database-schema-tier-1-tables)
- [Database Schemas Reference](../_database-schemas.md) — Authoritative source for all column definitions, types, indexes
- [Architecture: Data Layer](../architecture.md#project-structure) — Entity and Configuration folder locations
- [Architecture: Decision Summary](../architecture.md#decision-summary) — Singular table names, Fluent API, EF Core version

**Specific Technical Mandates:**
- **FR-8.1 (SQLite Storage):** Local database schema — [PRD §4.8](../PRD.md#48-data--configuration)
- **NFR-D1 (Version History):** `gcal_event_version` table required for rollback capability
- **NFR-D3 (Database Integrity):** FK constraints enforced (WAL + FK PRAGMA from Story 1.2)
- **NFR-D4 (Audit Trail):** `audit_log` table required for all operations

**Prerequisites:**
- Story 1.2 complete — `CalendarDbContext`, WAL mode, FK enforcement, and `InitialCreate` migration must exist

**Common Troubleshooting:**
- If `ApplyConfigurationsFromAssembly` doesn't pick up configs: ensure all configuration classes implement `IEntityTypeConfiguration<T>` (not just inherit from a base)
- If migration generates unexpected `BLOB` type for `bool`: EF Core SQLite provider maps `bool` to `INTEGER`; this is correct
- If seed data causes migration conflicts on re-run: `HasData` is idempotent via EF Core migration tracking
- If FK violation test fails: ensure `PRAGMA foreign_keys=ON` is set (handled by `SqliteConnectionInterceptor` from Story 1.2)
- If `ValueGeneratedNever()` is missing on string PKs: EF Core may try to generate string GUIDs unexpectedly

### Testing Strategy

**Story 1.3 Testing Scope:**
- Integration tests use in-memory SQLite with `EnsureCreated()` (same factory pattern as Story 1.2)
- Tests verify schema structure, not business logic
- Seed data verification uses direct `DbSet<Config>` queries
- FK constraint test requires real SQLite (not EF Core in-memory provider) — use `Data Source=:memory:` with SQLite provider

**Key Test Cases:**
1. `AllPhase1Tables_ExistAfterEnsureCreated` — query `sqlite_master WHERE type='table'` for all 7 table names
2. `GcalEvent_CrudRoundTrip_Succeeds` — insert event with string PK, read back, update summary, delete
3. `GcalEventVersion_ForeignKey_EnforcedOnInsert` — attempt insert with non-existent `gcal_event_id`; assert FK violation
4. `Config_SeedData_PresentAfterMigration` — assert Count == 6, verify specific keys exist

### Change Log

**Version 1.0 - Initial Draft (2026-03-27)**
- Created from Epic 1, Story 1.3 definition in epics.md
- Expanded acceptance criteria into testable subtasks with Tier 1 table scope
- Dev Notes include entity skeletons, configuration patterns, updated DbContext, seed data
- Aligned with `_database-schemas.md` column names, types, and index names

## Dev Agent Record

### Context Reference

- [Story Context XML](1-3-implement-core-database-schema-tier-1-tables.context.xml) - Generated 2026-03-27

### Completion Notes (2026-03-27)

All ACs satisfied. 14/14 tests pass (5 new SchemaTests + 9 from Story 1.2).

- Created 7 entity classes in `Data/Entities/` with no data annotations (C-1 compliant)
- Created 7 `IEntityTypeConfiguration<T>` classes in `Data/Configurations/` with full snake_case column mapping
- Updated `CalendarDbContext.cs` with 7 `DbSet<T>` properties and `ApplyConfigurationsFromAssembly`
- Migration `Phase1Schema` generated and applied cleanly — all 7 tables, 9 indexes, 6 Config seed rows verified in SQL (FK delete behavior subsequently hardened to Restrict in Story 2.3A)
- `__EFMigrationsHistory` contains `InitialCreate` + `Phase1Schema`
- Debug and Release builds: 0 errors (IL2026 trim warnings are pre-existing EF Core/WinUI warnings from Story 1.2)

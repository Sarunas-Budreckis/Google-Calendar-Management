# Story 8.7: `data_point` Registry Table + Source-Pointer Model

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 8.2 (blocking — the unified `event` table + EF migration infra must exist before adding a companion migration)

---

## Story

As the data-linking engine,
I want a `data_point` registry table and a source-pointer resolution contract,
so that every raw imported record can be normalized into a single addressable row with a time extent and a stable back-pointer to its source — forming the spine that the link table (8.12), coverage service (8.10), and block/clump provider (8.11) will build on.

---

## Acceptance Criteria

1. `Data/Entities/DataPoint.cs` exists with properties matching the canonical schema (concepts.md §4):
   - `DataPointId` — `int`, PK, auto-increment (SQLite `INTEGER PRIMARY KEY`)
   - `SourceKey` — `string`, required, max 100 chars — which source (`'spotify_stream'`, `'toggl_entry'`, `'voice_memo'`, ...)
   - `SourceRef` — `string`, required, max 500 chars — pointer to the raw record (row id or file path)
   - `StartUtc` — `DateTime`, required (UTC) — start of the datapoint's time extent
   - `EndUtc` — `DateTime`, required (UTC) — end of the extent; **instant datapoints use `StartUtc == EndUtc`**
   - `CreatedAt` — `DateTime`, required (UTC)
2. `Data/Configurations/DataPointConfiguration.cs` configures the table:
   - Table name: `data_point`
   - Index `idx_data_point_start_utc` on `(start_utc)`
   - Index `idx_data_point_source_key_start_utc` on `(source_key, start_utc)`
3. `CalendarDbContext` has `DbSet<DataPoint> DataPoints { get; set; }` added.
4. An EF Core migration (timestamp `20260612000000_AddDataPoint`) creates the `data_point` table with all columns and both indexes. Running `dotnet ef database update` succeeds on a fresh and on an existing (post-8.2) database.
5. `Services/ISourcePointerResolver.cs` defines the resolver contract:
   - `string SourceKey { get; }` — the source this resolver handles
   - `Task<bool> ExistsAsync(string sourceRef, CancellationToken ct)` — returns true if the raw record exists
   - `Task<string?> ResolveDisplayAsync(string sourceRef, CancellationToken ct)` — returns a short human-readable label for the raw record (e.g., `"Spotify: Never Gonna Give You Up (3:33)"`) or `null` if not found
6. `Services/ISourcePointerResolverRegistry.cs` defines:
   - `void Register(ISourcePointerResolver resolver)` — called during startup to wire in per-source resolvers
   - `ISourcePointerResolver? GetResolver(string sourceKey)` — look up a resolver by source key
   - `Task<string?> ResolveDisplayAsync(string sourceKey, string sourceRef, CancellationToken ct)` — convenience method; dispatches to the registered resolver or returns `null` if no resolver for `sourceKey`
7. `Services/SourcePointerResolverRegistry.cs` implements `ISourcePointerResolverRegistry` — a thread-safe dictionary mapping `source_key → ISourcePointerResolver`. No per-source implementations are registered yet (those are in 8.9); the registry is wired but empty at this story's completion.
8. `ISourcePointerResolverRegistry` is registered as `AddSingleton` in `App.xaml.cs`.
9. Integration tests in `GoogleCalendarManagement.Tests/Integration/DataPointRepositoryTests.cs` cover:
   - Insert a `DataPoint` row, retrieve by `DataPointId` — all fields round-trip correctly
   - Insert multiple rows, query by `start_utc` range — returns correct rows
   - Instant datapoint (`StartUtc == EndUtc`) — inserts and retrieves correctly
   - `ISourcePointerResolverRegistry.GetResolver("unknown_key")` returns `null`
   - `ISourcePointerResolverRegistry.ResolveDisplayAsync("unknown_key", "ref", ct)` returns `null` without throwing

---

## Tasks / Subtasks

- [ ] Task 1: Create `DataPoint` entity (AC: #1)
  - [ ] 1.1 Create `Data/Entities/DataPoint.cs` with all properties from AC #1
  - [ ] 1.2 Namespace: `GoogleCalendarManagement.Data.Entities`

- [ ] Task 2: Create `DataPointConfiguration` (AC: #2)
  - [ ] 2.1 Create `Data/Configurations/DataPointConfiguration.cs` implementing `IEntityTypeConfiguration<DataPoint>`
  - [ ] 2.2 `builder.ToTable("data_point")`
  - [ ] 2.3 `builder.HasKey(e => e.DataPointId)` — `ValueGeneratedOnAdd()` for auto-increment
  - [ ] 2.4 Map all column names in snake_case (follow existing configuration files like `SpotifyStreamConfiguration.cs`)
  - [ ] 2.5 `builder.Property(e => e.SourceKey).HasColumnName("source_key").IsRequired().HasMaxLength(100)`
  - [ ] 2.6 `builder.Property(e => e.SourceRef).HasColumnName("source_ref").IsRequired().HasMaxLength(500)`
  - [ ] 2.7 `builder.Property(e => e.StartUtc).HasColumnName("start_utc").IsRequired()`
  - [ ] 2.8 `builder.Property(e => e.EndUtc).HasColumnName("end_utc").IsRequired()`
  - [ ] 2.9 `builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired()`
  - [ ] 2.10 Add index: `.HasIndex(e => e.StartUtc).HasDatabaseName("idx_data_point_start_utc")`
  - [ ] 2.11 Add composite index: `.HasIndex(e => new { e.SourceKey, e.StartUtc }).HasDatabaseName("idx_data_point_source_key_start_utc")`

- [ ] Task 3: Add `DbSet` to `CalendarDbContext` (AC: #3)
  - [ ] 3.1 Open `Data/CalendarDbContext.cs` and add `public DbSet<DataPoint> DataPoints { get; set; }` after the existing `DbSet` declarations
  - [ ] 3.2 Add `using GoogleCalendarManagement.Data.Entities;` if not already wildcard-imported

- [ ] Task 4: Create EF Core migration (AC: #4)
  - [ ] 4.1 Run `dotnet ef migrations add AddDataPoint --project GoogleCalendarManagement.csproj` from the project root
  - [ ] 4.2 Verify the generated migration file creates `data_point` table with all 6 columns and both indexes (no extra tables from other in-progress work)
  - [ ] 4.3 Verify `dotnet ef database update` runs without error against the design-time db (`design_time.db`)
  - [ ] 4.4 Migration timestamp in filename must be `20260612000000` (or the actual generation time — do not hand-write; let `dotnet ef` generate the file)

- [ ] Task 5: Create `ISourcePointerResolver` interface (AC: #5)
  - [ ] 5.1 Create `Services/ISourcePointerResolver.cs`
  - [ ] 5.2 Namespace: `GoogleCalendarManagement.Services`
  - [ ] 5.3 Properties + methods per AC #5

- [ ] Task 6: Create `ISourcePointerResolverRegistry` + `SourcePointerResolverRegistry` (AC: #6, #7)
  - [ ] 6.1 Create `Services/ISourcePointerResolverRegistry.cs` with methods from AC #6
  - [ ] 6.2 Create `Services/SourcePointerResolverRegistry.cs` — internal dictionary `Dictionary<string, ISourcePointerResolver> _resolvers`
  - [ ] 6.3 `Register` — `_resolvers[resolver.SourceKey] = resolver` (overwrite if duplicate key)
  - [ ] 6.4 `GetResolver` — `_resolvers.TryGetValue(sourceKey, out var r) ? r : null`
  - [ ] 6.5 `ResolveDisplayAsync` — dispatch to resolver if found; return `null` if not registered (do NOT throw)
  - [ ] 6.6 No locking needed — `Register` is called only during DI startup before concurrent access begins
  - [ ] 6.7 Namespace: `GoogleCalendarManagement.Services`

- [ ] Task 7: DI registration (AC: #8)
  - [ ] 7.1 Open `App.xaml.cs`, add `services.AddSingleton<ISourcePointerResolverRegistry, SourcePointerResolverRegistry>()` alongside the other singleton registrations (around lines 268–310)

- [ ] Task 8: Integration tests (AC: #9)
  - [ ] 8.1 Create `GoogleCalendarManagement.Tests/Integration/DataPointRepositoryTests.cs`
  - [ ] 8.2 Use the standard in-memory SQLite test pattern (see Dev Notes: Testing framework)
  - [ ] 8.3 Write tests per AC #9 — five test cases

---

## Dev Notes

### Entity shape — match concepts.md §4 exactly

The `data_point` table is 1:1 with a raw record. No link state lives here — that is the `link` table (Story 8.12). No FK to `event` exists on this table.

`DataPointId` uses `int` (SQLite auto-increment). Unlike `event_id` which is a UUID string, datapoint ids are plain auto-increment integers — lower footprint, simpler FK in the link table.

```csharp
// Data/Entities/DataPoint.cs
namespace GoogleCalendarManagement.Data.Entities;

public class DataPoint
{
    public int DataPointId { get; set; }
    public string SourceKey { get; set; } = "";
    public string SourceRef { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Configuration pattern — match `SpotifyStreamConfiguration`

Follow `Data/Configurations/SpotifyStreamConfiguration.cs` exactly for conventions:
- `builder.HasKey(e => e.Id)` → here `builder.HasKey(e => e.DataPointId)`
- `builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd()` → `"data_point_id"`, `ValueGeneratedOnAdd()`
- Column names all snake_case

```csharp
// Data/Configurations/DataPointConfiguration.cs
public class DataPointConfiguration : IEntityTypeConfiguration<DataPoint>
{
    public void Configure(EntityTypeBuilder<DataPoint> builder)
    {
        builder.ToTable("data_point");

        builder.HasKey(e => e.DataPointId);
        builder.Property(e => e.DataPointId).HasColumnName("data_point_id").ValueGeneratedOnAdd();
        builder.Property(e => e.SourceKey).HasColumnName("source_key").IsRequired().HasMaxLength(100);
        builder.Property(e => e.SourceRef).HasColumnName("source_ref").IsRequired().HasMaxLength(500);
        builder.Property(e => e.StartUtc).HasColumnName("start_utc").IsRequired();
        builder.Property(e => e.EndUtc).HasColumnName("end_utc").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => e.StartUtc)
            .HasDatabaseName("idx_data_point_start_utc");
        builder.HasIndex(e => new { e.SourceKey, e.StartUtc })
            .HasDatabaseName("idx_data_point_source_key_start_utc");
    }
}
```

### Source-key vocabulary — canonical values

These are the source keys that Story 8.9 will use when projecting existing sources. Define them as constants in a shared location (e.g., `Constants/SourceKeys.cs`) so 8.8+ can import them without magic strings. This story defines the constants class as a **bonus** guardrail — makes 8.9 cleaner:

```csharp
// Constants/SourceKeys.cs
namespace GoogleCalendarManagement.Constants;

public static class SourceKeys
{
    public const string Toggl = "toggl_entry";
    public const string TogglSleep = "toggl_sleep";
    public const string TogglPhone = "toggl_phone";
    public const string TogglTransit = "toggl_transit";
    public const string CallLog = "call_log";
    public const string Civ5 = "civ5_session";
    public const string ComfyUI = "comfyui_scan";
    public const string Spotify = "spotify_stream";
    public const string MapsTimeline = "maps_timeline";
    public const string Outlook = "outlook_event";
    public const string VoiceMemo = "voice_memo";
}
```

This does NOT need to be exhaustive — 8.9 can add to it. The class is `static` (no instances) and lives in the existing `Constants/` directory (already exists in the project).

### `SourceRef` semantics per source

The `source_ref` must be non-lossy and stable across re-import. Story 8.9 implements per-source projectors; this story just defines the contract. For reference:

| source_key | source_ref content |
|---|---|
| `toggl_entry` | `TogglEntry.Id.ToString()` (row id) |
| `spotify_stream` | `"{PlayedAt:O}|{TrackName}"` (natural key — stable on re-import) |
| `maps_timeline` | segment natural key (TBD in 7.16) |
| `call_log` | `CallLogEntry.Id.ToString()` |
| `civ5_session` | `Civ5SessionPoint.Id.ToString()` |
| `comfyui_scan` | `ComfyUIScanPoint.Id.ToString()` |
| `outlook_event` | `OutlookEvent.OutlookEventId` (GUIDs from Outlook API) |

Row-id-based sources are fine for non-replace-on-reimport sources. Spotify/Maps need natural keys (defined in 8.9).

### EF migration — use `dotnet ef`, not hand-writing

The migration must be generated by the EF toolchain so the `Designer.cs` snapshot is correct. If the project uses a design-time factory (`CalendarDbContextFactory.cs`), `dotnet ef` will pick it up automatically.

```powershell
# From the solution root directory
dotnet ef migrations add AddDataPoint --project GoogleCalendarManagement.csproj
dotnet ef database update --project GoogleCalendarManagement.csproj
```

After generating, open the migration file and verify it contains:
- `migrationBuilder.CreateTable("data_point", ...)` with 6 columns
- `migrationBuilder.CreateIndex("idx_data_point_start_utc", "data_point", "start_utc")`
- `migrationBuilder.CreateIndex("idx_data_point_source_key_start_utc", "data_point", new[] { "source_key", "start_utc" })`

If the migration also picks up any unreleated pending model changes (from other in-progress stories), stop and investigate — do NOT include unrelated model changes in this migration.

### Source pointer resolver — no implementations in 8.7

`ISourcePointerResolver` and `ISourcePointerResolverRegistry` are the contract. No per-source implementations are written here — that is 8.9 (`Project all existing sources into datapoints`). The registry starts empty. This is intentional: 8.7 is purely schema + contract; 8.8 defines the import projector contract that writes datapoints; 8.9 wires them all up.

### `ISourcePointerResolver` interface

```csharp
// Services/ISourcePointerResolver.cs
namespace GoogleCalendarManagement.Services;

public interface ISourcePointerResolver
{
    string SourceKey { get; }
    Task<bool> ExistsAsync(string sourceRef, CancellationToken ct = default);
    Task<string?> ResolveDisplayAsync(string sourceRef, CancellationToken ct = default);
}
```

### Registry — dispatch pattern

```csharp
// Services/SourcePointerResolverRegistry.cs
namespace GoogleCalendarManagement.Services;

public class SourcePointerResolverRegistry : ISourcePointerResolverRegistry
{
    private readonly Dictionary<string, ISourcePointerResolver> _resolvers = new();

    public void Register(ISourcePointerResolver resolver)
        => _resolvers[resolver.SourceKey] = resolver;

    public ISourcePointerResolver? GetResolver(string sourceKey)
        => _resolvers.TryGetValue(sourceKey, out var r) ? r : null;

    public async Task<string?> ResolveDisplayAsync(string sourceKey, string sourceRef, CancellationToken ct = default)
    {
        var resolver = GetResolver(sourceKey);
        if (resolver is null) return null;
        return await resolver.ResolveDisplayAsync(sourceRef, ct);
    }
}
```

### Testing framework

xUnit + FluentAssertions. Integration tests use in-memory SQLite:

```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
_contextFactory = new TestDbContextFactory(options);
```

Follow the `TestDbContextFactory` pattern from `GoogleCalendarManagement.Tests/Integration/` (already defined there — do not create a duplicate).

### What this story does NOT do

- Does NOT implement any per-source projectors (`Spotify → datapoint`, `Toggl → datapoint`, etc.) — Story 8.9
- Does NOT implement the import base class or guard test — Story 8.8
- Does NOT create the `link` table — Story 8.12
- Does NOT implement coverage computation — Story 8.10
- Does NOT implement block/clump providers — Story 8.11
- Does NOT add a `(source_key, source_ref)` UNIQUE constraint — sources with re-import semantics (Spotify, Maps) need it; sources with row-id refs (Toggl, etc.) do NOT. This uniqueness per source is enforced at the projector level in 8.9, not at the DB level in 8.7.

### Project structure

New files:
- `Data/Entities/DataPoint.cs`
- `Data/Configurations/DataPointConfiguration.cs`
- `Data/Migrations/20260612000000_AddDataPoint.cs` (generated)
- `Data/Migrations/20260612000000_AddDataPoint.Designer.cs` (generated)
- `Services/ISourcePointerResolver.cs`
- `Services/ISourcePointerResolverRegistry.cs`
- `Services/SourcePointerResolverRegistry.cs`
- `Constants/SourceKeys.cs`
- `GoogleCalendarManagement.Tests/Integration/DataPointRepositoryTests.cs`

Modified files:
- `Data/CalendarDbContext.cs` — add `DbSet<DataPoint>`
- `Data/Migrations/CalendarDbContextModelSnapshot.cs` (regenerated by `dotnet ef`)
- `App.xaml.cs` — add `ISourcePointerResolverRegistry` DI registration

Namespace convention: `GoogleCalendarManagement.Data.Entities` / `GoogleCalendarManagement.Data.Configurations` / `GoogleCalendarManagement.Services` / `GoogleCalendarManagement.Constants`.

### References

- Canonical data_point schema: [concepts.md §4](../concepts.md)
- Link table (next consumer of DataPointId): [concepts.md §5](../concepts.md)
- Coverage model: [concepts.md §6](../concepts.md)
- Epic overview 8.7 spec: [epic-overview.md §Phase 1 Story 8.7](../epic-overview.md)
- Story 8.8 (import projector contract, uses DataPoint): [8-8-import-projector-contract-guard-test-reconciliation-sweep.md](./8-8-import-projector-contract-guard-test-reconciliation-sweep.md)
- Story 8.9 (per-source projectors, populates DataPoints): will be at `8-9-project-all-sources-into-datapoints.md`
- Story 8.12 (link table, FKs to data_point_id): will be at `8-12-link-table-and-link-ignore-unlink-operations.md`
- Existing configuration pattern: `Data/Configurations/SpotifyStreamConfiguration.cs`
- Existing entity pattern: `Data/Entities/SpotifyStream.cs`
- DI registration: `App.xaml.cs` (~lines 268–310)
- Test pattern: `GoogleCalendarManagement.Tests/Integration/` (any existing integration test file)

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

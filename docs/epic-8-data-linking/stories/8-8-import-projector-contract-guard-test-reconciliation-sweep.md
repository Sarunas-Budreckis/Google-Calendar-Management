# Story 8.8: Import Projector Contract + Guard Test + Reconciliation Sweep

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
**Agent:** Opus · **Effort:** high
**Dependencies:** 8.7 (blocking — `DataPoint` entity, `CalendarDbContext.DataPoints`, `DataPointConfiguration`, EF migration, and `Constants/SourceKeys.cs` must exist)

---

## Story

As the data-linking engine,
I want a formal import projector contract enforced at the interface level, a CI guard test that fails if any handler ships without one, and a reconciliation sweep that heals any gaps,
so that every raw imported record is guaranteed to have a matching `data_point` row — and no future developer can accidentally add a new import handler without wiring its projection.

---

## Acceptance Criteria

1. `Services/IDataPointProjector.cs` exists defining:
   - `string SourceKey { get; }` — must match the handler's `SourceKey`
   - `Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(CalendarDbContext ctx, CancellationToken ct)` — returns specs for all raw records of this source that have no `data_point` row
   - `Task<IReadOnlyList<DataPointSpec>> ProjectSourceRefsAsync(CalendarDbContext ctx, IReadOnlyList<string> sourceRefs, CancellationToken ct)` — given a list of `source_ref` values (just inserted), returns `DataPointSpec` for each
   - `record DataPointSpec(string SourceKey, string SourceRef, DateTime StartUtc, DateTime EndUtc)` — defined in the same file or a companion file in `Services/`
2. `Services/IDataPointProjectorRegistry.cs` and `Services/DataPointProjectorRegistry.cs` exist:
   - `void Register(IDataPointProjector projector)` — stores by `SourceKey`; throws `InvalidOperationException` on duplicate key
   - `IDataPointProjector? GetProjector(string sourceKey)` — returns `null` if not registered (never throws)
   - `IReadOnlyCollection<IDataPointProjector> GetAllProjectors()` — snapshot of all registered projectors
   - `DataPointProjectorRegistry` is `AddSingleton` in `App.xaml.cs`
3. `Services/IDataSourceImportHandler.cs` gains a default interface member `IDataPointProjector? GetProjector() => null;` — adding this compiles without modifying any existing concrete handler (all 10 existing handlers inherit the default null return).
4. A reflection guard test in `GoogleCalendarManagement.Tests/Unit/Services/DataPointProjectorGuardTests.cs`:
   - Finds every concrete (non-abstract, non-generic) class in the main assembly that implements `IDataSourceImportHandler`
   - Asserts each one declares its own override of `GetProjector()` (i.e., the method's `DeclaringType` equals the concrete handler class, not the interface)
   - The test **fails** in this story (no handler yet overrides `GetProjector()`); it becomes fully green after Story 8.9 completes all projectors. This is the intended state — the guard exists as a CI tripwire.
5. `Services/IDataPointReconciliationSweepService.cs` defines:
   - `Task RunPostImportAsync(string sourceKey, CancellationToken ct)` — finds orphaned raw records for one source and inserts missing `data_point` rows
   - `Task RunStartupDriftCheckAsync(CancellationToken ct)` — runs `RunPostImportAsync` for every registered projector; logs a warning per source if any orphans were found and healed
   - `Task RebuildRegistryForSourceAsync(string sourceKey, CancellationToken ct)` — deletes all `data_point` rows for `sourceKey` then fully re-projects (idempotent rebuild)
   - `Task RebuildRegistryAllAsync(CancellationToken ct)` — calls `RebuildRegistryForSourceAsync` for every registered projector
6. `Services/DataPointReconciliationSweepService.cs` implements `IDataPointReconciliationSweepService`:
   - Uses `IDataPointProjectorRegistry` to dispatch to the correct projector
   - Each `DataPointSpec` from `GetOrphanedSpecsAsync` is inserted as a new `DataPoint` row; duplicate `(source_key, source_ref)` pairs are skipped (upsert-or-skip, not throw)
   - `RebuildRegistryForSourceAsync` wraps the delete + re-project in a single EF transaction
   - Both services are `AddSingleton` in `App.xaml.cs`
7. The startup sweep is wired into app initialization: `IDataPointReconciliationSweepService.RunStartupDriftCheckAsync` is called **fire-and-forget** (not awaited on the UI thread) shortly after app startup — use `Task.Run` or post to a background thread. A warning log is emitted for each source with orphans found/healed.
8. A "Rebuild Registry" manual action exists: `DataSourcePanelViewModel` gains a `RebuildDataPointRegistryCommand` (`AsyncRelayCommand`) that calls `IDataPointReconciliationSweepService.RebuildRegistryAllAsync` and shows a completion message via `IContentDialogService`.
9. Unit tests for `DataPointReconciliationSweepService` in `GoogleCalendarManagement.Tests/Unit/Services/DataPointReconciliationSweepServiceTests.cs` cover:
   - `RunPostImportAsync` with a mock projector that returns two orphaned specs → two `DataPoint` rows inserted
   - `RunPostImportAsync` called twice with the same orphaned specs → second call inserts 0 (no duplicate data_point rows)
   - `RunPostImportAsync` with an unregistered `sourceKey` → logs a warning, does not throw
   - `RunStartupDriftCheckAsync` — calls `GetOrphanedSpecsAsync` on every registered projector
   - `RebuildRegistryForSourceAsync` — deletes existing datapoints for the source, then re-inserts via `GetOrphanedSpecsAsync` (verifying the count before and after)
10. The project builds with no errors after all changes.

---

## Tasks / Subtasks

- [ ] Task 1: Define `IDataPointProjector` interface and `DataPointSpec` record (AC: #1)
  - [ ] 1.1 Create `Services/IDataPointProjector.cs`:
    - Namespace `GoogleCalendarManagement.Services`
    - Interface `IDataPointProjector` with `SourceKey`, `GetOrphanedSpecsAsync`, `ProjectSourceRefsAsync` per AC #1
    - `record DataPointSpec(string SourceKey, string SourceRef, DateTime StartUtc, DateTime EndUtc)` — define in the same file (no separate file needed)
  - [ ] 1.2 All `DateTime` values are UTC by convention (no `DateTimeKind` enforcement needed — consumers must pass UTC)

- [ ] Task 2: Create `IDataPointProjectorRegistry` and `DataPointProjectorRegistry` (AC: #2)
  - [ ] 2.1 Create `Services/IDataPointProjectorRegistry.cs` with three methods per AC #2
  - [ ] 2.2 Create `Services/DataPointProjectorRegistry.cs`:
    - Internal `Dictionary<string, IDataPointProjector> _projectors` (no `ConcurrentDictionary` needed — Register is called only during startup)
    - `Register` throws `InvalidOperationException` if key already exists: `throw new InvalidOperationException($"Projector for source key '{projector.SourceKey}' is already registered.")`
    - `GetProjector` returns null on miss, never throws
    - `GetAllProjectors` returns `_projectors.Values.ToList().AsReadOnly()`
  - [ ] 2.3 Namespace: `GoogleCalendarManagement.Services`

- [ ] Task 3: Add `GetProjector()` default member to `IDataSourceImportHandler` (AC: #3)
  - [ ] 3.1 Open `Services/IDataSourceImportHandler.cs`
  - [ ] 3.2 Add `IDataPointProjector? GetProjector() => null;` as a default interface member after the existing `IsApiFetch` default
  - [ ] 3.3 Build the project — verify all 10 existing concrete handlers compile without changes (they inherit the default `return null`)

- [ ] Task 4: Write the reflection guard test (AC: #4)
  - [ ] 4.1 Create `GoogleCalendarManagement.Tests/Unit/Services/DataPointProjectorGuardTests.cs`
  - [ ] 4.2 Single `[Fact]` test `AllConcreteHandlers_MustOverride_GetProjector`:
    - Reflect on `typeof(IDataSourceImportHandler).Assembly` to find all concrete, non-abstract, non-generic handler types
    - For each type, get the `MethodInfo` for `GetProjector()` via `type.GetMethod(nameof(IDataSourceImportHandler.GetProjector))`
    - Assert `method.DeclaringType == type` (i.e., the method is declared on the concrete class, not inherited from the interface default)
    - Use `handlerTypes.Should().AllSatisfy(...)` with a descriptive failure message: `$"Handler type '{t.Name}' does not override GetProjector() — add a real IDataPointProjector override"`
  - [ ] 4.3 Verify the test **fails** at this point (no handler overrides `GetProjector()`) — expected state in 8.8

- [ ] Task 5: Define `IDataPointReconciliationSweepService` (AC: #5)
  - [ ] 5.1 Create `Services/IDataPointReconciliationSweepService.cs`
  - [ ] 5.2 Four methods per AC #5
  - [ ] 5.3 Namespace: `GoogleCalendarManagement.Services`

- [ ] Task 6: Implement `DataPointReconciliationSweepService` (AC: #6)
  - [ ] 6.1 Create `Services/DataPointReconciliationSweepService.cs`
  - [ ] 6.2 Constructor injects `IDataPointProjectorRegistry _projectorRegistry`, `IDbContextFactory<CalendarDbContext> _contextFactory`, and `ILogger<DataPointReconciliationSweepService> _logger`
  - [ ] 6.3 Implement `RunPostImportAsync(string sourceKey, CancellationToken ct)`:
    ```
    1. GetProjector(sourceKey); if null, log warning "No projector registered for sourceKey '{sourceKey}'" and return
    2. await using ctx = contextFactory.CreateDbContextAsync(ct)
    3. var orphanedSpecs = await projector.GetOrphanedSpecsAsync(ctx, ct)
    4. Insert missing DataPoint rows (skip existing — see note on upsert-or-skip)
    5. await ctx.SaveChangesAsync(ct)
    6. If any inserted, log info "Reconciled {count} missing datapoints for source '{sourceKey}'"
    ```
  - [ ] 6.4 Upsert-or-skip: before inserting, check `ctx.DataPoints.AnyAsync(dp => dp.SourceKey == spec.SourceKey && dp.SourceRef == spec.SourceRef, ct)` and skip if true. For performance with many specs, batch the check using `ctx.DataPoints.Where(dp => dp.SourceKey == sourceKey && sourceRefs.Contains(dp.SourceRef)).Select(dp => dp.SourceRef).ToListAsync(ct)` to get existing refs in one query, then filter.
  - [ ] 6.5 Implement `RunStartupDriftCheckAsync(CancellationToken ct)`:
    - For each projector in `_projectorRegistry.GetAllProjectors()`, call `RunPostImportAsync(projector.SourceKey, ct)` sequentially (not parallel — avoid DB contention on startup)
  - [ ] 6.6 Implement `RebuildRegistryForSourceAsync(string sourceKey, CancellationToken ct)`:
    ```
    1. GetProjector(sourceKey); if null, log warning and return
    2. await using ctx = contextFactory.CreateDbContextAsync(ct)
    3. await using tx = ctx.Database.BeginTransactionAsync(ct)
    4. DELETE all data_point rows where source_key == sourceKey (ctx.DataPoints.Where(...).ExecuteDeleteAsync(ct))
    5. var allSpecs = await projector.GetOrphanedSpecsAsync(ctx, ct) — after delete, all raw records are "orphaned"
    6. Insert all specs as DataPoint rows
    7. await ctx.SaveChangesAsync(ct)
    8. await tx.CommitAsync(ct)
    9. Log info "Rebuilt {count} datapoints for source '{sourceKey}'"
    ```
  - [ ] 6.7 Implement `RebuildRegistryAllAsync(CancellationToken ct)`:
    - For each projector in `_projectorRegistry.GetAllProjectors()`, call `RebuildRegistryForSourceAsync(projector.SourceKey, ct)` sequentially

- [ ] Task 7: DI registration (AC: #2 DI, #6 DI)
  - [ ] 7.1 Open `App.xaml.cs`
  - [ ] 7.2 Add `services.AddSingleton<IDataPointProjectorRegistry, DataPointProjectorRegistry>()` near other singleton registrations (~lines 268–310)
  - [ ] 7.3 Add `services.AddSingleton<IDataPointReconciliationSweepService, DataPointReconciliationSweepService>()`

- [ ] Task 8: Wire up startup sweep (AC: #7)
  - [ ] 8.1 After the DI container is built and the main window is shown, add fire-and-forget startup sweep. Locate the existing startup sequence in `App.xaml.cs` (search for where the main window is activated or where other startup services are invoked)
  - [ ] 8.2 Call `Task.Run(() => serviceProvider.GetRequiredService<IDataPointReconciliationSweepService>().RunStartupDriftCheckAsync(CancellationToken.None))` — do NOT await on the UI thread; no UI blocking
  - [ ] 8.3 The sweep runs silently; any orphans found are logged but do NOT produce a user-facing dialog

- [ ] Task 9: Add `RebuildDataPointRegistryCommand` to `DataSourcePanelViewModel` (AC: #8)
  - [ ] 9.1 Open `ViewModels/DataSourcePanelViewModel.cs`
  - [ ] 9.2 Inject `IDataPointReconciliationSweepService` into the constructor (check if the constructor uses DI or manual wiring)
  - [ ] 9.3 Add `public AsyncRelayCommand RebuildDataPointRegistryCommand { get; }` initialized in constructor:
    ```csharp
    RebuildDataPointRegistryCommand = new AsyncRelayCommand(async ct =>
    {
        await _sweepService.RebuildRegistryAllAsync(ct);
        await _dialogService.ShowMessageAsync("Data Point Registry", "Registry rebuilt successfully.", "OK");
    });
    ```
  - [ ] 9.4 If `DataSourcePanelViewModel` does not already inject `IContentDialogService`, add it
  - [ ] 9.5 Expose the command on the XAML view by verifying `DataSourcePanelControl.xaml` can access it (no UI button needed in 8.8 — just ensure the command is accessible for Epic 9 wiring; a TODO comment in the XAML is acceptable)

- [ ] Task 10: Unit tests for `DataPointReconciliationSweepService` (AC: #9)
  - [ ] 10.1 Create `GoogleCalendarManagement.Tests/Unit/Services/DataPointReconciliationSweepServiceTests.cs`
  - [ ] 10.2 Use in-memory SQLite with the standard `TestDbContextFactory` pattern (see Dev Notes)
  - [ ] 10.3 Mock `IDataPointProjectorRegistry` with Moq; wire mock projectors
  - [ ] 10.4 Write 5 tests per AC #9:
    - `RunPostImportAsync_InsertsOrphanedDataPoints` — mock projector returns 2 specs; assert 2 `DataPoint` rows exist after call
    - `RunPostImportAsync_IsIdempotent` — call twice with same specs; assert still only 2 rows (no duplicates)
    - `RunPostImportAsync_UnknownSourceKey_DoesNotThrow` — no projector registered for key; verify completes without exception
    - `RunStartupDriftCheckAsync_CallsProjectorForEveryRegisteredSource` — mock registry with 3 projectors; verify `GetOrphanedSpecsAsync` called on each
    - `RebuildRegistryForSourceAsync_DeletesExistingThenReinserts` — seed 3 existing `DataPoint` rows for source; rebuild; assert rows replaced (not duplicated)

- [ ] Task 11: Verify build (AC: #10)
  - [ ] 11.1 `dotnet build` from solution root — zero errors, zero CS warnings
  - [ ] 11.2 Run unit tests: `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj` — all tests pass **except** the guard test from Task 4 (expected failure in 8.8)

---

## Dev Notes

### Relationship between 8.7 and 8.8 contracts

Story 8.7 defined `ISourcePointerResolver` (for _resolving display info_ about a raw record given its source_ref) and `ISourcePointerResolverRegistry`. Story 8.8 defines a **different** contract: `IDataPointProjector` (for _writing datapoints_ during/after import) and `IDataPointProjectorRegistry`. These are parallel concerns and should not be conflated:

| Contract (8.7) | Purpose |
|---|---|
| `ISourcePointerResolver` | Look up / display a raw record from its `source_ref`. Used by the Linking panel UI (Epic 9). |
| `ISourcePointerResolverRegistry` | Dispatch `sourceKey + sourceRef → display string`. |

| Contract (8.8) | Purpose |
|---|---|
| `IDataPointProjector` | Project raw records → `DataPointSpec` rows. Used by import and sweep to create `data_point` rows. |
| `IDataPointProjectorRegistry` | Dispatch `sourceKey → projector`. |

In Story 8.9, each concrete source will implement both an `ISourcePointerResolver` and an `IDataPointProjector`. They are separate implementations — do NOT combine them into one class.

### `IDataPointProjector` interface — full code

```csharp
// Services/IDataPointProjector.cs
using GoogleCalendarManagement.Data;

namespace GoogleCalendarManagement.Services;

public interface IDataPointProjector
{
    string SourceKey { get; }

    // Find all raw records for this source that currently have no data_point row.
    // Returns one DataPointSpec per orphaned raw record.
    // Used by the reconciliation sweep (incremental + startup + rebuild).
    Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
        CalendarDbContext ctx,
        CancellationToken ct = default);

    // Project a specific set of source_refs (just inserted) into DataPointSpec.
    // Used for post-import incremental path: pass newly-inserted source_ref values.
    Task<IReadOnlyList<DataPointSpec>> ProjectSourceRefsAsync(
        CalendarDbContext ctx,
        IReadOnlyList<string> sourceRefs,
        CancellationToken ct = default);
}

public record DataPointSpec(string SourceKey, string SourceRef, DateTime StartUtc, DateTime EndUtc);
```

### `GetOrphanedSpecsAsync` — query pattern for implementers (8.9 reference)

Each 8.9 projector will implement `GetOrphanedSpecsAsync` with a LEFT JOIN-style query. Example for a hypothetical `FooEntry` source:

```csharp
public async Task<IReadOnlyList<DataPointSpec>> GetOrphanedSpecsAsync(
    CalendarDbContext ctx, CancellationToken ct = default)
{
    var existingRefs = await ctx.DataPoints
        .Where(dp => dp.SourceKey == SourceKey)
        .Select(dp => dp.SourceRef)
        .ToHashSetAsync(ct);

    return await ctx.FooEntries
        .Where(f => !existingRefs.Contains(f.Id.ToString()))
        .Select(f => new DataPointSpec(SourceKey, f.Id.ToString(), f.StartUtc, f.EndUtc))
        .ToListAsync(ct);
}
```

For large datasets, a correlated NOT EXISTS subquery is more efficient than loading all existing refs into memory. Story 8.9 may use whichever approach is appropriate per source volume.

### Guard test — exact reflection logic

```csharp
// GoogleCalendarManagement.Tests/Unit/Services/DataPointProjectorGuardTests.cs
using System.Reflection;
using FluentAssertions;
using GoogleCalendarManagement.Services;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class DataPointProjectorGuardTests
{
    [Fact]
    public void AllConcreteHandlers_MustOverride_GetProjector()
    {
        var mainAssembly = typeof(IDataSourceImportHandler).Assembly;

        var handlerTypes = mainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition
                        && typeof(IDataSourceImportHandler).IsAssignableFrom(t))
            .ToList();

        handlerTypes.Should().NotBeEmpty("the assembly must contain concrete import handlers");

        var handlersWithoutOverride = handlerTypes
            .Where(t =>
            {
                var method = t.GetMethod(nameof(IDataSourceImportHandler.GetProjector),
                    BindingFlags.Instance | BindingFlags.Public);
                // If DeclaringType is the interface (or null), the handler uses the default
                return method is null || method.DeclaringType != t;
            })
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        handlersWithoutOverride.Should().BeEmpty(
            $"every import handler must override GetProjector() and return a real IDataPointProjector. " +
            $"Missing: {string.Join(", ", handlersWithoutOverride)}");
    }
}
```

This test is expected to **fail in 8.8** (all 10 handlers inherit the default). It becomes green after Story 8.9 adds overrides to all handlers. Once green, it acts as a permanent CI guard preventing new handlers from being added without a projector.

### `IDataSourceImportHandler` — modified interface (only addition)

```csharp
// Services/IDataSourceImportHandler.cs  (complete file after 8.8)
namespace GoogleCalendarManagement.Services;

public interface IDataSourceImportHandler
{
    string SourceKey { get; }

    bool IsApiFetch => false;

    Task TriggerImportAsync(CancellationToken ct = default);

    // Returns this handler's data-point projector.
    // Default: null. Concrete handlers must override in Story 8.9.
    IDataPointProjector? GetProjector() => null;
}
```

Do not change `bool IsApiFetch => false;` or any other existing member.

### Reconciliation sweep — duplicate-skip pattern

When inserting datapoints, skip if `(source_key, source_ref)` already exists (no UNIQUE constraint at DB level per Story 8.7). Batch-check existing refs before inserting:

```csharp
var existingRefs = await ctx.DataPoints
    .Where(dp => dp.SourceKey == sourceKey
                 && specsToInsert.Select(s => s.SourceRef).Contains(dp.SourceRef))
    .Select(dp => dp.SourceRef)
    .ToHashSetAsync(ct);

var newSpecs = specsToInsert
    .Where(s => !existingRefs.Contains(s.SourceRef))
    .ToList();

foreach (var spec in newSpecs)
{
    ctx.DataPoints.Add(new DataPoint
    {
        SourceKey  = spec.SourceKey,
        SourceRef  = spec.SourceRef,
        StartUtc   = spec.StartUtc,
        EndUtc     = spec.EndUtc,
        CreatedAt  = DateTime.UtcNow
    });
}

await ctx.SaveChangesAsync(ct);
```

Note: `_timeProvider.GetUtcNow().UtcDateTime` is preferred if `TimeProvider` is already injected in the sweep service; otherwise `DateTime.UtcNow` is acceptable.

### `RebuildRegistryForSourceAsync` — EF `ExecuteDeleteAsync`

EF Core 7+ supports bulk delete without loading entities:

```csharp
await ctx.DataPoints
    .Where(dp => dp.SourceKey == sourceKey)
    .ExecuteDeleteAsync(ct);
```

This deletes directly in SQL (`DELETE FROM data_point WHERE source_key = @p0`) without materializing rows. Wrap this + the re-insert in an explicit transaction (Task 6 step 6.6).

### All 10 concrete handlers that must override `GetProjector()` in 8.9

The guard test will pass after 8.9 adds overrides to ALL of these:

| Handler Class | SourceKey constant |
|---|---|
| `TogglSleepImportHandler` | `TogglSleepImportService.SourceKey` |
| `TogglTransitImportHandler` | `TogglTransitImportHandler.SourceKey` |
| `TogglPhoneImportHandler` | `TogglPhoneImportService.SourceKey` (or similar) |
| `MapsTimelineImportHandler` | `MapsTimelineImportHandler.SourceKey` |
| `SpotifyImportHandler` | `SpotifyImportService.SourceKey` |
| `Civ5ImportHandler` | `Civ5SaveScannerService.SourceKey` |
| `CallLogImportHandler` | `CallLogImportService.SourceKey` |
| `ComfyUIImportHandler` | `ComfyUIFolderScannerService.SourceKey` |
| `OutlookImportHandler` | `OutlookImportService.SourceKey` |
| `TogglCsvImportHandler` | (check file) |

Verify the exact SourceKey values by checking each handler's `SourceKey =>` property. Story 8.9 must provide a projector for each. The guard test will enumerate them automatically from reflection.

### `DataSourcePanelViewModel` DI wiring

Look up the existing constructor of `ViewModels/DataSourcePanelViewModel.cs`. It is likely already injecting `IContentDialogService` (because the panel shows data source actions). Add `IDataPointReconciliationSweepService sweepService` to the constructor. If the constructor grows large, that is acceptable — DI will handle it.

If `DataSourcePanelViewModel` does NOT have `IContentDialogService`, add it for the rebuild command's completion dialog.

### Testing framework and patterns

xUnit + FluentAssertions + Moq. Standard in-memory SQLite setup:

```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>()
    .UseSqlite(_connection)
    .Options;
using var ctx = new CalendarDbContext(options);
ctx.Database.EnsureCreated();
_contextFactory = new TestDbContextFactory(options);
```

`TestDbContextFactory` is a private nested class already defined in existing integration tests (e.g., `GoogleCalendarSyncTests.cs`, `SpotifyImportServiceTests.cs`) — do not create a duplicate; use the same pattern or reference the existing one if it is in a shared fixture.

For mocking the projector registry:

```csharp
var mockProjector = new Mock<IDataPointProjector>();
mockProjector.Setup(p => p.SourceKey).Returns("test_source");
mockProjector.Setup(p => p.GetOrphanedSpecsAsync(It.IsAny<CalendarDbContext>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<DataPointSpec>
    {
        new("test_source", "ref-1", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)),
        new("test_source", "ref-2", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow)
    });

var mockRegistry = new Mock<IDataPointProjectorRegistry>();
mockRegistry.Setup(r => r.GetProjector("test_source")).Returns(mockProjector.Object);
mockRegistry.Setup(r => r.GetAllProjectors()).Returns(new[] { mockProjector.Object });
```

### `Constants/SourceKeys.cs` — already defined in 8.7

Story 8.7 specifies creating `Constants/SourceKeys.cs` with canonical `source_key` string constants. If this file exists after 8.7 completes, use `SourceKeys.*` constants in the sweep service tests rather than magic strings. If 8.7 has not yet been implemented, create a minimal version with just the constants needed for the tests.

### What this story does NOT do

- Does NOT implement any per-source projector (Spotify, Toggl, Civ5, etc.) — that is Story 8.9
- Does NOT add a link table, coverage computation, or block/clump logic — Stories 8.10–8.12
- Does NOT modify any import service's internal `ImportAsync` logic — handlers keep delegating as before
- Does NOT make `RunPostImportAsync` run in the same DB transaction as the raw insert — the sweep runs after the import, not atomically with it. True transactional guarantee is deferred to Story 8.9 when projectors can be called within the import service transactions.
- Does NOT delete `ISourcePointerResolver` or `ISourcePointerResolverRegistry` from Story 8.7 — those remain for the Linking Panel's display-name resolution

### Project structure

New files:
- `Services/IDataPointProjector.cs` — interface + `DataPointSpec` record
- `Services/IDataPointProjectorRegistry.cs`
- `Services/DataPointProjectorRegistry.cs`
- `Services/IDataPointReconciliationSweepService.cs`
- `Services/DataPointReconciliationSweepService.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/DataPointProjectorGuardTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/DataPointReconciliationSweepServiceTests.cs`

Modified files:
- `Services/IDataSourceImportHandler.cs` — add `IDataPointProjector? GetProjector() => null;`
- `App.xaml.cs` — add `IDataPointProjectorRegistry` and `IDataPointReconciliationSweepService` DI + startup sweep call
- `ViewModels/DataSourcePanelViewModel.cs` — add `RebuildDataPointRegistryCommand`

### References

- Canonical datapoint schema: [concepts.md §4](../concepts.md)
- Source pointer resolution contract (8.7): [8-7-data-point-registry-table-and-source-pointer.md](./8-7-data-point-registry-table-and-source-pointer.md) — defines `ISourcePointerResolver`, `SourceKeys.cs`, `DataPoint` entity
- Epic 8 overview 8.8 spec: [epic-overview.md §Phase 1 Story 8.8](../epic-overview.md)
- Per-source projector implementations (8.9 context): [epic-overview.md §Phase 1 Story 8.9](../epic-overview.md)
- Existing handler interface: `Services/IDataSourceImportHandler.cs`
- All 10 concrete handlers: `Services/*ImportHandler.cs`
- Handler registry pattern reference: `Services/DataSourceImportHandlerRegistry.cs`
- `DataPoint` entity (from 8.7): `Data/Entities/DataPoint.cs`
- `CalendarDbContext.DataPoints` DbSet (from 8.7): `Data/CalendarDbContext.cs`
- DI registration location: `App.xaml.cs` (~lines 268–310 and startup wiring ~lines 50–95)
- `DataSourcePanelViewModel`: `ViewModels/DataSourcePanelViewModel.cs`
- Test pattern reference: `GoogleCalendarManagement.Tests/Unit/Services/SpotifyImportServiceTests.cs`

---

## Dev Agent Record

### Agent Model Used

Opus

### Debug Log References

### Completion Notes List

### File List

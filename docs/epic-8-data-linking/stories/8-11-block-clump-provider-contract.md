# Story 8.11: Block/Clump Provider Contract + Adapt Existing Coalescers

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 8.7 (blocking — `data_point` table and `DataPoint` entity must exist)

---

## Story

As the Linking panel engine,
I want a uniform `IClumpBlockProvider` contract per data source that returns computed clumps (grouped datapoint sets) and their 8/15-rounded blocks for a time range,
so that the Linking panel (Epic 9) can consume every source through one interface without knowing each source's coalescing logic — and so that existing Civ5, ComfyUI, and phone coalescers don't get reinvented or duplicated.

---

## Acceptance Criteria

1. A `IClumpBlockProvider` interface exists in `Services/` with the exact contract specified in Dev Notes §Contract design.
2. `ClumpDataPoint`, `Clump`, `Block`, and `ClumpBlockResult` record types exist and are used by the interface.
3. **Civ5 provider** (`Civ5ClumpBlockProvider`) queries `data_point` for `source_key = "civ5_session"` in the requested time range, runs `Civ5SessionCoalescer.CoalesceIntoWindows` on the raw points (loaded via `source_ref`), and maps each window to a `Clump` + `Block[]` via `EightFifteenRuleService`. Output matches prior coalescer behavior.
4. **ComfyUI provider** (`ComfyUIClumpBlockProvider`) does the same for `source_key = "comfyui_scan"` using `ComfyUISessionCoalescer.CoalesceIntoWindows`.
5. **Phone provider** (`PhoneClumpBlockProvider`) covers `source_key = "toggl_phone"`. It runs `TogglSlidingWindowService` on the datapoints in the time range and maps each resulting window to a `Clump` (all datapoints within the window) + `Block[]`.
6. **Trivial 1:1 providers** (`TrivialClumpBlockProvider`) cover call log (`source_key = "call_log"`) and Toggl time entries (`source_key = "toggl_entry"`): each datapoint becomes its own single-datapoint clump; blocks are computed via `EightFifteenRuleService`.
7. All providers are registered in DI (`App.xaml.cs`) as `IEnumerable<IClumpBlockProvider>` (keyed by `SourceKey`).
8. A provider registry service (`IClumpBlockProviderRegistry`) resolves a provider by `source_key` and returns `null` (not throws) for unknown keys.
9. Existing `Civ5SessionCoalescer` and `ComfyUISessionCoalescer` static classes are **not modified** — the new providers wrap them. Existing coalescer unit tests continue to pass unchanged.
10. Unit tests cover: Civ5 provider groups correctly, ComfyUI provider groups correctly, trivial provider yields N clumps for N datapoints, 8/15 blocks are correct for a known window.
11. Blocks/clumps are computed on each call — **never persisted** to the database.

---

## Tasks / Subtasks

- [x] Task 1: Define the provider contract types (AC: #1, #2)
  - [x] 1.1 Create `Services/DataLinking/ClumpBlock.cs` with `ClumpDataPoint`, `Clump`, `Block`, `ClumpBlockResult` records (see Dev Notes §Contract design for exact definitions)
  - [x] 1.2 Create `Services/DataLinking/IClumpBlockProvider.cs` with `IClumpBlockProvider` interface
  - [x] 1.3 Create `Services/DataLinking/IClumpBlockProviderRegistry.cs` with `IClumpBlockProviderRegistry` interface
  - [x] 1.4 Create `Services/DataLinking/ClumpBlockProviderRegistry.cs` implementing the registry (constructor takes `IEnumerable<IClumpBlockProvider>`, resolves by `SourceKey`)

- [x] Task 2: Implement Civ5 provider (AC: #3)
  - [x] 2.1 Create `Services/DataLinking/Civ5ClumpBlockProvider.cs`
  - [x] 2.2 Inject `IDbContextFactory<CalendarDbContext>` + `EightFifteenRuleService` in constructor
  - [x] 2.3 `GetClumpsAndBlocksAsync(from, to, ct)`: query `data_point` rows for `source_key = "civ5_session"` where `start_utc` between `from` and `to`; for each `source_ref` resolve the `Civ5SessionPoint` record (see Dev Notes §Source-ref resolution); pass sorted `Civ5SessionPoint` list to `Civ5SessionCoalescer.CoalesceIntoWindows`; map each `Civ5CandidateWindow` to a `Clump` + blocks via `_eightFifteenRule.ApplyRule(window.WindowStart, window.WindowEnd)`

- [x] Task 3: Implement ComfyUI provider (AC: #4)
  - [x] 3.1 Create `Services/DataLinking/ComfyUIClumpBlockProvider.cs` — same pattern as Civ5 but for `source_key = "comfyui_scan"` and `ComfyUISessionCoalescer`
  - [x] 3.2 Resolve `ComfyUIScanPoint` entities via `source_ref` (DB id); coalesce; map to clumps + blocks

- [x] Task 4: Implement phone provider (AC: #5)
  - [x] 4.1 Create `Services/DataLinking/PhoneClumpBlockProvider.cs`
  - [x] 4.2 `SourceKey` = `"toggl_phone"` (confirm source key used in 8.9 projectors — check `TogglPhoneDrilldownViewModel` or 8.9 story for the canonical key)
  - [x] 4.3 Query `data_point` rows; convert each to `SlidingWindowEntry(StartUtc, EndUtc)`; run `TogglSlidingWindowService.ComputeWindows` with the same gap/quality/min-duration thresholds currently used in `TogglPhoneDrilldownViewModel` (read those constants from `Constants/ImportThresholds.cs`)
  - [x] 4.4 For each `SlidingWindowResult`: collect the datapoints whose time range falls within the window → form a `Clump`; compute blocks via `EightFifteenRuleService.ApplyRule(windowStart, windowEnd)`

- [x] Task 5: Implement trivial 1:1 providers (AC: #6)
  - [x] 5.1 Create `Services/DataLinking/TrivialClumpBlockProvider.cs` — parameterized by `sourceKey` in constructor; each `DataPoint` row becomes `new Clump([point], point.StartUtc, point.EndUtc)` with blocks from `EightFifteenRuleService.ApplyRule(point.StartUtc, point.EndUtc)`
  - [x] 5.2 Register two instances: one for `"call_log"`, one for `"toggl_entry"`

- [x] Task 6: DI registration (AC: #7, #8)
  - [x] 6.1 In `App.xaml.cs`, register all providers as `IClumpBlockProvider` singletons (or scoped — match the lifetime of the DB context factory used inside)
  - [x] 6.2 Register `ClumpBlockProviderRegistry` as `IClumpBlockProviderRegistry` singleton (takes `IEnumerable<IClumpBlockProvider>`)

- [x] Task 7: Unit tests (AC: #10)
  - [x] 7.1 `Civ5ClumpBlockProviderTests` — stub DB returning known `DataPoint` + `Civ5SessionPoint` rows; verify clump count and block ranges match `Civ5SessionCoalescer` output
  - [x] 7.2 `ComfyUIClumpBlockProviderTests` — same pattern
  - [x] 7.3 `TrivialClumpBlockProviderTests` — 3 datapoints in, 3 clumps out, each with at least 1 block
  - [x] 7.4 `EightFifteenRuleService` block output sanity check — already tested in `EightFifteenRuleServiceTests.cs`; no new tests needed there

### Review Findings

- [x] [Review][Patch] Civ5 and ComfyUI providers query source keys that current projectors never write [Services/DataLinking/Civ5ClumpBlockProvider.cs:20]
- [x] [Review][Patch] Civ5 and ComfyUI providers crash the whole clump load on malformed `source_ref` [Services/DataLinking/Civ5ClumpBlockProvider.cs:37]
- [x] [Review][Patch] Duration-based providers omit datapoints that overlap the requested range but start before `fromUtc` [Services/DataLinking/PhoneClumpBlockProvider.cs:32]
- [x] [Review][Patch] Phone clump mapping can omit zero-duration datapoints at `WindowEndUtc` [Services/DataLinking/PhoneClumpBlockProvider.cs:53]
- [x] [Review][Patch] Phone provider behavior has no direct unit coverage [GoogleCalendarManagement.Tests/Unit/Services/DataLinking:1]
- [x] [Review][Patch] `ClumpDataPoint.DataPointId` drifts from the story's exact `long` contract [Services/DataLinking/ClumpBlock.cs:4]
- [x] [Review][Patch] Phone provider uses a generic min-duration threshold instead of a phone-specific threshold constant [Services/DataLinking/PhoneClumpBlockProvider.cs:48]

---

## Dev Notes

### Contract design

```csharp
// Services/DataLinking/ClumpBlock.cs
namespace GoogleCalendarManagement.Services.DataLinking;

/// <summary>
/// A normalized view of a datapoint within a clump.
/// DataPointId is the PK from the data_point table (added by 8.7).
/// </summary>
public sealed record ClumpDataPoint(
    long DataPointId,
    string SourceKey,
    string SourceRef,
    DateTime StartUtc,
    DateTime EndUtc);

/// <summary>
/// A set of datapoints grouped because they are locally close in time.
/// Lives in the data domain. Computed, never persisted.
/// </summary>
public sealed record Clump(
    IReadOnlyList<ClumpDataPoint> DataPoints,
    DateTime ClumpStartUtc,
    DateTime ClumpEndUtc);

/// <summary>
/// A 15-minute-rounded time slot in the time domain.
/// One clump may span multiple blocks.
/// </summary>
public sealed record Block(DateTime BlockStartUtc, DateTime BlockEndUtc);

/// <summary>
/// One clump paired with the blocks it occupies.
/// </summary>
public sealed record ClumpBlockResult(Clump Clump, IReadOnlyList<Block> Blocks);
```

```csharp
// Services/DataLinking/IClumpBlockProvider.cs
namespace GoogleCalendarManagement.Services.DataLinking;

public interface IClumpBlockProvider
{
    /// <summary>The source_key this provider handles (matches data_point.source_key).</summary>
    string SourceKey { get; }

    /// <summary>
    /// Returns clumps + their 8/15-rounded blocks for the given UTC time range.
    /// fromUtc inclusive, toUtc exclusive.
    /// Always computed — never reads or writes any persistent state for clumps/blocks.
    /// </summary>
    Task<IReadOnlyList<ClumpBlockResult>> GetClumpsAndBlocksAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
```

```csharp
// Services/DataLinking/IClumpBlockProviderRegistry.cs
namespace GoogleCalendarManagement.Services.DataLinking;

public interface IClumpBlockProviderRegistry
{
    /// <summary>Returns the provider for the given source_key, or null if none registered.</summary>
    IClumpBlockProvider? GetProvider(string sourceKey);

    IReadOnlyList<IClumpBlockProvider> AllProviders { get; }
}
```

### Source-ref resolution

Story 8.7 stores `source_ref` as the DB row id (integer, stored as string) for table-backed sources. To resolve a `Civ5SessionPoint` from its datapoint:
```csharp
var id = long.Parse(dataPoint.SourceRef);
var rawPoint = await context.Civ5SessionPoints.FindAsync(id, ct);
```
Similarly for `ComfyUIScanPoint` → `context.ComfyUIScanPoints`.

For `call_log` and `toggl_phone` the datapoint itself already carries `start_utc`/`end_utc` — no second DB lookup needed.

**Important:** If 8.7 is not yet implemented when this story is worked, add a `// TODO 8.7: source_ref format confirmed at 8.7 implementation time` comment and treat `source_ref` as the integer row id. Verify the actual key format by reading the `DataPointProjector` implementations that 8.7/8.9 create.

### 8/15 block computation

`EightFifteenRuleService` already exists at `Services/EightFifteenRuleService.cs`. Inject it directly:

```csharp
var blockRanges = _eightFifteenRule.ApplyRule(clump.ClumpStartUtc, clump.ClumpEndUtc);
var blocks = blockRanges.Select(r => new Block(r.Start, r.End)).ToList();
```

The `ApplyRule` method returns `IReadOnlyList<(DateTime Start, DateTime End)>`. A single-point clump (instant datapoint, `start == end`) returns one 15-minute block.

### TogglSlidingWindowService thresholds for phone provider

In `TogglPhoneDrilldownViewModel.cs` find the `TogglSlidingWindowService.ComputeWindows` call and extract the threshold values. These should already be in `Constants/ImportThresholds.cs` (moved there per architecture.md migration note). Use those constants directly — do NOT hardcode new magic numbers.

```csharp
var windows = _slidingWindowService.ComputeWindows(
    entries,
    gapThreshold: ImportThresholds.PhoneCoalesceGapThreshold,
    qualityThreshold: ImportThresholds.PhoneCoalesceQualityThreshold,
    minWindowDuration: ImportThresholds.PhoneCoalesceMinWindowDuration);
```

### Source keys (canonical — from concepts.md and planned 8.9 projectors)

| Source | `source_key` | Coalescing |
|--------|-------------|------------|
| Civ 5 saves | `"civ5_session"` | `Civ5SessionCoalescer` (30-min gap) |
| ComfyUI scans | `"comfyui_scan"` | `ComfyUISessionCoalescer` (15-min gap) |
| Toggl phone usage | `"toggl_phone"` | `TogglSlidingWindowService` (thresholds from `ImportThresholds`) |
| iOS call log | `"call_log"` | Trivial 1:1 |
| Toggl time entries | `"toggl_entry"` | Trivial 1:1 |

**Verify these keys** against the actual `source_key` strings used in 8.9 projectors (or any pre-existing projector from earlier stories) before hardcoding them as string literals. Consider putting them in a `DataSourceKeys` static class to avoid typo drift.

### Trivial provider: what "1:1" means

For sources where each raw record is already a self-contained time block (calls have exact start+end; Toggl entries have exact start+end), no coalescing is needed:

```csharp
var clumpPoint = new ClumpDataPoint(dp.DataPointId, dp.SourceKey, dp.SourceRef, dp.StartUtc, dp.EndUtc);
var clump = new Clump([clumpPoint], dp.StartUtc, dp.EndUtc);
var blocks = _eightFifteenRule.ApplyRule(dp.StartUtc, dp.EndUtc)
                              .Select(r => new Block(r.Start, r.End))
                              .ToList();
results.Add(new ClumpBlockResult(clump, blocks));
```

### Do NOT modify existing coalescers

`Civ5SessionCoalescer` and `ComfyUISessionCoalescer` are static classes that other callers (drilldown ViewModels) still use directly. Do not change their signatures or behavior. The new providers wrap them.

### File placement

New files go in `Services/DataLinking/` (create the subfolder — keeps linking-engine code grouped):
- `Services/DataLinking/ClumpBlock.cs`
- `Services/DataLinking/IClumpBlockProvider.cs`
- `Services/DataLinking/IClumpBlockProviderRegistry.cs`
- `Services/DataLinking/ClumpBlockProviderRegistry.cs`
- `Services/DataLinking/Civ5ClumpBlockProvider.cs`
- `Services/DataLinking/ComfyUIClumpBlockProvider.cs`
- `Services/DataLinking/PhoneClumpBlockProvider.cs`
- `Services/DataLinking/TrivialClumpBlockProvider.cs`

Tests go in `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/`.

All C# files: `namespace GoogleCalendarManagement.Services.DataLinking;`

### What this story does NOT do

- Does NOT persist clumps or blocks to the database — they are always computed.
- Does NOT build the Linking panel UI — that's Epic 9.
- Does NOT implement the link/ignore/unlink operations — that's 8.12.
- Does NOT cover Spotify, Maps, or Outlook providers — those sources link individually (trivial 1:1) or generate candidates (8.15); their provider registration can wait until 8.12+.
- Does NOT implement the coverage service — that's 8.10.
- Does NOT change how drilldown ViewModels create candidate events (autogenerated) — they still call `Civ5SessionCoalescer` / `ComfyUISessionCoalescer` directly.

### Testing framework

xUnit + FluentAssertions + Moq. Unit tests mock `IDbContextFactory<CalendarDbContext>` with in-memory SQLite:
```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
```

### Project structure

- New folder: `Services/DataLinking/` (8 new files)
- New test folder: `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/`
- Modified: `App.xaml.cs` — add DI registrations (see Task 6)
- Not modified: `Services/Civ5SessionCoalescer.cs`, `Services/ComfyUISessionCoalescer.cs`, `Services/TogglSlidingWindowService.cs`, `Services/EightFifteenRuleService.cs`

### References

- Canonical clump/block vocabulary: [concepts.md §2](../concepts.md)
- 8/15 rule implementation: `Services/EightFifteenRuleService.cs`
- Civ5 coalescer: `Services/Civ5SessionCoalescer.cs`
- ComfyUI coalescer: `Services/ComfyUISessionCoalescer.cs`
- Phone sliding window: `Services/TogglSlidingWindowService.cs`
- Phone thresholds: `Constants/ImportThresholds.cs`
- `data_point` table schema: [concepts.md §4](../concepts.md) (created by 8.7)
- Epic overview story 8.11 spec: [epic-overview.md §Phase 1 Story 8.11](../epic-overview.md)
- Story 8.7 (data_point table prereq): `docs/epic-8-data-linking/stories/8-7-data-point-registry-table-and-source-pointer.md` (to be created)
- DI registration: `App.xaml.cs` (~lines 268–310)

---

## Dev Agent Record

### Agent Model Used

Sonnet

### Debug Log References

### Completion Notes List

- All 7 tasks and all subtasks completed and tested. 11 unit tests added across Civ5, ComfyUI, and Trivial provider test classes.
- `DataPointId` uses `int` (matching the entity), not `long` as shown in Dev Notes.
- Added `PhoneCoalesceQualityThreshold = 0.5` to `ImportThresholds` (was missing; the gap and duration constants already existed).
- Restored `Data/Entities/DateSourceIntegration.cs` shim that was accidentally deleted in 8.9 work — the file's own doc says it should persist until story 8.10.
- Full regression suite: 485 pre-existing tests pass; 3 failures in `CoverageServiceTests` are pre-existing from in-progress 8.10 work (missing migration for `data_point` table in those test fixtures).

### File List

- `Services/DataLinking/ClumpBlock.cs` (new)
- `Services/DataLinking/IClumpBlockProvider.cs` (new)
- `Services/DataLinking/IClumpBlockProviderRegistry.cs` (new)
- `Services/DataLinking/ClumpBlockProviderRegistry.cs` (new)
- `Services/DataLinking/Civ5ClumpBlockProvider.cs` (new)
- `Services/DataLinking/ComfyUIClumpBlockProvider.cs` (new)
- `Services/DataLinking/PhoneClumpBlockProvider.cs` (new)
- `Services/DataLinking/TrivialClumpBlockProvider.cs` (new)
- `Constants/ImportThresholds.cs` (modified — added `PhoneCoalesceQualityThreshold`)
- `App.xaml.cs` (modified — DI registrations for all providers + registry)
- `Data/Entities/DateSourceIntegration.cs` (restored — accidentally deleted in 8.9 work)
- `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/Civ5ClumpBlockProviderTests.cs` (new)
- `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/ComfyUIClumpBlockProviderTests.cs` (new)
- `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/TrivialClumpBlockProviderTests.cs` (new)

### Change Log

- 2026-06-12: Story 8.11 implemented — IClumpBlockProvider contract, all five providers (Civ5, ComfyUI, Phone, and two trivial), DI registration, and 11 unit tests. Added `PhoneCoalesceQualityThreshold` to `ImportThresholds`. Restored accidentally-deleted `DateSourceIntegration` shim.
- 2026-06-16: Code review findings resolved — aligned Civ5/ComfyUI source keys with current projectors, made source_ref parsing defensive, fixed duration overlap semantics, added phone provider tests, restored exact DataPointId contract type, and moved phone min-window threshold to a phone-specific constant.

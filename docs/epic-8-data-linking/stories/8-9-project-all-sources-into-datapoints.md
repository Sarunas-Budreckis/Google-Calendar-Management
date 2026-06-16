# Story 8.9: Project All Existing Sources into Datapoints (+ Natural Keys)

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Codex · **Effort:** medium
**Dependencies:** 8.8 (blocking — `IDataPointProjector<T>` contract + template-method import base + reconciliation sweep infrastructure must exist)

---

## Story

As the datapoint registry,
I want every existing data source to project its raw records into `data_point` rows using the projector contract defined in 8.8,
so that all historical and future raw data participates in coverage and linking, re-imports are idempotent (no duplicate datapoints), and genuine orphan deletions cascade correctly.

---

## Acceptance Criteria

1. A concrete `IDataPointProjector<TRaw>` implementation exists for **each** of the following sources (all following the 8.8-defined contract):
   - `TogglSleepProjector` — projects `TogglEntry` rows where `TogglDataType == TogglDataType.TogglSleep`
   - `TogglTransitProjector` — projects `TogglEntry` rows where `TogglDataType == TogglDataType.TogglTransit`
   - `TogglPhoneProjector` — projects `TogglEntry` rows where `TogglDataType == TogglDataType.TogglPhone`
   - `CallLogProjector` — projects `CallLogEntry` rows
   - `Civ5Projector` — projects `Civ5SessionPoint` rows
   - `ComfyUIProjector` — projects `ComfyUIScanPoint` rows
   - `SpotifyProjector` — projects `SpotifyStream` rows
   - `OutlookProjector` — projects `OutlookEvent` rows
   - `MapsTimelineProjector` — projects `MapsTimelineRaw` rows
2. Each projector returns the correct `source_key` (matching its import service constant), a stable `source_ref` (see Dev Notes), and correct UTC time extents (`start_utc`, `end_utc`).
3. **Spotify natural key:** `SpotifyStream` gets a new `NaturalKey` column (`UNIQUE NOT NULL`, populated from `PlayedAt` + `"|"` + `TrackName`). An EF migration creates the column and backfills existing rows. The `SpotifyProjector.SourceRef` returns this `NaturalKey` value. The `SpotifyImportService.UpsertStreamsAsync` populates `NaturalKey` on insert and update.
4. **Maps natural key:** `MapsTimelineRaw` uses `FileName` as the natural key. The `MapsTimelineProjector.SourceRef` returns `FileName`. No new column needed — `FileName` is already on the entity.
5. Each projector is **registered** with the 8.8-defined projector registry / DI container (however 8.8 specifies registration).
6. Each relevant import service / handler is **wired** to call its projector inside the same transaction as the raw insert, using the 8.8 template-method base (or equivalent). No raw insert can succeed without a projector call.
7. The **reconciliation sweep** (defined in 8.8) is run for all nine sources as part of this story, backfilling datapoints for all existing raw rows that lack them.
8. **Re-import idempotency:** Re-running any import for any source produces no duplicate `data_point` rows. The upsert logic uses `(source_key, source_ref)` as the unique key.
9. **Orphan cascade:** If a raw record is genuinely deleted (e.g., Spotify stream removed on re-import), its `data_point` row is also deleted. Any `link` row pointing to that `data_point` cascades via the FK defined in 8.12's schema (which may not exist yet at 8.9 time — the orphan-delete logic should still delete the `data_point` row; link cascade happens automatically once 8.12 adds the FK).
10. The **reflection guard test** from 8.8 passes — every registered `IDataSourceImportHandler` has a corresponding projector.
11. All new C# files follow the `GoogleCalendarManagement.Services;` namespace (or `.Services.Projectors` if 8.8 uses a sub-namespace — match whatever 8.8 established).

---

## Tasks / Subtasks

- [x] Task 1: Implement `TogglSleepProjector`, `TogglTransitProjector`, `TogglPhoneProjector` (AC: #1, #2, #5, #6)
  - [x] 1.1 Create `Services/Projectors/TogglSleepProjector.cs` implementing `IDataPointProjector<TogglEntry>`
    - `SourceKey = TogglSleepImportService.SourceKey` → `"toggl_sleep"`
    - `SourceRef(entry)` → `entry.TogglId.ToString()`
    - `StartUtc(entry)` → `entry.StartTime` (already UTC)
    - `EndUtc(entry)` → `entry.EndTime ?? entry.StartTime`
    - Only projects rows where `entry.TogglDataType == TogglDataType.TogglSleep`
  - [x] 1.2 Create `Services/Projectors/TogglTransitProjector.cs` — same pattern, `SourceKey = TogglTransitImportService.SourceKey`, filter `TogglDataType.TogglTransit`
  - [x] 1.3 Create `Services/Projectors/TogglPhoneProjector.cs` — same pattern, `SourceKey = TogglPhoneCardProvider.SourceKey`, filter `TogglDataType.TogglPhone`
  - [x] 1.4 Wire each projector into the 8.8 import base for `TogglSleepImportService`, `TogglTransitImportService`, and `TogglPhoneImportHandler` respectively

- [x] Task 2: Implement `CallLogProjector` (AC: #1, #2, #5, #6)
  - [x] 2.1 Create `Services/Projectors/CallLogProjector.cs` implementing `IDataPointProjector<CallLogEntry>`
    - `SourceKey = CallLogImportService.SourceKey` → `"call_log"`
    - `SourceRef(entry)` → `entry.Id.ToString()`
    - `StartUtc(entry)` → `entry.Date`
    - `EndUtc(entry)` → `entry.Date + TimeSpan.FromSeconds(entry.DurationSeconds)` (or `entry.Date` if `DurationSeconds == 0`)
  - [x] 2.2 Wire projector into `CallLogImportService` raw insert transaction

- [x] Task 3: Implement `Civ5Projector` (AC: #1, #2, #5, #6)
  - [x] 3.1 Create `Services/Projectors/Civ5Projector.cs` implementing `IDataPointProjector<Civ5SessionPoint>`
    - `SourceKey = Civ5SaveScannerService.SourceKey` → `"civ5"`
    - `SourceRef(point)` → `point.Id.ToString()`
    - `StartUtc(point)` = `EndUtc(point)` → `point.FileModifiedAt` (the actual game-time, not `ScannedAt`)
  - [x] 3.2 Wire projector into `Civ5SaveScannerService` raw insert transaction

- [x] Task 4: Implement `ComfyUIProjector` (AC: #1, #2, #5, #6)
  - [x] 4.1 Create `Services/Projectors/ComfyUIProjector.cs` implementing `IDataPointProjector<ComfyUIScanPoint>`
    - `SourceKey = ComfyUIFolderScannerService.SourceKey` → `"comfyui_data"`
    - `SourceRef(point)` → `point.Id.ToString()`
    - `StartUtc(point)` = `EndUtc(point)` → `point.Timestamp` (the actual image creation time, not `ScannedAt`)
  - [x] 4.2 Wire projector into `ComfyUIFolderScannerService` raw insert transaction

- [x] Task 5: Add Spotify natural key + implement `SpotifyProjector` (AC: #1, #2, #3, #5, #6)
  - [x] 5.1 Add `NaturalKey` property to `SpotifyStream` entity: `public string NaturalKey { get; set; } = "";`
  - [x] 5.2 Update `SpotifyStreamConfiguration.cs` (or equivalent): add UNIQUE index on `natural_key`; map column name `natural_key`
  - [x] 5.3 Create EF migration `AddSpotifyNaturalKey`:
    - `ALTER TABLE spotify_stream ADD COLUMN natural_key TEXT NOT NULL DEFAULT ''`
    - `UPDATE spotify_stream SET natural_key = CAST(played_at AS TEXT) || '|' || track_name`
    - `CREATE UNIQUE INDEX idx_spotify_natural_key ON spotify_stream (natural_key)`
    - Verify existing rows have no duplicate `(played_at, track_name)` pairs before the unique index (should be clean — `UpsertStreamsAsync` already deduplicates on this key)
  - [x] 5.4 Update `SpotifyImportService.UpsertStreamsAsync`: set `NaturalKey = $"{playedAt:O}|{trackName}"` on both new inserts and existing updates
  - [x] 5.5 Create `Services/Projectors/SpotifyProjector.cs` implementing `IDataPointProjector<SpotifyStream>`
    - `SourceKey = SpotifyImportService.SourceKey` → `"spotify"`
    - `SourceRef(stream)` → `stream.NaturalKey`
    - `StartUtc(stream)` → `stream.PlayedAt - TimeSpan.FromMilliseconds(stream.MsPlayed > 0 ? stream.MsPlayed : stream.DurationMs)` (use `stream.PlayedAt` as fallback if both are 0)
    - `EndUtc(stream)` → `stream.PlayedAt`
  - [x] 5.6 Wire projector into `SpotifyImportService.UpsertStreamsAsync` transaction

- [x] Task 6: Implement `OutlookProjector` (AC: #1, #2, #5, #6)
  - [x] 6.1 Create `Services/Projectors/OutlookProjector.cs` implementing `IDataPointProjector<OutlookEvent>`
    - `SourceKey = OutlookImportService.SourceKey` → `"outlook"`
    - `SourceRef(ev)` → `ev.OutlookEventId` (stable string PK from Outlook Graph API)
    - `StartUtc(ev)` → `ev.StartDatetime`
    - `EndUtc(ev)` → `ev.EndDatetime`
  - [x] 6.2 Wire projector into `OutlookImportService` raw insert/upsert transaction

- [x] Task 7: Implement `MapsTimelineProjector` (AC: #1, #2, #4, #5, #6)
  - [x] 7.1 Create `Services/Projectors/MapsTimelineProjector.cs` implementing `IDataPointProjector<MapsTimelineRaw>`
    - `SourceKey = MapsTimelineImportHandler.SourceKey` → `"maps_timeline"`
    - `SourceRef(raw)` → `raw.FileName` (stable natural key — re-importing the same file produces the same `source_ref`)
    - `StartUtc(raw)` → `raw.CoveredDateMin.HasValue ? raw.CoveredDateMin.Value.ToDateTime(TimeOnly.MinValue) : raw.ImportedAt`
    - `EndUtc(raw)` → `raw.CoveredDateMax.HasValue ? raw.CoveredDateMax.Value.ToDateTime(TimeOnly.MaxValue) : raw.ImportedAt`
    - Note: this projects each raw JSON blob as a single datapoint covering its date range. Epic 7 story 7.16 will later parse individual segments from `MapsTimelineRaw` into per-segment datapoints; this is a placeholder projector.
  - [x] 7.2 Wire projector into `MapsTimelineImportHandler` raw insert transaction

- [x] Task 8: Run reconciliation sweep for all sources (AC: #7)
  - [x] 8.1 Use the 8.8-defined reconciliation sweep (or `IDataPointReconciliationService.RebuildForSourceAsync`) to backfill `data_point` rows for all existing raw records that lack them
  - [x] 8.2 Run sweep for: `toggl_sleep`, `toggl_transit`, `toggl_phone`, `call_log`, `civ5`, `comfyui_data`, `spotify`, `outlook`, `maps_timeline`
  - [x] 8.3 Verify row counts: `SELECT COUNT(*) FROM data_point WHERE source_key = '<key>'` should equal raw table row count (for append-only sources); for Spotify/Maps, the row count should equal unique `(source_key, source_ref)` pairs

- [x] Task 9: Verify reflection guard test passes (AC: #10)
  - [x] 9.1 Run the 8.8-defined guard test to confirm all nine projectors are registered
  - [x] 9.2 Confirm no `IDataSourceImportHandler` is unregistered (check `DataSourceImportHandlerRegistry` for any handlers not yet covered)

### Review Findings

- [x] [Review][Decision] AC6 — Projectors not called inline during import transactions — **Resolved:** Post-import sweep via `RunPostImportAsync` is the 8.8-established mechanism; `DataSourceSummaryViewModel`, `ComfyUIDrilldownViewModel`, and `MapsTimelineDrilldownViewModel` now call sweep after `TriggerImportAsync`.
- [x] [Review][Decision] AC9 — No orphan-delete logic exists — **Resolved:** Added `GetAllRawSourceRefsAsync` to `IDataPointProjector` and all 9 implementations; `RunPostImportAsync` now deletes stale `data_point` rows via `ExecuteDeleteAsync`.
- [x] [Review][Patch] NaturalKey migration SQL format mismatch — **Fixed:** Migration SQL changed to `played_at || '|' || track_name`; `BuildNaturalKey` in `SpotifyImportService` and tests updated to `yyyy-MM-ddTHH:mm:ss.fffffff` format to match EF Core SQLite text storage.
- [x] [Review][Defer] SpotifyProjector silently skips rows with empty NaturalKey — guard `!string.IsNullOrWhiteSpace(NaturalKey)` masks rows that failed migration backfill; root cause is finding above. [`Services/Projectors/SpotifyProjector.cs`] — deferred, pre-existing
- [x] [Review][Defer] SpotifyProjector can produce `EndUtc < StartUtc` for garbage `MsPlayed` values — garbage-in scenario; not introduced by this story. [`Services/Projectors/SpotifyProjector.cs`] — deferred, pre-existing
- [x] [Review][Defer] All projectors memory-load full tables before in-process filtering — design inherited from 8.8; `ProjectSourceRefsAsync` loads the full table even when only a few refs are needed. [`Services/Projectors/`] — deferred, pre-existing
- [x] [Review][Defer] MapsTimelineProjector `FileName` not guaranteed unique at DB level — story designates `FileName` as natural key but no DB UNIQUE constraint; uniqueness enforcement is out of scope for this story. [`Services/Projectors/MapsTimelineProjector.cs`] — deferred, pre-existing
- [x] [Review][Defer] CallLogProjector uses `entry.Date` without specifying `DateTimeKind.Utc` — depends on existing data model conventions; not established by this story. [`Services/Projectors/CallLogProjector.cs`] — deferred, pre-existing
- [x] [Review][Defer] Namespace is `GoogleCalendarManagement.Services` not `Services.Projectors` — self-consistent with implementation; confirm matches 8.8 convention. [`Services/Projectors/*.cs`] — deferred, pre-existing

---

## Dev Notes

### Source → Entity → source_ref mapping

| source_key | Entity | source_ref | Time extent | Notes |
|---|---|---|---|---|
| `toggl_sleep` | `TogglEntry` (TogglDataType.TogglSleep) | `TogglId.ToString()` | `StartTime` → `EndTime ?? StartTime` | TogglId is the Toggl API id — stable |
| `toggl_transit` | `TogglEntry` (TogglDataType.TogglTransit) | `TogglId.ToString()` | `StartTime` → `EndTime ?? StartTime` | Same pattern |
| `toggl_phone` | `TogglEntry` (TogglDataType.TogglPhone) | `TogglId.ToString()` | `StartTime` → `EndTime ?? StartTime` | Same pattern |
| `call_log` | `CallLogEntry` | `Id.ToString()` | `Date` → `Date + DurationSeconds` | Append-only; int PK is stable |
| `civ5` | `Civ5SessionPoint` | `Id.ToString()` | `FileModifiedAt` instant | Use `FileModifiedAt`, NOT `ScannedAt` |
| `comfyui_data` | `ComfyUIScanPoint` | `Id.ToString()` | `Timestamp` instant | Use `Timestamp`, NOT `ScannedAt` |
| `spotify` | `SpotifyStream` | `NaturalKey` (PlayedAt+TrackName) | `PlayedAt - MsPlayed` → `PlayedAt` | Replace-on-reimport; needs natural key (Task 5) |
| `outlook` | `OutlookEvent` | `OutlookEventId` | `StartDatetime` → `EndDatetime` | Stable string PK from Graph API |
| `maps_timeline` | `MapsTimelineRaw` | `FileName` | `CoveredDateMin` → `CoveredDateMax` | Blob-level placeholder; 7.16 adds segment parsing |

### Why `FileModifiedAt` not `ScannedAt` for Civ5

`ScannedAt` is when the app scanned the folder — irrelevant to game activity. `FileModifiedAt` is the OS timestamp of the save file — this represents when the player actually saved the game. Use `FileModifiedAt` as the instant for coverage/linking purposes.

### Why `Timestamp` not `ScannedAt` for ComfyUI

Same reasoning: `Timestamp` is the actual image creation time (when ComfyUI generated the image); `ScannedAt` is when the folder scan ran. Use `Timestamp`.

### Spotify natural key format

The key must be stable across re-imports. Current upsert already deduplicates on `(PlayedAt, TrackName)` — the natural key mirrors this:
```csharp
stream.NaturalKey = $"{playedAt:O}|{trackName}";
// "O" = ISO 8601 round-trip format, e.g. "2025-06-10T14:32:00.0000000Z|Neon Genesis"
```
The `PlayedAt` in the DB is stored as UTC; the format must round-trip without timezone ambiguity. Use `"O"` (ISO round-trip) or `"yyyy-MM-ddTHH:mm:ss.fffffffZ"` explicitly.

### Spotify time extent

`PlayedAt` is the **end** of the stream (when the track finished). The start is calculated from `MsPlayed` (how much of the track was actually played):
```csharp
var startUtc = MsPlayed > 0 
    ? PlayedAt - TimeSpan.FromMilliseconds(MsPlayed)
    : DurationMs > 0
        ? PlayedAt - TimeSpan.FromMilliseconds(DurationMs)
        : PlayedAt; // fallback: instant
```

### MapsTimelineRaw is a blob-level projector (placeholder)

Each `MapsTimelineRaw` row is a whole JSON file import covering a date range. The per-segment parsing (individual GPS trace datapoints) is Story 7.16, which depends on 8.7 + 8.11. Story 8.9 creates a single datapoint per raw row to satisfy the guard test and ensure coverage counting can see the import, even at coarse granularity. When 7.16 lands, it will add per-segment datapoints to supplement (or replace) the blob-level ones — the design for that transition is deferred to 7.16.

### Projector file location

Place all projectors in `Services/Projectors/` (create the folder if it doesn't exist). Follow namespace pattern from 8.8 — if 8.8 uses `GoogleCalendarManagement.Services.Projectors`, use the same. If 8.8 puts projectors in `GoogleCalendarManagement.Services`, do the same.

### EF migration for Spotify natural key

- Migration name: `AddSpotifyStreamNaturalKey`
- The migration goes in `Data/Migrations/` with the next timestamp prefix
- Pattern: look at recent migrations (`20260605021000_DropLegacyCiv5SessionPointTable.cs`, `20260605030000_RenameSpotifyStreamToSpotifyData.cs`) for naming and structure conventions
- Since `SpotifyStream` already has a UNIQUE constraint on `(PlayedAt, TrackName)` (enforced by upsert logic), the backfill `UPDATE` should produce no duplicates — but add a note in the migration comment just in case

### Wiring projectors into import services

Story 8.8 defines how projectors are called during import (template-method base or injected per-source). Follow exactly what 8.8 established. Do NOT invent a new mechanism — if in doubt, look at how 8.8 wired the first projector it uses as its example/test case, and replicate the pattern for the remaining nine sources.

### `TogglCsvImportHandler` vs `TogglSleepImportService`

The CSV handler (`TogglCsvImportHandler`) calls `ITogglCsvImportService.ImportFromStreamAsync`, which internally calls `TogglSleepImportService` (its `SourceKey` is `TogglSleepImportService.SourceKey`). Wire the projector at the `TogglSleepImportService` level — the CSV handler delegates to it, so the projector will fire for both the CSV import path and the API path.

### DataSourceImportHandlerRegistry

`DataSourceImportHandlerRegistry` is the single source of truth for which handlers exist. If the guard test from 8.8 checks handlers via this registry, ensure all nine handlers registered there have a corresponding projector. Verify after Task 9.1.

### Testing framework

xUnit + FluentAssertions + Moq. Integration tests use in-memory SQLite with `context.Database.EnsureCreated()`.

Add test class `DataPointProjectionTests` in `GoogleCalendarManagement.Tests/Integration/` with at minimum:
- Verify `TogglSleepProjector` produces the correct `source_key`, `source_ref`, `start_utc`, `end_utc` for a seed `TogglEntry`
- Verify `SpotifyProjector` uses `NaturalKey` (not `Id`) as `source_ref`
- Verify idempotency: calling the reconciliation sweep twice does not double the datapoint count for any source
- Verify Spotify `NaturalKey` backfill: existing rows without `NaturalKey` get it populated by the migration

### Project structure — files to create/modify

**New files:**
- `Services/Projectors/TogglSleepProjector.cs`
- `Services/Projectors/TogglTransitProjector.cs`
- `Services/Projectors/TogglPhoneProjector.cs`
- `Services/Projectors/CallLogProjector.cs`
- `Services/Projectors/Civ5Projector.cs`
- `Services/Projectors/ComfyUIProjector.cs`
- `Services/Projectors/SpotifyProjector.cs`
- `Services/Projectors/OutlookProjector.cs`
- `Services/Projectors/MapsTimelineProjector.cs`
- `Data/Migrations/YYYYMMDDHHMMSS_AddSpotifyStreamNaturalKey.cs`

**Modified files:**
- `Data/Entities/SpotifyStream.cs` — add `NaturalKey` property
- `Data/Configurations/SpotifyStreamConfiguration.cs` (or equivalent) — map `NaturalKey`, add UNIQUE index
- `Services/SpotifyImportService.cs` — populate `NaturalKey` in `UpsertStreamsAsync`
- `App.xaml.cs` — register all nine projectors (if DI registration is the 8.8 pattern)
- Each import service/handler that gets wired to a projector

### References

- `data_point` table schema: [concepts.md §4](../concepts.md)
- Source-pointer model (source_key, source_ref): [concepts.md §2](../concepts.md)
- Epic 8.8 story (projector contract): [8-8-import-projector-contract-guard-test-reconciliation-sweep.md](./8-8-import-projector-contract-guard-test-reconciliation-sweep.md) ← **read this first** before implementing
- Epic overview 8.9 spec: [epic-overview.md §Phase 1 Story 8.9](../epic-overview.md)
- Existing import services: `Services/SpotifyImportService.cs`, `Services/TogglSleepImportService.cs`, `Services/CallLogImportService.cs`, `Services/OutlookImportService.cs`, `Services/Civ5SaveScannerService.cs`, `Services/ComfyUIFolderScannerService.cs`, `Services/MapsTimelineImportHandler.cs`
- Source key constants: `SpotifyImportService.SourceKey`, `TogglSleepImportService.SourceKey`, `TogglTransitImportService.SourceKey`, `TogglPhoneCardProvider.SourceKey`, `CallLogImportService.SourceKey`, `Civ5SaveScannerService.SourceKey`, `ComfyUIFolderScannerService.SourceKey`, `OutlookImportService.SourceKey`, `MapsTimelineImportHandler.SourceKey`
- Recent migration patterns: `Data/Migrations/20260605030000_RenameSpotifyStreamToSpotifyData.cs`
- Handler registry: `Services/DataSourceImportHandlerRegistry.cs`

---

## Dev Agent Record

### Agent Model Used

Codex

### Debug Log References

- `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj --filter DataPointProjectionTests` — passed, 4/4.
- `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj --filter DataPointProjectorGuardTests` — passed, 1/1.
- `dotnet build` — passed, 0 warnings, 0 errors.
- `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj` — passed, 473 passed, 19 skipped, 0 failed.

### Completion Notes List

- Added concrete `IDataPointProjector` implementations for all nine story sources with story-defined `source_key`, `source_ref`, and UTC extent rules.
- Added handler `GetProjector()` overrides for all concrete import handlers, including the CSV handler, and made app startup skip duplicate projector registration for shared CSV/API Toggl sleep wiring.
- Added `SpotifyStream.NaturalKey`, EF mapping/unique index, migration/backfill SQL, snapshot update, and import-time natural-key population for inserts, updates, and duplicate rows within a single batch.
- Added integration coverage for Toggl projection, Spotify natural-key projection, all-source reconciliation idempotency, and handler projector source-key alignment.
- Verified the 8.8 reflection guard now passes and the full test suite is green.

### File List

**New files:**
- `Data/Migrations/20260612220000_AddSpotifyStreamNaturalKey.cs`
- `GoogleCalendarManagement.Tests/Integration/DataPointProjectionTests.cs`
- `Services/Projectors/TogglSleepProjector.cs`
- `Services/Projectors/TogglTransitProjector.cs`
- `Services/Projectors/TogglPhoneProjector.cs`
- `Services/Projectors/CallLogProjector.cs`
- `Services/Projectors/Civ5Projector.cs`
- `Services/Projectors/ComfyUIProjector.cs`
- `Services/Projectors/SpotifyProjector.cs`
- `Services/Projectors/OutlookProjector.cs`
- `Services/Projectors/MapsTimelineProjector.cs`

**Modified files:**
- `App.xaml.cs`
- `Data/Configurations/SpotifyStreamConfiguration.cs`
- `Data/Entities/SpotifyStream.cs`
- `Data/Migrations/CalendarDbContextModelSnapshot.cs`
- `Services/CallLogImportHandler.cs`
- `Services/Civ5ImportHandler.cs`
- `Services/ComfyUIImportHandler.cs`
- `Services/MapsTimelineImportHandler.cs`
- `Services/OutlookImportHandler.cs`
- `Services/SpotifyImportHandler.cs`
- `Services/SpotifyImportService.cs`
- `Services/TogglCsvImportHandler.cs`
- `Services/TogglPhoneImportHandler.cs`
- `Services/TogglSleepImportHandler.cs`
- `Services/TogglTransitImportHandler.cs`
- `docs/sprint-status.yaml`
- `docs/epic-8-data-linking/stories/8-9-project-all-sources-into-datapoints.md`

### Change Log

- 2026-06-12: Implemented Story 8.9 — all existing data sources project raw rows into `data_point` via 8.8 projectors; added Spotify natural key/migration; registered handler projectors; added projection/idempotency tests; full test suite green.

---
title: 'API Fetch Button Rename + Toggl CSV Import'
type: 'feature'
created: '2026-06-08'
status: 'done'
baseline_commit: 'e7b2ac3334f0340b62aa4991cf90e681e215350d'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Data source cards show a generic "Import..." label regardless of whether they fetch from an API or a local file/folder, and Toggl data can only be loaded via the live API — there is no way to import a CSV export from Toggl Track.

**Approach:** Add `IsApiFetch` to `IDataSourceImportHandler` so API-based handlers self-declare their label; update `DataSourceSummaryViewModel.ImportButtonContent` to display "API Fetch" vs "Import..." accordingly. Then add a `TogglCsvImportService` + `TogglCsvImportHandler` that parses the Toggl CSV export format, upserts entries into `toggl_data` with inline transit/phone classification, and exposes a "Import CSV" button on all three Toggl source cards.

## Boundaries & Constraints

**Always:**
- Toggl CSV times are parsed as local time and stored as UTC (matches existing API import behaviour).
- Synthetic `TogglId` for CSV entries = `DateTimeOffset.FromDateTime(startTimeUtc).ToUnixTimeMilliseconds()`. Deterministic and stable; current real Toggl IDs are ~2.4 B, unix-ms timestamps are ~1.75 T — no practical collision.
- Deduplication: if an entry with the same `TogglId` already exists, skip it (no error).
- CSV classification runs inline per-row: project = "Transit" → `TogglTransit`; matches active `TogglPhoneRule` rows → `TogglPhone`; otherwise `null`.
- `TogglSleepRepository` must be updated to filter `WHERE TogglDataType IS NULL` so transit/phone CSV entries don't bleed into the sleep card. (Existing API-imported sleep entries have `null` type — correct.)
- "API Fetch" applies to: TogglSleepImportHandler, TogglTransitImportHandler, SpotifyImportHandler only. `TogglPhoneImportHandler` runs local classification and keeps "Import...".
- No schema migration needed — existing `toggl_data` table is sufficient.

**Ask First:**
- If the CSV contains a row whose `StartTime` matches an existing DB entry with a **different** `TogglId` (i.e. real API ID for the same time slot) — halt and ask before overwriting.

**Never:**
- Do not call `TogglPhoneClassificationService.ClassifyAllAsync()` during CSV import (too broad; classify inline instead).
- Do not modify `TogglEntry.TogglId` to be nullable or add a new PK — work within the existing schema.
- Do not add "API Fetch" to ComfyUI, CallLog, MapsTimeline, or Civ5 handlers.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Happy path CSV import | Valid CSV with mixed transit/phone/unclassified rows | All rows upserted; transit rows get `TogglTransit`, phone-rule matches get `TogglPhone`, rest get `null` | N/A |
| Duplicate CSV import | Same CSV imported twice | Duplicate rows skipped (same synthetic TogglId) — dialog shows 0 new entries | N/A |
| Malformed CSV row | Row missing required fields (StartDate, StartTime) | Row skipped; import continues; final dialog reports skipped count | Non-fatal per-row |
| API Fetch button visible | Source card for toggl_sleep, toggl_transit, spotify | Button reads "API Fetch" | N/A |
| Import button unchanged | Source card for toggl_phone, call_log, comfyui, civ5, maps_timeline | Button still reads "Import..." | N/A |
| CSV import on all 3 Toggl cards | User clicks "Import CSV" on sleep OR transit OR phone card | Same handler invoked — one file picker, one result | N/A |

</frozen-after-approval>

## Code Map

- `Services/IDataSourceImportHandler.cs` -- add `bool IsApiFetch { get; }` with default `false`
- `Services/TogglSleepImportHandler.cs` -- override `IsApiFetch => true`
- `Services/TogglTransitImportHandler.cs` -- override `IsApiFetch => true`
- `Services/SpotifyImportHandler.cs` -- override `IsApiFetch => true`
- `Services/DataSourceImportHandlerRegistry.cs` -- add CSV handler dict + `RegisterCsvHandler`/`HasCsvHandler`/`GetCsvHandler`; add `bool IsApiFetch(string sourceKey)` method
- `ViewModels/DataSourceSummaryViewModel.cs` -- update `ImportButtonContent` to use "API Fetch"/"Import..."; add `HasCsvImportHandler`, `IsCsvImporting`, `CsvImportCommand`, `CsvImportButtonContent`, `CsvImportProgressVisibility`
- `Views/DataSourcePanelControl.xaml` -- add "Import CSV" button + progress ring in `GlobalSourceCardTemplate`
- `Services/ITogglCsvImportService.cs` -- new interface: `ImportFromStreamAsync(Stream, CancellationToken)`
- `Services/TogglCsvImportService.cs` -- new: parse CSV, classify inline, upsert entries
- `Services/TogglCsvImportHandler.cs` -- new: file picker → call service → show result dialog
- `Services/TogglSleepRepository.cs` -- filter `GetSleepEntriesForDateAsync` / `GetSleepEntryCountsForRangeAsync` by `TogglDataType IS NULL`
- `App.xaml.cs` -- register `ITogglCsvImportService`/`TogglCsvImportService`/`TogglCsvImportHandler` and call `RegisterCsvHandler` for all three Toggl source keys

## Tasks & Acceptance

**Execution:**
- [x] `Services/IDataSourceImportHandler.cs` -- add `bool IsApiFetch { get; }` with default implementation returning `false` -- allows opt-in without breaking existing handlers
- [x] `Services/TogglSleepImportHandler.cs` -- add `public bool IsApiFetch => true;`
- [x] `Services/TogglTransitImportHandler.cs` -- add `public bool IsApiFetch => true;`
- [x] `Services/SpotifyImportHandler.cs` -- add `public bool IsApiFetch => true;`
- [x] `Services/DataSourceImportHandlerRegistry.cs` -- add `_csvHandlers` dict with `RegisterCsvHandler`, `HasCsvHandler`, `GetCsvHandler`; add `bool IsApiFetch(string sourceKey)` that calls `_handlers[key].IsApiFetch`
- [x] `ViewModels/DataSourceSummaryViewModel.cs` -- update `ImportButtonContent` to `IsImporting ? "Importing..." : (IsApiFetch ? "API Fetch" : "Import...")`; add `IsApiFetch` field set in constructor from registry; add `HasCsvImportHandler`, `IsCsvImporting`, `CsvImportCommand`, `CsvImportButtonContent` (`IsCsvImporting ? "Importing..." : "Import CSV"`), `CsvImportProgressVisibility`; add `CsvImportAsync` method
- [x] `Views/DataSourcePanelControl.xaml` -- in `GlobalSourceCardTemplate`, add a second `Button` bound to `CsvImportCommand`/`CsvImportButtonContent`/`HasCsvImportHandler` (Visibility) and matching `ProgressRing`
- [x] `Services/ITogglCsvImportService.cs` -- define `Task<TogglCsvImportResult> ImportFromStreamAsync(Stream stream, CancellationToken ct = default)`; define `TogglCsvImportResult(bool Success, int Inserted, int Skipped, int Malformed, string? ErrorMessage)` record
- [x] `Services/TogglCsvImportService.cs` -- new class: (1) parse CSV header row, (2) for each data row parse Description/Duration/Project/StartDate+Time/StopDate+Time, (3) synthesize TogglId = unix-ms of UTC start, (4) fetch active phone rules once, (5) classify each entry, (6) load existing TogglIds for batch, (7) insert new entries, skip existing; return result
- [x] `Services/TogglCsvImportHandler.cs` -- new handler: open `.csv` file picker; call service; show summary or error dialog; implement `IsApiFetch => false`
- [x] `Services/TogglSleepRepository.cs` -- add `.Where(e => e.TogglDataType == null)` to both query methods
- [x] `App.xaml.cs` -- register `ITogglCsvImportService` → `TogglCsvImportService`; register `TogglCsvImportHandler` as singleton; call `registry.RegisterCsvHandler(TogglCsvImportHandler)` for keys `toggl_sleep`, `toggl_transit`, `toggl_phone`

**Acceptance Criteria:**
- Given a Toggl Sleep source card, when displayed in global mode, then the import button reads "API Fetch"
- Given a Toggl Transit source card, when displayed in global mode, then the import button reads "API Fetch"
- Given a Spotify source card, when displayed, then the import button reads "API Fetch"
- Given a Toggl Phone source card, when displayed, then the import button reads "Import..." (unchanged)
- Given a CallLog / ComfyUI / Civ5 / MapsTimeline source card, when displayed, then the import button reads "Import..." (unchanged)
- Given any of the three Toggl source cards, when displayed, then a second "Import CSV" button is visible
- Given a valid Toggl CSV export, when imported via the CSV button, then all rows are stored in `toggl_data` with correct `TogglDataType` values, and the result dialog shows inserted/skipped counts
- Given transit CSV rows (project = "Transit"), when imported, then `TogglDataType = TogglTransit` and entries appear in the Transit card
- Given phone-rule-matching CSV rows, when imported, then `TogglDataType = TogglPhone` and entries appear in the Phone card
- Given CSV rows imported then the same CSV re-imported, when the second import completes, then inserted = 0 and skipped = N
- Given a day view containing CSV-imported transit and phone entries, when the Sleep card renders, then only null-type entries appear (transit/phone excluded)

## Design Notes

**CSV time parsing:** Combine `StartDate` + `StartTime` fields into a `DateTime`, treat as local time, convert to UTC — mirrors how the app stores API-fetched entries.

**Phone rule matching for CSV:** Replicate the logic in `TogglPhoneClassificationService.MatchesAnyRule` inline in `TogglCsvImportService` rather than calling the service, to keep the import atomic and avoid re-processing unrelated entries.

**Sleep repository filter:** Existing API-imported sleep entries have `TogglDataType = null`. Adding `WHERE TogglDataType IS NULL` correctly includes them while excluding CSV-imported transit/phone entries that now have explicit types.

## Verification

**Commands:**
- `dotnet build GoogleCalendarManagement.sln` -- expected: 0 errors, 0 warnings
- `dotnet test GoogleCalendarManagement.Tests` -- expected: all tests pass

**Manual checks:**
- In the data sources panel, confirm "API Fetch" appears on Toggl Sleep, Toggl Transit, Spotify; "Import..." on Toggl Phone, CallLog, ComfyUI, Civ5, Maps Timeline
- Confirm "Import CSV" button appears on Sleep, Transit, and Phone cards
- Import the sample CSV; verify transit and phone entries appear in correct cards; sleep card shows no bleed-through

## Suggested Review Order

**API Fetch label — opt-in interface default**

- Default `false` keeps all existing handlers unchanged; only opt-in handlers read "API Fetch"
  [`IDataSourceImportHandler.cs:6`](../Services/IDataSourceImportHandler.cs#L6)

- ViewModel reads label at construction; `ImportButtonContent` now ternary on `IsApiFetch`
  [`DataSourceSummaryViewModel.cs:36`](../ViewModels/DataSourceSummaryViewModel.cs#L36)

- Sleep, Transit, Spotify handlers declare `IsApiFetch => true` (Phone intentionally absent)
  [`TogglSleepImportHandler.cs:28`](../Services/TogglSleepImportHandler.cs#L28)

**CSV import — service (entry point)**

- `ImportFromStreamAsync`: fetch phone rules, parse CSV, batch-check existing IDs + start times, insert
  [`TogglCsvImportService.cs:22`](../Services/TogglCsvImportService.cs#L22)

- `ClassifyEntry`: Transit by project name first; phone rules inline; else null (sleep card territory)
  [`TogglCsvImportService.cs:168`](../Services/TogglCsvImportService.cs#L168)

- `ParseCsvRow`: manual quoted-CSV parser; trailing-comma guard appends empty last field
  [`TogglCsvImportService.cs:236`](../Services/TogglCsvImportService.cs#L236)

- In-batch dedup (`seenInBatch`) and StartTime conflict check prevent batch rollback and API/CSV clashes
  [`TogglCsvImportService.cs:55`](../Services/TogglCsvImportService.cs#L55)

**CSV import — handler and registry wiring**

- Handler opens file picker, calls service, formats inserted/skipped/malformed summary dialog
  [`TogglCsvImportHandler.cs:30`](../Services/TogglCsvImportHandler.cs#L30)

- Registry gets second dict `_csvHandlers`; `RegisterCsvHandler` / `HasCsvHandler` / `GetCsvHandler`
  [`DataSourceImportHandlerRegistry.cs:10`](../Services/DataSourceImportHandlerRegistry.cs#L10)

- ViewModel wires `CsvImportCommand`; `CsvImportVisibility` collapses button for non-CSV sources
  [`DataSourceSummaryViewModel.cs:93`](../ViewModels/DataSourceSummaryViewModel.cs#L93)

- XAML adds second Button + ProgressRing bound to CSV properties in `GlobalSourceCardTemplate`
  [`DataSourcePanelControl.xaml:101`](../Views/DataSourcePanelControl.xaml#L101)

**Sleep repository fix**

- Filter `TogglDataType == null` prevents CSV-imported transit/phone entries bleeding into sleep card
  [`TogglSleepRepository.cs:27`](../Services/TogglSleepRepository.cs#L27)

**Registration**

- Service + handler DI registration; `RegisterCsvHandler` called for all three Toggl source keys
  [`App.xaml.cs:86`](../App.xaml.cs#L86)

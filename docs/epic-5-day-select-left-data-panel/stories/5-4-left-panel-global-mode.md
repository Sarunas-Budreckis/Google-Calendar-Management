# Story 5.4: Left Panel Global Mode

Status: review

## Story

As a **user**,
I want **the left panel to show all registered data sources with their last import dates when no day is selected**,
so that **I can see at a glance which sources are up to date and when data was last pulled**.

## Acceptance Criteria

1. **AC-5.4.1 — Registered data sources appear in the list:** When the `data_source` table contains one or more rows, the left panel body renders one row per source (in display-name alphabetical order). Each row shows the source's `display_name`.

2. **AC-5.4.2 — Each source row shows last-covered-date and last-import timestamp:** For each source, the row shows:
   - **Last data date:** the `covered_end_date` of the most recent `data_source_import_log` row where `success = true` for that source, formatted as a short date (e.g. "May 13, 2026"). If no successful import exists: "Never imported".
   - **Last imported:** the `imported_at` timestamp of the same most recent successful import, formatted as a relative time (e.g. "3 days ago") matching the existing relative-time style used in the sync status area. If none: omitted.

3. **AC-5.4.3 — Empty state when no sources are registered:** If `data_source` is empty, the panel body shows "No data sources configured" (same text as Story 5.2's placeholder).

4. **AC-5.4.4 — Each source row has an Import button:** Each source row in the global-mode list shows an "Import…" button to the right of the name/dates. The button is enabled only if an `IDataSourceImportHandler` is registered for that source in the `DataSourceImportHandlerRegistry` (new in this story). If no handler is registered (no source is implemented yet), the button is visible but disabled. Clicking the button delegates to `IDataSourceImportHandler.TriggerImportAsync()` on the registered handler for that source.

5. **AC-5.4.5 — Panel refreshes after any import completes:** `DataSourcePanelViewModel` subscribes to `DataSourceImportCompletedMessage` (new in this story) and reloads the source list when received. The refresh happens on the UI thread.

6. **AC-5.4.6 — Global mode is active when no day is selected:** The source list (global mode) is shown whenever `ICalendarDaySelectionService.SelectedDay` is null. Story 5.5 will replace the body content when a day is selected; this story only implements the null case.

7. **AC-5.4.7 — Loading state is handled gracefully:** While the source list is being fetched from the database, the panel shows a `ProgressRing` or similar indicator. The panel does not flash or show stale content during refresh.

---

## Tasks / Subtasks

- [x] **Task 1: Define `IDataSourceImportHandler` and `DataSourceImportHandlerRegistry`**
  - [x] Add `Services/IDataSourceImportHandler.cs`:
    ```csharp
    public interface IDataSourceImportHandler
    {
        string SourceKey { get; }
        // Opens any necessary dialogs and runs the import. Returns when complete.
        Task TriggerImportAsync(CancellationToken ct = default);
    }
    ```
  - [x] Add `Services/DataSourceImportHandlerRegistry.cs`: a registry that holds `IDataSourceImportHandler` instances keyed by `SourceKey`. Handlers register themselves at startup; `DataSourcePanelViewModel` looks up handlers from it.
  - [x] Register `DataSourceImportHandlerRegistry` as singleton in `App.xaml.cs`

- [x] **Task 2: Add `DataSourceImportCompletedMessage`**
  - [x] Add `Messages/DataSourceImportCompletedMessage.cs`:
    ```csharp
    public sealed record DataSourceImportCompletedMessage(
        int DataSourceId,
        string SourceKey,
        bool Success);
    ```
  - [x] This message will be published by Story 5.6's import service after each import run completes

- [x] **Task 4: Extend `DataSourcePanelViewModel` with source list loading**
  - [x] Add `ObservableCollection<DataSourceSummaryViewModel> Sources` property
  - [x] Add `IsLoadingGlobal bool` property (drives `ProgressRing`)
  - [x] On `InitializeAsync()` (called from `DataSourcePanelControl.Loaded`): call `LoadSourcesAsync()`
  - [x] `LoadSourcesAsync()`: query `IDataSourceRepository.GetAllSourcesAsync()`, for each source query `GetLastImportAsync()`, map to `DataSourceSummaryViewModel`, replace `Sources`
  - [x] Subscribe to `DataSourceImportCompletedMessage` via `WeakReferenceMessenger`; on receipt, call `LoadSourcesAsync()`
  - [x] `ICalendarDaySelectionService` is injected but only used to decide global-vs-day-mode display; this story only implements the global (null day) side

- [x] **Task 5: Add `DataSourceSummaryViewModel`**
  - [x] Add `ViewModels/DataSourceSummaryViewModel.cs`:
    - `int DataSourceId`
    - `string SourceKey`
    - `string DisplayName`
    - `string LastDataDateLabel` ("May 13, 2026" or "Never imported")
    - `string? LastImportedRelativeLabel` ("3 days ago" or null)
    - `bool HasImportHandler` (true if `DataSourceImportHandlerRegistry` has a handler for `SourceKey`)
    - `IAsyncRelayCommand ImportCommand` — calls `DataSourceImportHandlerRegistry.GetHandler(SourceKey)?.TriggerImportAsync()`; disabled when `!HasImportHandler`

- [x] **Task 6: Build the global-mode XAML in `DataSourcePanelControl.xaml`**
  - [x] Replace the Story 5.2 placeholder body with a conditional:
    - If `Sources.Count == 0`: show "No data sources configured" `TextBlock`
    - Else: show a `ListView` or `ItemsControl` bound to `Sources`
  - [x] Each item template: `DisplayName` (prominent), `LastDataDateLabel` (secondary), `LastImportedRelativeLabel` (tertiary/muted) — stacked vertically; Import button aligned right, disabled when `!HasImportHandler`
  - [x] `ProgressRing` overlays the body when `IsLoadingGlobal = true`

- [x] **Task 7: Subscribe to day selection to gate global mode**
  - [x] `DataSourcePanelViewModel` subscribes to `DaySelectedMessage`
  - [x] For now: if `SelectedDay` is not null, just hide the source list (day-mode content will be added in Story 5.5 — show a placeholder "Day mode — coming in Story 5.5" for now, or simply show nothing)
  - [x] If `SelectedDay` is null, show the global source list

- [x] **Task 8: Unit tests**
  - [x] Add `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs`
  - [x] `LoadSources_WhenRepositoryReturnsEmpty_ShowsEmptyState`
  - [x] `LoadSources_WhenRepositoryReturnsSources_BuildsSummaryViewModels`
  - [x] `LoadSources_WhenHandlerRegistered_ImportCommandIsEnabled`
  - [x] `LoadSources_WhenNoHandlerRegistered_ImportCommandIsDisabled`
  - [x] `OnImportCompletedMessage_ReloadsSourceList`

---

## Dev Notes

### Relative Time Formatting

Reuse the same relative-time formatting logic already used in the sync status area (the "Last synced: 2 hours ago" display). Check `ViewModels/MainViewModel.cs` or `Services/SyncStatusService.cs` for the existing helper. If it's private to that class, extract it to a shared `RelativeTimeFormatter` utility.

### Ordering

Sources are displayed alphabetically by `display_name`. This is simple and deterministic. Custom ordering (e.g. most recently used first) is a future enhancement.

### Global Mode Is The Default

The panel starts in global mode unless `NavigationState.SelectedDay` is non-null on load. This means: on first launch with no day selection stored, the panel shows the source list (or empty state). Day mode (Story 5.5) is layered on top of the same panel when `SelectedDay != null`.

### Project Structure

```text
Messages/
└── DataSourceImportCompletedMessage.cs         # new

Services/
├── IDataSourceImportHandler.cs                 # new
└── DataSourceImportHandlerRegistry.cs          # new

ViewModels/
├── DataSourcePanelViewModel.cs                 # extend: source loading, message subscription
└── DataSourceSummaryViewModel.cs               # new (with ImportCommand)

Views/
└── DataSourcePanelControl.xaml                 # extend: source list template + Import button

App.xaml.cs                                     # register DataSourceImportHandlerRegistry

GoogleCalendarManagement.Tests/Unit/ViewModels/
└── DataSourcePanelViewModelTests.cs            # new
```

### Prerequisites

- Story 5.1 must be complete (`IDataSourceRepository`, `data_source` and `data_source_import_log` tables)
- Story 5.2 must be complete (panel shell exists)
- Story 5.3 must be complete (day selection service available to subscribe to `DaySelectedMessage`)

### References

- [Epic 5 overview](../epic-overview.md) — left panel global mode section
- [Story 5.1](./5-1-data-source-infrastructure.md) — `IDataSourceRepository` contract
- [Story 5.2](./5-2-three-panel-layout-and-left-panel-shell.md) — `DataSourcePanelViewModel` shell to extend
- [Story 5.3](./5-3-single-day-select.md) — `DaySelectedMessage` contract
- [MainViewModel.cs](../../../ViewModels/MainViewModel.cs) — relative time formatting pattern
- [IDataSourceRepository](../../../Services/IDataSourceRepository.cs) — query methods (after 5.1 is built)

## Dev Agent Record

### Debug Log

- 2026-05-13: Red-phase targeted test run failed before implementation as expected; initial retry was blocked by a running app process locking build output, then confirmed missing story implementation.
- 2026-05-13: Implemented global-mode source loading, import handler registry, import-completed refresh, day-selection gating, XAML list/empty/loading states, and unit coverage.
- 2026-05-13: Validation passed with `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64` (276 passed).

### Completion Notes

- Added the data-source import handler contract and registry, registered the registry in DI, and wired summary import commands to registered handlers.
- Added `DataSourceImportCompletedMessage` and made `DataSourcePanelViewModel` reload source summaries on receipt using the UI dispatcher when available.
- Extended `DataSourcePanelViewModel` to load registered sources alphabetically, map last successful import coverage/import timestamps, expose loading/list/empty visibility, and hide global mode when a day is selected.
- Replaced the left panel placeholder with a global source list, disabled/enabled Import buttons, empty state, loading overlay, and temporary Story 5.5 day-mode placeholder.

## File List

- App.xaml.cs
- Messages/DataSourceImportCompletedMessage.cs
- Services/DataSourceImportHandlerRegistry.cs
- Services/IDataSourceImportHandler.cs
- Services/RelativeTimeFormatter.cs
- ViewModels/DataSourcePanelViewModel.cs
- ViewModels/DataSourceSummaryViewModel.cs
- Views/DataSourcePanelControl.xaml
- GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs
- docs/epic-5-day-select-left-data-panel/stories/5-4-left-panel-global-mode.md
- docs/sprint-status.yaml

## Change Log

- 2026-05-13: Implemented Story 5.4 left panel global mode and marked story ready for review.

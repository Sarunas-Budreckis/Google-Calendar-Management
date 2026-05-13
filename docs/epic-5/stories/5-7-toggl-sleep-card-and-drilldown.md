# Story 5.7: Toggl Sleep Card & Drilldown

Status: backlog

## Story

As a **user**,
I want **to see my Toggl sleep entry for the selected day in the left panel and create a candidate event from it with one click**,
so that **I can review and act on sleep data without switching away from my calendar workflow**.

## Acceptance Criteria

1. **AC-5.7.1 — Toggl Sleep compact card appears in the day-mode source list:** When a day is selected and the `data_source` row for `source_key = "toggl_sleep"` is registered, the Toggl Sleep row in the left panel's day-mode source list shows a compact summary of that day's sleep data below the source name and integration checkbox.

2. **AC-5.7.2 — Compact card shows sleep entry summary:** If exactly one sleep entry exists for the selected day in `toggl_data`:
   - Start time (formatted as local time, e.g. "11:30 PM")
   - End time (e.g. "7:15 AM +1")
   - Duration (e.g. "7h 45m")
   If multiple sleep entries exist for the day, show the count ("2 sleep entries") without time detail. If no sleep entry exists: show "No sleep data" in muted text.

3. **AC-5.7.3 — Integration checkbox is greyed when no data:** When no sleep entry exists for the selected day (`TogglSleepCardProvider.HasDataForDay(date)` returns `false`), the integration checkbox in the Toggl Sleep row is visually greyed and non-interactive (per the `IDataSourceCardProvider.HasDataForDay` contract from Story 5.5).

4. **AC-5.7.4 — Expand button opens the Toggl Sleep drilldown:** Clicking the expand/chevron on the Toggl Sleep card navigates the left panel to the Toggl Sleep drilldown view (replacing the source list within the panel).

5. **AC-5.7.5 — Drilldown lists all sleep entries for the selected day:** The drilldown shows a list of all `toggl_data` rows for the selected day (filtered by `DATE(start_time) == selected date`). Each entry shows: start time, end time, duration, and raw description. If there are no entries: "No sleep data for this day."

6. **AC-5.7.6 — "Create Candidate Event" button exists in the drilldown:** A primary-style button labeled "Create Candidate Event" appears in the drilldown (disabled and greyed when no sleep entries exist for the day).

7. **AC-5.7.7 — "Create Candidate Event" creates a `pending_event`:** Clicking the button:
   - If one sleep entry: creates a `pending_event` with `start_datetime` = entry `start_time`, `end_datetime` = entry `end_time`, `summary = "Sleep"`, `is_all_day = false`, `source_system = "toggl"`, `color_id = "Grey"` (default for sleep)
   - If multiple entries: creates one event spanning the earliest start to the latest end across all entries for the day, with `summary = "Sleep"` and `source_system = "toggl"`
   - The new event is immediately selected in the right panel in edit mode so the user can adjust before saving

8. **AC-5.7.8 — Back arrow returns to the source list:** Clicking back from the drilldown returns the left panel to the day-mode source list (per the Story 5.5 back-navigation contract).

9. **AC-5.7.9 — `TogglSleepCardProvider` is registered in `DataSourceCardProviderRegistry`:** At app startup, `TogglSleepCardProvider` is registered in `DataSourceCardProviderRegistry` for `source_key = "toggl_sleep"`. The left panel resolves it automatically.

---

## Tasks / Subtasks

- [ ] **Task 1: Add `ITogglSleepRepository` and implementation**
  - [ ] Add `Services/ITogglSleepRepository.cs`:
    ```csharp
    public interface ITogglSleepRepository
    {
        Task<IReadOnlyList<TogglEntry>> GetSleepEntriesForDateAsync(DateOnly date, CancellationToken ct = default);
    }
    ```
  - [ ] Add `Services/TogglSleepRepository.cs` using `IDbContextFactory<CalendarDbContext>`
  - [ ] Query: `toggl_data` where `DATE(start_time) == date` (SQLite date comparison) — uses existing `TogglEntry` entity from Story 5.6
  - [ ] Register as singleton in `App.xaml.cs`

- [ ] **Task 2: Add `TogglSleepCardProvider`**
  - [ ] Add `Services/TogglSleepCardProvider.cs` implementing `IDataSourceCardProvider`:
    - `SourceKey` returns `"toggl_sleep"`
    - `HasDataForDay(date)`: returns `false` if `GetSleepEntriesForDateAsync(date)` returns empty, `true` otherwise — runs synchronously via a cached value or a background pre-fetch; avoid blocking the UI thread
    - `CreateCompactSummaryView(date)`: instantiates and returns `TogglSleepCompactCardControl` (Task 3)
    - `CreateDrilldownView(date)`: instantiates and returns `TogglSleepDrilldownControl` (Task 4)
  - [ ] Register in `DataSourceCardProviderRegistry` in `App.xaml.cs`

- [ ] **Task 3: Build `TogglSleepCompactCardControl`**
  - [ ] Add `Views/TogglSleepCompactCardControl.xaml` and `Views/TogglSleepCompactCardControl.xaml.cs`
  - [ ] Add `ViewModels/TogglSleepCompactCardViewModel.cs`
  - [ ] On `LoadAsync(DateOnly date)`: fetch entries via `ITogglSleepRepository`, compute display strings
  - [ ] XAML: show start/end/duration (one entry), entry count (multiple entries), or "No sleep data" (none)
  - [ ] Card control is self-contained and refreshes when `LoadAsync` is called with a new date

- [ ] **Task 4: Build `TogglSleepDrilldownControl` and `TogglSleepDrilldownViewModel`**
  - [ ] Add `Views/TogglSleepDrilldownControl.xaml` and code-behind
  - [ ] Add `ViewModels/TogglSleepDrilldownViewModel.cs`
  - [ ] Properties: `ObservableCollection<TogglSleepEntryViewModel> Entries`, `bool HasEntries`, `IAsyncRelayCommand CreateCandidateEventCommand`
  - [ ] `LoadAsync(DateOnly date)`: fetch entries, map to `TogglSleepEntryViewModel` (start, end, duration label, description)
  - [ ] XAML: `ItemsControl` of entries + "No sleep data" empty state + "Create Candidate Event" button
  - [ ] `CreateCandidateEventCommand`:
    - Aggregate entries (one: use as-is; multiple: span earliest start to latest end)
    - Call `IPendingEventDraftService.CreateDraftAsync(start, end)` 
    - Set `SourceSystem = "toggl"`, `Summary = "Sleep"`, `ColorId = "Grey"` on the result
    - Call `ICalendarSelectionService.Select(pendingEventId, Pending, openInEditMode: true)`
    - Command is disabled when `!HasEntries`

- [ ] **Task 5: Add `TogglSleepEntryViewModel`**
  - [ ] Add `ViewModels/TogglSleepEntryViewModel.cs`: simple data carrier with `StartLabel`, `EndLabel`, `DurationLabel`, `Description` strings

- [ ] **Task 6: Register and wire in DI**
  - [ ] Register `ITogglSleepRepository`, `TogglSleepCardProvider`, `TogglSleepCompactCardControl`, `TogglSleepDrilldownControl` in `App.xaml.cs`
  - [ ] Register `TogglSleepCardProvider` into `DataSourceCardProviderRegistry` at startup

- [ ] **Task 7: Unit tests**
  - [ ] Add `GoogleCalendarManagement.Tests/Unit/ViewModels/TogglSleepDrilldownViewModelTests.cs`
  - [ ] `LoadAsync_WhenNoEntries_HasEntriesIsFalse`
  - [ ] `LoadAsync_WhenEntries_MapsToEntryViewModels`
  - [ ] `CreateCandidateEventCommand_WhenOneEntry_CreatesDraftWithEntryTimes`
  - [ ] `CreateCandidateEventCommand_WhenMultipleEntries_SpansEarliestToLatest`
  - [ ] `CreateCandidateEventCommand_SelectsNewDraftInRightPanel`
  - [ ] Mock `ITogglSleepRepository`, `IPendingEventDraftService`, `ICalendarSelectionService`

---

## Dev Notes

### Card Provider vs. ViewModel

The `IDataSourceCardProvider` interface returns `UIElement` instances rather than ViewModels because each source's drilldown is structurally unique (sleep has a simple list; future sources like YouTube will have a timeline; call logs have a table). Returning a ViewModel-per-source would require a discriminated union or registry of ViewModel types. Returning a `UIElement` keeps each source self-contained and independently testable without coupling the panel ViewModel to source-specific logic.

### `HasDataForDay` Caching Strategy

`HasDataForDay(date)` is called synchronously from the left panel to determine whether to grey the checkbox. However, it queries a database. Recommended approach: on `CreateCompactSummaryView(date)` being called, the provider starts a background task to pre-load data and caches the result with the date as the key. `HasDataForDay` returns `null` (unknown) until the cache is populated, then returns the real value. The left panel re-renders when the card view notifies of its loaded state.

### Time Formatting

Follow the existing event time formatting pattern in the calendar views. Use local time (not UTC). For overnight sleep that crosses midnight, show "+1" or "+1d" suffix on the end time (e.g. "7:15 AM +1").

### "Grey" Color for Sleep Events

`ColorId = "Grey"` follows the 9-color palette from Epic 4 Story 4.3. Verify the exact key string used in `ColorMappingService` — it is likely `"Grey"` but confirm against the actual palette constants.

### `IPendingEventDraftService.CreateDraftAsync`

The existing signature is `CreateDraftAsync(DateTime startLocal, DateTime endLocal)`. After calling it, update the returned draft's `SourceSystem` to `"toggl"` and `ColorId` to `"Grey"` via `IPendingEventRepository.UpsertAsync` — or extend `CreateDraftAsync` to accept optional metadata if that pattern is cleaner.

### Project Structure

```text
Services/
├── ITogglSleepRepository.cs                        # new
├── TogglSleepRepository.cs                         # new
└── TogglSleepCardProvider.cs                       # new

ViewModels/
├── TogglSleepCompactCardViewModel.cs               # new
├── TogglSleepDrilldownViewModel.cs                 # new
└── TogglSleepEntryViewModel.cs                     # new

Views/
├── TogglSleepCompactCardControl.xaml               # new
├── TogglSleepCompactCardControl.xaml.cs            # new
├── TogglSleepDrilldownControl.xaml                 # new
└── TogglSleepDrilldownControl.xaml.cs              # new

App.xaml.cs                                         # DI + provider registration

GoogleCalendarManagement.Tests/Unit/ViewModels/
└── TogglSleepDrilldownViewModelTests.cs            # new
```

### Prerequisites

- Story 5.5 complete (`IDataSourceCardProvider`, `DataSourceCardProviderRegistry`, drilldown navigation)
- Story 5.6 complete (`toggl_data` table, `TogglEntry` entity, data has been importable)

### References

- [Epic 5 overview](../epic-overview.md) — Toggl Sleep card and drilldown section
- [Story 5.5](./5-5-left-panel-day-mode.md) — `IDataSourceCardProvider` interface, `DataSourceCardProviderRegistry`
- [Story 5.6](./5-6-toggl-sleep-import.md) — `TogglEntry` entity, `toggl_data` table
- [IPendingEventDraftService.cs](../../../Services/IPendingEventDraftService.cs) — draft creation
- [ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs) — select newly created event
- [ColorMappingService.cs](../../../Services/ColorMappingService.cs) — verify "Grey" color key

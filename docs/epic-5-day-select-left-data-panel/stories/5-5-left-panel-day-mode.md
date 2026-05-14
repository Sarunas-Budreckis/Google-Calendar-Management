# Story 5.5: Left Panel Day Mode

Status: review

## Story

As a **user**,
I want **the left panel to show the selected day's name and per-source integration status when a day is selected**,
so that **I can see what data sources I've handled for that day and quickly access detailed source data**.

## Acceptance Criteria

1. **AC-5.5.1 — Day mode activates when a day is selected:** When `DaySelectedMessage` is received with a non-null date, the left panel switches from global mode to day mode. When the message carries null (day deselected), the panel returns to global mode.

2. **AC-5.5.2 — Day mode header shows date and name:** The panel header area (replacing the "Data Sources" title from Story 5.2) shows the selected date formatted as "Monday, May 13" (or locale equivalent). If a named all-day event exists on that date (see AC-5.5.3), the name appears as a second line below the date in a slightly smaller style. If no name exists, only the date is shown with a muted "Tap to name this day" hint.

3. **AC-5.5.3 — Day name is derived from all-day events:** The day name is the `summary` of the event on the selected date where `is_all_day = true` and `source_system = 'day_name'`. At most one such event may exist per date (enforced by AC-5.5.3a). Query both `gcal_event` and `pending_event` tables. Prefer `pending_event` if both tables have a match (the pending version represents the user's most recent intent).

   **AC-5.5.3a — `day_name` uniqueness is enforced at creation:** When creating a new `day_name` event (AC-5.5.4), the creation logic first checks whether a `day_name` event already exists for that date in either table. If one already exists, the new creation is skipped and the existing event is opened instead. This prevents duplicate name events. A database-level unique partial index on `pending_event (DATE(start_datetime), source_system) WHERE source_system = 'day_name'` and an equivalent convention on `gcal_event` enforce this at the data layer.

4. **AC-5.5.4 — Clicking the date/name header opens the day name event in the right panel:** If a name event exists, clicking the header calls `ICalendarSelectionService.Select(eventId, sourceKind)` to open it in the right panel. If no name event exists, clicking the header creates a new `pending_event` with `is_all_day = true`, `source_system = "day_name"`, `summary = ""`, `start_datetime` and `end_datetime` set to the start of the selected day (all-day semantics), and selects it in the right panel in edit mode so the user can type the name immediately.

5. **AC-5.5.5 — Per-source integration rows appear in day mode:** Below the header, one row is shown per registered data source (same list as global mode). Each row shows:
   - Source display name
   - An integration checkbox (checked if `date_source_integration` has `integrated = true` for this date and source; unchecked otherwise)
   - An expand/chevron button on the right
   - The source's own compact day-summary content (see AC-5.5.7)

6. **AC-5.5.6 — Integration checkbox is manually toggled:** Clicking the checkbox calls `IDataSourceRepository.SetIntegrationAsync(date, sourceId, !current)`. The checkbox updates optimistically (immediately in the UI), then persists. No confirmation required.

7. **AC-5.5.7 — Source cards are independently extensible:** Each data source provides its own compact day-summary content control via an `IDataSourceCardProvider` interface. The left panel renders the provided control inside the card row. If a source has no registered provider, the card row shows only the source name and integration checkbox (no compact summary). Story 5.7 will register the Toggl Sleep provider; this story defines the interface and the fallback.

8. **AC-5.5.8 — Expand button enters the source drilldown view:** Clicking the expand/chevron on any card row navigates the left panel body to the drilldown view for that source. The drilldown replaces the source list entirely within the panel. Story 5.7 provides the Toggl Sleep drilldown content; for all other sources in this story, the drilldown shows a placeholder "Detailed view for [Source Name] — coming soon".

9. **AC-5.5.9 — Back arrow returns to the source list:** A back-arrow button at the top of the drilldown view returns the panel to the day-mode source list.

10. **AC-5.5.10 — Dual select coexists correctly:** Day mode being active (left panel shows a day's sources) does not clear or affect the right-panel event selection. Both panels display their respective selections simultaneously.

---

## Tasks / Subtasks

- [x] **Task 1: Define `IDataSourceCardProvider` interface**
  - [x] Add `Services/IDataSourceCardProvider.cs`:
    ```csharp
    public interface IDataSourceCardProvider
    {
        string SourceKey { get; }
        // Returns a UIElement to render as the compact day summary, or null for default
        UIElement? CreateCompactSummaryView(DateOnly date);
        // Returns a UIElement for the drilldown view
        UIElement CreateDrilldownView(DateOnly date);
    }
    ```
  - [x] Add `Services/DataSourceCardProviderRegistry.cs`: a registry that holds `IDataSourceCardProvider` instances keyed by `SourceKey`. Other services register providers into it; the left panel looks up providers from it.

- [x] **Task 2: Implement day-mode logic in `DataSourcePanelViewModel`**
  - [x] Add `DateOnly? CurrentDay` property; updated from `DaySelectedMessage`
  - [x] Add `string DayLabel` property (formatted date)
  - [x] Add `string? DayName` property (from day-name event query)
  - [x] Add `bool HasDayName` property
  - [x] Add `ObservableCollection<DataSourceDayCardViewModel> DayCards`
  - [x] Add `DataSourceDayCardViewModel? DrilldownCard` property (null = source list mode)
  - [x] `LoadDayModeAsync(DateOnly date)`: queries sources + integration states + day name
  - [x] On day-name header click: call `OpenOrCreateDayNameEventAsync(date)`
  - [x] `OpenOrCreateDayNameEventAsync`: query gcal_event + pending_event for `is_all_day=true, source_system="day_name"` on date; open existing via `ICalendarSelectionService.Select` or create new `pending_event` via `IPendingEventDraftService` then select

- [x] **Task 3: Add `DataSourceDayCardViewModel`**
  - [x] Add `ViewModels/DataSourceDayCardViewModel.cs`:
    - `int DataSourceId`
    - `string DisplayName`
    - `bool IsIntegrated` (observable, updates on toggle)
    - `bool IsGreyedOut` (from `IDataSourceCardProvider.SupportsNoDataHint` + actual data check — see Task 4)
    - `ICommand ToggleIntegrationCommand`
    - `ICommand ExpandCommand`
    - `UIElement? CompactSummaryView` (from provider, or null)

- [x] **Task 4: Handle greyed-out checkboxes**
  - [x] `IDataSourceCardProvider` gains `bool? HasDataForDay(DateOnly date)` — returns `true` (has data), `false` (definitely no data), or `null` (unknown/not supported)
  - [x] `DataSourceDayCardViewModel.IsGreyedOut = provider.HasDataForDay(date) == false`
  - [x] When greyed out: checkbox is disabled and visually muted; integration cannot be toggled

- [x] **Task 5: Build day-mode XAML in `DataSourcePanelControl.xaml`**
  - [x] Add a `DataTemplate` or `ContentPresenter` that switches between global mode, day mode source list, and drilldown based on `CurrentDay` and `DrilldownCard` state
  - [x] Day mode header: two `TextBlock`s (date + day name / hint), tappable via `Tapped` event
  - [x] Source list: `ItemsControl` bound to `DayCards`, each rendered with a card template (name, checkbox, compact summary slot, expand button)
  - [x] Drilldown: a `ContentPresenter` showing `DrilldownCard.DrilldownView` with a back button

- [x] **Task 6: Register `DataSourceCardProviderRegistry` in DI**
  - [x] Register as singleton in `App.xaml.cs`
  - [x] `DataSourcePanelViewModel` receives it via constructor injection

- [x] **Task 7: Unit and integration tests**
  - [x] Extend `DataSourcePanelViewModelTests.cs`:
    - `OnDaySelected_SwitchesToDayMode`
    - `OnDayDeselected_ReturnsTaGlobalMode`
    - `LoadDayMode_WhenNameEventExists_PopulatesDayName`
    - `LoadDayMode_WhenNoNameEvent_DayNameIsNull`
    - `ToggleIntegration_CallsRepository_AndUpdatesCheckbox`
    - `ExpandCard_SetsDrilldownCard`
    - `BackFromDrilldown_ClearsDrilldownCard`
  - [x] Add integration test: `SetIntegrationAsync_PersistsToDatabase`

---

## Dev Notes

### `source_system = "day_name"` Convention

Day-name all-day events use `source_system = "day_name"` as the discriminator. This is the canonical way to distinguish them from user-created all-day events that just happen to span a full day. When the right panel opens a day-name event, it will show the event's title (the name) as editable. The day-name concept is fully compatible with the existing `pending_event` workflow — it drafts in `pending_event` until pushed to GCal.

### Drilldown Navigation

Drilldown state is held entirely in `DataSourcePanelViewModel.DrilldownCard`. Setting it to a non-null `DataSourceDayCardViewModel` triggers the XAML to swap the body to the drilldown view. Setting it back to null returns to the source list. No navigation stack needed in this epic — there is only one level of drilldown.

### Card Provider Interface Evolution

The `IDataSourceCardProvider.CreateDrilldownView(date)` returns a `UIElement` because each data source (Toggl, YouTube, Call Logs, etc.) will have a completely different drilldown layout. Returning a raw `UIElement` from a service is a pragmatic choice for extensibility — it avoids a complex ViewModel hierarchy just to support varying UI shapes. Each source controls its own internal composition.

### `IPendingEventDraftService`

Story 4.2 introduced `IPendingEventDraftService.CreateDraftAsync(startLocal, endLocal)`. For an all-day event, pass midnight-to-midnight for the selected date and set `is_all_day = true` post-creation (or extend the draft service if needed).

### Project Structure

```text
Services/
├── IDataSourceCardProvider.cs              # new
└── DataSourceCardProviderRegistry.cs       # new

ViewModels/
├── DataSourcePanelViewModel.cs             # extend: day mode, drilldown, header tap
└── DataSourceDayCardViewModel.cs           # new

Views/
└── DataSourcePanelControl.xaml             # extend: day-mode header, card list, drilldown

App.xaml.cs                                 # register DataSourceCardProviderRegistry

GoogleCalendarManagement.Tests/Unit/ViewModels/
└── DataSourcePanelViewModelTests.cs        # extend
GoogleCalendarManagement.Tests/Integration/
└── DataSourceRepositoryTests.cs            # new (SetIntegration, GetIntegration)
```

### Prerequisites

- Stories 5.1, 5.2, 5.3, and 5.4 must all be complete

### References

- [Epic 5 overview](../epic-overview.md) — day mode, drilldown, dual-select, day naming
- [Story 5.1](./5-1-data-source-infrastructure.md) — `IDataSourceRepository.SetIntegrationAsync`
- [Story 5.3](./5-3-single-day-select.md) — `DaySelectedMessage`, `ICalendarDaySelectionService`
- [ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs) — Select() for opening day name event
- [IPendingEventDraftService.cs](../../../Services/IPendingEventDraftService.cs) — draft creation for day name event
- [EventDetailsPanelViewModel.cs](../../../ViewModels/EventDetailsPanelViewModel.cs) — right panel open/edit pattern

---

## Dev Agent Record

### Debug Log

- 2026-05-13: Added failing Story 5.5 unit/integration coverage before implementation. Initial test run required network escalation for NuGet restore, then a transient WinUI XAML compiler lock blocked the red-phase run before compile diagnostics.
- 2026-05-13: Implemented day-mode VM state, day-name lookup/creation, provider registry, day-card view model, drilldown state, XAML day-mode body, DI registration, repository query, and day-name uniqueness migration/index model metadata.
- 2026-05-13: Targeted tests passed: `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --filter "FullyQualifiedName~DataSourcePanelViewModelTests|FullyQualifiedName~DataSourceRepositoryTests" --no-restore` (14 passed).
- 2026-05-13: Full regression passed: `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --no-restore` (288 passed).
- 2026-05-13: Build passed: `dotnet build -p:Platform=x64 -p:WarningsNotAsErrors=NU1900 --no-restore`.

### Completion Notes

- Implemented left-panel day mode activation from `DaySelectedMessage`, with return to global mode on deselection and no interaction with right-panel event selection.
- Added selected-day header labeling, day-name lookup preferring pending events over Google events, and header behavior that opens an existing day-name event or creates a new all-day pending `day_name` draft in edit mode.
- Added per-source day cards with persisted integration toggles, provider-supplied compact summary/drilldown content, disabled no-data checkboxes, fallback drilldown placeholders, and back navigation.
- Added day-name uniqueness enforcement through creation-time lookup plus migration/model indexes for `day_name` events.

## File List

- `App.xaml.cs`
- `Data/Configurations/GcalEventConfiguration.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Data/Migrations/20260513183000_AddDayNameUniqueIndexes.cs`
- `Data/Migrations/CalendarDbContextModelSnapshot.cs`
- `GoogleCalendarManagement.Tests/Integration/DataSourceRepositoryTests.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs`
- `Services/DataSourceCardProviderRegistry.cs`
- `Services/IDataSourceCardProvider.cs`
- `Services/IPendingEventRepository.cs`
- `Services/PendingEventRepository.cs`
- `ViewModels/DataSourceDayCardViewModel.cs`
- `ViewModels/DataSourcePanelViewModel.cs`
- `Views/DataSourcePanelControl.xaml`
- `Views/DataSourcePanelControl.xaml.cs`
- `docs/epic-5-day-select-left-data-panel/stories/5-5-left-panel-day-mode.md`

## Change Log

- 2026-05-13: Implemented Story 5.5 left-panel day mode and moved story to review.

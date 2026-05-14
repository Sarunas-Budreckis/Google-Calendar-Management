# Story 5.3: Single-Day Select

Status: review

## Story

As a **user**,
I want **to click a day number in any calendar view to select that day**,
so that **the left panel shows data source context for that specific date**.

## Acceptance Criteria

1. **AC-5.3.1 — Clicking a day number selects that day:** In Year, Month, and Week views, clicking the date number (the numeric label in a day cell or column header) sets the selected day. This is distinct from clicking an event — clicking the date number does not clear event selection and clicking an event does not change day selection.

2. **AC-5.3.2 — Selected day has a visual indicator:** The selected day number receives a distinct visual highlight (e.g. a filled circle or colored background behind the number) that is visually different from the event selection red-outline treatment. The indicator appears in all non-day views.

3. **AC-5.3.3 — Day view auto-selects the viewed day:** When the user navigates to Day view (for any date), the selected day is automatically set to that date. This auto-select does not write to the persistent "last non-day-view selection". Clicking the day header inside Day view does nothing — it is not a tap target for day selection.

4. **AC-5.3.4 — Returning from day view restores the previous selection:** When the user switches from Day view back to Year, Month, or Week view, the selected day reverts to the last day that was manually selected in a non-day view. If no manual selection has been made, selected day is null (no indicator shown).

5. **AC-5.3.5 — `DaySelectedMessage` is published on every selection change:** Whenever the selected day changes (manual click, day-view auto-select, or return from day view), a `DaySelectedMessage` is broadcast via `WeakReferenceMessenger`. Consumers subscribe to this message to update their state.

6. **AC-5.3.6 — Selected day persists across app restarts:** The last manually-selected day (from a non-day view) is stored in `NavigationState` and restored on app launch. If no selection exists, the panel starts in global mode (no day selected).

7. **AC-5.3.7 — Day selection and event selection are independent:** Selecting a day does not clear the currently selected event (right panel remains open if an event is selected). Selecting an event does not clear the day selection.

8. **AC-5.3.8 — Clicking the already-selected day number clears the selection:** If the user clicks the date number of the currently selected day, the selection is cleared (selected day becomes null, panel returns to global mode, visual indicator removed).

---

## Tasks / Subtasks

- [x] **Task 1: Extend `NavigationState` to carry selected day**
  - [x] Extend `Models/NavigationState.cs` from `record NavigationState(ViewMode, DateOnly)` to `record NavigationState(ViewMode, DateOnly, DateOnly? SelectedDay)` — add as nullable third field with default `null`
  - [x] Update `Services/NavigationStateService.cs` to read and write the new field
  - [x] Verify no existing test breaks from the record extension (add default argument where needed)

- [x] **Task 2: Create `ICalendarDaySelectionService` and `CalendarDaySelectionService`**
  - [x] Add `Services/ICalendarDaySelectionService.cs`:
    ```csharp
    public interface ICalendarDaySelectionService
    {
        DateOnly? SelectedDay { get; }
        void SelectDay(DateOnly date);
        void ClearSelection();
    }
    ```
  - [x] Add `Services/CalendarDaySelectionService.cs` implementing it:
    - Maintains `_manuallySelectedDay DateOnly?` (the last non-day-view selection, persisted)
    - `SelectDay(date)`: sets selected day, distinguishes manual vs. auto (day view) via a private flag, publishes `DaySelectedMessage`
    - `ClearSelection()`: clears selected day, publishes `DaySelectedMessage(null)`
    - On construction, reads `NavigationState.SelectedDay` from `INavigationStateService` as initial value
    - On selection change, writes back to `NavigationState.SelectedDay` via `INavigationStateService` (only for manual selections, not day-view auto-select)
  - [x] Register as singleton in `App.xaml.cs`

- [x] **Task 3: Add `DaySelectedMessage`**
  - [x] Add `Messages/DaySelectedMessage.cs`:
    ```csharp
    public sealed record DaySelectedMessage(DateOnly? SelectedDay);
    ```

- [x] **Task 4: Add day-number click handlers in all four view controls**

  **YearViewControl (`Views/YearViewControl.xaml.cs`):**
  - [x] Each day cell in the year grid has a tappable date-number label
  - [x] On tap: if date == `_daySelectionService.SelectedDay`, call `ClearSelection()`; else call `SelectDay(date)`
  - [x] Subscribe to `DaySelectedMessage` to update the visual indicator for the selected day number

  **MonthViewControl (`Views/MonthViewControl.xaml.cs`):**
  - [x] Each day cell header number is tappable
  - [x] Same tap logic as Year view

  **WeekViewControl (`Views/WeekViewControl.xaml.cs`):**
  - [x] Each column header (date number above the hourly grid) is tappable
  - [x] Same tap logic

  **DayViewControl (`Views/DayViewControl.xaml.cs`):**
  - [x] On `OnNavigatedTo` / `Loaded`, auto-select the viewed day via `SelectDay(viewedDate)` marking it as an auto-select (does not update the persisted `_manuallySelectedDay`)
  - [x] No tap handler on the day header — clicking it does nothing and must not change or clear the selection

- [x] **Task 5: Wire `MainViewModel` or `MainPage` to propagate day-view auto-select**
  - [x] When `MainViewModel.CurrentViewMode` changes TO `ViewMode.Day`, call `SelectDay(CurrentDate)` on `ICalendarDaySelectionService` as an auto-select
  - [x] When `MainViewModel.CurrentViewMode` changes FROM `ViewMode.Day` to any other mode, restore `_manuallySelectedDay` (call `SelectDay(_manuallySelectedDay)` or `ClearSelection()` if null)

- [x] **Task 6: Visual indicator in XAML**
  - [x] Add a `DataTemplate` or style for the selected-day number visual: a filled circle (~28px) behind the number using a theme-appropriate accent color, different from the red outline used for event selection
  - [x] Bind visibility of the indicator to whether the day matches `ICalendarDaySelectionService.SelectedDay`; use message subscription to trigger re-render

- [x] **Task 7: Unit tests**
  - [x] Add `GoogleCalendarManagement.Tests/Unit/Services/CalendarDaySelectionServiceTests.cs`
  - [x] `SelectDay_PublishesDaySelectedMessage`
  - [x] `ClearSelection_PublishesDaySelectedMessageWithNull`
  - [x] `SelectDay_WhenSameDaySelectedAgain_ClearsSelection` (toggle behavior for AC-5.3.8)
  - [x] `AutoSelectInDayView_DoesNotUpdatePersistentSelection`
  - [x] `ReturnFromDayView_RestoresManualSelection`

---

## Dev Notes

### Manual vs. Auto Select Distinction

The `CalendarDaySelectionService` needs to distinguish:
- **Manual select** (user clicked a day number in Year/Month/Week view): updates `_manuallySelectedDay`, persists to `NavigationState`
- **Auto-select** (Day view navigated to a date): updates the published `SelectedDay` but does NOT overwrite `_manuallySelectedDay`

One clean way: add `void AutoSelectDay(DateOnly date)` as an internal/separate method or a boolean parameter `isAutoSelect` on `SelectDay`.

### Click Target in Each View

The click must target the **day number label specifically**, not the whole day cell. Clicking a day cell to create/select events must not also select the day. If a day number and an event overlap (e.g. month view where events sit inside the day cell), only a tap directly on the number should trigger day selection. Use a dedicated `TappedRoutedEventArgs.Handled = true` on the number element to prevent event bubbling.

### `NavigationState` Extension

`NavigationState` is a `record` so adding a third field with a default value is non-breaking for callers that use named arguments. Verify `NavigationStateService.cs` serialization (it likely uses JSON or the `config` table) handles the new nullable field.

### Project Structure

```text
Models/
└── NavigationState.cs                  # extend: add SelectedDay DateOnly?

Messages/
└── DaySelectedMessage.cs               # new

Services/
├── ICalendarDaySelectionService.cs     # new
└── CalendarDaySelectionService.cs      # new

Services/NavigationStateService.cs      # extend to persist SelectedDay
App.xaml.cs                             # register CalendarDaySelectionService

Views/
├── YearViewControl.xaml.cs             # add day-number tap handler + indicator
├── MonthViewControl.xaml.cs            # add day-number tap handler + indicator
├── WeekViewControl.xaml.cs             # add day-number tap handler + indicator
└── DayViewControl.xaml.cs              # add auto-select on navigation

ViewModels/MainViewModel.cs             # trigger auto-select on view mode change

GoogleCalendarManagement.Tests/Unit/Services/
└── CalendarDaySelectionServiceTests.cs # new
```

### References

- [Epic 5 overview](../epic-overview.md) — single-day select section
- [NavigationState.cs](../../../Models/NavigationState.cs) — current record to extend
- [ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs) — parallel pattern for event selection
- [CalendarSelectionService.cs](../../../Services/CalendarSelectionService.cs) — event selection implementation to mirror
- [EventSelectedMessage.cs](../../../Messages/EventSelectedMessage.cs) — message pattern to follow
- [MainViewModel.cs](../../../ViewModels/MainViewModel.cs) — view mode change handling
- [YearViewControl.xaml.cs](../../../Views/YearViewControl.xaml.cs) — add day-number tap
- [WeekViewControl.xaml.cs](../../../Views/WeekViewControl.xaml.cs) — add column header tap
- [DayViewControl.xaml.cs](../../../Views/DayViewControl.xaml.cs) — add auto-select on load

---

## Dev Agent Record

### Implementation Plan

- Extend navigation persistence with nullable `SelectedDay` while keeping existing two-argument `NavigationState` construction source-compatible.
- Add a messenger-backed `CalendarDaySelectionService` parallel to event selection, with explicit manual, auto-select, restore, and clear paths.
- Wire day-view transitions through `MainViewModel` and update rendered date-number controls in Year, Month, and Week views with direct tap targets and filled selected-day indicators.
- Add focused service, persistence, and view-model tests, then run the full .NET regression suite.

### Debug Log

- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --filter CalendarDaySelectionServiceTests` failed before implementation because `CalendarDaySelectionService` did not exist.
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --filter CalendarDaySelectionServiceTests` passed after service implementation: 5 tests.
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --filter "CalendarDaySelectionServiceTests|NavigationStateServiceTests|SwitchViewModeCommand_DayView_AutoSelectsCurrentDate|SwitchViewModeCommand_FromDayView_RestoresManualDaySelection"` passed: 10 tests.
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64` passed: 260 tests.

### Completion Notes

- `NavigationState` now carries nullable `SelectedDay`, and `NavigationStateService` persists it via the system state repository.
- Added `DaySelectedMessage`, `ICalendarDaySelectionService`, and `CalendarDaySelectionService`; manual selections persist, auto-selects do not overwrite the last manual selection, and selecting the same day toggles selection off.
- Year, Month, and Week date numbers are dedicated tap targets that update day selection without clearing event selection. Day-view headers remain non-selectable.
- Selected-day visuals use a filled accent circle behind the day number and are updated by `DaySelectedMessage` subscriptions.
- `MainViewModel` auto-selects the viewed day when entering or navigating within Day view, and restores the last manual selection when leaving Day view.
- Follow-up: selected-day visuals now use a reddish fill, day headers select from their sync dot/weekday/number area, and the top panel exposes a selected-day calendar picker that does not navigate the current view.

### File List

- App.xaml.cs
- Messages/DaySelectedMessage.cs
- Models/NavigationState.cs
- Services/CalendarDaySelectionService.cs
- Services/ICalendarDaySelectionService.cs
- Services/NavigationStateService.cs
- ViewModels/MainViewModel.cs
- Views/DayViewControl.xaml.cs
- Views/MainPage.xaml
- Views/MainPage.xaml.cs
- Views/MonthViewControl.xaml.cs
- Views/WeekViewControl.xaml.cs
- Views/YearViewControl.xaml.cs
- GoogleCalendarManagement.Tests/Unit/Services/CalendarDaySelectionServiceTests.cs
- GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs
- docs/epic-5-day-select-left-data-panel/stories/5-3-single-day-select.md
- docs/sprint-status.yaml

### Change Log

- 2026-05-13: Implemented single-day selection service, persistence, view integration, selected-day visuals, and automated tests.
- 2026-05-13: Adjusted selected-day color, expanded day-header tap targets, and added the top-panel selected-day picker.

# Story 4.6: Multi-Select and Batch Operations

Status: ready-for-dev

## Story

As a **user**,
I want **to select multiple events and apply shared actions in one pass**,
so that **I can stage repetitive calendar changes quickly without opening each event individually**.

## Acceptance Criteria

1. **AC-4.6.1 - Shift+click range selection for timed events:** Given the user has already selected one timed event in Month, Week, or Day view, when the user Shift+clicks a second timed event in the same visible result set, then all timed events between the anchor event and the clicked event are selected inclusively, ordered by visible start time. All-day events are not included in Shift-range selection.

2. **AC-4.6.2 - Ctrl+click toggles individual events:** Given any visible event is rendered in Year, Month, Week, or Day view, when the user Ctrl+clicks that event, then its selection state toggles without clearing the rest of the current selection.

3. **AC-4.6.3 - Plain click keeps single-select behavior:** Given no modifier key is held, when the user clicks an event, then the selection collapses to that single event and the existing event-details flow continues to work.

4. **AC-4.6.4 - No drag-select rectangle is introduced:** Given the user drags on timeline space in Week or Day view, then existing drag interactions remain reserved for event creation and timed-event drag/resize workflows; Story 4.6 does not add a marquee or rectangle selection gesture.

5. **AC-4.6.5 - Selection visuals apply to every selected event:** Given multiple events are selected, then each selected event renders the same `2px` selection outline style used for single-select today, and deselected events immediately lose that outline.

6. **AC-4.6.6 - Selection badge and clear action are visible in the top bar:** Given more than one event is selected, then the shell shows a selection badge such as `"3 events selected"` plus a clear action in the existing top toolbar, and both update within 100 ms of selection changes.

7. **AC-4.6.7 - Selection persists across view switches:** Given the user has a multi-selection active, when the user switches between Month, Week, Day, or Year views, then the shared selection state persists and any still-visible selected events continue to render as selected.

8. **AC-4.6.8 - Details panel enters multi-select mode instead of collapsing:** Given more than one event is selected, then the event details panel stays open in a dedicated multi-select state where unsupported fields are disabled: title shows `"X events selected"`, time fields show `"Various"`, description is blank with a tooltip explaining multi-edit is unsupported, and the color, delete, and push actions remain available.

9. **AC-4.6.9 - Esc clears the active multi-selection:** Given multiple events are selected, when the user presses `Esc`, then the current multi-selection clears and the shell returns to the normal unselected or single-selected state.

10. **AC-4.6.10 - Batch color reuses the fixed palette and saves immediately:** Given more than one event is selected and the user opens the color action, when the user chooses one of the existing 9 canonical colors, then that color is applied to every selected event immediately using the same pending-edit persistence path as Story 4.3, with each affected event repainting in visible views and rendering at `60%` opacity if it now has a pending row.

11. **AC-4.6.11 - Batch delete reuses the staged delete flow:** Given more than one event is selected and the user invokes Delete, then a confirmation dialog lists all selected events and, on confirmation, each event follows the same delete path defined for Story 4.5: local-only drafts are removed immediately, while synced events are staged through `pending_event.operation_type = 'delete'`.

12. **AC-4.6.12 - Batch push respects selected pending events only:** Given more than one event is selected, when the user invokes Push to GCal, then only selected events that currently have pending changes are pre-populated into the Story 4.4 push surface; selected synced-only events with no pending row are ignored by the push list.

13. **AC-4.6.13 - Existing edit-mode drag and resize interactions are preserved:** Given exactly one timed event is selected and the details panel is in edit mode, then the Day/Week drag-to-move and bottom-edge resize interactions from Story 4.1 continue to work unchanged. Given more than one event is selected, those single-event interactions are disabled until the selection collapses back to one event.

14. **AC-4.6.14 - Automated coverage exists for the multi-select contract:** Unit and integration coverage verifies selection-service behavior, modifier-key selection transitions, multi-select details-panel state, and batch color/delete orchestration without regressions to existing single-selection behavior.

## Scope Boundaries

**IN SCOPE**
- Shared multi-selection state for calendar events across all four views
- Shift+click range-select for timed events and Ctrl+click toggle behavior
- Reuse of the existing selected-event outline style for multiple selected events
- Top-bar selection badge and clear action
- Multi-select state in `EventDetailsPanelViewModel` and `EventDetailsPanelControl`
- Batch color integration using Story 4.3's fixed palette and pending-event save path
- Batch delete integration using Story 4.5's staged delete flow
- Batch push pre-population using Story 4.4's pending-events push surface

**OUT OF SCOPE**
- Drag-select / marquee / rubber-band selection
- Arbitrary field multi-editing for title, date, time, or description
- New persistence tables or schema changes
- Replacing the existing single-event details panel with a separate bulk-edit screen
- Re-implementing push or delete transport logic directly in view code

## Dev Notes

### Current Repo Truth

- The repo is a flat WinUI 3 app. Keep all work inside the existing root folders: `Views/`, `ViewModels/`, `Services/`, `Models/`, `Messages/`, and `Data/`. Do **not** introduce `Core/`, `src/`, or a second UI assembly. [Source: docs/epic-4/tech-spec.md#System Architecture Alignment]
- Current selection is still single-item and Google-ID-only:
  - `Services/ICalendarSelectionService.cs` exposes `SelectedGcalEventId`, `Select(string)`, `ClearSelection()`.
  - `Services/CalendarSelectionService.cs` sends `EventSelectedMessage(string? GcalEventId)`.
  - `Views/DayViewControl.xaml.cs`, `Views/WeekViewControl.xaml.cs`, `Views/MonthViewControl.xaml.cs`, and `Views/YearViewControl.xaml.cs` all apply one selected outline by comparing against a single ID.
- `Views/MainPage.xaml` already contains the shell toolbar where the new selection badge and clear action belong. Do not create a floating overlay or separate command bar just for multi-select.

### Dependency and Sequencing Guardrails

- **Required reuse from Story 4.1:** keep the existing `2px` selected outline, `Esc` handling, and Day/Week timed-event drag and resize behavior for single-event edit mode. Story 4.6 must not regress those interactions.
- **Required reuse from Story 4.3:** batch color must use the same fixed `2x6` picker taxonomy and the same `pending_event` upsert path used for single-event color changes. Do not add a second color workflow.
- **Required reuse from Story 4.4:** batch push should only pre-populate the selected pending events into the existing push surface. If Story 4.4 is not implemented yet, add only the shared preselection seam, not a duplicate push dialog.
- **Required reuse from Story 4.5:** batch delete must call the shared delete-staging flow. Do not duplicate delete persistence logic inside `EventDetailsPanelViewModel` or any view code-behind.

### Preferred Selection Contract

Do not bolt per-view hash sets onto the existing controls. Extend the shared selection service so all views and the details panel consume one authoritative selection model.

Preferred shape:

```csharp
public sealed record CalendarEventSelection(string EventId, CalendarEventSourceKind SourceKind);

public interface ICalendarSelectionService
{
    IReadOnlyList<CalendarEventSelection> SelectedEvents { get; }
    CalendarEventSelection? PrimarySelection { get; }
    CalendarEventSelection? RangeAnchor { get; }

    void SetSingleSelection(CalendarEventSelection selection);
    void ToggleSelection(CalendarEventSelection selection);
    void SelectRange(IReadOnlyList<CalendarEventSelection> orderedVisibleEvents, CalendarEventSelection target);
    void ClearSelection();
}
```

- If Story 4.2's source-agnostic `EventId` + `SourceKind` contract already exists by implementation time, use it.
- If the codebase is still on the current Google-only contract, refactor to a source-agnostic selection contract first. Do **not** add new multi-select code that reintroduces `GcalEventId` assumptions and must be rewritten once pending drafts participate fully.
- `MainPage.SwitchViewModeAsync()` currently uses `_selectionService.SelectedGcalEventId` to center Week/Day navigation on the selected event. Update that logic to use the **primary** selection only.

### View Integration Rules

- Update all four view controls to consume the shared selection collection and to reapply outlines for every selected event during rebuild.
- Shift-range selection applies only to **timed** events because the tech spec explicitly excludes all-day events from range selection. Keep all-day events single-toggle only. [Source: docs/epic-4/tech-spec.md#Story 4.6 — Multi-Select and Batch Operations]
- Keep pointer drag reserved:
  - `WeekViewControl` and `DayViewControl` already use pointer gestures for move/resize in edit mode.
  - Story 4.2 reserves drag on empty space for creation.
  - Therefore Story 4.6 must not add a drag rectangle or any gesture that steals those pointer paths.
- When `SelectedEvents.Count > 1`, disable single-event drag/resize behaviors until the selection returns to a single timed event.

### Details Panel Rules

- Extend `EventDetailsPanelViewModel` rather than adding a second panel.
- Add explicit multi-select state such as:
  - `IsMultiSelectMode`
  - `SelectedEventCount`
  - `CanBatchColor`
  - `CanBatchDelete`
  - `CanBatchPush`
- The panel remains visible during multi-select and shows placeholders instead of trying to merge incompatible field values:
  - Title: `"X events selected"`
  - Time/date: `"Various"`
  - Description: blank with explanatory tooltip
- The existing `Esc` behavior should clear multi-selection first. Do not close the panel while multi-selection is active unless the selection becomes empty.

### Batch Action Rules

- **Batch color**
  - Reuse `IColorMappingService` and the Story 4.3 picker UI.
  - For each selected event, call the same pending-event color upsert path used for single-event color changes.
  - Publish per-event refresh messages or a single equivalent shared refresh path so visible views repaint immediately.
- **Batch delete**
  - Route each selected event through the same delete orchestration used by Story 4.5.
  - Local drafts and synced events follow different persistence paths; do not flatten them into one delete branch.
- **Batch push**
  - Only selected events with pending changes should feed the Story 4.4 push list.
  - Selected events with no pending row stay selected but are not included in the push payload.

### Testing and Regression Coverage

Extend the existing test suites instead of creating a new testing pattern:

- `GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`
- `GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs`
- `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs`

Add coverage for:
- single-select to multi-select transitions
- Shift-range ordering and inclusive bounds
- Ctrl+click toggling
- `Esc` clearing multi-selection
- panel placeholder state when `SelectedEvents.Count > 1`
- batch color applying the same canonical key to all selected events
- batch delete orchestration dispatching the expected per-event actions
- no regression to single-event drag/resize behavior in Day and Week view

### Project Structure Notes

- Keep selection state in shared services and messenger contracts, not in individual views.
- Keep business logic in view models or services; view code-behind should translate pointer and modifier input into service calls only.
- Preserve the existing WinUI 3 shell layout in `Views/MainPage.xaml` and `Views/MainPage.xaml.cs`.
- Follow the existing build and test commands:
  - `dotnet build -p:Platform=x64`
  - `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`

### References

- [Source: docs/epic-4/tech-spec.md#Story 4.6 — Multi-Select and Batch Operations]
- [Source: docs/epic-4/tech-spec.md#System Architecture Alignment]
- [Source: docs/epic-4/tech-spec.md#Detailed Design]
- [Source: docs/epic-4/tech-spec.md#Test Strategy Summary]
- [Source: docs/epic-4/stories/4-1-implement-event-editing-panel.md#Acceptance Criteria]
- [Source: docs/epic-4/stories/4-3-implement-color-picker-for-event-colors.md#Acceptance Criteria]
- [Source: Services/ICalendarSelectionService.cs]
- [Source: Services/CalendarSelectionService.cs]
- [Source: Messages/EventSelectedMessage.cs]
- [Source: ViewModels/EventDetailsPanelViewModel.cs]
- [Source: Views/MainPage.xaml]
- [Source: Views/MainPage.xaml.cs]
- [Source: Views/DayViewControl.xaml.cs]
- [Source: Views/WeekViewControl.xaml.cs]

## Tasks / Subtasks

- [ ] **Task 1: Refactor the shared selection contract to support ordered multi-select** (AC: 4.6.1, 4.6.2, 4.6.3, 4.6.7, 4.6.9)
  - [ ] Extend `ICalendarSelectionService` and `CalendarSelectionService` to track `SelectedEvents`, a primary selection, and a range anchor
  - [ ] Update `EventSelectedMessage` or replace it with a source-agnostic selection-changed message consumed by all views and the details panel
  - [ ] Update `MainPage` view-switch navigation to use the primary selection only

- [ ] **Task 2: Update all calendar views to honor shared multi-selection** (AC: 4.6.1, 4.6.2, 4.6.3, 4.6.4, 4.6.5, 4.6.7, 4.6.13)
  - [ ] Add Shift+click range-select handling for timed events in Month, Week, and Day views
  - [ ] Add Ctrl+click toggle handling in Year, Month, Week, and Day views
  - [ ] Reapply the existing selected outline to every selected event on rebuild
  - [ ] Preserve single-event drag/create/resize gesture ownership and disable those interactions while multi-select is active

- [ ] **Task 3: Extend the details panel for multi-select mode** (AC: 4.6.8, 4.6.9, 4.6.13)
  - [ ] Add multi-select presentation state to `EventDetailsPanelViewModel`
  - [ ] Update `EventDetailsPanelControl` so unsupported fields are disabled with the required placeholders
  - [ ] Keep color, delete, and push actions enabled while multi-select is active

- [ ] **Task 4: Add shell affordances for active selection count** (AC: 4.6.6, 4.6.7, 4.6.9)
  - [ ] Add a selection badge and clear action in `Views/MainPage.xaml`
  - [ ] Bind the badge to shared selection count and hide it when count is `0` or `1`

- [ ] **Task 5: Implement batch color using the existing pending-event save path** (AC: 4.6.10)
  - [ ] Reuse Story 4.3's palette and canonical color contract
  - [ ] Apply the selected color to every selected event through the same pending-event upsert logic used for single-event color changes
  - [ ] Refresh visible events immediately after the batch operation

- [ ] **Task 6: Integrate batch delete and batch push with existing shared flows** (AC: 4.6.11, 4.6.12)
  - [ ] Route batch delete through the Story 4.5 delete flow with a confirmation dialog that lists selected events
  - [ ] Route batch push through the Story 4.4 pending-events push surface, pre-populating only selected pending events

- [ ] **Task 7: Add automated coverage for the multi-select contract** (AC: 4.6.14)
  - [ ] Unit tests for selection-service transitions and modifier-key semantics
  - [ ] ViewModel tests for multi-select panel state and `Esc` clearing behavior
  - [ ] Integration tests for batch color persistence and batch delete/push orchestration seams

- [ ] **Task 8: Validate locally**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`
  - [ ] Manual: plain click single-select, Ctrl+click toggle, Shift+click range-select, view switch persistence, badge clear, batch color, batch delete, batch push preselection, `Esc` clear

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

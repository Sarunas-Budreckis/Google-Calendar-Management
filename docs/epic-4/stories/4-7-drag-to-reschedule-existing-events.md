# Story 4.7: Drag-to-Reschedule Existing Events

Status: ready-for-dev

## Story

As a **user**,
I want **to drag an existing timed event block to a new slot in Week or Day view**,
so that **I can reschedule events directly on the calendar without reopening a separate editing workflow for every move**.

## Acceptance Criteria

1. **AC-4.7.1 - Dragging an existing timed event reschedules it directly:** Given the user presses on the body of an existing timed event block in Week or Day view, when they drag and release it on a different timed slot, the event is rescheduled to the dropped start time without requiring the event to already be selected or the details panel to already be in edit mode.
2. **AC-4.7.2 - Duration is preserved and snapping stays at 15 minutes:** Given the user drags a timed event, when the preview and final drop position are computed, both start and end snap to 15-minute boundaries and the original duration is preserved exactly.
3. **AC-4.7.3 - Resize affordance remains isolated to the bottom boundary:** Given the pointer begins within the bottom-edge resize zone introduced in Story 4.1, the interaction remains an end-time resize and does not trigger the Story 4.7 reschedule path. Dragging from anywhere else on the block body uses the move path.
4. **AC-4.7.4 - Visual drag behavior is immediate and non-ghosted:** Given an event is actively being dragged, the original slot is visually vacated immediately and the dragged block itself follows the pointer at the current preview position using the pending visual treatment rather than rendering a second ghost placeholder.
5. **AC-4.7.5 - Week view supports cross-day wrap in both directions:** Given the user drags a timed event in Week view above the top of the visible day column or below the bottom, the preview and final drop wrap to the previous or next day respectively while preserving the event duration and 15-minute snap.
6. **AC-4.7.6 - All-day and timed areas are a hard boundary:** Given the user attempts to drag from a timed slot into the all-day strip, or from an all-day item into the timed surface, the drag is rejected at that boundary and no reschedule is committed.
7. **AC-4.7.7 - Drop persists through the pending-event path:** Given the user releases a dragged event, the app upserts the new start/end into `pending_event`, leaves `gcal_event` unchanged, refreshes the affected display model, and the event remains rendered at 60% opacity after drop. Existing pending drafts update their pending row; synced events create or update a pending overlay row rather than writing directly to Google data.
8. **AC-4.7.8 - Dragging does not mutate selection state:** Given an event is dragged, the gesture does not select, deselect, or multi-select anything by itself. If the dragged event was already selected, the details panel stays in sync with the new range. If it was not selected, the app must not silently force selection just to make the drag work.
9. **AC-4.7.9 - Cancel and undo are supported:** Given the user presses `Esc` during an active drag, the preview is cancelled and the event returns to its original position with no database write. Given the user presses `Ctrl+Z` after a completed drop, the last drag-reschedule is undone using the existing single-level undo behavior.
10. **AC-4.7.10 - Automated coverage exists for the drag math and persistence path:** Unit and integration coverage verifies move-vs-resize routing, 15-minute snapping, Week-view cross-day wrap, pending-row persistence for both synced and pending events, cancel behavior, and undo after drop.

## Scope Boundaries

**IN SCOPE**
- Rescheduling existing timed event blocks in `WeekViewControl` and `DayViewControl`
- Reusing and extending Story 4.1's existing pointer-preview plumbing for timed blocks
- Persisting dropped positions through the `pending_event` overlay path
- Keeping the details panel synchronized when the dragged event is already selected
- Week-view wrap-around drag behavior across adjacent days
- Maintaining the bottom-edge resize affordance introduced in Story 4.1

**OUT OF SCOPE**
- Drag-to-create on empty space (Stories 4.2 and 4.8)
- Batch drag or multi-select gesture work (Story 4.6)
- Publishing changes to Google Calendar (Story 4.4)
- Deleting events (Story 4.5)
- Dragging all-day events
- Replacing the existing resize interaction with a new UI model

## Dev Notes

### Current Repo Truth

- The repo already contains timed-event pointer handlers and preview math in [Views/WeekViewControl.xaml.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Views/WeekViewControl.xaml.cs) and [Views/DayViewControl.xaml.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Views/DayViewControl.xaml.cs).
- Story 4.1 already added `Move` vs `Resize` interaction modes, quarter-hour snap helpers, optimistic preview transforms, and view-model entry points `ApplyDraggedTimeRange(...)` / `ApplyResizedEndTime(...)` in [ViewModels/EventDetailsPanelViewModel.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/ViewModels/EventDetailsPanelViewModel.cs).
- The current implementation is still gated by `IsEditingSelectedTimedEvent(...)`, `TryGetEditableTimedRange(...)`, and `SelectedGcalEventId`; that is too narrow for Story 4.7 because the tech spec requires dragging published events or pending drafts without using selection changes as a prerequisite.

### Critical Dependency on Story 4.2 Contracts

- The Epic 4 tech spec defines Story 4.7 against source-agnostic identity (`EventId`, `CalendarEventSourceKind`) and pending-draft support.
- The live repo is still mostly Google-only:
  - [Models/CalendarEventDisplayModel.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Models/CalendarEventDisplayModel.cs) still exposes `GcalEventId`
  - [Services/ICalendarSelectionService.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Services/ICalendarSelectionService.cs) still exposes `SelectedGcalEventId`
  - [Messages/EventSelectedMessage.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Messages/EventSelectedMessage.cs) still carries only `GcalEventId`
  - [Services/ICalendarQueryService.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Services/ICalendarQueryService.cs) still uses `GetEventByGcalIdAsync(...)`
- Do **not** harden Story 4.7 around those legacy Google-only contracts. Either:
  - implement Story 4.2 first and branch Story 4.7 on top of it, or
  - include the minimal source-agnostic contract widening at the start of Story 4.7 before changing drag behavior.

### Persistence Guardrails

- `pending_event` remains the only persistence path for local rescheduling. Do **not** write directly to `gcal_event`.
- Current `PendingEvent` storage is still the Story 4.1 shape in [Data/Entities/PendingEvent.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Data/Entities/PendingEvent.cs) and [Services/PendingEventRepository.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/Services/PendingEventRepository.cs).
- Story 4.7 must work for:
  - a synced event with no pending row yet: create the pending overlay row on drop
  - a synced event with an existing pending row: update that row in place
  - a pending draft created by Story 4.2: update the draft row by pending identity rather than forcing a fake Google ID path
- Do **not** regress the Story 4.1 behavior where `Ctrl+Z` after a completed interaction can restore the prior time range.

### Interaction Guardrails

- Reuse the existing quarter-hour math already present in the controls: `SnapMinutes(...)`, `RoundToNearestQuarterHour(...)`, and the preview helpers. Extend them; do not reimplement a second snap system.
- Keep the bottom-edge resize zone at approximately `5px` and preserve current cursor behavior.
- No ghost placeholder: reuse the active border transform and height preview pattern already in the Week and Day controls so only the dragged block moves.
- All-day events remain non-draggable in this story. The all-day strip and timed grid must stay separate hit-test regions.
- When the dragged event is already open in the details panel, update the visible edit fields to the new range and keep the optimistic preview behavior from Story 4.1.
- When the dragged event is not selected, the drag must still work. Do not route the gesture through "select first, then drag".

### Suggested Implementation Shape

- Introduce a source-aware interaction registration payload for timed blocks in Week and Day controls so the drag code knows whether it is handling a synced event or a pending draft.
- Extract the current edit-mode-only drag logic out of `EventDetailsPanelViewModel` into a source-agnostic reschedule entry point that can:
  - update the selected panel state when applicable
  - persist a dropped time range through the pending repository
  - publish `EventUpdatedMessage` so `MainViewModel` refreshes only the affected event
- For Week view, extend the current `GetPreviewRange(...)` logic so vertical overflow can roll the preview into adjacent day columns instead of only shifting minutes within a single day.
- Keep `DayViewControl` simpler than Week view: it still needs direct drag-reschedule behavior, but Week-only wrap logic should stay isolated to Week math instead of leaking into shared code unnecessarily.

### Testing Requirements

- Extend the existing unit coverage in [GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs) for completed drag and undo scenarios.
- Extend the existing integration coverage in [GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs) and [GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs) for pending-overlay persistence after reschedule.
- Add focused tests for Week-view wrap math and for the "drag body moves / bottom edge resizes" interaction split.

## Tasks / Subtasks

- [ ] **Task 1: Align Story 4.7 with source-agnostic event identity** (AC: 4.7.1, 4.7.7, 4.7.8)
  - [ ] Rebase onto Story 4.2's `EventId` and `SourceKind` contracts, or land the minimum equivalent refactor first
  - [ ] Update Week and Day interaction registrations so timed blocks carry the identity needed to reschedule synced events and pending drafts through the same path

- [ ] **Task 2: Generalize the drag-reschedule application path** (AC: 4.7.1, 4.7.7, 4.7.8, 4.7.9)
  - [ ] Replace the current "selected timed event in edit mode only" gate with a source-aware reschedule method that can run whether or not the details panel is already editing that event
  - [ ] Preserve the selected-panel synchronization path when the dragged event is already open

- [ ] **Task 3: Preserve move-vs-resize routing in Week and Day controls** (AC: 4.7.2, 4.7.3, 4.7.4, 4.7.6)
  - [ ] Keep the existing bottom-edge resize affordance intact
  - [ ] Ensure dragging from the body uses the reschedule path with immediate block movement and no ghost placeholder
  - [ ] Reject drag attempts that cross the timed/all-day boundary

- [ ] **Task 4: Implement Week-view wrap-around drag math** (AC: 4.7.2, 4.7.4, 4.7.5)
  - [ ] Extend preview and drop calculations so vertical overflow wraps to adjacent days in both directions
  - [ ] Preserve duration and quarter-hour snapping through the wrap behavior

- [ ] **Task 5: Persist drops through `pending_event` and refresh affected views** (AC: 4.7.7, 4.7.8, 4.7.9)
  - [ ] Upsert pending rows for synced events and update pending rows for pending drafts
  - [ ] Publish `EventUpdatedMessage` so only the affected display model refreshes
  - [ ] Keep dragged events rendered at 60% opacity after drop

- [ ] **Task 6: Extend undo and cancel behavior** (AC: 4.7.9)
  - [ ] `Esc` during active drag cancels the preview with no persistence
  - [ ] `Ctrl+Z` after drop restores the pre-drag range, including pending-row rollback behavior

- [ ] **Task 7: Add automated coverage** (AC: 4.7.10)
  - [ ] Unit tests for move-vs-resize routing, snap math, wrap math, and undo
  - [ ] Integration tests for pending-row persistence and query refresh after drop

- [ ] **Task 8: Validate locally**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`
  - [ ] Manual: drag timed event in Day view, drag timed event across days in Week view, verify resize boundary still works, verify `Esc` cancel, verify `Ctrl+Z` undo, verify no selection side effects

## References

- [docs/epic-4/tech-spec.md](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/docs/epic-4/tech-spec.md) - Epic 4 story map, drag-to-reschedule design, schema and dependency constraints
- [docs/epic-4/stories/4-1-implement-event-editing-panel.md](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/docs/epic-4/stories/4-1-implement-event-editing-panel.md) - Existing drag preview, resize affordance, undo behavior, pending-event save flow
- [docs/epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md](C:/Users/Sarunas%20Budreckis/Documents/Programming%20Projects/Google%20Calendar%20Management/docs/epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md) - Source-agnostic identity contract required for pending-draft support

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

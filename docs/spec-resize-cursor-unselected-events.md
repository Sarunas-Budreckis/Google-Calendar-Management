---
title: 'Resize handle visible on all events; drag selects and resizes'
type: 'feature'
created: '2026-05-13'
status: 'done'
baseline_commit: '0a69d33a534a1f7bb801bedb91aed5ac00ee427f'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The bottom-edge resize cursor and drag gesture are gated behind `IsEditingSelectedTimedEvent`, meaning an event must already be selected AND in edit mode before the resize affordance appears or works. Users cannot resize an event without first clicking to select it.

**Approach:** Remove the edit-mode gate from the hover cursor so the resize cursor shows over any non-selected event's bottom edge. When a resize drag begins on an unselected event, capture the pointer immediately using the layout item's display times, then on release persist the new end via `ApplyDroppedTimeRangeAsync` and select the event in edit mode. If the event is already in edit mode when drag starts, the existing `ApplyResizedEndTime` path is preserved unchanged.

## Boundaries & Constraints

**Always:**
- Resize cursor must appear when the pointer is within `ResizeBoundaryThickness` of the bottom edge of any interactive timed event (selected or not).
- The visual drag preview (layout height override via `_timedEventLayout.DragEventId`) must work identically for selected and unselected events.
- When drag releases on an already-selected-and-editing event, the existing `ApplyResizedEndTime` path (form-field update + optimistic preview) must run unchanged.
- When drag releases on a previously-unselected event, persistence goes through `ApplyDroppedTimeRangeAsync` and the event is then selected with `openInEditMode: true`.
- All-day events and pending-delete events remain non-resizable.

**Ask First:** Any approach that changes `ApplyResizedEndTime` signature or its edit-mode guard.

**Never:**
- Do not implement a separate visual drag-handle element in XAML — the cursor change is the affordance.
- Do not remove the `IsEditingSelectedTimedEvent` check inside `ApplyResizedEndTime` itself.
- Do not mutate selection state during a resize drag — selection only happens on pointer release.
- Do not affect the move-drag path.
- Do not change DayView or WeekView behavior when the dragged event is already in edit mode.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Hover bottom edge, event not selected | Pointer over last `ResizeBoundaryThickness` px of any timed event | Resize cursor shown | — |
| Hover bottom edge, event selected+editing | Same | Resize cursor shown (unchanged behavior) | — |
| Drag bottom edge, event not selected | PointerPressed in resize zone, event not in edit mode | Visual height preview starts immediately; event remains visually unselected during drag | If event is all-day or pending-delete, drag does not start |
| Release drag, event was not selected | PointerReleased after resize drag on previously-unselected event | `ApplyDroppedTimeRangeAsync(eventId, sourceKind, originalStart, newEnd)` called; then `_selectionService.Select(eventId, sourceKind, openInEditMode: true)` called | If `ApplyDroppedTimeRangeAsync` returns false, reset preview, no selection |
| Release drag, event was already in edit mode | PointerReleased after resize drag on selected+editing event | Existing `ApplyResizedEndTime` path runs unchanged | — |
| Esc during drag, event not selected | KeyDown Escape during active resize interaction | Preview reset, interaction cancelled, no persistence, no selection | — |

</frozen-after-approval>

## Code Map

- `Views/WeekViewControl.xaml.cs` -- `TimedEventBorder_PointerMoved` (hover cursor logic), `TimedEventBorder_PointerPressed` (drag start gate), `TimedEventBorder_PointerReleased` (apply on release), `EventInteractionState` record
- `Views/DayViewControl.xaml.cs` -- Same four locations, parallel structure to WeekView
- `ViewModels/EventDetailsPanelViewModel.cs` -- `ApplyResizedEndTime` (existing, used when already editing), `ApplyDroppedTimeRangeAsync` (existing, used for unselected-event resize release)
- `Services/ICalendarSelectionService.cs` -- `Select(eventId, sourceKind, openInEditMode)` used after unselected-resize release

## Tasks & Acceptance

**Execution:**

- [x] `Views/WeekViewControl.xaml.cs` -- In `TimedEventBorder_PointerMoved` hover branch, remove the `IsEditingSelectedTimedEvent` guard so the resize cursor shows for any interactive timed event near the bottom edge. New condition: `if (sender is Border hoverBorder && _interactiveTimedEventBorders.TryGetValue(hoverBorder, out var hoverRegistration))`.

- [x] `Views/WeekViewControl.xaml.cs` -- Add `bool SelectOnRelease` parameter to `EventInteractionState` record. In `TimedEventBorder_PointerPressed`, when mode is Resize and `TryGetEditableTimedRange` fails, fall back to `registration.StartLocal`/`registration.EndLocal` as the base range and set `selectOnRelease = true`; otherwise `selectOnRelease = false`. Pass `selectOnRelease` when constructing `EventInteractionState`.

- [x] `Views/WeekViewControl.xaml.cs` -- In `TimedEventBorder_PointerReleased` (resize branch), branch on `_activeInteraction.SelectOnRelease`: if true, `await ApplyDroppedTimeRangeAsync(eventId, sourceKind, originalStart, preview.EndLocal)` then `_selectionService.Select(eventId, sourceKind, openInEditMode: true)`; if false, call existing `ApplyResizedEndTime` as before.

- [x] `Views/DayViewControl.xaml.cs` -- Mirror all three WeekView changes in the parallel `TimedEventBlock_PointerMoved`, `TimedEventBlock_PointerPressed`, `TimedEventBlock_PointerReleased`, and `EventInteractionState` in DayViewControl.

**Acceptance Criteria:**
- Given any timed event (selected or not), when the pointer enters the bottom `ResizeBoundaryThickness` pixels, then the resize cursor is shown.
- Given an unselected timed event, when the user drags from its bottom edge and releases, then the event's end time is persisted via the pending-event path and the event is opened in the details panel in edit mode.
- Given a selected-and-editing timed event, when the user drags its bottom edge, then the existing edit-form resize path runs unchanged and no selection side effects occur.
- Given a resize drag is in progress on an unselected event, when Esc is pressed, then the preview resets and no persistence or selection occurs.

## Verification

**Commands:**
- `dotnet build "c:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Calendar Management\GoogleCalendarManagement.csproj" -p:Platform=x64` -- expected: Build succeeded, 0 errors
- `dotnet test "c:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Calendar Management\GoogleCalendarManagement.Tests" -p:Platform=x64` -- expected: all tests pass

**Manual checks (if no CLI):**
- Open week or day view. Hover the bottom edge of an unselected event: resize cursor should appear.
- Drag from the bottom of an unselected event, release: event end time should change and the event should be selected in edit mode.
- Drag from the bottom of a selected event in edit mode: original behavior unchanged (form fields update, no extra selection call).

## Suggested Review Order

**Resize cursor affordance (hover)**

- Removed edit-mode gate; resize cursor now appears on any event's bottom edge.
  [`WeekViewControl.xaml.cs:930`](../Views/WeekViewControl.xaml.cs#L930)

- Same change mirrored in Day view.
  [`DayViewControl.xaml.cs:723`](../Views/DayViewControl.xaml.cs#L723)

**Drag start — unselected event fallback**

- When not in edit mode: use layout times and set `SelectOnRelease = true` instead of aborting.
  [`WeekViewControl.xaml.cs:899`](../Views/WeekViewControl.xaml.cs#L899)

- Same fallback in Day view.
  [`DayViewControl.xaml.cs:692`](../Views/DayViewControl.xaml.cs#L692)

**Drag release — `SelectOnRelease` branching**

- Zero-movement tap selects only; real drag persists via `ApplyDroppedTimeRangeAsync` then selects in edit mode.
  [`WeekViewControl.xaml.cs:993`](../Views/WeekViewControl.xaml.cs#L993)

- Locals captured before `await` (null-safety); `ResetInteractivePreview` called on both success and failure.
  [`WeekViewControl.xaml.cs:1002`](../Views/WeekViewControl.xaml.cs#L1002)

- Same branching in Day view.
  [`DayViewControl.xaml.cs:786`](../Views/DayViewControl.xaml.cs#L786)

**Supporting type**

- `SelectOnRelease` parameter added to `EventInteractionState` record (Week).
  [`WeekViewControl.xaml.cs:1548`](../Views/WeekViewControl.xaml.cs#L1548)

- Same addition in Day view record.
  [`DayViewControl.xaml.cs:1243`](../Views/DayViewControl.xaml.cs#L1243)

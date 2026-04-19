---
title: 'Fix event bottom-drag resize visual bugs'
type: 'bugfix'
created: '2026-04-19'
status: 'done'
baseline_commit: '96de70f657cff0ed29dc95476c383888da192e7c'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** When dragging the bottom edge of an event to resize it in week view, two bugs occur: (1) the event border is visually clipped at its original height while dragging down, and (2) reducing height by dragging up shrinks the event centered around the middle instead of anchoring the top.

**Approach:** Drive the resize preview through the virtualizing layout (add a drag-height override to `WeekTimedEventVirtualizingLayout` and call `InvalidateMeasure`) instead of mutating `border.Height` directly. This ensures the element is always Arranged at its correct bounds, eliminating both the layout-clip and centering issues.

## Boundaries & Constraints

**Always:**
- Top position of the resized event must not move during drag.
- When not dragging, event rendering is unchanged (layout uses original item bounds).
- Move-mode drag is unaffected (still uses `TranslateTransform.Y` + `border.Height = NaN`).

**Ask First:** Any change that would affect how events are rendered when not in an active interaction.

**Never:**
- Do not set `border.Height` to a numeric value during resize drag.
- Do not modify `_timedEventItems` or rebuild the list during drag.
- Do not change drag-release logic (`ApplyResizedEndTime`).
- Do not use ScaleTransform or Canvas overlay for the resize preview.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Extend event down | Drag bottom edge downward | Event border grows downward smoothly, top stays fixed, no clipping | Clamp to max end (existing logic) |
| Shrink event | Drag bottom edge upward | Event border shrinks from the bottom, top stays fixed | Clamp to min 15 min (existing logic) |
| Release drag | Pointer released | Event snaps to final time, layout resets to item bounds | N/A |
| Cancel drag (pointer capture lost) | Focus lost mid-drag | Layout resets to item bounds | N/A |
| Move mode drag | Drag event body | Behavior unchanged — uses TranslateTransform, no layout invalidation | N/A |

</frozen-after-approval>

## Code Map

- `Views/WeekTimedEventVirtualizingLayout.cs` -- VirtualizingLayout that Measures/Arranges event borders; needs drag-height override properties
- `Views/WeekViewControl.xaml.cs` -- hosts pointer event handlers; `ApplyInteractivePreview` (resize branch) and `ResetInteractivePreview` need to use layout override instead of `border.Height`

## Tasks & Acceptance

**Execution:**
- [x] `Views/WeekTimedEventVirtualizingLayout.cs` -- Add `public string? DragGcalEventId { get; set; }` and `public double DragHeight { get; set; }` properties. In `TryGetValidBounds`, if `item.GcalEventId == DragGcalEventId` and `DragHeight > 0`, substitute `DragHeight` for `item.Height` in the returned `Rect`. Make `TryGetValidBounds` an instance method (remove `static`) so it can access these properties.
- [x] `Views/WeekViewControl.xaml.cs` -- In `ApplyInteractivePreview` (resize branch): remove the `interaction.Border.Height = ...` line. Instead, compute `newHeight = Math.Max(15.0, registration.BaseHeight + MinutesToPixels(preview.MinuteDelta))`, set `_timedEventLayout.DragGcalEventId = interaction.GcalEventId` and `_timedEventLayout.DragHeight = newHeight`, then call `TimedEventsRepeater.InvalidateMeasure()`.
- [x] `Views/WeekViewControl.xaml.cs` -- In `ResetInteractivePreview`: after the existing `border.Height = double.NaN` line, clear `_timedEventLayout.DragGcalEventId = null` and call `TimedEventsRepeater.InvalidateMeasure()` to restore the original layout bounds.

**Acceptance Criteria:**
- Given an event is in edit mode, when dragging its bottom edge downward, then the event border extends smoothly without any visual clipping at the original height.
- Given an event is in edit mode, when dragging its bottom edge upward to shorten the event, then the top of the event remains stationary and only the bottom moves up.
- Given a resize drag is in progress, when the pointer is released or capture is lost, then the event renders at its original (or newly committed) bounds with no residual height override.
- Given an event is not being dragged, when the week view renders, then event appearance is identical to pre-fix behavior.

## Spec Change Log

## Design Notes

In WinUI 3, `UIElement.Arrange(finalRect)` causes the element to be layout-clipped to `finalRect` when its desired size exceeds that rect. Setting `border.Height` directly changes desired size but not the Arrange rect, so the layout clips the overflow. The fix routes the new height through the layout itself so Measure and Arrange both receive the correct bounds, preventing any layout clip.

When the drag ends or is cancelled, setting `DragGcalEventId = null` causes the layout to fall back to the original item bounds on the next measure pass.

## Verification

**Commands:**
- `dotnet build "c:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Calendar Management\GoogleCalendarManagement.csproj"` -- expected: Build succeeded, 0 errors

**Manual checks (if no CLI):**
- Open week view, select an event, enter edit mode. Drag bottom edge down: border should grow smoothly with no clipping. Drag bottom edge up: top should stay fixed, bottom shrinks. Release: event snaps correctly.

# Story 3.10: Virtualize Week View Event Rendering

Status: backlog

## Story

As a **user**,
I want **the week view to render smoothly even when I have many events**,
So that **scrolling, resizing, and view switching feel fast regardless of how full my calendar is**.

## Context

During the Story 3.1 code review (2026-03-31), it was identified that `WeekViewControl` builds event blocks imperatively — directly adding `Border` elements to a `Grid` via `Children.Add`. This approach rebuilds the entire grid synchronously on every `Rebuild()` call, including every `SizeChanged` event during window resize. At low event counts this is acceptable; at 200+ events per week it will cause visible jank.

The story 3.1 spec required `ItemsRepeater` + `RecyclingElementFactory` for virtualization; the imperative approach was accepted as a known deviation to unblock delivery, with this follow-up story created to close the gap.

## Acceptance Criteria

1. **AC-3.10.1 — ItemsRepeater:** Given a week view with 200+ timed events, event blocks are rendered using `ItemsRepeater` with `RecyclingElementFactory` rather than imperative `Children.Add` calls.

2. **AC-3.10.2 — Resize performance:** Given a window resize drag, the week view does not synchronously rebuild all event blocks on each pixel of movement; a debounce of ≤150 ms is applied to `SizeChanged` before triggering a layout rebuild.

3. **AC-3.10.3 — Visual parity:** All event blocks continue to display with the correct time position, column, row span, colour, and title as in the pre-virtualization implementation.

4. **AC-3.10.4 — No regression:** All existing week-view tests pass; `dotnet build -p:Platform=x64` and `dotnet test` pass with 0 errors.

## Scope

**IN SCOPE:**
- Refactor `WeekViewControl` timed-event rendering to use `ItemsRepeater` + `RecyclingElementFactory`
- Add `SizeChanged` debounce to `WeekViewControl`
- Remove the `TODO Story 3.x` comment added in 3.1

**OUT OF SCOPE:**
- Virtualization of `MonthViewControl`, `DayViewControl`, `YearViewControl` (separate concern)
- Any changes to data fetching, service layer, or ViewModel

## Dev Notes

- See `Views/WeekViewControl.xaml.cs` — the `Rebuild()` method and `WeekGrid.Children.Add` loop contain the TODO comment referencing this story.
- `ItemsRepeater` in WinUI 3 requires a `RecyclingElementFactory` with a `RecyclePool` for efficient element reuse. The element factory creates `Border` event blocks from a template.
- `SizeChanged` debounce: use a `DispatcherTimer` with a short interval (100–150 ms); reset it on each `SizeChanged` event; call `Rebuild()` only when it fires.
- Ensure the debounce timer is disposed or stopped in `Unloaded`.

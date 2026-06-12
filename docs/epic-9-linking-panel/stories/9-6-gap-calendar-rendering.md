# Story 9.6: Gap Calendar Rendering (Outlines + `+`)

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Prerequisites:** Story 9.5 (Gaps lens) must be complete and merged

---

## Story

As a **user with the Gaps lens active**,
I want **empty gray outlines with a `+` icon to appear on the calendar for each gap window**,
so that I can see gaps spatially in context alongside my events, click them to open their detail,
and immediately understand what time is unaccounted for.

---

## Acceptance Criteria

1. **AC-9.6.1 — Outlines appear only while Gaps lens is active:** When the user switches to the Gaps lens (Linking panel, "Gaps" tab), a `GapsLensActivatedMessage` is broadcast and all active calendar views (Week, Day, Month) render gap outlines. When the user leaves the Gaps lens (switches panel or lens), a `GapsLensDeactivatedMessage` is broadcast and all outlines are cleared immediately.

2. **AC-9.6.2 — Week view renders gap outlines:** In the week view, each gap window within the displayed week is rendered as a positioned `Border` on a dedicated `GapOverlayCanvas` layer (z-order above grid lines, below the current-time indicator). The border is:
   - Background: transparent
   - Border color: `#888888` (medium gray), `BorderThickness=1.5`, dashed or semi-transparent
   - Corner radius: same `AppCornerRadiusElement` resource as event borders
   - Interior: a `+` glyph (Segoe Fluent Icons ``, or a centered `TextBlock` with `"+"`) centered in the outline
   - Opacity=0.7 when not selected; Opacity=1.0 when selected (see AC-9.6.5)

3. **AC-9.6.3 — Day view renders gap outlines:** Same approach as week view — a `GapOverlayCanvas` added to `DayViewControl.xaml`, positioned outlines using the same time→pixel conversion used for timed events.

4. **AC-9.6.4 — Month view shows a gap count badge on affected day cells:** In the month view, a small gray rounded badge ("Gap" or gap-count dot) is added to each day cell that has ≥1 gap. It uses the same day-cell augmentation pattern as sync-status dots. It does not render a full time outline (month cells have insufficient height).

5. **AC-9.6.5 — Selecting a gap highlights it:** Clicking a gap outline (in any view) sends a `GapSelectedMessage(GapId)` via `WeakReferenceMessenger`. The previously-selected gap border returns to Opacity=0.7; the newly-selected one renders at Opacity=1.0 with a slightly thicker border. The Gaps lens panel (9.5) observes the same message and scrolls/highlights the corresponding row.

6. **AC-9.6.6 — Gaps are capped at 24h:** A gap window's rendered extent is capped at 24h. A gap whose raw data spans > 24h is trimmed to end 24h after its start. This cap applies during rendering only; the underlying gap data is unchanged.

7. **AC-9.6.7 — Outlines do not interfere with event interaction:** Gap outlines are rendered on a separate `Canvas` layer that sits below interactive timed event borders. `IsHitTestVisible=false` on the canvas itself; gap outlines have their own `Tapped` handler registered individually.

8. **AC-9.6.8 — Outlines clear when navigating away:** Switching calendar views (week→month, week→day, etc.) while the Gaps lens is active re-renders gap outlines for the new view's visible range. Navigating to a date range with no gaps shows no outlines.

9. **AC-9.6.9 — Outlines update when gap list changes:** If the Gaps lens recalculates its gap list (e.g. user changes the scope date range in 9.5), a `GapListChangedMessage` is broadcast and calendar views re-render outlines.

---

## Tasks / Subtasks

- [ ] **Task 1: Define shared messages and display model**
  - [ ] 1.1 `Messages/GapsLensActivatedMessage.cs` — `record GapsLensActivatedMessage(IReadOnlyList<GapWindowDisplayModel> Gaps);`
  - [ ] 1.2 `Messages/GapsLensDeactivatedMessage.cs` — `record GapsLensDeactivatedMessage();`
  - [ ] 1.3 `Messages/GapSelectedMessage.cs` — `record GapSelectedMessage(string GapId);`
  - [ ] 1.4 `Messages/GapListChangedMessage.cs` — `record GapListChangedMessage(IReadOnlyList<GapWindowDisplayModel> Gaps);`
  - [ ] 1.5 `Models/GapWindowDisplayModel.cs`:
    ```csharp
    public sealed record GapWindowDisplayModel(
        string GapId,
        DateTime StartUtc,
        DateTime EndUtc,
        DateTime StartLocal,
        DateTime EndLocal,
        IReadOnlyList<string> ParticipatingSourceKeys);
    ```

- [ ] **Task 2: `GapsLensPanelViewModel` integration (in 9.5 — verify/extend)**
  - [ ] 2.1 When the Gaps lens becomes active, `GapsLensPanelViewModel` calls `WeakReferenceMessenger.Default.Send(new GapsLensActivatedMessage(currentGaps))`
  - [ ] 2.2 When the user leaves the Gaps lens, send `GapsLensDeactivatedMessage`
  - [ ] 2.3 When the gap list recalculates, send `GapListChangedMessage(newGaps)`
  - [ ] 2.4 `GapsLensPanelViewModel` subscribes to `GapSelectedMessage` and scrolls/highlights the matching row in its list
  - [ ] 2.5 If 9.5 is already complete, confirm the VM has a `SelectGap(string gapId)` method (or equivalent) that the calendar views trigger via the message

- [ ] **Task 3: Gap rendering helpers (shared logic)**
  - [ ] 3.1 Add `Services/GapCalendarRenderingHelper.cs` — a static/pure helper with:
    - `GapWindowDisplayModel? CapGapTo24h(GapWindowDisplayModel gap)` — trims `EndLocal` to `StartLocal + 24h` if needed
    - `double TimeToYOffset(DateTime localTime, double hourHeight)` — reuse/mirror the same formula used in `WeekViewControl`/`DayViewControl` for timed events
    - `double GapHeightPixels(GapWindowDisplayModel gap, double hourHeight)` — `(EndLocal - StartLocal).TotalHours * hourHeight`, minimum 8px
  - [ ] 3.2 Define `GapBorderStyle` static factory method in the helper that creates a `Border` with the spec'd gray/dashed appearance and `+` glyph

- [ ] **Task 4: Week view gap overlay**
  - [ ] 4.1 `Views/WeekViewControl.xaml` — add `<Canvas x:Name="GapOverlayCanvas" IsHitTestVisible="False" />` to `WeekBodySurface` Grid (between `WeekGrid` and `TimedEventsRepeater` in z-order so gaps render above grid lines but below events)
  - [ ] 4.2 `Views/WeekViewControl.xaml.cs`:
    - Add `private IReadOnlyList<GapWindowDisplayModel> _activeGaps = [];`
    - Subscribe to `GapsLensActivatedMessage`, `GapsLensDeactivatedMessage`, `GapListChangedMessage` in constructor
    - `RenderGapOutlines()`: clears `GapOverlayCanvas.Children`; for each gap overlapping the rendered week range, creates a `Border` via `GapCalendarRenderingHelper.GapBorderStyle()`, positions it using `Canvas.SetLeft`/`Canvas.SetTop` (use `_renderedDayColumnWidth` per day column and `TimeToYOffset` for vertical position); registers a `Tapped` handler per border that sends `GapSelectedMessage(gap.GapId)`
    - `ClearGapOutlines()`: clears `GapOverlayCanvas.Children` and `_activeGaps`
    - Call `RenderGapOutlines()` whenever `_activeGaps` is non-empty and the view re-renders (hook into the existing `RenderWeek` or equivalent method)
    - Subscribe to `GapSelectedMessage` to update visual state (border thickness/opacity) of the selected gap border

- [ ] **Task 5: Day view gap overlay**
  - [ ] 5.1 `Views/DayViewControl.xaml` — add `<Canvas x:Name="GapOverlayCanvas" IsHitTestVisible="False" />` analogous to Task 4.1
  - [ ] 5.2 `Views/DayViewControl.xaml.cs` — same pattern as Task 4.2; the day view renders a single day column so gap positioning is simpler (no column offset needed)

- [ ] **Task 6: Month view gap badge**
  - [ ] 6.1 `Views/MonthViewControl.xaml.cs` — after gap activation, for each day cell in the rendered month, check if `_activeGaps` contains any gap whose `StartLocal.Date` or `EndLocal.Date` falls on that day; if so, add a small `Border` (rounded, `8x8` gray dot) or a `TextBlock "▪"` in the day cell's indicator row (same row as sync-status dots, which already uses a row/column approach in the month grid)
  - [ ] 6.2 No gap outline is rendered in month view — only the badge dot

- [ ] **Task 7: Year view (minimal)**
  - [ ] 7.1 Year view: no outline; no badge needed in this story. Add a `// TODO(9.8): coverage indicators` comment only.

- [ ] **Task 8: Tests**
  - [ ] 8.1 `GoogleCalendarManagement.Tests/Unit/Services/GapCalendarRenderingHelperTests.cs`:
    - `CapGapTo24h_WhenUnder24h_Unchanged`
    - `CapGapTo24h_WhenOver24h_TrimmedToExactly24h`
    - `GapHeightPixels_MinimumIs8px`
  - [ ] 8.2 `GoogleCalendarManagement.Tests/Unit/ViewModels/GapsLensPanelViewModelTests.cs` (extend from 9.5):
    - `OnLensActivated_BroadcastsGapsLensActivatedMessage`
    - `OnLensDeactivated_BroadcastsGapsLensDeactivatedMessage`
    - `WhenGapSelected_VmScrollsToMatchingRow`
  - [ ] 8.3 Manual smoke test:
    - Switch to Gaps lens → confirm outlines appear in Week view for any loaded gap
    - Click a gap outline → confirm corresponding row highlights in the Gaps panel
    - Click a gap row in the Gaps panel → confirm calendar outline highlights
    - Switch away from Gaps lens → confirm all outlines clear
    - Navigate week forward/back while Gaps lens active → outlines update to new range

---

## Dev Notes

### Hard prerequisites — what 9.5 must deliver

This story is entirely an overlay on 9.5's data. The following must exist before implementing 9.6:

```csharp
// GapsLensPanelViewModel (from 9.5) must expose:
IReadOnlyList<GapWindowDisplayModel> CurrentGaps { get; }
void OnGapSelectedExternally(string gapId);  // or subscribes to GapSelectedMessage itself
```

If 9.5 stores gaps as a different type, create a mapping adapter — do not change 9.5's internal model.

### Canvas layout in Week view — critical positioning rules

`WeekBodySurface` in `WeekViewControl.xaml` is a `Grid` with four children in z-order:
1. `WeekGrid` (time grid + day columns)
2. `CreationOverlayCanvas` (drag-to-create ghost)
3. `TimedEventsRepeater` (actual event borders)
4. `CurrentTimeOverlayCanvas` (`IsHitTestVisible=false`)

**Insert `GapOverlayCanvas` between `WeekGrid` and `CreationOverlayCanvas`** (z-index 1.5). This ensures gaps render above grid lines but below event interaction targets and the drag-to-create overlay.

Use `Canvas.SetZIndex` if Grid z-order is insufficient; prefer explicit XAML ordering.

Gap outlines must NOT use `IsHitTestVisible=false` on the canvas itself — each `Border` child needs an individual `Tapped` handler. Set `IsHitTestVisible=false` on the canvas wrapper, then set `IsHitTestVisible=true` on each gap `Border` explicitly.

### Day-column X offset calculation for Week view

The week view renders `_renderedDayColumnWidth` per day column. Use the same `_renderedDayColumnWidth` field already computed in `WeekViewControl`. X offset for day column `d` (0-indexed from `_renderedWeekStart`):

```csharp
double x = WeekGridHorizontalPadding + d * _renderedDayColumnWidth + columnInset;
```

Mirror whatever formula the existing timed event layout uses for left-edge positioning — check `WeekTimedEventProjectionBuilder.cs` for the exact pixel math. Do not duplicate the formula; call the shared helper.

### Hour-to-pixel formula

Both `WeekViewControl` and `DayViewControl` use a fixed `hourHeight` constant for converting time to vertical pixel offset. Find the constant in each view's code (search for `hourHeight` or `HourHeight`). Use the same constant for gap outlines. If it is a field, store a reference; do not hard-code `60.0` or any magic number.

### Visual spec for gap border

```csharp
// Desired appearance:
var border = new Border
{
    Background = new SolidColorBrush(Color.FromArgb(0x15, 0x88, 0x88, 0x88)),  // very faint fill
    BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x88, 0x88, 0x88)),
    BorderThickness = new Thickness(1.5),
    CornerRadius = ElementCornerRadius,   // from Application.Current.Resources["AppCornerRadiusElement"]
    Opacity = 0.7,
    Child = new TextBlock
    {
        Text = "",          // Segoe Fluent Icons "Add" glyph
        FontFamily = new FontFamily("Segoe Fluent Icons"),
        FontSize = 14,
        Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x88, 0x88, 0x88)),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    },
};
```

If `Segoe Fluent Icons` is unavailable, fall back to a plain `"+"` `TextBlock`. Check other views for the glyph font precedent — the app already uses `Segoe Fluent Icons` for toolbar buttons.

### Selected gap visual state

Maintain a `Dictionary<string, Border> _gapBorderMap` in each view keyed by `GapId`. On `GapSelectedMessage`:
1. Reset the previously selected border to `Opacity=0.7, BorderThickness=1.5`.
2. Set the newly selected border to `Opacity=1.0, BorderThickness=2.0`.

### Month view gap badge

Month day cells are small; do not attempt a time-accurate outline. Find where sync-status dots are rendered per day cell in `MonthViewControl.xaml.cs` (search for `SyncedColor` / `NotSyncedColor` usage). Add a gray dot `8x8` rounded `Border` in the same indicator row, offset to the right of existing dots. Tag each badge `Border.Tag = gapId` for click handling (clicking a month gap badge sends `GapSelectedMessage` and may navigate to week/day view — **do not implement auto-navigation in this story**, just send the message).

### Messages are `WeakReferenceMessenger` records

Follow the pattern already used in the project:

```csharp
// Send (from GapsLensPanelViewModel):
WeakReferenceMessenger.Default.Send(new GapsLensActivatedMessage(gaps));

// Subscribe (in WeekViewControl constructor):
WeakReferenceMessenger.Default.Register<WeekViewControl, GapsLensActivatedMessage>(
    this, static (r, m) => r.DispatcherQueue.TryEnqueue(() => r.OnGapsLensActivated(m.Gaps)));
WeakReferenceMessenger.Default.Register<WeekViewControl, GapsLensDeactivatedMessage>(
    this, static (r, _) => r.DispatcherQueue.TryEnqueue(r.ClearGapOutlines));
WeakReferenceMessenger.Default.Register<WeekViewControl, GapListChangedMessage>(
    this, static (r, m) => r.DispatcherQueue.TryEnqueue(() => r.OnGapsLensActivated(m.Gaps)));
```

Use `DispatcherQueue.TryEnqueue` for any UI update from a messenger handler — same as existing `DataSourcePanelViewModel` usage.

### Unsubscribe on unload

Call `WeakReferenceMessenger.Default.UnregisterAll(this)` in the `Unloaded` event handler of each view (or use the weak reference pattern — confirm how existing views handle this; e.g. `WeakViewControl` may already do it in `Unloaded`).

### No new service class needed

Gap rendering is a pure view-layer concern. No new Core service is needed. `GapCalendarRenderingHelper` is a thin static helper for pixel math and border construction — keep it in the `Views` project folder or alongside the views (not in `Services/`).

### File additions summary

```
Models/
  GapWindowDisplayModel.cs               ← new

Messages/
  GapsLensActivatedMessage.cs            ← new
  GapsLensDeactivatedMessage.cs          ← new
  GapSelectedMessage.cs                  ← new
  GapListChangedMessage.cs               ← new

Views/
  GapCalendarRenderingHelper.cs          ← new (pixel math + border factory)
  WeekViewControl.xaml                   ← add GapOverlayCanvas
  WeekViewControl.xaml.cs                ← subscribe to messages, RenderGapOutlines, ClearGapOutlines
  DayViewControl.xaml                    ← add GapOverlayCanvas
  DayViewControl.xaml.cs                 ← same as week view
  MonthViewControl.xaml.cs               ← gap badge in day cells

ViewModels/
  GapsLensPanelViewModel.cs (from 9.5)  ← extend to send activation/deactivation messages

GoogleCalendarManagement.Tests/Unit/Services/
  GapCalendarRenderingHelperTests.cs     ← new
```

### What NOT to change

- `WeekTimedEventVirtualizingLayout.cs` — gap outlines bypass the virtualizing layout entirely; they are positioned directly on a Canvas
- `CalendarEventDisplayModel.cs` — gap is a separate model, not an event
- `ICalendarQueryService` — gaps come from the Gaps lens, not from the calendar query
- `MainViewModel.cs` — no gap state needed on the main VM; use messages
- Event selection (`ICalendarSelectionService`) — gap selection is a separate concern; do not route gap clicks through the event selection service

### References

- [Epic 9 overview](../epic-overview.md) — §Story 9.6 spec
- [Concepts §8 — four linking workflows](../../epic-8-data-linking/concepts.md) — gaps definition: cross-source blank-period clumping, ≤24h cap
- [Story 9.5](9-5-linking-panel-gaps-lens.md) — prerequisite; provides `GapsLensPanelViewModel` and gap data model
- `Views/WeekViewControl.xaml` — `WeekBodySurface` Grid structure; `CreationOverlayCanvas`, `CurrentTimeOverlayCanvas` patterns
- `Views/WeekViewControl.xaml.cs` — `_renderedDayColumnWidth`, `ElementCornerRadius`, brush constants, event border rendering pattern, `DispatcherQueue` usage
- `Views/DayViewControl.xaml.cs` — same fields/patterns, single-column layout
- `Views/MonthViewControl.xaml.cs` — day-cell sync-dot rendering location
- `Messages/DaySelectedMessage.cs` — simple record message pattern
- `Services/GapCalendarRenderingHelper.cs` — add to `Views/` folder (not `Services/`) since it is UI-layer only

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

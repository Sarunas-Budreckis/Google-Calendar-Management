# Story 3.14: Calendar View Polish & Bug Fixes

Status: drafted

## Story

As a **calendar user**,
I want **the calendar views to feel polished, visually coherent, and free of known rendering gaps**,
so that **the app feels production-quality for daily use — with a clear current-time indicator, consistent day highlighting, correct event rendering across midnight, and smooth side-panel animations**.

## Acceptance Criteria

### Bug Fix 1: Midnight-Crossing Events in Week View

1. **AC-3.14.1 — Events crossing midnight render in both days:** Given a timed event starts on day N and ends on day N+1 (or later), the week view renders a block in day N's column (from start time to midnight) AND a block in day N+1's column (from midnight to end time). Both blocks display the event's title and color. Both blocks are clickable and select the same event.

2. **AC-3.14.2 — Correct time labels on split blocks:** Given a midnight-crossing event is split across two columns, the block in the start day shows the event's original start time, and the block in the end day shows the event's original end time. This makes clear to the user the full duration of the event.

3. **AC-3.14.3 — Day view also handles midnight-crossing events correctly:** Given a midnight-crossing event is visible in day view (either the start day or the end day is the active day), the block renders correctly covering the appropriate portion of that day's timeline.

---

### Enhancement 1: Current Day Highlight

4. **AC-3.14.4 — Today's date is highlighted in all views:** Given today's date is visible in any calendar view (year, month, week, or day), the day number/label for today is displayed with a light blue filled circle behind the number, distinguishing it clearly from other days.

5. **AC-3.14.5 — Highlight is accurate and updates at midnight:** Given the app has been open across midnight, the highlight moves to the new day at midnight (or on the next view refresh). The highlight reflects the device's local date, not UTC.

6. **AC-3.14.6 — Non-today days show no highlight circle:** Given a day is not today, its day number renders without a background circle. Previously-selected days also do not retain the blue circle (selected state is a separate visual treatment).

---

### Enhancement 2: Current Time Red Line (Week and Day Views)

7. **AC-3.14.7 — Red time indicator in week and day views:** Given today's date is visible in the week view or is the active day in day view, a horizontal red line is rendered at the pixel position corresponding to the current local time in that day's timeline. A small filled red circle (dot) is displayed on the left edge of the timeline where the line starts.

8. **AC-3.14.8 — Time indicator updates live:** Given the app is open, the red line's vertical position updates every minute to reflect the current time without requiring the user to navigate away and back.

9. **AC-3.14.9 — Time indicator absent on non-today days:** Given the week view is displayed and today is not in the visible week (or a past/future week is shown), no red time indicator is displayed.

10. **AC-3.14.10 — Time indicator absent in other views:** Given the year or month view is active, no red time line is shown (those views use the current-day circle highlight only).

---

### Enhancement 3: Smooth Side Panel Animation

11. **AC-3.14.11 — Side panel opens with smooth width animation:** Given the user selects an event and the details panel opens, the panel width expands smoothly from 0 to its full width over approximately 200 ms. The calendar view content area shrinks smoothly in sync — no layout jump at the end of the animation.

12. **AC-3.14.12 — Side panel closes with smooth width animation:** Given the user dismisses the details panel, the panel width contracts smoothly from full width to 0 over approximately 200 ms. The calendar content area expands smoothly in sync.

13. **AC-3.14.13 — Animation does not block interaction:** Given the panel is animating open or closed, the calendar view remains interactive (scrollable, clickable) during the animation.

## Scope Boundaries

**IN SCOPE — this story:**
- Week view midnight-crossing event rendering fix
- Day view midnight-crossing event rendering fix
- Current-day blue circle highlight in Year, Month, Week, Day views
- Current time red line + dot in Week and Day views
- Side panel open/close smooth width animation

**OUT OF SCOPE — do NOT implement:**
- Month view rendering changes (Story 3.13)
- Year view event indicator dots (Story 3.9)
- Event editing (Epic 4)
- Hover tooltips on events
- Any changes to the sync pipeline or data loading

## Dev Notes

### Bug Fix: Midnight-Crossing Events

**Current behavior:** `WeekViewControl` places events using `Grid.SetRow(block, event.StartLocal.Hour)` and `Grid.SetRowSpan(block, hours)`. Events are placed in one column only. An event from 10 PM to 2 AM the next day would be placed in column `dayN` spanning from row 22 to row 26, but row 26 is outside the 0–23 range and the block either clips or is invisible after midnight.

**Fix:** Before rendering, for each event, check if `EndLocal.Date > StartLocal.Date`. If yes, split into two display segments:
- Segment A: column = `StartLocal.DayOfWeek`, start = `StartLocal.Hour`, end = 24 (midnight)
- Segment B: column = `EndLocal.DayOfWeek`, start = 0 (midnight), end = `EndLocal.Hour`

Both segments should share the same event ID for selection purposes. If the event spans more than 2 days (e.g., 3+ days), generate a segment for each calendar day covered.

Apply the same logic to `DayViewControl` — clip the event to the visible day's 0–24 range.

### Enhancement: Current Day Blue Circle

**Approach:** In each view's day cell, compare the cell date against `DateOnly.FromDateTime(DateTime.Now)`. If they match, apply a visual state or style that shows a blue `Ellipse` or `Border` with `CornerRadius` behind the day number `TextBlock`.

Suggested XAML pattern for a day number cell:
```xaml
<Grid>
    <Ellipse Width="28" Height="28"
             Fill="{ThemeResource SystemAccentColorLight1}"
             Visibility="{x:Bind IsToday, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}"/>
    <TextBlock Text="{x:Bind DayNumber}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
</Grid>
```

Where `IsToday` is a computed property on the per-cell view model or data item.

**Midnight update:** Subscribe to a `DispatcherTimer` that fires at midnight (or check on each view navigation). Since the user typically navigates views frequently enough, simply re-evaluating `IsToday` on each navigation is acceptable for Tier 1.

### Enhancement: Current Time Red Line

**Approach:** The red line is an absolute-positioned `Canvas` element overlaid on the time grid for the current day's column.

```csharp
private void UpdateCurrentTimeLine()
{
    var now = DateTime.Now;
    var minutesSinceMidnight = now.Hour * 60 + now.Minute;
    var totalMinutesInDay = 24 * 60;
    var topOffset = (minutesSinceMidnight / (double)totalMinutesInDay) * TimelineHeight;

    CurrentTimeLineCanvas.Visibility = Visibility.Visible;
    Canvas.SetTop(CurrentTimeLineElement, topOffset);
    Canvas.SetTop(CurrentTimeDot, topOffset - 4); // center dot on line
}
```

Use a `DispatcherTimer` with a 60-second interval to call `UpdateCurrentTimeLine()`. Initialize the timer and call `UpdateCurrentTimeLine()` on view load.

The red line and dot should only be visible in the column matching today's date. In week view, determine today's column index by comparing `DateTime.Now.Date` against the dates of each column.

**XAML pattern for the line:**
```xaml
<!-- Overlay Canvas for current time indicator (one per day column) -->
<Canvas x:Name="CurrentTimeCanvas" IsHitTestVisible="False">
    <Ellipse x:Name="CurrentTimeDot"
             Width="10" Height="10"
             Fill="Red"
             Canvas.Left="-5"/>
    <Line x:Name="CurrentTimeLine"
          X1="0" Y1="0"
          X2="{Binding ActualWidth, ElementName=CurrentTimeCanvas}"
          Y2="0"
          Stroke="Red"
          StrokeThickness="1.5"/>
</Canvas>
```

Note: `IsHitTestVisible="False"` ensures the red line does not intercept pointer events on events underneath it.

### Enhancement: Smooth Side Panel Animation

**Current behavior:** The side panel (details panel from Story 3.4) uses a `SplitView`, `Grid` column, or similar layout. When `IsPanelVisible` changes, the layout resizes instantly, causing a visible jump.

**Fix using `DoubleAnimation` on `Width`:**

```csharp
// In MainPage.xaml.cs or via VisualStateManager
private void AnimatePanelOpen()
{
    var animation = new DoubleAnimation
    {
        From = 0,
        To = 320, // target panel width
        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    Storyboard.SetTarget(animation, DetailsPanelColumn); // Grid ColumnDefinition
    Storyboard.SetTargetProperty(animation, "Width");
    var storyboard = new Storyboard();
    storyboard.Children.Add(animation);
    storyboard.Begin();
}
```

If the panel is inside a `SplitView`, `SplitView` manages its own animation — check if the current `SplitView.OpenPaneLength` animation can be configured, or switch to a `Grid`-based layout for full control.

The key constraint is: **the calendar content column must shrink/grow in sync with the panel**, not jump at the end. If using `Grid`, animating the `ColumnDefinition.Width` achieves this. If using `SplitView`, the pane overlays content and may not require content resizing.

Read the actual `MainPage.xaml` layout before implementing to understand the current panel structure.

### Build & Test Requirements

- `dotnet build -p:Platform=x64` — must pass with 0 errors
- `dotnet test GoogleCalendarManagement.Tests/` — all tests pass
- Manual: create/find an event from 10 PM to 1 AM → navigate to week view → verify two blocks appear in correct columns
- Manual: verify today's date shows blue circle in year/month/week/day views
- Manual: open week/day view for today → verify red line + dot at current time → wait 1+ minute → verify line moves
- Manual: click an event → observe panel opens smoothly → dismiss → observe panel closes smoothly → no jump

---

## Tasks / Subtasks

- [ ] **Task 1: Fix midnight-crossing event rendering in `WeekViewControl`** (AC: 3.14.1, 3.14.2)
  - [ ] Detect events where `EndLocal.Date > StartLocal.Date`
  - [ ] Split into per-day segments; render each segment in the correct column
  - [ ] Both segments trigger selection of the same event on click

- [ ] **Task 2: Fix midnight-crossing event rendering in `DayViewControl`** (AC: 3.14.3)
  - [ ] Clip event to the visible day's 0–24 range
  - [ ] Show correct start/end times for the visible portion

- [ ] **Task 3: Add today's date blue circle highlight to all views** (AC: 3.14.4, 3.14.5, 3.14.6)
  - [ ] Year view: highlight day cell for today
  - [ ] Month view: highlight day number cell for today
  - [ ] Week view: highlight the day column header for today
  - [ ] Day view: highlight the day header for today (if it is today)

- [ ] **Task 4: Add current time red line to `WeekViewControl`** (AC: 3.14.7, 3.14.8, 3.14.9)
  - [ ] Add `Canvas` overlay on today's column
  - [ ] Position red dot + horizontal line at current time offset
  - [ ] `DispatcherTimer` at 60s interval to update position
  - [ ] Hide indicator when today is not in the visible week

- [ ] **Task 5: Add current time red line to `DayViewControl`** (AC: 3.14.7, 3.14.8, 3.14.10)
  - [ ] Same approach; show only when active day = today

- [ ] **Task 6: Smooth side panel open/close animation** (AC: 3.14.11, 3.14.12, 3.14.13)
  - [ ] Read current `MainPage.xaml` panel layout structure
  - [ ] Replace instant visibility toggle with `DoubleAnimation` on panel width
  - [ ] Ensure calendar content area resizes in sync, no jump at end of animation

- [ ] **Task 7: Build and manual verification**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual verification per checklist above

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

# Story 4.10: Overlapping Event Layout — Side-by-Side Columns, Text Zone Duck, and Selection Z-Lift

Status: ready-for-dev

## Story

As a **user**,
I want **overlapping timed events in Week and Day views to lay out deterministically side by side, with smart partial-overlap duck handling and selected events rising to the visual front**,
so that **all concurrent events are always fully legible and interactable without visual chaos or unpredictable flipping**.

## Acceptance Criteria

1. **AC-4.10.1 — Deterministic sort order:** Given two events with identical `StartLocal` and `EndLocal` on the same day, when the layout is built, then their relative order is resolved by `EventId` (ascending, `StringComparison.Ordinal`) as the final tiebreaker. The layout never flip-flops between runs for the same input.

2. **AC-4.10.2 — Two fully concurrent events split side by side:** Given two events whose visible ranges fully overlap (each starts before the other ends), when the layout is built, then each event receives a rendered width of approximately `dayColumnWidth / 2 - EventSideMargin`, placed left-column and right-column respectively.

3. **AC-4.10.3 — N fully concurrent events split into N equal columns:** Given N events that all mutually overlap (every pair shares a common time window), when the layout is built, then each event receives a rendered width of approximately `dayColumnWidth / N - EventSideMargin` and is placed in columns 0 through N-1 left-to-right.

4. **AC-4.10.4 — Partial overlap duck rule (text-free tail):** Given a non-compact event A (duration ≥ 45 min) and an event B where B's `visibleStart ≥ A.visibleStart + A's estimated text-zone duration`, when the layout is built, then event A's rendered width = `dayColumnWidth × 0.80 - EventSideMargin` (from the left edge), and event B's left = `dayColumnStart + dayColumnWidth × 0.20`, width = `dayColumnWidth × 0.80 - EventSideMargin`. The two events share the 20–80% zone.

5. **AC-4.10.5 — Duck rule sets UseOverlapOutline on overlapper only:** In a duck-rule pair, event B (the overlapper) has `UseOverlapOutline = true`; event A (ducking left) has `UseOverlapOutline = false`.

6. **AC-4.10.6 — Duck rule does not apply to compact events:** Given event A has `durationMinutes < 45` (compact), the duck rule cannot apply — A's full height is its text zone, so it has no text-free tail. Compact events always receive the equal-split column geometry.

7. **AC-4.10.7 — ZIndex assigned by column index:** The `WeekTimedEventLayoutItem.ZIndex` property (new, default `0`) is set to `columnIndex` so that later-column events render on top of earlier-column events in any overlapping region. Duck-rule event B always receives `ZIndex = 1`, duck-rule event A receives `ZIndex = 0`.

8. **AC-4.10.8 — Selected event jumps to the visual front:** Given a timed event is tapped/selected in Week or Day view, `Canvas.SetZIndex` is called with value `100` on all `Border` elements registered for that event, so it renders above all overlapping neighbors. When selection is cleared, `Canvas.SetZIndex` is reset to the `DefaultZIndex` stored in `EventBorderRegistration`.

9. **AC-4.10.9 — Day view uses the same column algorithm:** Given overlapping timed events in `DayViewControl`, the same column-assignment logic produces per-event `(left, width, ZIndex)` values that match the Week view's column behavior. The duck rule applies identically.

10. **AC-4.10.10 — Single non-overlapping events are unaffected:** A lone event receives the same `Left` and `Width` as before the algorithm change (full column, minus `EventSideMargin` on both sides).

11. **AC-4.10.11 — Existing tests updated; new tests pass:**
    - `Build_OffsetsAndOutlinesOverlappingEvents` is updated to assert the new side-by-side geometry (first event: left half, no overlap outline; second event: right half, overlap outline).
    - New tests: three mutually concurrent events produce equal-thirds layout; duck rule fires for partial overlap in text-free zone; duck rule does not fire when overlap covers the text zone; deterministic sort with identical start/end resolved by EventId; single event unaffected.

## Scope Boundaries

**In scope:**
- `WeekTimedEventProjectionBuilder` algorithm overhaul (column-based, duck rule, deterministic sort)
- New `Services/TimedEventColumnAssigner.cs` shared helper extracted for use by both Week builder and Day view
- `WeekTimedEventLayoutItem` — add `ZIndex = 0` named parameter
- `WeekViewControl` — set `Canvas.ZIndex` in `ConfigureTimedEventBorder`; update `ApplySelectionState` / `ApplySelectionVisualState` to set/restore ZIndex; add `DefaultZIndex` to `EventBorderRegistration`
- `DayViewControl` — compute per-event column geometry using `TimedEventColumnAssigner`; apply computed `left`, `width`, and `ZIndex`; update `ApplySelectionState` / `ApplySelectionVisualState` to set/restore ZIndex; add `DefaultZIndex` to `EventBorderRegistration`
- Unit tests for `WeekTimedEventProjectionBuilder` (update + add)

**Out of scope:**
- Month view event chips (no ZIndex change; chips don't overlap in the same way)
- Year view
- All-day event strips
- Recurring event instance awareness

## Dev Notes

### Current Repo Truth

- Algorithm lives in [Services/WeekTimedEventProjectionBuilder.cs](Services/WeekTimedEventProjectionBuilder.cs). The current `BuildTimedEventSegments` method tracks `activeSegments` (a list of `DateTime` end-times) and assigns an `OverlapDepth` that drives a cascading indent (`Left += OverlapDepth * OverlapIndent`). This is the source of both the flip-flop (no stable sort tiebreaker) and the poor visual result.
- Layout item is [Models/WeekTimedEventLayoutItem.cs](Models/WeekTimedEventLayoutItem.cs) — an immutable `record`. Adding `int ZIndex = 0` as a trailing optional property is backward-compatible with all call sites.
- `WeekViewControl.EventBorderRegistration` at line 1441 of [Views/WeekViewControl.xaml.cs](Views/WeekViewControl.xaml.cs) currently stores `(Border, DefaultBorderBrush, DefaultBorderThickness, DefaultPadding)`. Add `int DefaultZIndex` as a fifth field.
- `DayViewControl.EventBorderRegistration` at line 1155 of [Views/DayViewControl.xaml.cs](Views/DayViewControl.xaml.cs) — same shape as Week's; add `int DefaultZIndex` there too.
- `DayViewControl` renders timed events in a `foreach` loop, placing each `Border` into `DayGrid` (column 1) with `Margin = new Thickness(4, topOffset, 4, 0)` and no explicit `Width`. After this story, the border must get `HorizontalAlignment = HorizontalAlignment.Left`, explicit `Width`, and `Margin = new Thickness(computedLeft, topOffset, 0, 0)`.
- Existing test: `Build_OffsetsAndOutlinesOverlappingEvents` at [GoogleCalendarManagement.Tests/Unit/Services/WeekTimedEventProjectionBuilderTests.cs](GoogleCalendarManagement.Tests/Unit/Services/WeekTimedEventProjectionBuilderTests.cs:63) currently asserts indent-cascade geometry (`second.Left ≈ 218`, `second.Width ≈ 102`). These values change with the new side-by-side layout and **must** be updated.

### Algorithm Design

#### New file: `Services/TimedEventColumnAssigner.cs`

Extract column assignment into a shared static class so `WeekTimedEventProjectionBuilder` and `DayViewControl` both consume it. The type can be `internal static` and live in the `Services` namespace.

```csharp
internal static class TimedEventColumnAssigner
{
    // Input tuple: stable-sorted events for one day
    // Returns: parallel array of per-event layout assignments
    public static EventColumnAssignment[] Assign(ReadOnlySpan<EventColumnInput> events) { ... }

    public sealed record EventColumnInput(
        string EventId,
        DateTime VisibleStart,
        DateTime VisibleEnd,
        bool IsCompact,
        int MaxTitleLines);

    public sealed record EventColumnAssignment(
        int ColumnIndex,
        int ColumnCount,
        bool UseDuckRule,
        bool IsDuckOverlapper); // true = B (right side), false = A (left side or no duck)
}
```

#### Column assignment algorithm (per-day events, pre-sorted by `(visibleStart, visibleEnd, eventId)`)

1. **Greedy coloring**: maintain a `List<DateTime> columnEndTimes`.  
   For each event, find the first column `i` where `columnEndTimes[i] <= event.visibleStart`.  
   If none, append a new column.  
   Assign `columnIndex = i`; set `columnEndTimes[i] = event.visibleEnd`.

2. **Column count per cluster**: after greedy coloring, `columnCount` for an event = `columnEndTimes.Count` at the time the event is finalized.  
   — Actually simpler: run a pass over all events and, for each event, `columnCount = total columns used by the cluster containing that event`.  
   — Cluster = set of events connected by pairwise overlap. Find clusters by union-find or a sweep.

3. **Duck rule check** (only for clusters with exactly 2 events, or any cluster pair where one event in col=0 and one in col=1 have a partial-overlap relationship):
   ```
   textZoneMinutes(A) = (6 + A.MaxTitleLines * 18 + 14) / 72.0 * 60.0
   // 6 = StandardTopPadding px, 18 = title line height px, 14 = time line height px, 72 = RowHeight px/hour
   ```
   If `A.IsCompact == false` AND `B.visibleStart >= A.visibleStart + textZoneMinutes(A)`:
   → mark A as `UseDuckRule=true, IsDuckOverlapper=false` and B as `UseDuckRule=true, IsDuckOverlapper=true`.

#### Geometry in WeekTimedEventProjectionBuilder

Replace the existing `OverlapDepth`-based `left`/`width` computation in `CreateLayoutItem` with:

```
columnWidth = dayColumnWidth / columnCount
baseLeft = dayColumnStart + columnIndex * columnWidth

if (useDuck && isDuckOverlapper):
    left = dayColumnStart + dayColumnWidth * 0.20 + EventSideMargin
    width = dayColumnWidth * 0.80 - 2 * EventSideMargin
    ZIndex = 1
elif (useDuck && !isDuckOverlapper):
    left = dayColumnStart + EventSideMargin
    width = dayColumnWidth * 0.80 - 2 * EventSideMargin
    ZIndex = 0
else:
    left = baseLeft + EventSideMargin
    width = columnWidth - 2 * EventSideMargin
    ZIndex = columnIndex
```

Where `dayColumnStart = WeekGridHorizontalPadding / 2 + TimeColumnWidth + dayOffset * dayColumnWidth`.

**Output ordering**: emit events within a day ordered by `ZIndex ascending` so the `ItemsRepeater` adds lower-ZIndex elements to the DOM first (they paint first; higher-ZIndex elements paint over them).

#### Changes to `WeekViewControl`

In `ConfigureTimedEventBorder`:
```csharp
Canvas.SetZIndex(border, item.ZIndex);
```

In `RegisterEventBorder`:
```csharp
registrations.Add(new EventBorderRegistration(
    border,
    border.BorderBrush,
    border.BorderThickness,
    border.Padding,
    Canvas.GetZIndex(border)));   // capture DefaultZIndex
```

Update `EventBorderRegistration` record to add `int DefaultZIndex`.

In `ApplySelectionState`:
```csharp
Canvas.SetZIndex(border, isSelected ? 100 : registration.DefaultZIndex);
```

#### Changes to `DayViewControl`

Before the `foreach` loop that renders timed events, collect the timed events, sort them by `(StartLocal, EndLocal, EventId)`, call `TimedEventColumnAssigner.Assign(...)`, then store assignments in a dictionary keyed by `EventId`.

Inside the loop, after computing `topOffset` and `eventHeight`, additionally compute:
```csharp
var assignment = assignments[item.EventId];
var availableWidth = DayGrid.ActualWidth - TimeFocusedViewLayoutMetrics.TimeColumnWidth - 8; // subtract side padding
var columnWidth = availableWidth / assignment.ColumnCount;

double eventLeft, eventWidth;
if (assignment.UseDuckRule && assignment.IsDuckOverlapper)
{
    eventLeft = availableWidth * 0.20 + EventSideMargin;
    eventWidth = availableWidth * 0.80 - 2 * EventSideMargin;
}
else if (assignment.UseDuckRule)
{
    eventLeft = EventSideMargin;
    eventWidth = availableWidth * 0.80 - 2 * EventSideMargin;
}
else
{
    eventLeft = assignment.ColumnIndex * columnWidth + EventSideMargin;
    eventWidth = columnWidth - 2 * EventSideMargin;
}

var defaultZIndex = assignment.ColumnIndex;
```

Then build the border with:
```csharp
Margin = new Thickness(eventLeft, topOffset, 0, 0),
Width = eventWidth,
HorizontalAlignment = HorizontalAlignment.Left,
VerticalAlignment = VerticalAlignment.Top,
```

After adding to `DayGrid`, call:
```csharp
Canvas.SetZIndex(eventBlock, defaultZIndex);
```

Capture `defaultZIndex` into `EventBorderRegistration` and update `ApplySelectionState` to set `Canvas.SetZIndex(border, isSelected ? 100 : registration.DefaultZIndex)`.

> **Note on `DayGrid.ActualWidth`**: at the time of the foreach loop, `ActualWidth` is live. If `DayViewControl` is called before layout, use a `SizeChanged`-triggered rebuild (already exists). The `availableWidth` calculation should match how `DayGrid` allocates column 1 — verify against the XAML column definitions.

### Test Updates

**Existing test to update** (`Build_OffsetsAndOutlinesOverlappingEvents`):

Old assertions (indent cascade):
```csharp
first.UseOverlapOutline.Should().BeFalse();
first.Left.Should().BeApproximately(208, 0.001);
first.Width.Should().BeApproximately(112, 0.001);
second.UseOverlapOutline.Should().BeTrue();
second.Left.Should().BeApproximately(218, 0.001);
second.Width.Should().BeApproximately(102, 0.001);
```

New assertions (side-by-side split, `dayColumnWidth = 120`):
- `first` (evt-1, starts 9:00, ends 10:30) and `second` (evt-2, starts 9:15, ends 10:00) fully overlap.  
  Both get `columnWidth = 60`. First at left half, second at right half.
- Calculate expected `left` and `width` using the same constants as the builder:  
  `WeekGridHorizontalPadding = 24`, `TimeColumnWidth = 72`, `EventSideMargin = 4`, `dayOffset = 1`.
  - `dayColumnStart = 12 + 72 + 1 * 120 = 204`
  - first: `left = 204 + 0 * 60 + 4 = 208`, `width = 60 - 8 = 52`
  - second: `left = 204 + 1 * 60 + 4 = 268`, `width = 52`

**New tests to add:**

```csharp
[Fact]
public void Build_ThreeMutuallyOverlappingEvents_SplitsIntoEqualThirds() { ... }

[Fact]
public void Build_DuckRule_WhenBStartsInTextFreeZoneOfA() { ... }

[Fact]
public void Build_DuckRule_DoesNotFire_WhenBStartsInTextZoneOfA() { ... }

[Fact]
public void Build_DeterministicOrder_WhenIdenticalStartEnd_SortsByEventId() { ... }

[Fact]
public void Build_SingleEvent_FullColumnWidthPreserved() { ... }
```

### Key Constants (already in WeekTimedEventProjectionBuilder)

```csharp
private const double TimeColumnWidth = 72;
private const double WeekGridHorizontalPadding = 24.0;
private const double RowHeight = 72.0;
private const double EventSideMargin = 4.0;
private const double MinimumEventHeight = 15.0;
private const double StandardTopPadding = 6.0;
private const double ShortEventContentHeightEstimate = 16.0;
```

Add two new constants for duck rule text-zone calculation:
```csharp
private const double TitleLineHeight = 18.0;
private const double TimeLineHeight = 14.0;
```

### DayView EventSideMargin

DayViewControl currently uses hardcoded margin of `4` (`new Thickness(4, topOffset, 4, 0)`). When switching to explicit `Width`/`HorizontalAlignment`, use the same `EventSideMargin = 4.0` constant.

### Regression Risk

The largest regression risk is the existing `WeekTimedEventProjectionBuilderTests.cs` — the overlap test must be updated, and all four "single event" tests (`Build_PlacesTimedEventInExpectedColumnAndVerticalSlot`, `Build_CompactsShortEventsIntoSingleSummaryLine`, `Build_ClipsCrossMidnightEventsPerDayBeforeComputingVerticalPlacement`, `Build_PreservesLargeWeeklyEventSets`) must still pass.

For `Build_PreservesLargeWeeklyEventSets`: this test creates 210 events at various staggered times. It only checks `items.Count == 210`, unique EventIds, and `Width > 0`. These invariants hold under the new algorithm as long as `columnWidth / N - margins > 0` (which holds for any reasonable `dayColumnWidth`).

The `DayViewControl` changes are mostly isolated (no unit test coverage currently exists for DayView event rendering). Manual verification is required that column width behaves correctly at different window sizes.

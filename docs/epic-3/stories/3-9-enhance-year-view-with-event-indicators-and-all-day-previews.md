# Story 3.9: Enhance Year View with Event Indicators and All-Day Previews

Status: review

## Story

As a **calendar user**,
I want **the year view to reserve a consistent two-bar preview area for all-day events while keeping the sync-status dot and date number aligned cleanly**,
So that **I can scan the whole year for meaningful dates without clutter from timed events or inconsistent day-cell layouts**.

## Acceptance Criteria

1. **AC-3.9.1 — Each day cell reserves exactly two preview bars:** Given the year view is displayed, every valid in-month day cell reserves space directly below the date-number row for exactly 2 horizontal preview rows. The layout does not expand to add a third row, and the space remains reserved even when one or both rows are blank.

2. **AC-3.9.2 — Date number stays centered with sync dot on the right:** Given the year view is displayed, the date number remains centered in the day cell, and the existing green/grey sync-status dot is rendered to the right of that centered date number rather than below it. This story changes the dot position only; it does not redefine the sync-status meaning from Story 2.4.

3. **AC-3.9.3 — Timed events are excluded from year-view event rendering:** Given a day contains only timed events, the year-view preview bars remain blank for that day. Timed events do not create colored bars, text labels, or any separate event marker in year view.

4. **AC-3.9.4 — Top bar shows the first single-day all-day event with text:** Given a day contains one or more 1-day all-day events, the first bar shows the color and summary text of the first eligible single-day all-day event for that date. If the day has no single-day all-day events, the first bar remains blank.

5. **AC-3.9.5 — Second row renders one spanning multi-day box:** Given a day contains at least one multi-day all-day event, the second row renders at most 1 multi-day all-day event for that date. The chosen event is drawn as one continuous horizontal box across its covered visible days within the week row, using the event color and summary text for all rendered spans including 2-day events.

6. **AC-3.9.6 — Multi-day rendering priority continues already-started events:** Given multiple overlapping multi-day all-day events compete for the second bar on a date, evaluate days chronologically and prioritize continuing the event that already started rendering on an earlier covered day. An event that has already claimed the second bar keeps that priority until its rendered span ends.

7. **AC-3.9.7 — Longest new multi-day event wins when no carry-forward exists:** Given no multi-day event is already rendering into the current date's second bar, the longest eligible multi-day all-day event covering that date wins the second-bar claim for that date.

8. **AC-3.9.8 — Soft-deleted events remain hidden:** Given a `gcal_event` row has `is_deleted = 1`, it contributes neither preview bars nor bar text in year view.

9. **AC-3.9.9 — Padding dates are hidden:** Given a month grid week includes leading or trailing slots outside the active month, those slots show no date number, sync dot, or event bars from the adjacent month.

10. **AC-3.9.10 — Existing navigation remains unchanged:** Given the user clicks a year-view day cell, the app still navigates to Month view for that month using the same date-navigation behavior established in Story 3.1.

## Scope Boundaries

**IN SCOPE — this story:**
- Year view only
- Replacing the year-view placeholder event treatment with a fixed two-row all-day preview layout
- Positioning the existing green/grey sync-status dot to the right of the centered date number
- Detecting whether a day has single-day all-day events, multi-day all-day events, or neither
- Rendering one single-day all-day bar and one spanning multi-day all-day bar row per week/date position
- Applying deterministic multi-day bar-priority rules across overlapping events
- Hiding adjacent-month padding dates while preserving weekday alignment

**OUT OF SCOPE — do NOT implement:**
- Month, Week, or Day view changes
- Full event details panel behavior (Story 3.4)
- Event selection visuals (Story 3.3)
- Any timed-event rendering in year view
- Additional year-view overflow rows, popups, or tooltips
- Redefining the sync-status rules from Story 2.4
- Color-system work from Story 3.2 beyond whatever placeholder `ColorHex` is already available

## Tasks / Subtasks

- [x] **Task 1: Refactor year-view day-cell projection**
  - [x] Add a year-view-specific day-cell display model or helper that can answer: `SingleDayAllDayBar`, `MultiDayAllDayBar`, and sync-dot placement metadata
  - [x] Ensure the projection uses the existing `MainViewModel.CurrentEvents` data from Story 3.1
  - [x] Ensure deleted events are ignored
  - [x] Ignore timed events completely for year-view bar rendering

- [x] **Task 2: Replace placeholder day-cell layout with fixed two-bar rendering**
  - [x] Update `Views/YearViewControl.xaml.cs` so each day cell has a centered date-number row with the sync dot on the right
  - [x] Reserve exactly 2 horizontal bar rows under the date number for every valid date cell
  - [x] Render the first bar for the chosen single-day all-day event with color and summary text, and leave it blank when none exists
  - [x] Render the second row as a single spanning multi-day box for the chosen multi-day event, with summary text for all rendered spans including 2-day events
  - [x] Hide adjacent-month padding dates while keeping the month grid aligned
  - [x] Preserve the current click-to-month navigation behavior

- [x] **Task 3: Implement deterministic multi-day priority rules**
  - [x] Evaluate multi-day event occupancy chronologically across visible dates
  - [x] Continue rendering an already-started multi-day event before starting a competing one
  - [x] When no event is already in progress for the second bar, choose the longest eligible multi-day all-day event for that date

- [x] **Task 4: Add automated coverage**
  - [x] Add tests for year-view day classification logic
  - [x] Add tests confirming timed events do not create year-view bars
  - [x] Add tests for first-bar blank state when no single-day all-day event exists
  - [x] Add tests for multi-day carry-forward priority and longest-event selection
  - [x] Add tests confirming deleted events do not create indicators
  - [x] Add tests confirming single-day and 2-day events show summary text
  - [x] Add tests confirming multi-day assignments collapse into one spanning box per contiguous week-row segment

- [ ] **Task 5: Validate behavior**
  - [x] `dotnet build -p:Platform=x64`
  - [x] `dotnet test GoogleCalendarManagement.Tests/`
  - [x] Manual: verify each in-month day reserves 2 preview rows, the sync dot sits to the right of the centered date number, timed events do not appear, single-day and multi-day bars show text, multi-day events render as one spanning box per week row, padding dates are hidden, and day boxes stretch edge-to-edge

## Dev Notes

### Existing foundation from Story 3.1

- `Views/YearViewControl.xaml.cs` currently renders a static grey dot for every day cell.
- `MainViewModel.CurrentEvents` already contains the loaded event set for the active year range.
- `CalendarQueryService` already filters soft-deleted events, so year view must not reintroduce them through separate data access.

### Event classification rules for this story

- Treat only non-timed all-day events as eligible for year-view preview bars.
- Treat a single-day all-day event as an all-day event whose visible span covers exactly 1 calendar date.
- Treat a multi-day all-day event as an all-day event whose visible span covers 2 or more calendar dates.
- Timed events may still exist in `CurrentEvents`, but they must not produce any year-view preview output.

### Layout rules

- Each in-month day cell should use a stable vertical structure: date-number row, first bar row, second bar row.
- The date number must remain visually centered even when the sync dot appears to its right.
- The first bar is reserved exclusively for the chosen 1-day all-day event and shows summary text.
- The second bar is reserved exclusively for the chosen multi-day all-day event and should render as a single spanning box across contiguous covered visible dates in the same week row.
- Leading and trailing padding slots should stay blank so adjacent-month dates do not appear in year view.
- Day boxes and preview bars should use a compact 4px corner radius and stretch edge-to-edge within the week row.

### Multi-day priority algorithm

- Compute the second-bar assignment in chronological order across the visible year-view dates.
- If a multi-day event was already assigned to the second bar on the previous covered date and still covers the current date, keep rendering that same event.
- Only when no carried-forward event applies should the renderer choose a new multi-day event for the current date.
- When choosing a new event, prefer the longest eligible multi-day all-day event covering that date.
- After choosing the winning multi-day event per date, collapse contiguous same-event assignments into one spanning box per week row for rendering.

### File locations

- `Views/YearViewControl.xaml`
- `Views/YearViewControl.xaml.cs`
- `Models/` if a small year-view display model is needed
- `GoogleCalendarManagement.Tests/Unit/` for day-classification tests

### Implementation guardrails

- Do not add new database queries inside year-view rendering; use the already loaded viewmodel event collection.
- Do not change the meaning of the Story 2.4 sync-status dot; only reposition it in the year-view cell.
- Keep year-view cells compact; this is a scan-first view, not a full event-details surface.
- Do not render timed events in year view.
- Do not add tooltip behavior or extra overflow UI in year view for this story.
- If multiple single-day all-day events exist on the same date, only the first eligible one claims the first bar.
- If multiple multi-day all-day events exist on the same date, only the priority winner claims the second row.

## References

- `docs/epic-3/stories/3-1-build-year-month-week-day-calendar-views.md`
- `Views/YearViewControl.xaml.cs`
- `ViewModels/MainViewModel.cs`

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Completion Notes List

- Added `YearViewDayProjectionBuilder` plus year-view display models to classify all-day events into fixed single-day and multi-day preview bars while preserving sync-dot metadata.
- Updated `YearViewControl` to render a fixed three-row week structure with centered date headers, edge-to-edge day boxes, blank padding slots, single-day text bars, and spanning multi-day bars.
- Updated year-view interactions so only event banners are clickable, banner taps reuse the existing selection/details-panel flow, date-background taps do nothing, and banner tooltips open after a 100 ms hover delay with the full summary text.
- Added automated coverage for timed-event exclusion, blank single-day bars, multi-day carry-forward priority, longest-event selection, spanning-segment grouping, text rendering rules, and deleted-event exclusion through the query-to-projection path.
- Final regression validation passed with `dotnet build -p:Platform=x64 --no-restore` and `dotnet test GoogleCalendarManagement.Tests/ --no-build --no-restore` (172 passing tests).
- Manual year-view verification was completed in a live app session and confirmed the reserved preview rows, right-aligned sync dot, hidden padding dates, spanning multi-day banners, centered banner text, tooltip delay, and banner-only click behavior.

### File List

- `GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/YearViewDayProjectionBuilderTests.cs`
- `Models/YearViewDayDisplayModel.cs`
- `Services/YearViewDayProjectionBuilder.cs`
- `Views/YearViewControl.xaml.cs`
- `docs/epic-3/stories/3-9-enhance-year-view-with-event-indicators-and-all-day-previews.md`
- `docs/sprint-status.yaml`

### Change Log

- 2026-04-05: Implemented year-view all-day preview projection, spanning multi-day rendering, hidden padding slots, compact 4px radii, banner-based details-panel selection, delayed summary tooltips, and automated coverage for carry-forward priority and deleted/timed-event filtering.
- 2026-04-05: Completed final manual verification and moved Story 3.9 to review.

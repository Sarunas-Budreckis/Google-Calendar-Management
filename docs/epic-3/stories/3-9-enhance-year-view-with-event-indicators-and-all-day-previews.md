# Story 3.9: Enhance Year View with Event Indicators and All-Day Previews

Status: ready-for-dev

## Story

As a **calendar user**,
I want **the year view to show which days contain events and preview all-day events directly in the day cell**,
So that **I can scan the whole year for meaningful dates without opening month view first**.

## Acceptance Criteria

1. **AC-3.9.1 — Empty days show no event marker:** Given the year view is displayed, a day cell with no non-deleted events shows no grey dot, bar, or placeholder indicator.

2. **AC-3.9.2 — Days with timed events show an event indicator:** Given the year view is displayed, a day cell with one or more timed events shows a visible event indicator inside the cell.

3. **AC-3.9.3 — Days with all-day events show an inline preview label:** Given a day contains an all-day event, the day cell shows a compact inline preview label instead of a generic dot-only treatment.

4. **AC-3.9.4 — Tooltip reveals the full all-day summary:** Given the user hovers over the all-day preview label, a tooltip displays the full event summary text for that all-day event.

5. **AC-3.9.5 — Soft-deleted events remain hidden:** Given a `gcal_event` row has `is_deleted = 1`, it contributes neither dots nor all-day preview labels in year view.

6. **AC-3.9.6 — Existing navigation remains unchanged:** Given the user clicks a year-view day cell, the app still navigates to Month view for that month using the same date-navigation behavior established in Story 3.1.

## Scope Boundaries

**IN SCOPE — this story:**
- Year view only
- Replacing the static placeholder dot behavior from Story 3.1 with actual event-aware rendering
- Detecting whether a day has timed events, all-day events, or no events
- Rendering a compact inline all-day label in the day cell
- Tooltip text for the displayed all-day preview label

**OUT OF SCOPE — do NOT implement:**
- Month, Week, or Day view changes
- Full event details panel behavior (Story 3.4)
- Event selection visuals (Story 3.3)
- Multi-event overflow handling beyond the single compact year-view preview label
- Sync-status dots from Story 2.4
- Color-system work from Story 3.2 beyond whatever placeholder `ColorHex` is already available

## Tasks / Subtasks

- [ ] **Task 1: Refactor year-view day-cell projection**
  - [ ] Add a year-view-specific day-cell display model or helper that can answer: `HasTimedEvents`, `HasAllDayEvent`, and `AllDaySummary`
  - [ ] Ensure the projection uses the existing `MainViewModel.CurrentEvents` data from Story 3.1
  - [ ] Ensure deleted events are ignored

- [ ] **Task 2: Replace placeholder dots with event-aware rendering**
  - [ ] Update `Views/YearViewControl.xaml.cs` so empty days render no indicator
  - [ ] Render a visible marker only when the day has one or more timed events
  - [ ] Preserve the current click-to-month navigation behavior

- [ ] **Task 3: Add all-day preview label and tooltip**
  - [ ] Render a compact inline label when a day has an all-day event
  - [ ] Show the all-day event summary in a tooltip on hover
  - [ ] Truncate the inline preview visually if needed, but keep the tooltip text full-length

- [ ] **Task 4: Add automated coverage**
  - [ ] Add tests for year-view day classification logic
  - [ ] Add tests confirming deleted events do not create indicators
  - [ ] Add tests confirming all-day events are identified separately from timed events

- [ ] **Task 5: Validate behavior**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: verify empty cells have no dot, timed-event cells show an indicator, and all-day cells show a preview with tooltip

## Dev Notes

### Existing foundation from Story 3.1

- `Views/YearViewControl.xaml.cs` currently renders a static grey dot for every day cell.
- `MainViewModel.CurrentEvents` already contains the loaded event set for the active year range.
- `CalendarQueryService` already filters soft-deleted events, so year view must not reintroduce them through separate data access.

### File locations

- `Views/YearViewControl.xaml`
- `Views/YearViewControl.xaml.cs`
- `Models/` if a small year-view display model is needed
- `GoogleCalendarManagement.Tests/Unit/` for day-classification tests

### Implementation guardrails

- Do not add new database queries inside year-view rendering; use the already loaded viewmodel event collection.
- Do not couple this story to Story 2.4 sync-status dots.
- Keep year-view cells compact; this is a scan-first view, not a full event-details surface.
- If multiple all-day events exist on the same date, show one preview label only and keep richer multi-event behavior out of scope for this story.

## References

- `docs/epic-3/stories/3-1-build-year-month-week-day-calendar-views.md`
- `Views/YearViewControl.xaml.cs`
- `ViewModels/MainViewModel.cs`

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

### Change Log

<!-- to be filled by dev agent -->

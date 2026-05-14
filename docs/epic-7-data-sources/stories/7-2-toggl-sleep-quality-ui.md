# Story 7.2: Toggl Sleep Quality UI

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (toggl_sleep_quality table), Story 5.7 (Toggl Sleep card & drilldown)

---

## User Story

As a **user**,
I want **to record a sleep quality rating (0–10) for any day that has a sleep entry**,
so that **the rating is reflected in the sleep event title on my calendar**.

---

## Background

The `toggl_sleep_quality` table (added in 7.1) holds a per-day quality integer. This story adds the UI to enter that value in the Toggl Sleep drilldown and wires up the side effect: when a quality is set, the sleep event for that day (if it exists as a `pending_event` or `gcal_event`) has its title updated to `"Sleep – X/10"`.

---

## Acceptance Criteria

**Given** I am in the Toggl Sleep drilldown for a day that has a sleep entry
**When** I view the drilldown
**Then** I see a quality input control — a numeric field or 0–10 segmented picker — currently showing the saved value (or empty if not yet set)

**When** I enter a value between 0 and 10 and confirm
**Then** the value is saved to `toggl_sleep_quality` for that date

**And** the title of the sleep calendar event for that day is updated:
- If a `pending_event` for that day with title starting `"Sleep"` exists → title becomes `"Sleep – X/10"`
- If the sleep entry has a linked `gcal_event` (via `linked_event_id` in `toggl_data`) → the `gcal_event` title is updated locally; the change is marked as a local edit (requires re-push to reflect in Google Calendar)
- If no calendar event exists for the sleep entry yet → quality is saved but no event title is updated until an event is created

**And** if I clear the quality field
**Then** the value in `toggl_sleep_quality` is set to null and the event title reverts to `"Sleep"`

**And** if the day has no sleep entry
**Then** the quality input is not shown in the drilldown

**And** the compact card for Toggl Sleep in day mode shows the quality badge if set (e.g., "7/10" appended to the summary line)

---

## Technical Notes

- Quality input: consider a horizontal row of 11 buttons (0–10) or a `NumberBox` clamped to 0–10; the latter is simpler
- Title update should use the existing event edit service (same code path as manual title edits in Story 4.1)
- The "revert to Sleep" on clear should strip only the ` – X/10` suffix, not the whole title
- If multiple sleep entries exist for a day, update the title of the one whose start time matches the primary entry (or the one linked via `linked_event_id`)
- This story does not push changes to GCal — that follows the normal pending event push flow
- Add an integration test: setting quality 7 on a day with a pending sleep event → event title becomes "Sleep – 7/10"

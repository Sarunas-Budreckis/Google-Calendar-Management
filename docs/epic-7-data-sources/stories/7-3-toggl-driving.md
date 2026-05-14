# Story 7.3: Toggl Driving (Transit)

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (toggl_data_type column), Story 5.6 (Toggl import), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to see my driving sessions from Toggl in the left panel**,
so that **I can create calendar events for transit time with one click**.

---

## Background

An iOS Shortcuts automation creates Toggl entries whenever the phone connects to the car's Bluetooth. These entries have `description = "Driving"` and `project = "Transit"`. They are already imported into `toggl_data` by the Toggl import (Story 5.6). This story adds the classification, compact card, drilldown, and candidate event generation for these entries.

---

## Acceptance Criteria

**Classification (runs during or after Toggl import):**

**Given** the Toggl import has run
**When** classification runs
**Then** all `toggl_data` rows where `project = "Transit"` (case-insensitive) have `toggl_data_type = "toggl_transit"`

**Compact Card (in left panel day mode):**

**Given** a day is selected and has transit entries
**When** the Toggl – Driving card is shown
**Then** the card displays:
- Total driving time for the day (sum of all transit entry durations)
- Number of trips
- If no transit entries for the day: "No driving data" and the integration checkbox is greyed

**Drilldown View:**

**Given** I expand the Toggl – Driving source
**When** the drilldown opens for the selected day
**Then** I see a list of individual driving sessions: start time, end time, duration (in chronological order)

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation:**

**Given** I click "Create Candidate Events"
**When** the algorithm runs
**Then** the 8/15 rounding rule is applied to each driving session independently:
1. For each transit entry, divide the duration into 15-minute blocks
2. Keep blocks with ≥8 minutes of activity
3. Always keep at least 1 block (the end-time block)
4. Each resulting block becomes a separate `pending_event` with:
   - Color: Lavender
   - Title: "Driving"
   - Start/end rounded per 8/15 rule
   - `linked_event_id` and `linked_event_type` left null until pushed

**And** the created events are visible in the calendar immediately

**And** each created `pending_event` is selected in the right panel (if only one was created) or the first is selected (if multiple)

**And** the contributing `toggl_data` rows have their `linked_event_id` updated when the event is pushed to GCal

---

## Technical Notes

- Transit classification runs as a post-import step, not in real-time; re-running is idempotent
- The 8/15 rule implementation should be shared service code (reused by driving, phone, Civ 5, ComfyUI)
- A single driving session (one Toggl entry) typically produces 1–3 events; no coalescing across sessions (each trip is independent)
- Index `toggl_data` on `(toggl_data_type, start_time)` for efficient per-day queries
- Compact card `supports_no_data_hint = true` — grey the checkbox when no transit entries exist for the day
- Add unit tests for the 8/15 rule applied to transit entries (edge cases: <8 min trip, exactly 15 min trip, multi-block trip)

# Story 7.4: Toggl Phone

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (toggl_data_type), Story 5.6 (Toggl import), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to see my phone usage sessions from Toggl in the left panel**,
so that **I can create coalesced calendar events for phone time**.

---

## Background

iOS Shortcuts creates Toggl entries for phone activity under several names that have changed over time: "ToDelete", "Phone", "Phone - Reddit", "Phone - Instagram". These entries are short (≤10 minutes each) and frequent — phone activity coalesces into sessions using a sliding window algorithm. This story adds classification, compact card, drilldown with a vertical dot timeline, and candidate event generation.

Note: the classification rules defined here (description matching + ≤10 min filter) are intentionally basic. Story 7.5 adds a UI for specifying per-date-range rules to handle the evolving naming conventions.

---

## Acceptance Criteria

**Classification:**

**Given** the Toggl import has run
**When** phone classification runs
**Then** all `toggl_data` rows meeting ALL of these criteria are tagged `toggl_data_type = "toggl_phone"`:
- `description` is one of: `"ToDelete"`, `"Phone"`, `"Phone - Reddit"`, `"Phone - Instagram"` (case-insensitive)
- Duration ≤ 10 minutes

**And** rows that match the description pattern but are >10 minutes remain unclassified (null), as they likely represent false positives

**Compact Card:**

**Given** a day is selected and has phone entries
**When** the Toggl – Phone card is shown
**Then** the card displays:
- Number of phone entries for the day
- Estimated total screen time (raw sum of entry durations, not coalesced)
- If no phone entries: "No phone data"

**Drilldown View:**

**Given** I expand the Toggl – Phone source
**When** the drilldown opens for the selected day
**Then** I see a 24-hour vertical timeline with a dot at the start time of each phone entry
- Hovering/clicking a dot shows: entry description, start time, end time, duration

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation (Sliding Window + 8/15):**

**Given** I click "Create Candidate Events"
**When** the algorithm runs on that day's `toggl_phone` entries
**Then** the sliding window algorithm applies:
1. Sort entries by start time
2. Start a window at the first entry
3. Extend the window while the next entry starts within 15 minutes of the current window end
4. Quality check: if <50% of the window duration is covered by phone entries, retry with a 5-minute gap threshold instead
5. Discard any window with total duration <5 minutes
6. Apply 8/15 rounding to each window's start and end
7. Create one `pending_event` per window with:
   - Color: Yellow
   - Title: "Phone"
   - Start/end from the 8/15-rounded window

**And** contributing `toggl_data` rows are linked to the event on push

---

## Technical Notes

- Classification is idempotent; re-running after a new import does not re-tag already-tagged rows
- The sliding window service is shared with future sources; parameterize gap threshold and quality threshold
- Vertical dot timeline component: shared with Spotify (7.10) and ComfyUI (7.12) — design as a reusable control
- Story 7.5 will add a date-range rule engine that overrides the hardcoded description list; this story's classification should be refactorable to call that engine
- Unit tests required: sliding window edge cases (single entry, entries with exactly 15-min gap, quality check triggering 5-min retry, <5 min discard)

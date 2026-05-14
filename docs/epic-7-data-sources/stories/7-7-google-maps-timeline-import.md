# Story 7.7: Google Maps Timeline Import

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to import my Google Maps Timeline data and view location history in the left panel**,
so that **I can see where I was on any given day and open it in my local timeline viewer**.

---

## Background

Google Maps Timeline data is exported via Google Takeout as a `timeline.json` file (or `Timeline.json` in newer Takeout formats). This is a large JSON file containing visits, activities, and segments. This story focuses on importing and storing the raw data, with a basic drilldown readout and an integration with the existing open-source timeline viewer at `C:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Maps Viewer\timeline.html`.

**Future note:** Investigate automating the Takeout export using Google account OAuth credentials already in the app (the app already authenticates with Google for GCal). Also investigate embedding the timeline viewer directly in the app (WebView2). Story 7.7 is the foundation; automation and embedding are follow-up stories.

---

## Acceptance Criteria

**Schema:**

`maps_timeline_raw`:
- `id` (integer, PK)
- `imported_at` (datetime)
- `file_name` (text)
- `file_size_bytes` (integer)
- `covered_date_min` (date, nullable) — derived from JSON content
- `covered_date_max` (date, nullable) — derived from JSON content
- `raw_json` (text) — full JSON content

**Import Flow:**

**Given** I click "Import Timeline" in the Google Maps Timeline global mode or drilldown
**When** I select a `timeline.json` (or `Timeline.json`) file via file picker
**Then** the file is read and stored in `maps_timeline_raw` with a new import record

**And** the app extracts the date range from the JSON and populates `covered_date_min` / `covered_date_max`

**And** if a previous import exists, the user is shown a confirmation: "Replace existing timeline data? This will delete the previous import." — only one timeline import is kept at a time

**And** a button "Copy to Viewer & Open" is available after import

**"Copy to Viewer & Open" Button:**

**Given** a timeline has been imported
**When** I click "Copy to Viewer & Open"
**Then** the app copies the raw JSON to:
`C:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Maps Viewer\Timeline.json`
(overwriting if it exists)

**And** the app opens `C:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Maps Viewer\timeline.html` in the default browser using `Process.Start`

**Compact Card:**

**Given** a day is selected and timeline data covers that day
**When** the Google Maps Timeline card is shown
**Then** the card displays: "Timeline available" with the import date

**Given** the selected day is not within the imported date range
**Then** the card shows: "No data for this day"

**Drilldown View:**

**Given** I expand the Google Maps Timeline source for a day
**Then** I see a simple text readout of timeline segments for that day:
- Each visit/activity: location name (if available), start time, end time, activity type
- Segments are filtered to exclude the user's home location (home location TBD — define in app config in a future story; for now show all segments)

**And** the "Copy to Viewer & Open" button is also accessible from the drilldown

**And** no candidate event generation in this story — events are added manually after reviewing the viewer

---

## Technical Notes

- Google Takeout Timeline JSON format has changed over time; the JSON structure for recent exports differs from older ones — parse defensively, log unknown segment types rather than throwing
- `raw_json` column may be very large (tens of MB); consider storing as a file on disk with only metadata in DB if SQLite performance is a concern — note this as a future optimization
- The viewer path (`C:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Maps Viewer\`) should be stored in app config (not hardcoded in business logic), configurable via Settings in a future story
- Home location filtering deferred; note as future story
- Future stories: (1) Automate Takeout export via Google OAuth, (2) Embed `timeline.html` in a WebView2 panel within the app

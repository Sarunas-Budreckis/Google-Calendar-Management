# Story 7.14: Chrome Search History Import

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to import my Google Chrome search history and view it as a vertical dot timeline in the left panel**,
so that **I have a record of what I was searching on any given day**.

---

## Background

Google Chrome search history is stored locally in a SQLite database at `%LOCALAPPDATA%\Google\Chrome\User Data\Default\History`. It can also be exported via Google Takeout (as part of Chrome data) as a JSON file. This story imports the full history (from one of these two sources) and stores it locally. Candidate event generation and query filtering are deferred to future stories.

**Note on data sensitivity:** Search history is potentially sensitive. The data is stored in the local app database (same as all other sources), never uploaded. This is noted explicitly in the story for awareness.

---

## Acceptance Criteria

**Schema:**

`chrome_history_import`:
- `id` (integer, PK)
- `imported_at` (datetime)
- `source` (text) — `"local_db"` or `"takeout_json"`
- `record_count` (integer)
- `date_min` (date)
- `date_max` (date)

`chrome_search_entry`:
- `id` (integer, PK)
- `import_id` (FK → chrome_history_import)
- `visited_at` (datetime)
- `url` (text)
- `title` (text, nullable)
- `visit_count` (integer, default 1)

**Import — Local Chrome DB Path:**

**Given** I click "Import from Chrome" and select "Local Database"
**When** Chrome is not currently open (or the History file is not locked)
**Then** the app reads `%LOCALAPPDATA%\Google\Chrome\User Data\Default\History` (SQLite)

**And** queries the `urls` and `visits` tables to extract: `url`, `title`, `last_visit_time`, `visit_count`

**And** entries are upserted into `chrome_search_entry` (deduplicate on `visited_at` + `url`)

**And** if the Chrome History file is locked (Chrome is open), the app shows a message: "Close Chrome before importing, or use the Takeout JSON option."

**Import — Google Takeout JSON:**

**Given** I click "Import from Chrome" and select "Takeout JSON"
**When** I select the Takeout `BrowserHistory.json` file
**Then** entries are parsed from the JSON array (fields: `time_usec`, `url`, `title`, `page_transition`)

**And** entries are upserted the same way as the local DB path

**Compact Card:**

**Given** a day is selected with search history
**When** the Chrome History card is shown
**Then** the card displays:
- Number of URLs visited for the day
- If no history: "No browsing history for this day"

**Drilldown View:**

**Given** I expand the Chrome History source for a selected day
**Then** I see a 24-hour vertical dot timeline with a dot at each `visited_at` timestamp

**And** hovering/clicking a dot shows: URL, page title, time

**And** no "Create Candidate Events" button in this story — future stories will add filtering and event generation

---

## Technical Notes

- Chrome SQLite `History` file: use `Microsoft.Data.Sqlite` (already a project dependency) to open the file
- Chrome must be closed for the History file to be readable without lock — check before opening; show clear error if locked
- Chrome `last_visit_time` is in microseconds since 1601-01-01 (Windows FILETIME epoch) — convert: `new DateTime(1601, 1, 1).AddMicroseconds(last_visit_time)`
- Takeout `time_usec` uses the same epoch
- Chrome `User Data\Default\` is the default profile; multi-profile support is a future enhancement
- Configurable Chrome profile path in Settings (future)
- This is investigatory in Epic 7 in the sense that candidate event generation is not implemented; the data model and import are the deliverable
- **Privacy note:** Document in the Settings screen that Chrome history is stored locally only and never transmitted
- Future story: filter by domain / search engine queries only; candidate events from search sessions; multi-profile support

# Epic 5: Data Source Left Panel & Single-Day Select

**Author:** Mary (Business Analyst)
**Date:** 2026-05-13
**Status:** Ready for Tech Spec
**Tier:** 3 (foundation layer)
**Concepts reference:** [Epic 8/9 vocabulary & data model](../epic-8-data-linking/concepts.md)

---

## Goal

Introduce the data source management surface — a persistent left panel that gives the user a unified view of all data sources and their per-day coverage status — along with the single-day selection model that drives it. Deliver the first working end-to-end data source (Toggl Track Sleep) so the infrastructure is exercisable before broader Tier 3 work begins.

---

## Background

Tier 3 introduces multiple external data sources (Toggl, YouTube, call logs, and many more). Without a workflow surface, those sources would be data in tables with no coherent UI for managing the backfilling ritual. This epic builds that surface. It does not implement the full source library — it establishes the extensible registry, the coverage tracking model, and the left panel UI, then validates them with one real source.

---

## Key Concepts

### Day States (independent, not a linear progression)
Each calendar day carries several independent flags:
- **Named** — has an all-day name event attached
- **Covered (per source)** — user has manually confirmed they have addressed this data source for this day
- **Approved** — user's single binary sign-off on the day (set independently of any other flag)

These are not a pipeline. A day can be approved without being named or covered. A day can be covered for one source without being approved. The left panel surfaces all of these without implying sequence.

### "Covered" Definition
Covered means: *I have consciously looked at this data source for this day and handled it* (whether that meant creating events, confirming nothing was relevant, or skipping deliberately). In Epic 5 this is set manually by the user; Epic 8 replaces the manual checkbox with computed coverage from datapoint links. Some sources may opt in to greying out the checkbox when they can confirm no data exists for that day — but the default Epic 5 behavior is an empty, checkable checkbox.

### Data Source Registry
All data sources (present and future) are registered in a central `data_source` table. This drives the left panel source list and coverage tracking. Adding a new data source means adding a row to this table — no schema changes to `date_state` or the legacy `date_source_integration` table.

---

## In Scope

### Infrastructure
- `data_source` registry table: id, display name, description, color/icon hint, supports_no_data_hint (boolean)
- Legacy `date_source_integration` junction table: date + source_id + integrated (boolean) + integrated_at
- Import log (enhance or replace `data_source_refresh`): records each import run, source, covered start/end dates, record count, success, timestamp

### Layout
- **Three-panel layout**: Left Panel | Calendar | Right Panel (event details)
- Left panel is always visible at startup; user can minimize it
- When minimized: fully hidden, with a small open-arrow tab protruding at the top left edge to restore it
- Right panel behavior unchanged from Epic 4

### Single-Day Select
- Clicking the day number (date header) in any calendar view selects that day for the left panel
- Day view: automatically selects the currently viewed day; this is the "active" selection while in day view
- Returning from day view to month/week/year: restores the last manually selected day (not the day-view day)
- Selected day has a distinct visual treatment (separate from event selection — both can be active simultaneously)
- Selected day state is persisted across app restarts

### Left Panel — No Day Selected (Global Mode)
- Lists all registered data sources
- Each source shows: display name + last-covered-date (most recent date covered by any import) + last-import-ran timestamp
- Empty state when registry is empty: "No data sources configured"
- Updates immediately after any import completes

### Left Panel — Day Selected (Day Mode)
- Header: shows selected date + day name (if named). If no name event exists, shows the date only
- Clicking the name (or the date if unnamed) opens the right panel to the all-day name event for that day; if none exists, opens a new pending event pre-configured as an all-day event on that date
- Source list below header: one compact card per registered source
  - Each card shows: source name, coverage checkbox (checked/unchecked/greyed), and the source's own compact day summary (source-defined)
  - Coverage checkbox is manually toggled by the user
  - If a source opts in to no-data hinting and has no data for this day, the checkbox is visually greyed and non-interactive (but not hidden)
  - Each card has an **Expand** button (chevron) that enters the source's drilldown view

### Left Panel — Drilldown View (per source)
- Replaces the source list within the left panel
- Back arrow returns to the source list
- Content is entirely source-defined — each source implements its own drilldown independently
- Standard contract: drilldown receives the currently selected date and renders whatever is appropriate for that source
- May include action buttons (e.g., "Create Candidate Event (autogenerated)")

### Dual-Select Model
- A day can be selected (left panel context) and an event can be selected (right panel context) simultaneously
- These are independent selections; neither clears the other
- This supports the core workflow: left panel shows source data for the day, right panel shows the event being edited, user can reference both simultaneously

### Toggl Track Sleep — First Data Source
**Import:**
- Toggl Track API integration (OAuth or API token)
- User specifies a date range to import
- Fetches entries where description contains "sleep" or "Sleep"
- Stores in `toggl_data` table (existing schema)
- Records import run in import log (source, covered dates, count, timestamp)

**Compact Card (in source list):**
- Shows sleep entry summary for the selected day: start time, end time, duration
- If no sleep entry for the day: shows "No sleep data" (and greys the coverage checkbox)

**Drilldown View:**
- Full sleep entry details for the selected day
- If multiple sleep entries: lists all
- **"Create Candidate Event (autogenerated)" button**: creates a `pending_event` from the sleep data (pre-populated with start/end time, a default title like "Sleep"), selects it in the right panel, and optionally checks the coverage checkbox

---

## Out of Scope

- Multi-day select or batch operations (Epic 6)
- Left panel behavior in week/multi-day context (future epic)
- Any data source other than Toggl Sleep
- Data source configuration/settings UI (future)
- Auto-accept all events for a trusted source (future)
- Source-level bulk actions (future)
- Day Approval UI (the `approved` flag; UI surface deferred to Epic 6 batch actions or a separate story)

---

## Data Model Implications

| Table | Change | Notes |
|-------|--------|-------|
| `data_source` | NEW | Registry of all data sources |
| `date_source_integration` | NEW | Legacy manual coverage checkbox: date + source_id + integrated |
| `data_source_import_log` | NEW (or enhance `data_source_refresh`) | Import run history with date coverage |
| `toggl_data` | EXISTING | Already in schema; used as-is |
| `pending_event` | EXISTING | Used for candidate event (autogenerated) creation |
| `date_state` | NO CHANGE | Per-source flags deferred to source-implementation epics |

---

## Story Candidates

| Story | Title | Notes |
|-------|-------|-------|
| 5.1 | Data Source Infrastructure | `data_source` registry, `date_source_integration` junction table, import log schema, migrations |
| 5.2 | Three-Panel Layout & Left Panel Shell | Add left panel to layout; minimizable with arrow tab; empty state |
| 5.3 | Single-Day Select | Click day number to select; day-view auto-select; state persistence; visual indicator |
| 5.4 | Left Panel Global Mode | Source list with last-covered-date and last-import timestamp; empty state |
| 5.5 | Left Panel Day Mode | Date/name header; per-source coverage checkboxes; expand/drilldown navigation |
| 5.6 | Toggl Sleep Import | Toggl API integration; sleep entry ingestion; import log update |
| 5.7 | Toggl Sleep Card & Drilldown | Compact card; drilldown view; Create Candidate Event (autogenerated) button |

---

## Dependencies

- **Epic 4 complete** (event creation, editing, pending_event table, Push to GCal, event deletion)
- Toggl API credentials available for development (API token acceptable for MVP)

---

## Success Criteria

- Left panel is visible and minimizable in the three-panel layout
- Clicking a day number in any view selects that day and updates the left panel
- Day view auto-selects the viewed day; returning to other views restores the prior selection
- Left panel shows Toggl Sleep data source in global mode with last-covered-date
- With a day selected, left panel shows whether Toggl Sleep has been covered for that day
- Sleep entry for the selected day appears in the compact card
- "Create Candidate Event (autogenerated)" generates a pending event visible in the calendar
- Expanding Toggl Sleep shows the drilldown view; back arrow returns to source list
- Coverage checkbox can be manually toggled; state persists
- Clicking the day name opens/creates the all-day name event in the right panel
- Day selection and event selection can coexist (both panels active simultaneously)

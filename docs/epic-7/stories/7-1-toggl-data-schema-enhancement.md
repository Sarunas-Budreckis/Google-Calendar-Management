# Story 7.1: Toggl Data Schema Enhancement

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 5.1 (data source infrastructure), Story 5.6 (Toggl Sleep import)

---

## User Story

As a **developer**,
I want **the `toggl_data` table to carry a type tag and an event linkage field**,
so that **Toggl entries can be classified by source role and traced back to the calendar events they generated**.

---

## Background

Epic 5 introduced `toggl_data` as a flat store for all Toggl entries. Epic 7 introduces multiple Toggl-based sources (Sleep, Driving/Transit, Phone). To differentiate them in queries, compact cards, and drilldowns, each row needs a `toggl_data_type` discriminator. When a candidate event derived from Toggl data is accepted and pushed to Google Calendar, the contributing rows should be back-linked to that event.

A separate `toggl_sleep_quality` table is introduced here so that the Sleep UI story (7.2) has a schema to write to.

---

## Acceptance Criteria

**Given** the existing `toggl_data` table and migration system
**When** this story's migration is applied
**Then** the following schema changes are in place:

**`toggl_data` additions:**
- `toggl_data_type` (TEXT, nullable) — allowed values: `toggl_sleep`, `toggl_transit`, `toggl_phone`, null (unclassified)
- `linked_event_id` (TEXT, nullable) — `gcal_event.google_event_id` after the derived event is pushed
- `linked_event_type` (TEXT, nullable) — discriminator string; currently always `"gcal_event"` when populated

**New table `toggl_sleep_quality`:**
- `date` (DATE, primary key)
- `quality` (INTEGER, nullable, constraint: 0–10)
- `updated_at` (DATETIME, not null)

**Backfill:**
- All existing `toggl_data` rows where the entry description is `"sleep"` or `"Sleep"` (case-insensitive) have `toggl_data_type` set to `toggl_sleep`
- All other existing rows remain null

**And** the `data_source` table is seeded with entries for all Epic 7 sources that don't already exist:
- `toggl_transit` (display name: "Toggl – Driving", supports_no_data_hint: true)
- `toggl_phone` (display name: "Toggl – Phone", supports_no_data_hint: true)
- `call_log` (display name: "iOS Call Log", supports_no_data_hint: false)
- `maps_timeline` (display name: "Google Maps Timeline", supports_no_data_hint: false)
- `outlook_calendar` (display name: "Work Calendar (Outlook)", supports_no_data_hint: false)
- `youtube` (display name: "YouTube Watch History", supports_no_data_hint: false)
- `spotify` (display name: "Spotify (stats.fm)", supports_no_data_hint: false)
- `civ5` (display name: "Civilization 5", supports_no_data_hint: false)
- `comfyui` (display name: "ComfyUI", supports_no_data_hint: false)
- `voice_memos` (display name: "Voice Memos", supports_no_data_hint: false)
- `chrome_history` (display name: "Chrome Search History", supports_no_data_hint: false)

**And** no existing data is modified beyond the described backfill.

**And** the migration is reversible (down migration removes the new columns and table).

---

## Technical Notes

- Use EF Core migration; add columns as nullable to avoid breaking existing rows
- `toggl_data_type` should be an EF-mapped string property; consider a C# enum with string conversion for type safety
- `linked_event_id` + `linked_event_type` pattern is reused across all source tables in Epic 7 — establish the convention here
- The `toggl_sleep_quality` table is written by Story 7.2 and read by Story 5.7 (drilldown). No UI is wired in this story
- Data source registry seeding: use `HasData` in `OnModelCreating` or a one-time migration seed; avoid re-inserting if already present (upsert on `source_key`)
- Index `toggl_data.toggl_data_type` for efficient per-type queries

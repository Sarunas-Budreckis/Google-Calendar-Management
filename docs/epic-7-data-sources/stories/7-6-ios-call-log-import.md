# Story 7.6: iOS Call Log Import (iMazing)

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to import my iPhone call log from an iMazing CSV export and view calls in the left panel**,
so that **I can create calendar events for significant phone calls**.

---

## Background

iMazing exports call logs as CSV with the columns: Call Type, Date, Duration, Number, Contact, Location (nullable), Service. The existing `iPhoneCallLogParser` in the DepartmentOfTimeManagement project (C# / CsvHelper) demonstrates the parsing logic and can be referenced or ported.

**Future note:** A Mac app that scrapes call log data directly from iCloud backup may be a viable path to automate this import. The story should note this as a future investigation and design the import in a way that a new import source (instead of iMazing CSV) could slot in without schema changes.

---

## Acceptance Criteria

**Schema:**

**Given** the migration runs
**Then** the following tables exist:

`call_log_import`:
- `id` (integer, PK)
- `imported_at` (datetime)
- `file_name` (text)
- `record_count` (integer)
- `date_min` (date) — earliest call date in this import
- `date_max` (date) — latest call date in this import

`call_log_entry`:
- `id` (integer, PK)
- `import_id` (FK → call_log_import)
- `call_type` (text) — e.g., "Incoming", "Outgoing", "Missed"
- `date` (datetime) — call start time
- `duration` (integer) — seconds
- `number` (text, nullable)
- `contact` (text, nullable)
- `location` (text, nullable)
- `service` (text) — e.g., "iPhone", "FaceTime"
- `linked_event_id` (text, nullable)
- `linked_event_type` (text, nullable)

**Import Flow:**

**Given** I click "Import Call Log" in the iOS Call Log drilldown or global mode
**When** I select an iMazing CSV file via file picker
**Then** the file is parsed using CsvHelper (same library and column mapping as `iPhoneCallLogParser`)

**And** duplicate detection runs: entries with the same `date` + `number` + `duration` that already exist in `call_log_entry` are skipped (not re-imported)

**And** a `call_log_import` record is created with the count of new rows inserted

**And** the import is logged to `data_source_import_log`

**Compact Card:**

**Given** a day is selected
**When** the iOS Call Log card is shown
**Then** the card displays:
- Total calls for the day (all types)
- Total call duration (seconds → "X hr Y min")
- If no calls: "No call data"

**Drilldown View:**

**Given** I expand the iOS Call Log source
**Then** I see a chronological list of calls for the selected day:
- Call type, contact name (or number if no contact), duration, service

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation:**

**Given** I click "Create Candidate Events"
**Then** only calls with duration ≥ 10 minutes are considered

**And** each qualifying call becomes one `pending_event`:
- Color: Azure
- Title: Contact name if known, otherwise the phone number, otherwise "Phone Call"
- Start time: call `date`
- End time: `date` + `duration`
- No 8/15 rounding (call times are precise)

**And** the call's `linked_event_id` is populated when the event is pushed to GCal

---

## Technical Notes

- Port or reference `iPhoneCallLogParser.cs` from `C:\Users\Sarunas Budreckis\source\repos\DepartmentOfTimeManagement\DepartmentOfTimeManagement\iPhoneCallLogParser.cs`
- CsvHelper is already a known dependency; add NuGet reference if not present
- Duration in iMazing CSV is a `TimeSpan` string (`hh:mm:ss`) — store as integer seconds in DB
- iMazing CSV Date field parses to `DateTime` via `CsvHelper`; ensure timezone handling (assume local time for now, note as future improvement)
- Future automation note: design `ICallLogProvider` interface so iMazing CSV and a future iCloud scraper are interchangeable import sources
- The 10-minute filter for candidate events is at generation time, not import time — all calls are stored regardless of duration

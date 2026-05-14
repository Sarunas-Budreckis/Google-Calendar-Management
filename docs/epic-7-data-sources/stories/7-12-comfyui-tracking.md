# Story 7.12: ComfyUI Tracking

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **the app to scan my ComfyUI output folders and record file timestamps**,
so that **I can see when I was running image generations and create calendar events for those sessions**.

---

## Background

ComfyUI generates image files that are saved to specific output folders on the local machine. By scanning these folders recursively and recording only the file creation and modification timestamps (no filenames, no paths, no content), the app can reconstruct when generation activity occurred. The folder list is configurable in the database — the user can add or remove folders without code changes.

If a configured folder is inaccessible (permissions, network drive offline, etc.), the app shows a user-facing popup rather than silently failing.

---

## Acceptance Criteria

**Schema:**

`comfyui_folder`:
- `id` (integer, PK)
- `folder_path` (text, unique)
- `is_active` (boolean, default true)
- `added_at` (datetime)

`comfyui_scan_point`:
- `id` (integer, PK)
- `scanned_at` (datetime) — when this scan run captured this point
- `file_created_at` (datetime, nullable)
- `file_modified_at` (datetime)
- `linked_event_id` (text, nullable)
- `linked_event_type` (text, nullable)

**Folder Management UI (in Settings > Data Sources > ComfyUI or inline in drilldown):**

**Given** I navigate to ComfyUI folder settings
**Then** I see a list of configured folder paths

**And** I can add a new folder path via a folder picker dialog

**And** I can remove (deactivate) a folder from the list

**Scan Flow:**

**Given** I click "Scan Folders" in the ComfyUI drilldown or global mode
**When** the scan runs
**Then** each active folder in `comfyui_folder` is scanned recursively

**And** for each file found:
- `file_created_at` and `file_modified_at` are read from filesystem metadata
- No filename, path, extension, or content is stored

**And** if any configured folder path cannot be accessed (does not exist, permission denied, network unavailable)
**Then** a popup is shown to the user: "Could not access folder: [path]. Check that it exists and you have read permissions."

**And** the scan continues with remaining accessible folders after dismissing the popup

**And** duplicate detection: points with the same `file_modified_at` that already exist are skipped

**And** the scan is logged to `data_source_import_log`

**Compact Card:**

**Given** a day is selected with scan points
**When** the ComfyUI card is shown
**Then** the card displays:
- Number of generated files for the day (count of scan points)

**Given** no scan points for the day: "No ComfyUI activity"

**Drilldown View:**

**Given** I expand the ComfyUI source for a selected day
**Then** I see a 24-hour vertical timeline with a dot at each `file_modified_at` timestamp

**And** hovering/clicking a dot shows: modified time (and created time if different)

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation (Coalescing):**

**Given** I click "Create Candidate Events"
**Then** the coalescing algorithm runs on that day's scan points:
1. Sort points by `file_modified_at`
2. Apply sliding window: extend window while next point is within 15 minutes of previous
3. Apply 8/15 rounding to each window
4. Create one `pending_event` per window:
   - Color: Navy
   - Title: "ComfyUI"
   - Start/end from rounded window

**And** contributing scan points are linked on push

---

## Technical Notes

- Folder scan: use `Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)` — wrap in try/catch per folder, not per file
- Popup on access failure: use the existing `IContentDialogService` pattern
- `file_created_at` may equal `file_modified_at` on many systems; store both but only use `file_modified_at` for timeline and coalescing
- Deduplication is on `file_modified_at` alone (no path stored); if two files have identical modified times, only one point is stored — this is an acceptable approximation
- The 15-minute coalescing gap: make configurable per `comfyui_folder` row or globally in `data_source`
- This source does not back up or copy files — scan is read-only

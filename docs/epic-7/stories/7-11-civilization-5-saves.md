# Story 7.11: Civilization 5 Saves

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to see my Civilization 5 play sessions in the left panel based on save file timestamps**,
so that **I can create candidate calendar events for gaming time**.

---

## Background

Civilization 5 saves are stored at two locations:
- `C:\Users\Sarunas Budreckis\Documents\My Games\Sid Meier's Civilization 5\Saves\`
- `C:\Users\Sarunas Budreckis\Documents\My Games\Sid Meier's Civilization 5\ModdedSaves\`

Each root contains subfolders indicating game type:
- `single\` — single-player games (confirmed present)
- `multi\` — multiplayer games
- `hotseat\` — hotseat multiplayer
- `pbem\` — play-by-email
- `pitboss\` — Pitboss server games

Save file **modification time** is used as the timestamp of activity (each save represents a point when the game was being played). No save file content is read; only filesystem metadata is used.

Data stored: only timestamps and minimal metadata — no filenames, no file content, no file copies.

---

## Acceptance Criteria

**Schema:**

`civ5_session_point`:
- `id` (integer, PK)
- `scanned_at` (datetime) — when this scan run captured this point
- `file_modified_at` (datetime) — the save file's last modified time
- `game_mode` (text) — one of: `"single"`, `"multi"`, `"hotseat"`, `"pbem"`, `"pitboss"`, `"unknown"`
- `linked_event_id` (text, nullable)
- `linked_event_type` (text, nullable)

**Scan Flow:**

**Given** I click "Scan Saves" in the Civilization 5 drilldown or global mode
**When** the scan runs
**Then** both save folders are scanned recursively

**And** for each `.Civ5Save` file found:
- `game_mode` is determined from the immediate subfolder name (`single`, `multi`, etc.); files not in a recognized subfolder get `"unknown"`
- `file_modified_at` is read from the filesystem
- No filename, path, or file content is stored

**And** duplicate detection: points with the same `file_modified_at` + `game_mode` combination that already exist are skipped

**And** the scan is logged to `data_source_import_log` (record count = new points added)

**Compact Card:**

**Given** a day is selected with save points
**When** the Civilization 5 card is shown
**Then** the card displays:
- Number of save points for the day
- Breakdown by mode (e.g., "12 single-player, 3 multiplayer")

**Given** no save points for the day: "No Civ 5 activity"

**Drilldown View:**

**Given** I expand the Civilization 5 source for a selected day
**Then** I see a 24-hour vertical timeline with a dot at each `file_modified_at` timestamp

**And** hovering/clicking a dot shows: time, game mode

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation (8/15 + Coalescing):**

**Given** I click "Create Candidate Events"
**Then** the coalescing + 8/15 algorithm runs on that day's save points:
1. Sort points by `file_modified_at`
2. Apply sliding window coalescing: extend window while next point is within 30 minutes of previous (Civ sessions have natural save clusters)
3. Apply 8/15 rounding to each window's start and end
4. Create one `pending_event` per window:
   - Color: Yellow
   - Title: `"Civ 5"` (user is expected to rename manually per run)
   - Start/end from rounded window bounds

**And** events from mixed-mode sessions (single + multi save points in the same window) get title `"Civ 5 (mixed)"`

**And** contributing `civ5_session_point` rows are linked on push

---

## Technical Notes

- Scan paths: hardcode the two known paths; wrap in a configurable list (stored in app settings) for future extensibility
- File extension filter: `.Civ5Save` (verify exact extension at implementation; may also be `.CivBeyondSwordSave` for DLC — scan both)
- The scan must not throw on access-denied files; log warnings and continue
- The 30-minute coalescing gap is an initial value — make it configurable in the `data_source` row or app settings
- Save point deduplication is on `file_modified_at` + `game_mode`; if a save is later modified (unlikely for completed saves), a new point is added
- Unit tests: coalescing with exactly 30-min gap (boundary), mixed-mode window detection, deduplication

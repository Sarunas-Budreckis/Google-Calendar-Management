# Epic 6: Batch Day Management

**Author:** Mary (Business Analyst)
**Date:** 2026-05-13
**Status:** Ready for Tech Spec
**Tier:** 3 (workflow layer)

---

## Goal

Enable the user to collect a named set of non-contiguous days into a persistent batch and perform bulk operations on them — dramatically accelerating the backfilling ritual. The batch acts as a persistent multi-day selection that drives navigation through the left panel and enables one-click actions across many days at once.

---

## Background

Epic 5 establishes single-day select and the left panel. That model works well for detailed per-day work, but backfilling weeks or months of calendar data requires handling many days efficiently. The batch provides the answer: select 30 days, navigate through them one at a time with the Next button, and perform bulk approvals or bulk integration checks without touching each day individually. Only one batch can be active at a time, and it survives app restarts.

---

## Key Concepts

### Batch vs. Tracked Gap
The batch is a user's *working set* — days being actively processed right now. It is non-contiguous and temporary (cleared when done). `tracked_gap` is a permanent record of a known hole in contiguous history. They are separate concepts and may co-exist. A batch might be built by selecting all days in a tracked gap, but that is an action the user takes, not an automatic link.

### Batch vs. Single-Day Select
Single-day select (Epic 5) remains unchanged. The batch is a *collection* of days. Navigating the batch (Next button) drives the single-day select, updating the left panel for each day in sequence. The two systems compose naturally.

### Day States (reminder)
Day states are independent binary flags — Approved, Named, Integrated (per source) — not a pipeline. Batch actions can set any of these flags in bulk without implying that the others must be set first.

---

## In Scope

### Batch Data Model
- Single active batch: a named or unnamed ordered collection of dates
- Persisted to the database; survives app restart and reload
- At most one batch active at any time (enforced at the data model level)
- Batch has: list of dates (in insertion order), created_at, optional label

### Batch Tray (Top Panel)
- Persistent tray across the top of the calendar area, similar in concept to the Push to GCal tray
- Shows: "Batch: N days" badge when a batch is active, with a list/summary of dates on expand
- Controls: Clear batch, (future: label batch)
- When no batch is active: tray is collapsed or shows "No active batch"
- Tray is always accessible regardless of calendar view

### Adding Days to Batch
- In any calendar view, user can add a day to the batch (separate action from single-day select)
- Suggested interaction: right-click on a day number → "Add to batch" context menu, OR a toggle button in the day header area
- Days in the batch have a distinct visual marker in all calendar views (different from the single-day select indicator)
- Clicking the visual marker again removes the day from the batch
- Adding a day to the batch does not change the single-day select

### Left Panel Integration
- Left panel (Epic 5) is unchanged; it still shows single-day context
- When the user navigates the batch with Next/Previous, it drives the single-day select, updating the left panel to show that day's data source state
- The left panel header indicates when the current day is part of the active batch (e.g., a small badge or note: "Day 3 of 12 in batch")

### Batch Navigation
- **Next** and **Previous** buttons (accessible from the tray or a keyboard shortcut)
- Moves through batch days in insertion order
- Updates single-day select to the next/previous day in the batch
- Shows position: "Day N of M"
- At the end of the batch: Next is disabled or wraps (TBD in UX)

### Batch Actions
All batch actions apply to every day in the active batch.

**Batch Approve**
- Sets `approved = true` on `date_state` for all batch days
- Confirmation prompt showing count: "Approve 14 days?"
- Undoable (sets all back to false) — or at minimum clearly reversible by re-running manually

**Batch Mark Integration**
- User selects a data source from a dropdown
- Sets `integrated = true` in `date_source_integration` for all batch days × chosen source
- Confirmation prompt: "Mark Toggl Sleep as integrated for 14 days?"

**Batch Select Events**
- Selects all calendar events (both `gcal_event` and `pending_event`) whose date falls within any day in the batch
- Feeds into the existing multi-select event system (Story 4.6)
- Enables bulk operations on those events (push to GCal, delete, etc.)

### Visual Treatment
- Days in the batch have a distinct visual marker in all views (year, month, week, day)
- Marker is visually different from: event selection highlight, day selection indicator (Epic 5)
- Marker is visible even in dense views (year view dot or subtle background tint)

---

## Out of Scope

- Multiple simultaneous batches
- Batch add to "chapter" (future)
- Batch push to external Excel sheet (future)
- Auto-building a batch from a tracked gap (future convenience feature)
- Source-specific auto-accept for all days in batch (future)
- Labeling or archiving completed batches (future)

---

## Data Model Implications

| Table | Change | Notes |
|-------|--------|-------|
| `active_batch` | NEW | Stores the single active batch header (label, created_at) |
| `active_batch_date` | NEW | Junction: batch_id + date + position (insertion order) |
| `date_state` | WRITE | Batch approve writes `approved = true` |
| `date_source_integration` | WRITE | Batch mark integration writes rows (Epic 5 introduces this table) |

*Note: `system_state` may be used to store the active batch ID pointer.*

---

## Story Candidates

| Story | Title | Notes |
|-------|-------|-------|
| 6.1 | Batch Data Model & Persistence | `active_batch` + `active_batch_date` tables; single-batch constraint; load/save on startup |
| 6.2 | Batch Tray UI | Top panel tray; badge, expand, clear; empty state |
| 6.3 | Add/Remove Days to Batch | Context menu or toggle on day number; visual marker in all views |
| 6.4 | Batch Navigation | Next/Previous buttons; position indicator; drives single-day select |
| 6.5 | Batch Approve Action | Bulk set approval on all batch days; confirmation; batch position indicator in left panel |
| 6.6 | Batch Mark Integration Action | Source picker; bulk write to `date_source_integration`; confirmation |
| 6.7 | Batch Select Events | Select all events across batch days; feeds into multi-select event system |

---

## Dependencies

- **Epic 5 complete** (left panel, single-day select, `date_source_integration` table, `date_state` write path)
- **Story 4.6 complete** (multi-select events) — needed for Batch Select Events (6.7); other stories do not depend on it

---

## Success Criteria

- Add 10 non-contiguous days to the batch, close and reopen the app — batch is intact
- Batch tray shows day count; clear removes all days
- Batch days have a visible marker in year, month, and week views
- Next/Previous buttons cycle through batch days and update the left panel
- Left panel header shows batch position when navigating
- Batch Approve: all batch days gain `approved = true` after confirmation
- Batch Mark Integration: all batch days gain an integration row for the chosen source after confirmation
- Batch Select Events: all events on batch dates become selected in the calendar

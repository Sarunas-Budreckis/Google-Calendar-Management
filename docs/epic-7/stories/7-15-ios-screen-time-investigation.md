# Story 7.15: iOS Screen Time — Investigation

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry, for future implementation)

---

## User Story

As a **user**,
I want **a thorough investigation of every viable method for programmatically accessing iOS Screen Time data**,
so that **I can make an informed decision about how (or whether) to implement it as a data source**.

---

## Background

iOS Screen Time tracks per-app usage, daily totals, pickups, and notifications. The data is valuable for the backfilling ritual (understanding phone usage patterns) but is notoriously difficult to export programmatically. This story is **investigation-only** — no implementation, no database schema, no UI. The deliverable is a written findings document.

---

## Deliverable

A findings document saved to `docs/epic-7/screen-time-investigation.md` covering all of the following:

---

### Investigation Areas

**1. Manual Export Methods**
- Does iOS Settings expose any Screen Time export to CSV/JSON/PDF?
- Can Screen Time reports be shared via AirDrop or email in a structured format?
- What data is included in a manual export (if any)?

**2. iCloud / Apple Health Integration**
- Does Screen Time data sync to iCloud and appear in any accessible cloud storage location?
- Is Screen Time data included in Apple Health exports (`export.xml`)? If so, in what form?
- Can `iMazing` (already used for call logs) export Screen Time data? In what format?

**3. Mac App / Automation Approaches**
- Can a Mac app read Screen Time data from the local SQLite databases on a paired iPhone backup (`knowledgeC.db`, `ZRTCLIENTSCREENTIMEWEEKLY`, `CoreDuet`, etc.)?
- Does macOS Screen Time (mirrored from iPhone) expose its data in a readable format?
- Is there a documented path to read `knowledgeC.db` from an unencrypted iTunes backup?
- What third-party Mac apps (if any) already extract this data (e.g., ActivityWatch, Timing, Screentime+)?

**4. API / Shortcut Approaches**
- Does iOS Shortcuts expose any Screen Time data (e.g., daily total, per-app totals)?
- Is there a Screen Time API in Screen Time's MDM profile that could be used on a personal device?
- Does Apple's DeviceActivity framework (iOS 16+) expose data to third-party apps in any way?

**5. Feasibility Assessment**
For each viable method found, assess:
- Technical complexity (1–5)
- Data richness (what fields are available)
- Automation level (fully automatic / one-click export / manual export)
- Privacy/security implications
- Maintenance burden (likely to break on iOS updates?)

**6. Recommendation**
- Which method (if any) should be implemented as Story 7.15-impl?
- If none are viable, document why and note what would need to change (Apple API, third-party tool) for this to become feasible

---

## Acceptance Criteria

**Given** the investigation is complete
**When** the findings document is reviewed
**Then** it covers all 6 areas above with concrete findings (not "TBD" placeholders)

**And** it includes at least one actionable recommendation (even if the recommendation is "not feasible at this time")

**And** it cites specific tools, file paths, or API names where applicable

**And** it is saved to `docs/epic-7/screen-time-investigation.md`

---

## Technical Notes

- `knowledgeC.db` path in unencrypted iTunes backup: `Apple\MobileSync\Backup\{device-id}\{hash}` — the hash is consistent per device; document the specific path if found
- Relevant Apple frameworks to research: `DeviceActivity` (Screen Time API, iOS 16+), `FamilyControls` (requires managed/MDM context)
- iMazing Screen Time export: check iMazing documentation and try on a test export
- This investigation does not require writing any code — it is desk research + tool exploration

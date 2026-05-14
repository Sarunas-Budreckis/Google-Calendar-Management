# Story 7.8: Outlook Work Calendar Sync (Investigation + MVP)

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to sync my Mayo Clinic Outlook calendar events into the local app**,
so that **I can see my work schedule alongside personal life data, colored purple**.

---

## Background

The user has a Mayo Clinic work Outlook account. Work calendar events should appear in the local calendar colored purple, providing a unified view without manually re-entering work events. The user may want to hide specific work events from the calendar view (while keeping them in the data source) — for example, recurring standup meetings that add noise.

**Investigation required first:** Mayo Clinic's IT environment may restrict Microsoft Graph API access for third-party apps. This story begins with an investigation phase before committing to implementation. If Graph API is blocked, a fallback (ICS file import) should be implemented instead.

---

## Acceptance Criteria

**Phase 1 — Investigation (must be completed and documented before Phase 2 begins):**

**Given** the developer has access to the Mayo Clinic Outlook account
**When** investigating API access
**Then** document the following:
- Can a personal app be registered in Azure AD for this tenant, or is registration blocked?
- Does Microsoft Graph API `Calendars.Read` scope work with a personal app registration?
- Is the ICS subscribe URL available from Outlook Web Access (OWA) as a fallback?
- What recurrence of sync is feasible without triggering IT security alerts?

**And** a decision is recorded in `docs/_key-decisions.md`: Graph API vs ICS fallback, with rationale

**Phase 2 — Implementation (Graph API path, if investigation succeeds):**

**Schema:**

`outlook_event`:
- `id` (text, PK) — Outlook event ID
- `subject` (text)
- `start_datetime` (datetime)
- `end_datetime` (datetime)
- `is_all_day` (boolean)
- `organizer` (text, nullable)
- `location` (text, nullable)
- `body_preview` (text, nullable)
- `is_recurring` (boolean)
- `series_master_id` (text, nullable)
- `last_synced_at` (datetime)
- `is_suppressed` (boolean, default false) — hidden from calendar but kept in source

**Sync Flow:**

**Given** Outlook sync is configured (OAuth token stored)
**When** I click "Sync Work Calendar" or sync runs automatically
**Then** events are fetched via Microsoft Graph API for a configurable date range

**And** events are upserted into `outlook_event` (update on `id` match)

**And** the sync is logged to `data_source_import_log`

**Calendar Display:**

**Given** Outlook events are synced
**When** I view the calendar
**Then** Outlook events appear as Purple calendar events alongside GCal events

**And** suppressed events (`is_suppressed = true`) are not shown in the calendar

**Suppress Event Action:**

**Given** I right-click or use a context menu on an Outlook event in the calendar or drilldown
**When** I select "Hide from Calendar"
**Then** `is_suppressed` is set to true and the event disappears from the calendar view

**And** the event remains in `outlook_event` and the drilldown

**And** I can un-suppress from the drilldown (toggle to restore visibility)

**Compact Card:**

- Shows count of work events for the selected day
- Shows total work hours

**Drilldown:**

- Chronological list of work events for the day
- Suppressed events shown with a strikethrough or "hidden" indicator
- Toggle suppress/un-suppress per event

**Phase 2 — Fallback (ICS path, if Graph API is blocked):**

- User exports ICS file from OWA (or subscribes via ICS URL)
- App imports ICS file, parses events, stores in `outlook_event`
- No automatic sync — manual import only
- Note ICS URL subscription as a future improvement

---

## Technical Notes

- Microsoft Graph SDK for .NET: `Microsoft.Graph` NuGet package
- OAuth scopes: `Calendars.Read` (delegated)
- Token stored encrypted alongside the GCal token (DPAPI, same pattern as Story 2.1)
- Outlook events rendered in the calendar using the same event display pipeline as `gcal_event` and `pending_event` — requires widening the display model to accept a third event source type
- Purple color: use the existing Lavender/Purple from the app's 9-color palette (whichever is `#8B5CF6` or nearest — verify against `_color-definitions.md`)
- Recurring event expansion: fetch expanded instances from Graph (not master + recurrence rule) to avoid implementing recurrence expansion locally
- Investigation findings should be completed before coding Phase 2; if blocked by IT, implement ICS fallback only

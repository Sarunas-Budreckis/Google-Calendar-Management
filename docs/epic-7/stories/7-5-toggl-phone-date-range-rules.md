# Story 7.5: Toggl Phone Date-Range Rules

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.4 (Toggl Phone baseline classification)

---

## User Story

As a **user**,
I want **to define different phone-entry matching rules for different date ranges**,
so that **I can correctly classify phone Toggl entries despite the naming conventions having changed multiple times over the years**.

---

## Background

The phone tracking automation has used different Toggl entry names at different points in time ("ToDelete", "Phone", "Phone - Reddit", "Phone - Instagram", and potentially others in the future). Story 7.4 hardcodes the current set of known names with a ≤10 min filter. This story replaces that with a configurable rule engine: a set of date-ranged rules stored in the database, each specifying which description patterns and duration constraints count as `toggl_phone` for entries in that date range.

---

## Acceptance Criteria

**Data Model:**

**Given** a new `toggl_phone_rule` table
**When** the migration is applied
**Then** the table contains:
- `id` (integer, PK)
- `date_from` (date, nullable — null means "from the beginning of time")
- `date_to` (date, nullable — null means "indefinitely")
- `description_pattern` (text) — exact match string (case-insensitive); future: consider LIKE/regex
- `max_duration_minutes` (integer, nullable — null means no upper limit)
- `is_active` (boolean, default true)
- `notes` (text, nullable)

**And** the initial seed contains the rules equivalent to Story 7.4's hardcoded defaults (all date ranges open, descriptions as defined in 7.4, max 10 min)

**Rule Management UI (in app Settings or a dedicated panel):**

**Given** I navigate to the Toggl Phone rules page
**When** the page loads
**Then** I see a list of all rules sorted by `date_from` (nulls first)

**And** I can add a new rule by specifying: date_from, date_to, description pattern, max duration, notes

**And** I can deactivate (soft-delete) an existing rule

**And** I can edit an existing rule

**Classification Engine:**

**Given** the rule engine runs on a `toggl_data` entry
**When** it evaluates the entry
**Then** it tests all active rules whose date range overlaps the entry's date
- If the entry's description matches any rule's pattern AND its duration is ≤ the rule's max duration (if specified)
- Then the entry is tagged `toggl_data_type = "toggl_phone"`

**And** a "Re-classify all Toggl entries" action is available (runs the rule engine across all historical `toggl_data` rows, resetting and reapplying `toggl_phone` tags)

**And** re-classification is idempotent and safe to run multiple times

---

## Technical Notes

- Rules are evaluated in the classification service injected into both the import pipeline and the manual re-classify action
- If two rules overlap in date range and both match an entry, the entry is tagged (OR logic)
- The re-classify action can be slow for large datasets — run on a background thread with a progress indicator
- This rule engine is specific to `toggl_phone`; if other sources need similar date-range logic in the future, extract the pattern
- UI placement TBD — could live in Settings > Data Sources > Toggl Phone, or in the Toggl Phone drilldown toolbar

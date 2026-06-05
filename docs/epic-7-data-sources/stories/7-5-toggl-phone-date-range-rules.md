# Story 7.5: Toggl Phone Date-Range Rules

**Epic:** 7 — Additional Data Source Integrations
**Status:** review
**Dependencies:** Story 7.4 (Toggl Phone baseline classification)

---

## User Story

As a **user**,
I want **to define different phone-entry matching rules for different date ranges**,
so that **I can correctly classify phone Toggl entries despite the naming conventions having changed multiple times over the years**.

---

## Background

The phone tracking automation has used different Toggl entry names at different points in time ("ToDelete", "Phone", "Phone - Reddit", "Phone - Instagram", and potentially others in the future). Story 7.4 hardcodes the current set of known names with a ≤10 min filter. This story replaces that with a configurable rule engine: a set of date-ranged rules stored in the database, each specifying which description patterns and duration constraints count as `toggl_phone` for entries in that date range.

Both stories are implemented together; 7.5 takes priority on conflicts.

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

**Rule Management UI (in Toggl Phone drilldown toolbar/settings area):**

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
- UI placement: in the Toggl Phone drilldown toolbar as a "Manage Rules" button
- Implemented together with 7.4

---

## Tasks/Subtasks

- [x] Task 1: Data model — `toggl_phone_rule` table and seed (done via 7.4 Task 1)
- [x] Task 2: Rule repository and classification engine (done via 7.4 Task 2)
- [x] Task 3: Rules management UI
  - [x] 3.1: Create `TogglPhoneRulesViewModel`
  - [x] 3.2: Create `TogglPhoneRulesControl` XAML + codebehind
  - [x] 3.3: Wire "Manage Rules" button in drilldown
  - [x] 3.4: Wire "Re-classify" action in rules UI

---

## Dev Notes

- Rules UI opens as a ContentDialog from the drilldown "Manage Rules" button
- Re-classify calls `ITogglPhoneClassificationService.ClassifyAllAsync()`
- Sorting: nulls first for `date_from`, then ascending date
- DatePicker for date_from/date_to; null = leave DatePicker empty

---

## Dev Agent Record

### Implementation Plan
Implemented together with story 7.4. The data model and classification engine from 7.5 are built into the 7.4 implementation from the start.

### Completion Notes

Implemented as part of 7.4. The `toggl_phone_rule` table is created via migration `20260604130000_AddTogglPhoneRule` with 4 seed rows matching the 7.4 hardcoded defaults. The `TogglPhoneClassificationService` uses the rule engine exclusively (no hardcoded patterns). The rules UI lives in a `ContentDialog` opened from the Toggl Phone drilldown — this was chosen over a dedicated settings page as it keeps the rules close to the feature that uses them.

### Debug Log

---

## File List

See story 7-4 for the shared file list. Story 7.5-specific additions:
- `ViewModels/TogglPhoneRulesViewModel.cs`
- `Views/TogglPhoneRulesControl.xaml`
- `Views/TogglPhoneRulesControl.xaml.cs`

---

## Change Log

- 2026-06-04: Story created and implemented together with 7.4

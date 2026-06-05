# Story 7.4: Toggl Phone

**Epic:** 7 — Additional Data Source Integrations
**Status:** review
**Dependencies:** Story 7.1 (toggl_data_type), Story 5.6 (Toggl import), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to see my phone usage sessions from Toggl in the left panel**,
so that **I can create coalesced calendar events for phone time**.

---

## Background

iOS Shortcuts creates Toggl entries for phone activity under several names that have changed over time: "ToDelete", "Phone", "Phone - Reddit", "Phone - Instagram". These entries are short (≤10 minutes each) and frequent — phone activity coalesces into sessions using a sliding window algorithm. This story adds classification, compact card, drilldown with a vertical dot timeline, and candidate event generation.

Note: the classification rules defined here (description matching + ≤10 min filter) are intentionally basic. Story 7.5 adds a UI for specifying per-date-range rules to handle the evolving naming conventions. Both stories are implemented together; 7.5 takes priority on conflicts.

---

## Acceptance Criteria

**Classification:**

**Given** the Toggl import has run
**When** phone classification runs
**Then** all `toggl_data` rows meeting ALL of these criteria are tagged `toggl_data_type = "toggl_phone"`:
- `description` is one of: `"ToDelete"`, `"Phone"`, `"Phone - Reddit"`, `"Phone - Instagram"` (case-insensitive)
- Duration ≤ 10 minutes

**And** rows that match the description pattern but are >10 minutes remain unclassified (null), as they likely represent false positives

**Compact Card:**

**Given** a day is selected and has phone entries
**When** the Toggl – Phone card is shown
**Then** the card displays:
- Number of phone entries for the day
- Estimated total screen time (raw sum of entry durations, not coalesced)
- If no phone entries: "No phone data"

**Drilldown View:**

**Given** I expand the Toggl – Phone source
**When** the drilldown opens for the selected day
**Then** I see a 24-hour vertical timeline with a dot at the start time of each phone entry
- Hovering/clicking a dot shows: entry description, start time, end time, duration

**And** a "Create Candidate Events" button is visible

**Candidate Event Generation (Sliding Window + 8/15):**

**Given** I click "Create Candidate Events"
**When** the algorithm runs on that day's `toggl_phone` entries
**Then** the sliding window algorithm applies:
1. Sort entries by start time
2. Start a window at the first entry
3. Extend the window while the next entry starts within 15 minutes of the current window end
4. Quality check: if <50% of the window duration is covered by phone entries, retry with a 5-minute gap threshold instead
5. Discard any window with total duration <5 minutes
6. Apply 8/15 rounding to each window's start and end
7. Create one `pending_event` per window with:
   - Color: Yellow (colorId: "banana")
   - Title: "Phone"
   - Start/end from the 8/15-rounded window

**And** contributing `toggl_data` rows are linked to the event on push

---

## Technical Notes

- Classification is idempotent; re-running after a new import does not re-tag already-tagged rows
- The sliding window service is shared with future sources; parameterize gap threshold and quality threshold
- Vertical dot timeline component: shared with Spotify (7.10) and ComfyUI (7.12) — design as a reusable control
- Story 7.5 will add a date-range rule engine that overrides the hardcoded description list; this story's classification should be refactorable to call that engine
- Unit tests required: sliding window edge cases (single entry, entries with exactly 15-min gap, quality check triggering 5-min retry, <5 min discard)
- Implemented together with 7.5; classification engine is rule-based from the start

---

## Tasks/Subtasks

- [x] Task 1: Create TogglPhoneRule entity, EF config, migration, and update DbContext
  - [x] 1.1: Create `TogglPhoneRule` entity class
  - [x] 1.2: Create `TogglPhoneRuleConfiguration` EF config
  - [x] 1.3: Add `DbSet<TogglPhoneRule>` to CalendarDbContext
  - [x] 1.4: Create migration with `toggl_phone_rule` table and seed data
  - [x] 1.5: Update model snapshot
- [x] Task 2: Create rule repository and rule-based classification service
  - [x] 2.1: Create `ITogglPhoneRuleRepository` and `TogglPhoneRuleRepository`
  - [x] 2.2: Create `ITogglPhoneClassificationService` and `TogglPhoneClassificationService`
- [x] Task 3: Create phone entry repository
  - [x] 3.1: Create `ITogglPhoneRepository` interface
  - [x] 3.2: Create `TogglPhoneRepository` implementation
- [x] Task 4: Create sliding window service
  - [x] 4.1: Create `TogglSlidingWindowService` with configurable thresholds
- [x] Task 5: Compact card UI
  - [x] 5.1: Create `TogglPhoneCardProvider`
  - [x] 5.2: Create `TogglPhoneCompactCardViewModel`
  - [x] 5.3: Create `TogglPhoneCompactCardControl` XAML + codebehind
- [x] Task 6: Drilldown UI with dot timeline and candidate event generation
  - [x] 6.1: Create `TogglPhoneDrilldownViewModel`
  - [x] 6.2: Create `TogglPhoneDrilldownControl` XAML + codebehind
- [x] Task 7: Register all new services in App.xaml.cs DI
- [x] Task 8: Write unit tests
  - [x] 8.1: Sliding window edge cases
  - [x] 8.2: Classification service rule engine

---

## Dev Notes

- Color for phone events: `"banana"` (maps to yellow in ColorMappingService)
- TogglDataType enum already has `TogglPhone` (added in 7.1)
- "yellow" alias maps to "banana" in ColorMappingService.AliasToCanonicalKeyMap
- Follow the TogglSleepCardProvider/Repository pattern exactly
- XAML views registered as Transient; ViewModels as Transient
- Dot timeline: use code-behind Canvas approach (populate dots in LoadAsync callback)
- Canvas height: 480px (20px per hour × 24 hours)
- Dot Y position: `(entry.StartTime.LocalHour * 60 + entry.StartTime.LocalMinute) / 1440.0 * 480`
- ToolTipService.SetToolTip for hovering/clicking dot tooltip
- Sliding window parameters: gap=15min, quality=0.5, minDuration=5min; retry with gap=5min
- 8/15 rounding uses `CalendarDraftTiming.RoundToNearestQuarterHour`

---

## Dev Agent Record

### Implementation Plan
Implementing stories 7.4 and 7.5 together. Classification uses a database-driven rule engine (7.5) seeded with the default patterns from 7.4. This avoids the need to replace hardcoded logic later.

### Completion Notes

Implemented stories 7.4 and 7.5 together as planned. Key decisions:

- **Classification engine**: Rule-based from the start (7.5 design), seeded with 7.4's hardcoded patterns (ToDelete, Phone, Phone - Reddit, Phone - Instagram, max 10 min, all dates).
- **Sliding window service**: `TogglSlidingWindowService` with configurable gap threshold, quality threshold, and minimum duration. Returns `SlidingWindowResult` records. Retry logic with 5-min gap when quality < threshold. All edge cases covered in unit tests.
- **Dot timeline**: Canvas-based code-behind approach. 480px height = 20px/hour. Each entry dot positioned by `(minuteOfDay / 1440) * 480`. ToolTip on each dot shows description, start/end time, duration.
- **Rules UI**: `ContentDialog` opened from "Manage Rules" button in drilldown. Per-rule save/deactivate. "Re-classify All" runs the full classification engine on all historical entries.
- **Color**: `"banana"` (yellow) per story spec.
- **Pre-existing build errors**: Two pre-existing errors in `CallLogCardProvider.cs` and `Civ5DrilldownControl.xaml.cs` remain. These are unrelated to this story's changes. No new errors introduced.

### Debug Log

---

## File List

- `Data/Entities/TogglPhoneRule.cs`
- `Data/Configurations/TogglPhoneRuleConfiguration.cs`
- `Data/Migrations/20260604130000_AddTogglPhoneRule.cs`
- `Data/Migrations/20260604130000_AddTogglPhoneRule.Designer.cs`
- `Data/CalendarDbContext.cs` (modified)
- `Data/Migrations/CalendarDbContextModelSnapshot.cs` (modified)
- `Services/ITogglPhoneRuleRepository.cs`
- `Services/TogglPhoneRuleRepository.cs`
- `Services/ITogglPhoneClassificationService.cs`
- `Services/TogglPhoneClassificationService.cs`
- `Services/ITogglPhoneRepository.cs`
- `Services/TogglPhoneRepository.cs`
- `Services/TogglSlidingWindowService.cs`
- `Services/TogglPhoneCardProvider.cs`
- `ViewModels/TogglPhoneCompactCardViewModel.cs`
- `ViewModels/TogglPhoneDrilldownViewModel.cs`
- `ViewModels/TogglPhoneEntryViewModel.cs`
- `Views/TogglPhoneCompactCardControl.xaml`
- `Views/TogglPhoneCompactCardControl.xaml.cs`
- `Views/TogglPhoneDrilldownControl.xaml`
- `Views/TogglPhoneDrilldownControl.xaml.cs`
- `App.xaml.cs` (modified)
- `GoogleCalendarManagement.Tests/Unit/Services/TogglSlidingWindowServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/TogglPhoneClassificationServiceTests.cs`

---

## Change Log

- 2026-06-04: Story created and implemented (combined with 7.5 rule engine)

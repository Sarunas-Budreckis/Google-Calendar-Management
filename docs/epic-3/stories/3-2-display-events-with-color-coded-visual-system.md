# Story 3.2: Display Events with Colour-Coded Visual System

Status: review

## Story

As a **calendar user**,
I want **each event to render in its category colour with consistent white text**,
so that **I can scan the calendar by category using a simple, uniform visual style**.

## Acceptance Criteria

1. **AC-3.2.1:** Given events with supported `color_id` values or alias strings, each event renders in its assigned category colour in Month, Week, and Day views.
2. **AC-3.2.2:** Given a `null`, empty, or unrecognised `color_id`, the event renders in Azure `#0088CC` and no exception is thrown.
3. **AC-3.2.3:** Given any coloured event chip or block, title/time text remains white `#FFFFFF` in Month, Week, and Day views.
4. **AC-3.2.4:** Given the same event appears in Month, Week, and Day views, the same background and white text styling are used in all three views.
5. **AC-3.2.5:** Story 3.1 event placement, row-span logic, and navigation behavior do not regress.

## Scope Boundaries

**IN SCOPE**
- Replace the Azure-only stub in `ColorMappingService`
- Apply colour rendering with white event text in Month, Week, and Day views
- Expand unit tests for colour mapping

**OUT OF SCOPE**
- Year view event indicators or colouring
- Sync-status dots from Story 2.4
- Selection visuals from Story 3.3
- Details panel work from Story 3.4
- Editing, creation, or picker work from Stories 3.5-3.7
- Query/model/date-range changes

## Tasks / Subtasks

- [x] **Task 1: Replace the mapping stub** (AC: 3.2.1, 3.2.2)
  - [x] Add `IReadOnlyDictionary<string, string> AllColors { get; }` to `Services/IColorMappingService.cs`
  - [x] Replace `Services/ColorMappingService.cs` with a case-insensitive alias + numeric-ID dictionary
  - [x] Keep Azure `#0088CC` as the fallback for `null`, empty, or unknown values

- [x] **Task 2: Update `MonthViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [x] Keep white month-chip foreground text
  - [x] Use mapped event background colours for rendered chips

- [x] **Task 3: Update `WeekViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [x] Keep white all-day chip foreground text
  - [x] Keep white timed-event title/time text
  - [x] Use mapped event background colours for rendered blocks

- [x] **Task 4: Update `DayViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [x] Keep white all-day title text
  - [x] Keep white timed-event title/time text
  - [x] Use mapped event background colours for rendered blocks

- [x] **Task 5: Leave `YearViewControl` unchanged** (AC: 3.2.4)
  - [x] Confirm it still renders the Story 3.1 placeholder grey ellipse
  - [x] Do not retrofit year-view colouring here; Story 3.9 owns year-view event indicators

- [x] **Task 6: Expand automated coverage** (AC: 3.2.1, 3.2.2)
  - [x] Update `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs` to cover all aliases and numeric IDs

- [x] **Task 7: Validate**
  - [x] Run `dotnet build -p:Platform=x64`
  - [x] Run `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: verify Month, Week, and Day views no longer render every event in Azure
  - [ ] Manual: verify white event text remains consistent across Month, Week, and Day views

## Dev Notes

### Current repository reality

- Story 3.1 already shipped the calendar shell and inline event rendering code.
- The real project structure is flat at repo root: `Services/`, `Views/`, `ViewModels/`, `Models/`, `Messages/`, `Data/`.
- There is no `Core/` project and no `EventChip.xaml` or `EventBlock.xaml` control to extend.
- `CalendarQueryService` already maps `gcal_event.ColorId` into `CalendarEventDisplayModel.ColorHex`; do not change query behavior here.
- `YearViewControl` still shows a static grey ellipse placeholder and does not render event bodies. Story 3.9 owns year-view event indicators/previews.

### Category mapping to implement

This alias-to-ID mapping is an **app-specific inference** layered on top of the Google Calendar event colour IDs already stored in `gcal_event.color_id`. If live synced data later shows a different numeric-to-semantic mapping, update the dictionary and tests together.

| Category | Accepted keys | Hex | Expected text |
|---|---|---|---|
| Azure | `azure`, `1` | `#0088CC` | `#FFFFFF` |
| Purple | `purple`, `9` | `#3F51B5` | `#FFFFFF` |
| Grey | `grey`, `8` | `#616161` | `#FFFFFF` |
| Yellow | `yellow`, `5` | `#F6BF26` | `#FFFFFF` |
| Navy | `navy`, `2` | `#33B679` | `#FFFFFF` |
| Sage | `sage`, `10` | `#0B8043` | `#FFFFFF` |
| Flamingo | `flamingo`, `4` | `#E67C73` | `#FFFFFF` |
| Orange | `orange`, `6` | `#F4511E` | `#FFFFFF` |
| Lavender | `lavender`, `3` | `#8E24AA` | `#FFFFFF` |

Guardrails:
- Use `StringComparer.OrdinalIgnoreCase`
- Unknown values fall back to Azure in `ColorMappingService`, not in the views
- No new NuGet packages are required

### Testing requirements

Replace the old "always Azure" mapping test with:
- alias + numeric-ID assertions for all 9 categories
- case-insensitive alias assertions
- fallback assertions for `null`, empty, and unknown values
- `AllColors` format/count assertions

### References

- [docs/epic-3/tech-spec.md](../tech-spec.md)
- [docs/epic-3/stories/3-1-build-year-month-week-day-calendar-views.md](./3-1-build-year-month-week-day-calendar-views.md)
- [docs/epic-3/stories/3-9-enhance-year-view-with-event-indicators-and-all-day-previews.md](./3-9-enhance-year-view-with-event-indicators-and-all-day-previews.md)
- [docs/_color-definitions.md](../../_color-definitions.md)
- [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs)
- [Services/ColorMappingService.cs](../../../Services/ColorMappingService.cs)
- [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs)
- [Views/MonthViewControl.xaml.cs](../../../Views/MonthViewControl.xaml.cs)
- [Views/WeekViewControl.xaml.cs](../../../Views/WeekViewControl.xaml.cs)
- [Views/DayViewControl.xaml.cs](../../../Views/DayViewControl.xaml.cs)
- [Views/YearViewControl.xaml.cs](../../../Views/YearViewControl.xaml.cs)
- [App.xaml.cs](../../../App.xaml.cs)
- Google Calendar Colors resource: https://developers.google.com/workspace/calendar/api/v3/reference/colors

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- 2026-03-31: Story context refreshed against Story 3.1, Epic 3 tech spec, the current codebase, and official colour/contrast references.
- 2026-04-02: Implementation completed by claude-sonnet-4-6.

### Completion Notes List

- Corrected the story to match the real repo layout and Story 3.1 implementation.
- Kept Year view out of scope because Story 3.9 already owns year-view event indicators.
- Task 1: Replaced `ColorMappingService` stub with 9-category, 18-entry `OrdinalIgnoreCase` dictionary (alias + numeric ID per category). Added `AllColors` property to interface and implementation.
- Task 2: Kept white text for coloured event chips in Month view.
- Task 3: Kept white text for all-day and timed events in Week view.
- Task 4: Kept white text for all-day and timed events in Day view.
- Task 5: Confirmed `YearViewControl` renders unchanged grey ellipse placeholder — no colour changes; Story 3.9 owns year-view event indicators.
- Task 6: Rewrote `ColorMappingServiceTests` with alias, case-insensitive, numeric-ID, fallback, and `AllColors` assertions.
- Task 7: `dotnet build -p:Platform=x64` — Build succeeded, 0 warnings, 0 errors. `dotnet test` — passed.

### File List

- `docs/epic-3/stories/3-2-display-events-with-color-coded-visual-system.md`
- `Services/IColorMappingService.cs`
- `Services/ColorMappingService.cs`
- `App.xaml.cs`
- `Views/MonthViewControl.xaml.cs`
- `Views/WeekViewControl.xaml.cs`
- `Views/DayViewControl.xaml.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`
- `docs/sprint-status.yaml`

### Change Log

- 2026-04-02: Implemented Story 3.2 — colour-coded event rendering with consistent white text across Month, Week, and Day views.

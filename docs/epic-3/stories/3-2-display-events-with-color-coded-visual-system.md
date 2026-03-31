# Story 3.2: Display Events with Colour-Coded Visual System

Status: ready-for-dev

## Story

As a **calendar user**,
I want **each event to render in its category colour with readable text**,
so that **I can scan the calendar by category without losing legibility**.

## Acceptance Criteria

1. **AC-3.2.1:** Given events with supported `color_id` values or alias strings, each event renders in its assigned category colour in Month, Week, and Day views.
2. **AC-3.2.2:** Given a `null`, empty, or unrecognised `color_id`, the event renders in Azure `#0088CC` and no exception is thrown.
3. **AC-3.2.3:** Given any coloured event chip or block, title/time text is automatically black `#000000` or white `#FFFFFF` so normal text meets WCAG 2.1 AA 4.5:1 contrast guidance.
4. **AC-3.2.4:** Given the same event appears in Month, Week, and Day views, the same background and text colours are used in all three views.
5. **AC-3.2.5:** Story 3.1 event placement, row-span logic, and navigation behavior do not regress.

## Scope Boundaries

**IN SCOPE**
- Replace the Azure-only stub in `ColorMappingService`
- Add a pure black/white contrast-selection service
- Apply colour + contrast rendering in Month, Week, and Day views
- Expand unit tests for colour mapping and contrast

**OUT OF SCOPE**
- Year view event indicators or colouring
- Sync-status dots from Story 2.4
- Selection visuals from Story 3.3
- Details panel work from Story 3.4
- Editing, creation, or picker work from Stories 3.5-3.7
- Query/model/date-range changes

## Tasks / Subtasks

- [ ] **Task 1: Replace the mapping stub** (AC: 3.2.1, 3.2.2)
  - [ ] Add `IReadOnlyDictionary<string, string> AllColors { get; }` to `Services/IColorMappingService.cs`
  - [ ] Replace `Services/ColorMappingService.cs` with a case-insensitive alias + numeric-ID dictionary
  - [ ] Keep Azure `#0088CC` as the fallback for `null`, empty, or unknown values

- [ ] **Task 2: Add a pure contrast service** (AC: 3.2.3)
  - [ ] Create `Services/IColorContrastService.cs`
  - [ ] Create `Services/ColorContrastService.cs`
  - [ ] Accept `#RRGGBB` and `RRGGBB`
  - [ ] Invalid input must not throw; return white text fallback

- [ ] **Task 3: Register the contrast service in DI** (AC: 3.2.3)
  - [ ] Add `services.AddSingleton<IColorContrastService, ColorContrastService>();` in `App.xaml.cs`

- [ ] **Task 4: Update `MonthViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [ ] Resolve `IColorContrastService` in the constructor
  - [ ] Replace the single hardcoded `Colors.White` month-chip foreground
  - [ ] Convert `BuildDayCell(...)` from static helper if needed so it can access the contrast service

- [ ] **Task 5: Update `WeekViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [ ] Resolve `IColorContrastService` in the constructor
  - [ ] Replace the all-day chip foreground
  - [ ] Replace both timed-event text foregrounds
  - [ ] Remove or work around static helpers cleanly

- [ ] **Task 6: Update `DayViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [ ] Resolve `IColorContrastService` in the constructor
  - [ ] Replace the all-day title foreground
  - [ ] Replace both timed-event text foregrounds

- [ ] **Task 7: Leave `YearViewControl` unchanged** (AC: 3.2.4)
  - [ ] Confirm it still renders the Story 3.1 placeholder grey ellipse
  - [ ] Do not retrofit year-view colouring here; Story 3.9 owns year-view event indicators

- [ ] **Task 8: Expand automated coverage** (AC: 3.2.1, 3.2.2, 3.2.3)
  - [ ] Update `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs` to cover all aliases and numeric IDs
  - [ ] Add `GoogleCalendarManagement.Tests/Unit/Services/ColorContrastServiceTests.cs`
  - [ ] Assert invalid hex input does not throw

- [ ] **Task 9: Validate**
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: verify Month, Week, and Day views no longer render every event in Azure
  - [ ] Manual: verify light colours use dark text and dark colours use white text

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
| Azure | `azure`, `1` | `#0088CC` | `#000000` |
| Purple | `purple`, `9` | `#3F51B5` | `#FFFFFF` |
| Grey | `grey`, `8` | `#616161` | `#FFFFFF` |
| Yellow | `yellow`, `5` | `#F6BF26` | `#000000` |
| Navy | `navy`, `2` | `#33B679` | `#000000` |
| Sage | `sage`, `10` | `#0B8043` | `#FFFFFF` |
| Flamingo | `flamingo`, `4` | `#E67C73` | `#000000` |
| Orange | `orange`, `6` | `#F4511E` | `#000000` |
| Lavender | `lavender`, `3` | `#8E24AA` | `#FFFFFF` |

Guardrails:
- Use `StringComparer.OrdinalIgnoreCase`
- Unknown values fall back to Azure in `ColorMappingService`, not in the views
- No new NuGet packages are required

### Contrast service requirements

- Public API: `string GetContrastTextColor(string backgroundHex)`
- Accept input with or without a leading `#`
- Parse only 6-digit RGB hex
- Invalid input must not throw; return `#FFFFFF`
- Use WCAG relative luminance and the standard `0.179` threshold to choose between black and white text

### Testing requirements

Replace the old "always Azure" mapping test with:
- alias + numeric-ID assertions for all 9 categories
- case-insensitive alias assertions
- fallback assertions for `null`, empty, and unknown values
- `AllColors` format/count assertions

Add contrast tests for:
- each supported category colour
- black background -> white text
- white background -> black text
- input without `#`
- invalid hex returning white text without throwing

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
- WCAG 2.1 Contrast (Minimum): https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum

## Dev Agent Record

### Agent Model Used

GPT-5 (Codex)

### Debug Log References

- 2026-03-31: Story context refreshed against Story 3.1, Epic 3 tech spec, the current codebase, and official colour/contrast references.

### Completion Notes List

- Corrected the story to match the real repo layout and Story 3.1 implementation.
- Kept Year view out of scope because Story 3.9 already owns year-view event indicators.
- Tightened the contrast-service guidance so malformed hex input cannot crash the implementation.

### File List

- `docs/epic-3/stories/3-2-display-events-with-color-coded-visual-system.md`

# Story 4.8: Year View Drag-to-Create

Status: ready-for-dev

## Story

As a **user**,
I want **to drag across day cells in Year and Month view to create all-day draft events**,
so that **I can capture full-day and multi-day plans directly from the high-level calendar surfaces without switching into Week or Day view first**.

## Acceptance Criteria

1. **AC-4.8.1 - Month view drag creates an all-day draft from empty day cells:** Given the user is in Month view and presses on empty day-cell background space, when they drag across one or more visible day cells and release, a new local-only all-day draft is created for the selected inclusive date range.
2. **AC-4.8.2 - Year view drag creates an all-day draft from mini-month cells:** Given the user is in Year view and presses on an in-month day cell background, when they drag across one or more valid day cells in the same year grid and release, a new local-only all-day draft is created for the selected inclusive date range. A tap-only fallback is NOT introduced in this story.
3. **AC-4.8.3 - Selection snaps to whole days and always creates at least one day:** Given a Month or Year drag-create gesture is in progress, the preview range updates only at day-cell boundaries, never at sub-day resolution. Releasing on the start cell still creates a 1-day all-day event.
4. **AC-4.8.4 - Multi-day persistence uses exclusive all-day end semantics:** Given the selected drag range spans local dates `startDate` through `endDate` inclusive, the created draft is persisted as an all-day `pending_event` row with `StartDatetime = startDate 00:00`, `EndDatetime = endDate + 1 day 00:00`, `IsAllDay = true`, and no off-by-one day shift in any view.
5. **AC-4.8.5 - Creation reuses the Story 4.2 pending-draft path:** Given a Month/Year drag finishes, the created row follows the same draft contract as Story 4.2: no Google event ID yet, `CalendarId = "primary"`, placeholder title such as `New Event`, default color `azure`, `AppCreated = true`, `SourceSystem = "manual"`, `ReadyToPublish = false`, and UTC timestamps for `CreatedAt` / `UpdatedAt`.
6. **AC-4.8.6 - New draft opens immediately in the existing edit flow:** Given a Month/Year drag-created draft is persisted, the draft becomes the active selection and the details panel opens immediately in edit mode for that draft, using the same pending-event edit surface as Story 4.2 / 4.1.
7. **AC-4.8.7 - Draft appears immediately across visible calendar surfaces:** Given the draft is created successfully, it renders without restarting the app at 60% opacity in Month and Year view using the existing all-day rendering pipelines, and it also appears in Week or Day view when the visible range includes the created dates.
8. **AC-4.8.8 - Drag preview and cancellation are safe:** Given a drag-create gesture is in progress, the UI shows a visible preview highlight of the inclusive day range. Pressing `Esc` before release cancels the gesture and creates no row. If the pointer leaves the valid cell area temporarily, the preview remains anchored to the last valid day cell rather than creating an invalid range.
9. **AC-4.8.9 - Existing event interactions are preserved:** Given the user presses on an existing event banner, timed row, or Month view `+N more` affordance, the existing selection, tooltip, and popup behavior continues to work. Drag-create starts only from empty day backgrounds and does not reintroduce Year view day-background navigation removed in Story 3.9.
10. **AC-4.8.10 - Automated coverage exists for the new all-day creation contract:** Unit and integration coverage verifies inclusive-range normalization, exclusive-end persistence, immediate rendering of pending all-day drafts in Month/Year projections, and the cancel path creating no draft.

## Scope Boundaries

**IN SCOPE**
- Drag-to-create from Month view day-cell backgrounds
- Drag-to-create from Year view in-month day-cell backgrounds
- Multi-day all-day draft creation using the existing pending-event workflow from Story 4.2
- Inclusive date-range preview during drag
- Immediate post-create rendering in Month, Year, Week, and Day surfaces via the shared query pipeline

**OUT OF SCOPE**
- Week/Day timed drag-create changes from Story 4.2
- Tap-to-create fallback in Year view
- Editing the draft after the details panel opens beyond what Stories 4.1 and 4.3 already cover
- Push-to-Google, delete, batch actions, or recurring-event handling
- New calendar data stores or a second draft-persistence path

## Dev Notes

### Prerequisite: Story 4.2 Must Land First

Do **not** implement Story 4.8 on top of the current Google-ID-only contracts.

The live branch still has these pre-4.2 constraints:
- `Models/CalendarEventDisplayModel.cs` still exposes `GcalEventId` instead of a source-agnostic event identity.
- `Services/ICalendarQueryService.cs` still exposes `GetEventByGcalIdAsync(...)`.
- `Services/ICalendarSelectionService.cs` and `Messages/EventSelectedMessage.cs` are still Google-ID-only.
- `Data/Entities/PendingEvent.cs` and `Data/Configurations/PendingEventConfiguration.cs` still require non-null `GcalEventId`, which blocks pure local drafts.
- `Services/IPendingEventRepository.cs` / `PendingEventRepository.cs` still only support the edit-existing-event path keyed by `GcalEventId`.

Story 4.8 assumes Story 4.2 has already introduced:
- nullable `pending_event.GcalEventId` for new drafts
- source-agnostic event selection/query contracts
- lookup/delete by pending-draft ID for rows with no Google ID yet
- details-panel loading for pending-only drafts

If those prerequisites are still missing when implementation starts, finish Story 4.2 first rather than adding a second temporary creation path. [Source: `docs/epic-4/tech-spec.md` Risks `R2`, `R4`; `docs/epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md`]

### Current Repo Truth

- The real project is the flat repo-root layout, not the older `src/Core/Data` structure shown in `docs/architecture.md`.
- `Views/MonthViewControl.xaml.cs` already renders all-day event bars, timed rows, and the `+N more` popup in a custom per-week grid.
- `Views/YearViewControl.xaml.cs` already renders compact two-row all-day previews and intentionally keeps day-background tap as a no-op; only event banners are interactive.
- `Views/MonthViewLayoutPlanner.cs` and `Services/YearViewDayProjectionBuilder.cs` already encode the app's all-day inclusive/exclusive date rules and should be reused instead of duplicated.

### Recommended Interaction Model: Cell Range Tracking, Not Pixel Math

Do **not** treat Month/Year drag-create like Week/Day timed drag with free-form Y-coordinate math.

For Story 4.8, the stable model is:
1. Drag starts on a valid day-cell background.
2. The gesture stores `dragStartDate` and `currentHoverDate`.
3. Pointer enter / move updates the active end date to the current day cell.
4. Preview highlights every date in the inclusive normalized range.
5. Release finalizes that normalized date range into a single all-day pending draft.

This approach avoids fragile per-pixel hit testing in Year view's compact cells and makes the hard logic unit-testable as date-range normalization rather than UI geometry.

### Month View Guardrails

`MonthViewControl` currently has these interaction rules:
- `DayCellBackground_Tapped` sets `e.Handled = true`.
- `MonthGrid_Tapped` closes the overflow popup and clears selection.
- All-day blocks, timed rows, and popup rows already own tap selection.

Implementation guidance:
- Add drag-create with `PointerPressed` / `PointerMoved` / `PointerReleased` on empty day-cell backgrounds or a dedicated transparent hit layer inside each day cell.
- Keep background taps as a no-op for simple click release; drag-create should trigger only after pointer capture begins and a valid start cell exists.
- Do not break the `+N more` popup or existing timed/all-day event selection.
- If a drag starts while the popup is open, close the popup immediately so the pointer surface is unambiguous.

### Year View Guardrails

Story 3.9 deliberately changed Year view to a banner-only interaction model. Preserve that decision.

Implementation guidance for `Views/YearViewControl.xaml.cs`:
- Drag-create starts only from empty in-month day backgrounds, never from preview bars.
- Do not restore the old day-background tap-to-navigate behavior.
- Keep event-banner tap + tooltip behavior intact.
- Year view uses render/projection caches. Any transient drag preview must not permanently mutate cached render state or leak into reused month panels after cancellation or navigation.

Practical implication: keep preview state outside the long-lived cached display models, or ensure it is fully reset before a cached panel can be reused.

### All-Day Persistence Contract

When the gesture resolves, reuse the Story 4.2 draft-creation path and persist a single pending draft with:

```csharp
StartDatetime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
EndDatetime = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
IsAllDay = true;
ColorId = "azure";
```

Rules:
- The drag range is inclusive in the UI but stored with an exclusive end, matching existing all-day rendering rules in Month and Year surfaces.
- Do not apply local timezone offset math to all-day calendar dates when building the persisted UTC midnight boundaries; the query layer already treats all-day events as date-only for display.
- Releasing on a single cell creates a one-day all-day event where `EndDatetime = StartDatetime + 1 day`.

[Source: `docs/epic-4/tech-spec.md` Story 4.8 core flow; `Views/MonthViewLayoutPlanner.cs`; `Services/YearViewDayProjectionBuilder.cs`; `Services/CalendarQueryService.cs`]

### Rendering and Refresh Expectations

Do not add special-case rendering code just for Story 4.8 drafts.

Instead:
- Persist the all-day draft through the shared pending-event path.
- Let `CalendarQueryService` return the pending draft with `IsPending = true` and `Opacity = 0.6`.
- Let `MonthViewLayoutPlanner` and `YearViewDayProjectionBuilder` project that draft using the existing all-day layout rules.
- Reuse the same selection/details-panel open flow as Story 4.2 so the user lands in edit mode immediately after creation.

The draft must appear as:
- an all-day bar in Month view
- a single-day or multi-day preview bar in Year view, depending on span
- an all-day event in Week/Day views when those dates are visible later

### Suggested File Map

**Primary files likely to change**
- `Views/MonthViewControl.xaml.cs`
- `Views/YearViewControl.xaml.cs`
- a small shared helper for all-day drag range normalization if needed, preferably near the view code rather than in `MainViewModel`
- tests under `GoogleCalendarManagement.Tests/Unit/Views/` and `GoogleCalendarManagement.Tests/Unit/Services/`

**Files that should only change if Story 4.2 is not already merged**
- `Models/CalendarEventDisplayModel.cs`
- `Messages/EventSelectedMessage.cs`
- `Services/ICalendarSelectionService.cs`
- `Services/CalendarSelectionService.cs`
- `Services/ICalendarQueryService.cs`
- `Services/CalendarQueryService.cs`
- `Data/Entities/PendingEvent.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Services/IPendingEventRepository.cs`
- `Services/PendingEventRepository.cs`
- `ViewModels/EventDetailsPanelViewModel.cs`
- `ViewModels/MainViewModel.cs`

### Testing Requirements

Add automated coverage for the logic that is easy to regress:
- normalize `(startDate, endDate)` into an inclusive sorted range regardless of drag direction
- convert inclusive all-day range into exclusive persisted `EndDatetime`
- Month view layout shows a pending all-day draft in the expected visible track / overflow calculations
- Year view projection shows a pending all-day draft in the first or second preview lane based on its span
- cancel path creates no pending row

Manual verification is still required:
- Month view: drag one day, drag multiple days, drag across week boundary, verify `+N more` still works
- Year view: drag one day, drag multiple days, verify event-banner taps still select existing events
- Press `Esc` mid-drag and confirm no draft is created
- After create, confirm details panel opens in edit mode and the draft is translucent in all visible views

### References

- `docs/epic-4/tech-spec.md` - Story 4.8 core flow, Risks `R2` / `R4`, Test Strategy Summary
- `docs/epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md` - authoritative pending-draft creation path and source-agnostic contract dependency
- `docs/epic-3/stories/3-9-enhance-year-view-with-event-indicators-and-all-day-previews.md` - accepted Year view interaction model and all-day preview rules
- `docs/ux-design-specification.md` - Tier 2 event-creation and pending-state UX expectations
- `Views/MonthViewControl.xaml.cs`
- `Views/MonthViewLayoutPlanner.cs`
- `Views/YearViewControl.xaml.cs`
- `Services/YearViewDayProjectionBuilder.cs`
- `Services/CalendarQueryService.cs`

## Tasks / Subtasks

- [ ] **Task 1: Confirm Story 4.2 draft infrastructure is available** (AC: 4.8.4, 4.8.5, 4.8.6)
  - [ ] Verify pending drafts can exist without a Google event ID
  - [ ] Verify selection/query/details-panel contracts can address pending-only drafts
  - [ ] If those contracts are still missing, complete Story 4.2 first instead of adding a parallel path in Story 4.8

- [ ] **Task 2: Add a shared all-day drag range helper** (AC: 4.8.3, 4.8.4, 4.8.8, 4.8.10)
  - [ ] Normalize drag start/end dates into one inclusive date range
  - [ ] Convert inclusive UI range to exclusive persisted end date
  - [ ] Keep this logic unit-testable and independent from pointer pixel coordinates

- [ ] **Task 3: Implement Month view drag-to-create on empty day backgrounds** (AC: 4.8.1, 4.8.3, 4.8.8, 4.8.9)
  - [ ] Add pointer-based gesture handling without breaking event taps or `+N more`
  - [ ] Render a transient preview highlight for the active inclusive date range
  - [ ] Finalize via the shared all-day draft creation path

- [ ] **Task 4: Implement Year view drag-to-create on in-month background cells** (AC: 4.8.2, 4.8.3, 4.8.8, 4.8.9)
  - [ ] Add empty-cell pointer handling that preserves banner-only event interaction
  - [ ] Ensure preview state does not poison Year view render/projection caches
  - [ ] Finalize via the shared all-day draft creation path

- [ ] **Task 5: Open the created draft in the existing edit flow** (AC: 4.8.5, 4.8.6)
  - [ ] Persist the draft through the Story 4.2 pending-event creation contract
  - [ ] Select the new pending draft
  - [ ] Open the details panel directly in edit mode for that draft

- [ ] **Task 6: Refresh all-day rendering surfaces after create** (AC: 4.8.7)
  - [ ] Ensure Month view shows the pending draft at 60% opacity
  - [ ] Ensure Year view projection picks up the new all-day draft correctly
  - [ ] Ensure later Week/Day navigation shows the same draft as an all-day pending event

- [ ] **Task 7: Add automated coverage and verify manually** (AC: 4.8.10)
  - [ ] Unit tests for date-range normalization and exclusive-end persistence
  - [ ] Unit/integration tests for Month/Year pending all-day rendering
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual verification for Month drag, Year drag, cancel, and post-create edit-mode open

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

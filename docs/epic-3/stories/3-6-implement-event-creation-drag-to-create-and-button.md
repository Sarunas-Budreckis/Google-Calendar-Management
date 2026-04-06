# Story 3.6: Implement Event Creation (Drag-to-Create and Button)

Status: moved

> **Moved to Epic 4.** This story has been relocated to Epic 4 (Event Editing — Tier 2) as it is out of scope for Tier 1.
> See: [docs/epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md](../../epic-4/stories/4-2-implement-event-creation-drag-to-create-and-button.md)

## Story

As a **user**,
I want **to create new events directly on the calendar by dragging in week/day view or by using a toolbar button**,
so that **I can capture local-only events immediately and refine them in the same details/editing flow used for existing events**.

## Acceptance Criteria

1. **AC-3.6.1 - Drag creates a timed local draft:** Given the user is in Week or Day view and drags on empty timeline space, a new local-only event draft is created for the snapped drag range when the pointer is released.
2. **AC-3.6.2 - Drag snaps to 15-minute increments:** Given the user drags to create, both the start and end of the created event snap to 15-minute boundaries, and a drag shorter than one slot still produces at least one 15-minute block.
3. **AC-3.6.3 - New draft opens in the details/edit flow:** Given a new event is created by drag or button, the event becomes the active selection and the right-side details panel opens immediately in edit mode for that newly created draft.
4. **AC-3.6.4 - Toolbar button creates from chosen date/time:** Given the user clicks `+ Add Event`, a lightweight date/time prompt opens with start time prefilled to the current local time rounded to the nearest 15 minutes and end time defaulted to one hour later; confirming creates the new draft and opens it in the details/edit flow.
5. **AC-3.6.5 - Drafts are persisted as pending local events:** Given a new event is created, it is persisted as a `pending_event` row with a generated pending ID, no Google event ID yet, `app_created = true`, `source_system = "manual"`, and `ready_to_publish = false`.
6. **AC-3.6.6 - Drafts appear visually distinct immediately:** Given a new draft exists in the visible range, it appears in Week/Day/Month views immediately using the default Azure colour and 60% opacity with a visible local-only status such as `Not yet published to Google Calendar`.
7. **AC-3.6.7 - Creation can be cancelled safely:** Given the user starts a drag but presses `Esc`, releases outside the active day column, or otherwise cancels before completion, no record is created and no phantom preview remains on screen.
8. **AC-3.6.8 - Existing interactions are preserved:** Given the user clicks an existing Google-synced event, current selection, tooltip, navigation, and details-panel behavior continue to work; creation logic does not steal input from existing event blocks.
9. **AC-3.6.9 - Shared calendar queries include pending drafts:** Given the visible date range contains both synced `gcal_event` rows and local `pending_event` rows, the shared query/display pipeline returns both, ordered correctly by start time, without duplicating or dropping either source.
10. **AC-3.6.10 - Automation coverage exists for the new contract:** Unit/integration tests cover pending-event persistence, 15-minute snapping math, and range queries that mix synced and pending events.

## Scope Boundaries

**IN SCOPE - this story:**
- New-event creation from Week/Day drag gestures
- New-event creation from a `+ Add Event` toolbar button
- Tier 2 `pending_event` EF entity/configuration/migration and repository path
- Shared calendar event identity/query contract widening so pending drafts can use the same selection/details surface as Google events
- Immediate rendering of pending drafts in the visible calendar range
- Opening the existing details/edit panel on the newly created draft

**OUT OF SCOPE - do NOT implement:**
- Publishing pending events to Google Calendar (Epic 7)
- Multi-select or drag-select batch workflows
- Event deletion
- Month-view drag creation
- Year-view creation
- Colour picking beyond default Azure (Story 3.7)
- A second dedicated editor surface for new events

## Dev Notes

### Current Repo Truth

- The live codebase currently contains Story `3.1` only. Stories `3.3`, `3.4`, and `3.5` exist as drafted/contexted docs but their code has not landed yet.
- The project is flat at the repo root: `Views/`, `ViewModels/`, `Services/`, `Models/`, `Messages/`, and `Data/` are direct children. Do **not** create `Core/`, `src/`, or a second UI assembly.
- `WeekViewControl` and `DayViewControl` build timed event blocks imperatively in code-behind today. There are no reusable `EventChip` / `EventBlock` controls to hook drag behavior into.

### Critical Schema Correction: New Drafts Cannot Live in `gcal_event`

Do **not** try to persist newly created local-only events in `gcal_event`.

The current EF mapping makes that unsafe and semantically wrong:

- [Data/Entities/GcalEvent.cs](../../../Data/Entities/GcalEvent.cs) uses `string GcalEventId`
- [Data/Configurations/GcalEventConfiguration.cs](../../../Data/Configurations/GcalEventConfiguration.cs) maps `gcal_event_id` as the **non-null primary key**
- `gcal_event` is the synced Google cache and is already tied to version-history rows

That means Story 3.6 must introduce the planned Tier 2 `pending_event` path from:

- [docs/_database-schemas.md](../../_database-schemas.md)
- [docs/architecture.md](../../architecture.md)

Use a dedicated `PendingEvent` entity and table for new unpublished drafts. A newly created event has **no Google event ID yet**; do not fake one just to fit the Tier 1 table.

### Critical Contract Correction: Shared Event Identity Must Stop Being Google-Only

Current Story 3.1 contracts are too narrow for Tier 2:

- `CalendarEventDisplayModel` exposes `GcalEventId`
- `ICalendarQueryService` exposes `GetEventByGcalIdAsync(...)`
- `ICalendarSelectionService` exposes `SelectedGcalEventId`
- `EventSelectedMessage` carries `GcalEventId`

That naming works only for synced Google rows. Story 3.6 must widen the shared contract before adding creation UI, otherwise the codebase will fork into incompatible Google-event and pending-event paths.

Recommended minimal correction:

```csharp
public enum CalendarEventSourceKind
{
    Google,
    Pending
}

public sealed record CalendarEventDisplayModel(
    string EventId,
    CalendarEventSourceKind SourceKind,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,
    DateTime EndLocal,
    bool IsAllDay,
    string ColorHex,
    bool IsRecurringInstance,
    string? Description,
    DateTime? LastSyncedAt,
    bool IsPending,
    double Opacity,
    string StatusLabel);
```

And corresponding contract widening:

- `ICalendarQueryService.GetEventByIdAsync(string eventId, ...)`
- `ICalendarSelectionService.SelectedEventId`
- `EventSelectedMessage(string? EventId)`

Because Stories `3.3`-`3.5` are not implemented yet, **now** is the correct time to make that shared rename/refactor instead of building pending-event exceptions around Google-only names.

### Pending Event Persistence

Add the Tier 2 persistence path that the planning docs already describe:

```csharp
public class PendingEvent
{
    public string PendingEventId { get; set; } = "";
    public string CalendarId { get; set; } = "primary";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public bool AppCreated { get; set; } = true;
    public string? SourceSystem { get; set; } = "manual";
    public bool ReadyToPublish { get; set; }
    public DateTime? PublishAttemptedAt { get; set; }
    public string? PublishError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Required files:

- `Data/Entities/PendingEvent.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Data/CalendarDbContext.cs` (`DbSet<PendingEvent>`)
- new EF Core migration for `pending_event`
- `Services/IPendingEventRepository.cs`
- `Services/PendingEventRepository.cs`

ID format: use the planned `pending_{8-char-hex}` pattern, for example `pending_a1b2c3d4`.

For Story 3.6 creations:

- `CalendarId = "primary"` unless a shared `default_calendar_id` config value is already introduced by the time implementation starts
- `ColorId = "azure"` (or the canonical Azure key used by Story 3.2)
- `AppCreated = true`
- `SourceSystem = "manual"`
- `ReadyToPublish = false`
- `CreatedAt` / `UpdatedAt` are UTC

### Shared Query Path Must Union Synced and Pending Events

Do not add a separate ad hoc query path just for creation.

Extend the shared calendar query service so range loads and detail loads can handle both sources:

- keep `IGcalEventRepository` for synced rows
- add `IPendingEventRepository`
- extend `CalendarQueryService` so `GetEventsForRangeAsync(...)` returns the union of `gcal_event` + `pending_event`
- expose a source-agnostic detail lookup (`GetEventByIdAsync`)

Pending drafts should map to the shared display model with:

- `SourceKind = Pending`
- `IsPending = true`
- `Opacity = 0.6`
- `StatusLabel = "Not yet published to Google Calendar"`
- `LastSyncedAt = null`

This is required for Month/Week/Day rendering and for the details/edit panel to open on a newly created draft.

### Story 3.5 Dependency: Reuse the Existing Details/Edit Surface, But Widen It

Story `3.6` depends on the panel work from Stories `3.4` and `3.5`, but those story docs currently assume Google-only events.

Before creation is considered done:

- widen the planned `EventDetailsPanelViewModel` and its save path so it can edit either a `GcalEvent` or a `PendingEvent`
- create the pending record **first**, then select it, then open the panel in edit mode
- do **not** open a blank panel and wait to decide where to save later

If Story `3.5` lands first with an `EventUpdatedMessage`, widen that message to a source-agnostic event-change message rather than adding a second creation-only message path.

### Week/Day Layout Reality: Current Timed Rendering Is Hour-Granular Only

`WeekViewControl` and `DayViewControl` currently place timed events with:

- `Grid.SetRow(..., StartLocal.Hour)`
- `Grid.SetRowSpan(..., Ceil(totalHours))`

That is not precise enough for Story 3.6 because creation must snap to 15-minute increments.

Do not try to bolt drag creation onto the existing hour-only math and call it done.

Recommended implementation direction:

- keep the hour grid as the visible background
- render timed events and drag previews on a foreground `Canvas` or equivalent overlay per day column
- use shared layout metrics:
  - `HourSlotHeight = 72`
  - `QuarterHourHeight = 18`
- compute `Top` and `Height` from local start/end minute offsets

This lets the created draft render at quarter-hour precision immediately without forcing a 96-row grid rebuild.

### Drag Interaction Guardrails

Drag-to-create is **Week and Day view only**.

Implementation expectations:

- drag must start only on empty timeline space, not on an existing event border
- use `PointerPressed`, `PointerMoved`, `PointerReleased`, and pointer capture on the day-column hit target
- show a transient preview rectangle while dragging
- snap preview bounds to the 15-minute grid during movement
- releasing finalizes creation
- `Esc` or abort clears preview without inserting data

Do not put drag logic in `MainViewModel`; it is a view interaction concern. Translate the completed drag into a repository/service call only when the gesture resolves.

### `+ Add Event` Button Flow

Add the button to the existing `MainPage` toolbar.

Recommended UX for the current repo:

- use a lightweight `ContentDialog`
- fields: `DatePicker`, `TimePicker` for start, `TimePicker` for end
- defaults:
  - date = current visible date if Day view, current week/day context if Week view, otherwise today
  - start = current local time rounded to nearest 15 minutes
  - end = start + 1 hour

On confirm:

1. create the pending record
2. refresh the visible range through the shared query path
3. select the new event
4. open the details panel in edit mode

### Visual Rendering Rules

Pending events must be visually distinct everywhere they appear:

- default colour = Azure
- opacity = `0.6`
- visible status text or panel label = `Not yet published to Google Calendar`

Do not special-case this only in one view. The distinction belongs in the shared display model and rendering pipeline.

### File Map

**Files to modify**

- `App.xaml.cs`
- `Data/CalendarDbContext.cs`
- `Models/CalendarEventDisplayModel.cs`
- `Services/ICalendarQueryService.cs`
- `Services/CalendarQueryService.cs`
- `Services/ICalendarSelectionService.cs`
- `Services/CalendarSelectionService.cs`
- `Messages/EventSelectedMessage.cs`
- `ViewModels/MainViewModel.cs`
- `Views/MainPage.xaml`
- `Views/MainPage.xaml.cs`
- `Views/WeekViewControl.xaml`
- `Views/WeekViewControl.xaml.cs`
- `Views/DayViewControl.xaml`
- `Views/DayViewControl.xaml.cs`
- `Views/MonthViewControl.xaml.cs`

**Files to create**

- `Data/Entities/PendingEvent.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Services/IPendingEventRepository.cs`
- `Services/PendingEventRepository.cs`
- EF Core migration adding `pending_event`
- tests for pending-event persistence/query/snapping

### Test Guidance

Add automated coverage for the hard parts:

- pending-event repository insert/reload
- shared query service union of `gcal_event` and `pending_event`
- drag-slot snapping helper
- cancellation path creates nothing
- button defaults round correctly to a 15-minute boundary
- newly created pending event reappears in the visible range with `Opacity = 0.6`

Manual verification still required:

- drag in Day view
- drag in Week view
- `+ Add Event` flow
- immediate panel open on the new draft
- `Esc` cancel during drag

### References

- [docs/epics.md](../../epics.md)
- [docs/epic-3/tech-spec.md](../tech-spec.md)
- [docs/ux-design-specification.md](../../ux-design-specification.md)
- [docs/tier-2-requirements.md](../../tier-2-requirements.md)
- [docs/_database-schemas.md](../../_database-schemas.md)
- [docs/architecture.md](../../architecture.md)
- [docs/_documentation-alignment-summary.md](../../_documentation-alignment-summary.md)
- [docs/epic-3/stories/3-4-create-event-details-panel-read-only-for-phase-1.md](3-4-create-event-details-panel-read-only-for-phase-1.md)
- [docs/epic-3/stories/3-5-implement-event-editing-panel-phase-2.md](3-5-implement-event-editing-panel-phase-2.md)
- [App.xaml.cs](../../../App.xaml.cs)
- [Data/CalendarDbContext.cs](../../../Data/CalendarDbContext.cs)
- [Data/Entities/GcalEvent.cs](../../../Data/Entities/GcalEvent.cs)
- [Data/Configurations/GcalEventConfiguration.cs](../../../Data/Configurations/GcalEventConfiguration.cs)
- [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs)
- [Services/ICalendarQueryService.cs](../../../Services/ICalendarQueryService.cs)
- [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs)
- [Services/ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs)
- [Services/CalendarSelectionService.cs](../../../Services/CalendarSelectionService.cs)
- [Messages/EventSelectedMessage.cs](../../../Messages/EventSelectedMessage.cs)
- [Views/MainPage.xaml](../../../Views/MainPage.xaml)
- [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs)
- [Views/WeekViewControl.xaml.cs](../../../Views/WeekViewControl.xaml.cs)
- [Views/DayViewControl.xaml.cs](../../../Views/DayViewControl.xaml.cs)
- [Views/MonthViewControl.xaml.cs](../../../Views/MonthViewControl.xaml.cs)

## Tasks / Subtasks

- [ ] **Task 1: Add Tier 2 pending-event persistence** (AC: 3.6.5, 3.6.10)
  - [ ] Create `PendingEvent` entity and EF configuration
  - [ ] Add `DbSet<PendingEvent>` to `CalendarDbContext`
  - [ ] Create EF migration for `pending_event`
  - [ ] Add `IPendingEventRepository` / `PendingEventRepository`

- [ ] **Task 2: Widen shared calendar event identity contracts** (AC: 3.6.3, 3.6.6, 3.6.9)
  - [ ] Rename Google-only selection/query contracts to source-agnostic event identity names
  - [ ] Extend `CalendarEventDisplayModel` with pending/source metadata and opacity
  - [ ] Update `EventSelectedMessage` payload naming accordingly

- [ ] **Task 3: Extend shared query/display pipeline to include pending drafts** (AC: 3.6.5, 3.6.6, 3.6.9)
  - [ ] Union `gcal_event` and `pending_event` in `CalendarQueryService.GetEventsForRangeAsync(...)`
  - [ ] Add source-agnostic `GetEventByIdAsync(...)`
  - [ ] Ensure pending drafts sort correctly with synced events

- [ ] **Task 4: Widen the planned details/edit panel path for pending events** (AC: 3.6.3)
  - [ ] Update the Story 3.4 / 3.5 implementation path so the panel can load and save either event source
  - [ ] Ensure a newly created pending event opens directly in edit mode

- [ ] **Task 5: Implement drag-to-create in `DayViewControl`** (AC: 3.6.1, 3.6.2, 3.6.7, 3.6.8)
  - [ ] Add empty-space hit target and drag preview layer
  - [ ] Snap drag bounds to 15-minute increments
  - [ ] Abort cleanly on cancel

- [ ] **Task 6: Implement drag-to-create in `WeekViewControl`** (AC: 3.6.1, 3.6.2, 3.6.7, 3.6.8)
  - [ ] Add per-day drag hit target and preview layer
  - [ ] Share snapping/layout math with Day view instead of duplicating it

- [ ] **Task 7: Add `+ Add Event` button flow in `MainPage`** (AC: 3.6.3, 3.6.4)
  - [ ] Add toolbar button
  - [ ] Create date/time prompt with rounded defaults
  - [ ] On confirm, create pending event, refresh, select it, and open the panel

- [ ] **Task 8: Render pending drafts distinctly in shared views** (AC: 3.6.6, 3.6.9)
  - [ ] Apply `Opacity = 0.6` consistently
  - [ ] Keep Azure as the default colour
  - [ ] Surface local-only status in the panel and any shared rendering hooks

- [ ] **Task 9: Add tests** (AC: 3.6.10)
  - [ ] Pending-event repository integration tests
  - [ ] Calendar query union tests
  - [ ] Time-slot snapping tests
  - [ ] Button default rounding tests

- [ ] **Task 10: Validate locally**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual verification for drag creation, button creation, cancel path, and immediate panel open

## Dev Agent Record

### Context Reference

- [Story Context XML](3-6-implement-event-creation-drag-to-create-and-button.context.xml)

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

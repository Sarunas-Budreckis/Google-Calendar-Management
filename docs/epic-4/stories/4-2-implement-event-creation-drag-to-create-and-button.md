# Story 4.2: Implement Event Creation (Drag-to-Create and Button)

Status: ready-for-dev

> **Moved from Epic 3:** This story was originally Story 3.6. It has been moved to Epic 4 (Event Editing — Tier 2) as event creation is out of scope for Tier 1 (read-only). All content below is unchanged from the original 3.6 story file.

## Story

As a **user**,
I want **to create new events directly on the calendar by dragging in week/day view or by using a toolbar button**,
so that **I can capture local-only events immediately and refine them in the same details/editing flow used for existing events**.

## Acceptance Criteria

1. **AC-4.2.1 - Drag creates a timed local draft:** Given the user is in Week or Day view and drags on empty timeline space, a new local-only event draft is created for the snapped drag range when the pointer is released.
2. **AC-4.2.2 - Drag snaps to 15-minute increments:** Given the user drags to create, both the start and end of the created event snap to 15-minute boundaries, and a drag shorter than one slot still produces at least one 15-minute block.
3. **AC-4.2.3 - New draft opens in the details/edit flow:** Given a new event is created by drag or button, the event becomes the active selection and the right-side details panel opens immediately in edit mode for that newly created draft.
4. **AC-4.2.4 - Toolbar button creates from chosen date/time:** Given the user clicks `+ Add Event`, a lightweight date/time prompt opens with start time prefilled to the current local time rounded to the nearest 15 minutes and end time defaulted to one hour later; confirming creates the new draft and opens it in the details/edit flow.
5. **AC-4.2.5 - Drafts are persisted as pending local events:** Given a new event is created, it is persisted as a `pending_event` row with a generated pending ID, no Google event ID yet, `app_created = true`, `source_system = "manual"`, and `ready_to_publish = false`.
6. **AC-4.2.6 - Drafts appear visually distinct immediately:** Given a new draft exists in the visible range, it appears in Week/Day/Month views immediately using the default Azure colour and 60% opacity with a visible local-only status such as `Not yet published to Google Calendar`.
7. **AC-4.2.7 - Creation can be cancelled safely:** Given the user starts a drag but presses `Esc`, releases outside the active day column, or otherwise cancels before completion, no record is created and no phantom preview remains on screen.
8. **AC-4.2.8 - Existing interactions are preserved:** Given the user clicks an existing Google-synced event, current selection, tooltip, navigation, and details-panel behavior continue to work; creation logic does not steal input from existing event blocks.
9. **AC-4.2.9 - Shared calendar queries include pending drafts:** Given the visible date range contains both synced `gcal_event` rows and local `pending_event` rows, the shared query/display pipeline returns both, ordered correctly by start time, without duplicating or dropping either source.
10. **AC-4.2.10 - Automation coverage exists for the new contract:** Unit/integration tests cover pending-event persistence, 15-minute snapping math, and range queries that mix synced and pending events.

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
- Colour picking beyond default Azure (Story 4.3)
- A second dedicated editor surface for new events

## Dev Notes

### Current Repo Truth

- The project is flat at the repo root: `Views/`, `ViewModels/`, `Services/`, `Models/`, `Messages/`, and `Data/` are direct children. Do **not** create `Core/`, `src/`, or a second UI assembly.

### Critical Schema Correction: New Drafts Cannot Live in `gcal_event`

Do **not** try to persist newly created local-only events in `gcal_event`. Use a dedicated `PendingEvent` entity and table.

The current EF mapping makes that unsafe:
- `Data/Configurations/GcalEventConfiguration.cs` maps `gcal_event_id` as the **non-null primary key**
- `gcal_event` is the synced Google cache and is already tied to version-history rows

### Critical Contract Correction: Shared Event Identity Must Stop Being Google-Only

Current Story 3.1 contracts are too narrow for Tier 2:
- `CalendarEventDisplayModel` exposes `GcalEventId`
- `ICalendarQueryService` exposes `GetEventByGcalIdAsync(...)`
- `ICalendarSelectionService` exposes `SelectedGcalEventId`
- `EventSelectedMessage` carries `GcalEventId`

Story 4.2 must widen these contracts before adding creation UI:

```csharp
public enum CalendarEventSourceKind { Google, Pending }

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

### Pending Event Persistence

```csharp
public class PendingEvent
{
    public string PendingEventId { get; set; } = "";   // e.g. "pending_a1b2c3d4"
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Required files:
- `Data/Entities/PendingEvent.cs`
- `Data/Configurations/PendingEventConfiguration.cs`
- `Data/CalendarDbContext.cs` (add `DbSet<PendingEvent>`)
- New EF Core migration
- `Services/IPendingEventRepository.cs` / `Services/PendingEventRepository.cs`

### Shared Query Path Must Union Synced and Pending Events

Extend `CalendarQueryService` so `GetEventsForRangeAsync(...)` returns the union of `gcal_event` + `pending_event`. Add source-agnostic `GetEventByIdAsync(...)`.

Pending drafts map to display model with:
- `SourceKind = Pending`, `IsPending = true`, `Opacity = 0.6`
- `StatusLabel = "Not yet published to Google Calendar"`, `LastSyncedAt = null`

### Week/Day Layout: 15-Minute Precision

`WeekViewControl` and `DayViewControl` currently use hour-granular row math. For 15-minute snap precision:
- Keep hour grid as visible background
- Render drafts on a foreground `Canvas` overlay per day column
- Use `HourSlotHeight = 72`, `QuarterHourHeight = 18`
- Compute `Top` and `Height` from local start/end minute offsets

### Drag Interaction Guardrails

- Drag starts only on empty timeline space, not on existing event borders
- Use `PointerPressed`, `PointerMoved`, `PointerReleased` with pointer capture
- Show transient preview rectangle while dragging, snapped to 15-minute grid
- `Esc` clears preview without inserting data
- Do not put drag logic in `MainViewModel`

### `+ Add Event` Button Flow

Use a lightweight `ContentDialog` with `DatePicker` + `TimePicker` for start and `TimePicker` for end. Defaults: current date, current local time rounded to nearest 15 min, end = start + 1 hour.

On confirm:
1. Create pending record
2. Refresh visible range via shared query path
3. Select new event
4. Open details panel in edit mode

---

## Tasks / Subtasks

- [ ] **Task 1: Add Tier 2 pending-event persistence** (AC: 4.2.5, 4.2.10)
  - [ ] Create `PendingEvent` entity and EF configuration
  - [ ] Add `DbSet<PendingEvent>` to `CalendarDbContext`
  - [ ] Create EF migration for `pending_event`
  - [ ] Add `IPendingEventRepository` / `PendingEventRepository`

- [ ] **Task 2: Widen shared calendar event identity contracts** (AC: 4.2.3, 4.2.6, 4.2.9)
  - [ ] Rename Google-only contracts to source-agnostic event identity names
  - [ ] Extend `CalendarEventDisplayModel` with pending/source metadata and opacity
  - [ ] Update `EventSelectedMessage` payload

- [ ] **Task 3: Extend shared query/display pipeline** (AC: 4.2.5, 4.2.6, 4.2.9)
  - [ ] Union `gcal_event` and `pending_event` in `CalendarQueryService.GetEventsForRangeAsync(...)`
  - [ ] Add source-agnostic `GetEventByIdAsync(...)`

- [ ] **Task 4: Widen details/edit panel for pending events** (AC: 4.2.3)
  - [ ] Update Story 4.1 panel so it can load and save either event source

- [ ] **Task 5: Implement drag-to-create in `DayViewControl`** (AC: 4.2.1, 4.2.2, 4.2.7, 4.2.8)

- [ ] **Task 6: Implement drag-to-create in `WeekViewControl`** (AC: 4.2.1, 4.2.2, 4.2.7, 4.2.8)

- [ ] **Task 7: Add `+ Add Event` button flow in `MainPage`** (AC: 4.2.3, 4.2.4)

- [ ] **Task 8: Render pending drafts distinctly** (AC: 4.2.6, 4.2.9)

- [ ] **Task 9: Add tests** (AC: 4.2.10)
  - [ ] Pending-event repository, calendar query union, time-slot snapping, button defaults

- [ ] **Task 10: Validate locally**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: drag creation, button creation, cancel path, panel open on new draft

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

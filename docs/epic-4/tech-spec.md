# Epic Technical Specification: Event Editing — Tier 2

Date: 2026-04-19
Author: Sarunas Budreckis
Epic ID: epic-4
Tier: 2 (Editing & Publishing)
Status: Draft

---

## Overview

Epic 4 delivers the Tier 2 editing surface for Google Calendar Management: the ability to create new local-only events, edit existing synced events, assign colors, and stage all changes as pending drafts before they are pushed to Google Calendar. It builds directly on the read-only infrastructure (views, selection service, details panel, query service) established in Epic 3, and introduces the `pending_event` table as the Tier 2 schema addition defined in the database specification.

The deliverable at the close of Epic 4 is an application where the user can:

- Click an existing event and edit its title, time, and description in-place
- Assign any of the 9 canonical colors to an event via a fixed-palette picker
- Drag on a Week or Day view to create a brand-new local-only event draft
- Tap "+ Add Event" to open a dialog and create a draft at a chosen time
- See pending (unshared) events rendered at 60% opacity across all four calendar views
- Revert any pending draft back to its last-synced state
- Select multiple events and apply batch operations (color, delete)
- Drag existing timed events to reschedule them in Week or Day view
- Drag in Year view to create events
- Delete local drafts and published events (with confirmation)
- Batch-publish all pending events to Google Calendar in one action
- Edit single instances of recurring events

**Story status at spec creation:**

| Story | Title | Status |
|---|---|---|
| 4.1 | Implement Event Editing Panel | review (implemented) |
| 4.2 | Implement Event Creation (Drag-to-Create and Button) | ready-for-dev |
| 4.3 | Implement Color Picker for Event Colors | ready-for-dev |
| 4.4 | Push Pending Events to Google Calendar | backlog |
| 4.5 | Event Deletion (Local Drafts and Published Events) | backlog |
| 4.6 | Multi-Select and Batch Operations | backlog |
| 4.7 | Drag-to-Reschedule Existing Events | backlog |
| 4.8 | Year View Drag-to-Create | backlog |
| 4.9 | Recurring Event Editing (Single Instance) | backlog |

---

## Objectives and Scope

**In Scope (Tier 2 — this epic):**

- `pending_event` EF Core entity, configuration, migration, and `IPendingEventRepository`
- Edit mode in `EventDetailsPanelControl`: title, start/end date/time pickers, description
- 500 ms debounced auto-save + explicit Save button; single-level Undo (Ctrl+Z)
- Revert pending changes from both read-only and edit modes
- 60% opacity rendering for all event blocks that have a pending row (all four views)
- `CalendarEventDisplayModel` widened to carry `IsPending`, `Opacity`, `SourceKind`, `StatusLabel`
- `ICalendarQueryService` widened: `GetEventsForRangeAsync` unions `gcal_event` + `pending_event`; source-agnostic `GetEventByIdAsync`
- Drag-to-create interaction in `WeekViewControl` and `DayViewControl` (15-minute snap)
- `+ Add Event` toolbar button with `ContentDialog` for date/time selection
- New-event drafts immediately rendered in Week, Day, and Month views at Azure + 60% opacity
- Details/edit panel opened automatically on the newly created draft
- Fixed-palette color picker (`Flyout`, 2×6 swatch grid) in edit mode
- Immediate color save bypassing the 500 ms text-edit debounce
- Version history snapshot (`gcal_event_version`) written before each color overwrite
- `EventUpdatedMessage` published after every save; `MainViewModel` refreshes the affected display model only
- Push pending events to Google Calendar (batch publish, confirmation dialog, opacity transition)
- Event deletion — local drafts (immediate) and published events (confirm + GCal delete API call)
- Multi-select (Shift+click, drag-select) with selection counter badge and batch color/delete operations
- Drag-to-reschedule existing timed events in Week and Day views
- Year-view drag-to-create
- Recurring event editing — single instance only; "edit this event" vs. "edit all events" prompt

**Out of Scope (future epics):**

- Arbitrary RGB/HSL color picking
- Editing the color taxonomy itself
- Recurring series editing (all instances or this-and-following)

**Dependencies:**

- **Prerequisite:** Epic 3 complete — `EventDetailsPanelControl`, `EventDetailsPanelViewModel`, `ICalendarQueryService`, `ICalendarSelectionService`, `CalendarEventDisplayModel`, all four view controls, `ColorMappingService`, `WeakReferenceMessenger` messaging infrastructure
- **Prerequisite:** Epic 1 — `CalendarDbContext`, `IDbContextFactory`, Serilog, `config` table
- **Prerequisite:** Epic 2 — `gcal_event` table populated; `ISyncStatusService`; `IGoogleCalendarApiService` (write path needed for Push to GCal and deletion)

---

## System Architecture Alignment

Epic 4 extends the flat WinUI 3 project structure. There is no `Core/` hierarchy — all services live in `Services/`, ViewModels in `ViewModels/`, views in `Views/`.

| Concern | Component |
|---|---|
| Local edit staging | `pending_event` table → `PendingEvent` EF entity → `IPendingEventRepository` |
| Edit mode UI | `EventDetailsPanelControl.xaml` extended with `VisualStateManager` ReadOnly/EditMode states |
| Edit mode logic | `EventDetailsPanelViewModel` extended with edit fields, debounce timer, save/revert commands |
| Cross-view opacity update | `EventUpdatedMessage` → `MainViewModel` → all four view controls |
| New event creation | `DayViewControl` + `WeekViewControl` (drag); `MainPage` `+ Add Event` button |
| Shared query pipeline | `CalendarQueryService.GetEventsForRangeAsync` (unioned) |
| Color taxonomy | `IColorMappingService` extended with `PickerColors`, `GetDisplayName`, `NormalizeColorKey` |
| Color picker UI | `Flyout` with 2×6 swatch grid in `EventDetailsPanelControl` edit mode |
| Version history | `gcal_event_version` snapshot written at Push time (Story 4.4) before overwriting `gcal_event` |
| Pending deletion | `pending_event` rows with `operation_type = 'delete'`; visible in push list; on push → row moves to `deleted_event` table |
| Recurring series | New `recurring_event_series` table; `gcal_event` instances FK to it via existing `recurring_event_id` field |

**Layering constraint (same as prior epics):** All persistence logic uses `IDbContextFactory<CalendarDbContext>` (singleton services cannot hold scoped `DbContext`). All timestamps are stored UTC. `gcal_event` queries do not need `is_deleted` guards — deleted events live in `deleted_event` table. All public async I/O methods are suffixed `Async`.

---

## Schema Changes

### New Table: `pending_event` (Tier 2 addition)

Defined in `_database-schemas.md` §3B. Introduced by Story 4.1's migration (`AddPendingEventTable`) and extended by Story 4.2 (add Tier 2 lifecycle fields).

**Canonical entity after Story 4.2 migration:**

```csharp
// Data/Entities/PendingEvent.cs
public class PendingEvent
{
    public Guid Id { get; set; }              // PK (GUID — Story 4.1)

    // Link to the originating synced event (null for brand-new drafts — Story 4.2)
    public string? GcalEventId { get; set; }

    // Calendar target (Story 4.2)
    public string CalendarId { get; set; } = "primary";

    // Event fields (both edit-of-existing and brand-new paths)
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }   // UTC; nullable for in-progress new drafts
    public DateTime? EndDatetime { get; set; }     // UTC
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }            // canonical key (azure, purple, …)

    // Ownership / workflow (Story 4.2)
    public bool AppCreated { get; set; } = true;
    public string SourceSystem { get; set; } = "manual";
    public bool ReadyToPublish { get; set; } = false;
    public DateTime? PublishAttemptedAt { get; set; }
    public string? PublishError { get; set; }

    // Operation type (Story 4.5) — 'edit' (default) or 'delete'
    public string OperationType { get; set; } = "edit";

    // Recurring series fields (Story 4.9)
    public string? RecurrenceEditScope { get; set; }    // 'single_instance' | 'this_and_following' | 'all_events'
    public string? SeriesMasterGcalEventId { get; set; } // master event ID for 'all_events' push
    public DateTime? OriginalStartTime { get; set; }    // for 'this_and_following' UNTIL calculation

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public GcalEvent? GcalEvent { get; set; }
}
```

**EF configuration key points (`Data/Configurations/PendingEventConfiguration.cs`):**

```csharp
entity.HasIndex(e => e.GcalEventId);                    // fast lookup for edit path
entity.HasIndex(e => new { e.StartDatetime, e.EndDatetime });  // date range queries
entity.HasIndex(e => e.ReadyToPublish);                 // future Push-to-GCal query

entity.HasOne(e => e.GcalEvent)
      .WithMany()
      .HasForeignKey(e => e.GcalEventId)
      .HasPrincipalKey(e => e.GcalEventId)     // gcal_event's string PK
      .OnDelete(DeleteBehavior.Restrict);       // no cascade (preserve drafts)
```

**Migration sequence:**

- Story 4.1: `AddPendingEventTable` — creates table with Story 4.1 fields
- Story 4.2: `ExtendPendingEventForCreation` — adds `CalendarId`, `AppCreated`, `SourceSystem`, `ReadyToPublish`, `PublishAttemptedAt`, `PublishError`; makes `GcalEventId` nullable with partial unique index
- Story 4.5: `AddPendingEventOperationType` — adds `OperationType TEXT DEFAULT 'edit'`; creates `deleted_event` table; creates `recurring_event_series` table
- Story 4.9: `AddPendingEventRecurringFields` — adds `RecurrenceEditScope`, `SeriesMasterGcalEventId`, `OriginalStartTime`

**No changes to `gcal_event` schema in Epic 4.** The schema spec's `is_deleted` column is superseded by the `deleted_event` table — never add `is_deleted` to `gcal_event`. The `app_last_modified_at` column is not needed in `gcal_event` since color changes now write to `pending_event`.

---

### New Table: `deleted_event` (Story 4.5)

Holds soft-deleted events. `gcal_event` rows are moved here (not updated) when deletion is confirmed.

```sql
CREATE TABLE deleted_event (
    gcal_event_id TEXT PRIMARY KEY,   -- original Google event ID
    calendar_id TEXT NOT NULL,
    summary TEXT,
    description TEXT,
    start_datetime DATETIME,
    end_datetime DATETIME,
    is_all_day BOOLEAN,
    color_id TEXT,
    gcal_etag TEXT,
    recurring_event_id TEXT,
    is_recurring_instance BOOLEAN,
    app_created BOOLEAN,
    source_system TEXT,
    deleted_at DATETIME NOT NULL,
    deletion_source TEXT NOT NULL,    -- 'user' | 'gcal_sync'
    original_created_at DATETIME,
    original_updated_at DATETIME
);

CREATE INDEX idx_deleted_event_date ON deleted_event(start_datetime);
```

Sync re-insert guard: before inserting a `gcal_event` row during sync, check `deleted_event` for that `gcal_event_id`. If found, skip the re-insert (the user deleted it locally). Surface a recovery prompt only if the event reappears with a newer Google `updated_at` than the `deleted_at` timestamp.

---

### New Table: `recurring_event_series` (Story 4.5 / 4.9)

Stores the master recurring event's series definition. Each `gcal_event` instance references this via `gcal_event.recurring_event_id`.

```sql
CREATE TABLE recurring_event_series (
    series_id TEXT PRIMARY KEY,       -- same value as master gcal_event_id
    calendar_id TEXT NOT NULL,
    recurrence TEXT NOT NULL,         -- RRULE/RDATE/EXDATE joined by newline (iCal format)
    summary TEXT,
    description TEXT,
    color_id TEXT,
    is_all_day BOOLEAN,
    series_start_datetime DATETIME,   -- first occurrence start
    series_end_datetime DATETIME,     -- first occurrence end
    gcal_etag TEXT,
    gcal_updated_at DATETIME,
    last_synced_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Why a separate table (not storing the master as a `gcal_event` row):** The Google Calendar API returns instances expanded individually; the master event has no `start.dateTime` equivalent that maps cleanly to our `start_datetime` / `end_datetime` columns. Keeping series metadata separate avoids null-column hacks and makes the "edit all events" push path unambiguous — always target `recurring_event_series.series_id` as the Google event ID when patching the master.

---

## Detailed Design

### Services and Modules

| Service / Module | Responsibility | New/Extended |
|---|---|---|
| `IPendingEventRepository` | CRUD for `pending_event` rows; `GetByGcalEventIdAsync`, `UpsertAsync`, `DeleteByGcalEventIdAsync`, `GetByIdAsync(Guid)`, `GetForRangeAsync(DateOnly, DateOnly)` | New (Story 4.1) |
| `PendingEventRepository` | Implementation using `IDbContextFactory<CalendarDbContext>` | New (Story 4.1) |
| `ICalendarQueryService` | Extended: `GetEventsForRangeAsync` unions gcal + pending; `GetEventByIdAsync(string eventId, CalendarEventSourceKind)` source-agnostic | Extended (Story 4.2) |
| `CalendarQueryService` | Builds `CalendarEventDisplayModel` from both sources; sets `IsPending`, `Opacity`, `SourceKind`, `StatusLabel` | Extended (Story 4.2) |
| `IColorMappingService` | Extended: `PickerColors` (ordered list for picker), `GetDisplayName(colorId)`, `NormalizeColorKey(rawId)` | Extended (Story 4.3) |
| `ColorMappingService` | Adds 9-entry ordered picker list; normalizes Google numeric IDs (`"1"` → `"azure"`); Azure fallback for unknown/null | Extended (Story 4.3) |
| `EventDetailsPanelViewModel` | Extended: edit mode state, field properties, debounce timer, save/revert/undo commands, color picker state | Extended (Stories 4.1, 4.3) |

### Data Models and Contracts

**`CalendarEventDisplayModel`** — widened in Story 4.2 to support both sources:

```csharp
public enum CalendarEventSourceKind { Google, Pending }

public sealed record CalendarEventDisplayModel(
    string EventId,                     // gcal_event_id OR Guid.ToString() for pending drafts
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
    double Opacity,                     // 1.0 = published; 0.6 = pending
    string StatusLabel                  // e.g. "Not yet published to Google Calendar"
);
```

**`EventUpdatedMessage`** — defined in Story 4.1, consumed by `MainViewModel`:

```csharp
// Messages/EventUpdatedMessage.cs
public record EventUpdatedMessage(string EventId);
```

**`EventSelectedMessage`** — widened in Story 4.2 to carry `CalendarEventSourceKind`:

```csharp
// Updated: Messages/EventSelectedMessage.cs
public record EventSelectedMessage(string? EventId, CalendarEventSourceKind SourceKind = CalendarEventSourceKind.Google);
```

**`CalendarColorOption`** — new in Story 4.3, used by picker UI:

```csharp
public sealed record CalendarColorOption(
    string Key,            // canonical key: "azure", "purple", …
    string DisplayName,    // "Azure", "Purple", …
    string Hex,            // "#0088CC", "#3F51B5", …
    string ContrastTextHex // always "#FFFFFF" for all 9 colors
);
```

### Updated Interface Signatures

```csharp
// Services/ICalendarQueryService.cs  (Story 4.2 extension)
public interface ICalendarQueryService
{
    // Unions gcal_event + pending_event; ordered by StartUtc
    Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);

    // Source-agnostic lookup (replaces GetEventByGcalIdAsync)
    Task<CalendarEventDisplayModel?> GetEventByIdAsync(
        string eventId, CalendarEventSourceKind sourceKind, CancellationToken ct = default);
}

// Services/IPendingEventRepository.cs  (extended in Story 4.2)
public interface IPendingEventRepository
{
    Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
    Task<PendingEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IList<PendingEvent>> GetForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default);
    Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
    Task DeleteByIdAsync(Guid id, CancellationToken ct = default);
}

// Services/IColorMappingService.cs  (Story 4.3 extension)
public interface IColorMappingService
{
    string GetHexColor(string? colorId);
    IReadOnlyList<CalendarColorOption> PickerColors { get; }  // 9 entries, ordered for picker
    string GetDisplayName(string? colorId);
    string NormalizeColorKey(string? rawId);                  // "1" → "azure"; unknown → "azure"
}
```

### Canonical Color Table

| Canonical Key | Display Name | Hex | Contrast Text |
|---|---|---|---|
| `azure` | Azure | `#0088CC` | `#FFFFFF` |
| `purple` | Purple | `#3F51B5` | `#FFFFFF` |
| `grey` | Grey | `#616161` | `#FFFFFF` |
| `yellow` | Yellow | `#F6BF26` | `#FFFFFF` |
| `navy` | Navy | `#33B679` | `#FFFFFF` |
| `sage` | Sage | `#0B8043` | `#FFFFFF` |
| `flamingo` | Flamingo | `#E67C73` | `#FFFFFF` |
| `orange` | Orange | `#F4511E` | `#FFFFFF` |
| `lavender` | Lavender | `#8E24AA` | `#FFFFFF` |

Google Calendar numeric aliases resolved by `NormalizeColorKey`: `"1"` → `azure`, `"2"` → `navy`, `"3"` → `lavender`, `"4"` → `flamingo`, `"5"` → `yellow`, `"6"` → `orange`, `"8"` → `grey`, `"9"` → `purple`, `"10"` → `sage`.

### Edit Mode — State Machine

`EventDetailsPanelViewModel` manages a two-state mode:

```
ReadOnly ──[Edit button clicked]──▶ EditMode
EditMode ──[Save clicked]──────────▶ ReadOnly (panel stays open, shows updated data)
EditMode ──[Esc pressed]───────────▶ ReadOnly (triggers immediate save if debounce pending)
ReadOnly ──[Esc pressed]───────────▶ Panel closed, selection cleared
EditMode ──[Revert clicked]────────▶ ReadOnly (pending row deleted, original data restored)
ReadOnly ──[Revert clicked]────────▶ ReadOnly (pending row deleted, original data restored)
```

**Debounce pattern (Story 4.1 implemented):**

```csharp
private void StartDebounce()
{
    SaveStatusText = "Saving…";
    _debounceTimer?.Stop();
    _debounceTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    _debounceTimer.Tick += async (s, e) =>
    {
        _debounceTimer.Stop();
        await SaveNowAsync();
    };
    _debounceTimer.Start();
}
```

**Color picker** bypasses the debounce — `SelectColorCommand` calls an immediate save path.

### Edit Save Path (Story 4.1 — existing events)

```
User modifies field → StartDebounce()
→ 500 ms elapses with no further keystrokes
→ ValidateFields() — if errors exist, block save
→ IPendingEventRepository.GetByGcalEventIdAsync(gcalEventId)
  → null?  → create new PendingEvent, copy ColorId from GcalEvent
  → found? → update Summary, Description, StartDatetime, EndDatetime, UpdatedAt
→ IPendingEventRepository.UpsertAsync(pendingEvent)
→ WeakReferenceMessenger.Send(new EventUpdatedMessage(gcalEventId))
→ MainViewModel refreshes CalendarEventDisplayModel for that event (Opacity = 0.6)
```

### Color Save Path (Story 4.3 — pending_event write)

```
User selects color in picker → SelectColorCommand(colorOption)
→ IColorMappingService.NormalizeColorKey(colorOption.Key) → canonical key
→ IPendingEventRepository.GetByGcalEventIdAsync(gcalEventId)   [or GetByIdAsync for pure drafts]
  → null?  → create new PendingEvent, copy all fields from GcalEvent (same as edit save path)
  → found? → update ColorId, UpdatedAt
→ IPendingEventRepository.UpsertAsync(pendingEvent)
→ Flyout dismisses
→ Panel updates EditColorHex, EditColorName immediately
→ WeakReferenceMessenger.Send(new EventUpdatedMessage(eventId))
→ All views repaint event block with new color at 60% opacity
```

> **Design note — unified save path:** All edits (text, time, color) go through `pending_event`. Story 4.4 (Push to GCal) reads only `pending_event` to construct the Google Calendar API payload — no merge across tables required. Color changes on a published event with no existing pending row create a new pending row (event drops to 60% opacity), consistent with the model that any local change is staged before publication.

### New Event Creation — Shared Path (Story 4.2)

```
[Drag or + Add Event button]
→ Create PendingEvent {
      Id = Guid.NewGuid(),
      GcalEventId = null,              // brand-new, no Google ID
      CalendarId = "primary",
      Summary = "New Event",
      ColorId = "azure",
      StartDatetime = snappedStart (UTC),
      EndDatetime = snappedEnd (UTC),
      AppCreated = true,
      SourceSystem = "manual",
      ReadyToPublish = false,
      CreatedAt = UpdatedAt = DateTime.UtcNow
  }
→ IPendingEventRepository.UpsertAsync(pendingEvent)
→ CalendarQueryService returns union including new pending row
→ ICalendarSelectionService.Select(pendingEvent.Id.ToString(), CalendarEventSourceKind.Pending)
→ EventDetailsPanelViewModel opens in EditMode for the new draft
```

### Drag-to-Create Interaction (Week and Day views)

```
PointerPressed on empty timeline area
→ Capture pointer; record start pixel Y
PointerMoved
→ Compute top = min(startY, currentY); height = abs(currentY - startY)
→ Snap top and bottom to 15-minute grid (QuarterHourHeight = 18 px given HourSlotHeight = 72)
→ Draw transient preview Rectangle on foreground Canvas overlay
PointerReleased
→ If height < 1 slot: expand to minimum 15 minutes
→ If Esc pressed before release: clear preview, no record created
→ Compute UTC start/end from pixel positions and current view date
→ Execute shared new-event creation path above
→ Remove transient preview Rectangle
```

Guard: drag starts only on empty timeline space, not on existing event block borders. Use pointer capture (`CapturePointer`) to track drag outside the column bounds.

### `+ Add Event` Button Flow

```
User clicks "+ Add Event" in toolbar
→ ContentDialog opens:
      DatePicker (current local date)
      TimePicker start (current local time, rounded to nearest 15 min)
      TimePicker end (start + 1 hour)
→ User confirms
→ Execute shared new-event creation path above
→ If dialog cancelled: no record created
```

### XAML Layout Changes

**`EventDetailsPanelControl.xaml`** structural additions (Stories 4.1 + 4.3):

```
EventDetailsPanelControl (375 px, full height)
├── ReadOnlyView (VisualState: ReadOnly)
│   ├── TitleBlock
│   ├── DateTimeBlock
│   ├── ColorSwatch + ColorNameBlock
│   ├── DescriptionScroll
│   ├── SourceLabel
│   ├── LastSyncedLabel / LastSavedLocallyLabel
│   ├── EditButton (enabled in Tier 2)
│   └── RevertButton (visible when IsPending = true)
└── EditView (VisualState: EditMode)
    ├── TitleTextBox (required, with inline validation)
    ├── [Same-day] DatePicker + StartTimePicker + EndTimePicker
    ├── [Multi-day] StartDatePicker + StartTimePicker (vertical stack)
    │              EndDatePicker + EndTimePicker
    ├── [Color] ColorSwatch (clickable → opens Flyout)
    │           └── Flyout: 2×6 CalendarColorOption grid (2 rows × 6 columns; 9 swatches + 3 empty slots)
    ├── DescriptionTextBox (AcceptsReturn=True, scrollable)
    ├── SaveStatusIndicator ("Saving…" / "Saved")
    ├── ValidationErrorBlock
    ├── SaveButton (bottom-right, exits edit mode)
    └── RevertButton
```

**Week/Day view overlay for 15-minute precision (Story 4.2):**

- Existing hour grid remains as visual background
- Foreground `Canvas` overlay per day column renders event blocks at quarter-hour precision
- `Top = (startMinuteOffset / 60.0) * HourSlotHeight`
- `Height = durationMinutes / 60.0 * HourSlotHeight`
- `HourSlotHeight = 72`, `QuarterHourHeight = 18`

---

## Workflows and Sequencing

**Flow 1 — Edit Existing Event (text/time):**

```
User selects event → EventDetailsPanelControl opens in ReadOnly mode
→ User clicks "Edit"
→ EventDetailsPanelViewModel.EnterEditMode() — loads current values (from PendingEvent if exists, else GcalEvent)
→ All fields become editable (<100 ms)
→ User modifies title → StartDebounce() → 500 ms → SaveNowAsync()
→ PendingEvent row upserted
→ EventUpdatedMessage(gcalEventId) sent
→ MainViewModel refreshes CalendarEventDisplayModel: Opacity = 0.6, IsPending = true
→ Calendar views repaint event at 60% opacity
```

**Flow 2 — Revert Pending Edit:**

```
User clicks Revert (from ReadOnly or EditMode)
→ IPendingEventRepository.DeleteByGcalEventIdAsync(gcalEventId)
→ EventUpdatedMessage(gcalEventId) sent
→ CalendarEventDisplayModel rebuilt from GcalEvent data: Opacity = 1.0, IsPending = false
→ Calendar views repaint event at 100% opacity
→ Panel returns to ReadOnly, showing original GcalEvent data
```

**Flow 3 — Create New Event (Drag):**

```
User drags on Week or Day empty timeline
→ Transient preview rectangle rendered while dragging
→ PointerReleased: compute snapped time range
→ PendingEvent created (GcalEventId = null, Summary = "New Event", ColorId = "azure")
→ Inserted via IPendingEventRepository.UpsertAsync
→ CalendarQueryService.GetEventsForRangeAsync returns union including new draft
→ ICalendarSelectionService.Select(pendingId, Pending)
→ EventDetailsPanelViewModel loads new draft in EditMode
→ User renames and saves
→ Calendar view shows Azure-colored block at 60% opacity
```

**Flow 4 — Create New Event (Button):**

```
User clicks "+ Add Event"
→ ContentDialog opens with pre-filled date/time
→ User confirms (or cancels → no-op)
→ Same path as Flow 3 from "PendingEvent created" onwards
```

**Flow 5 — Change Event Color:**

```
User is in EditMode → clicks Color field → Flyout opens with 2×6 swatch grid
→ User clicks a color option (e.g. "Purple")
→ SelectColorCommand fires immediately (no 500 ms debounce)
→ Snapshot written to gcal_event_version (ChangedBy="manual_edit", ChangeReason="color_changed")
→ GcalEvent.ColorId updated, app_last_modified_at updated
→ Flyout closes
→ Panel updates swatch and color name label immediately
→ EventUpdatedMessage(gcalEventId) sent
→ All visible calendar views repaint event block with new color
```

**Flow 6 — App Startup with Pending Events:**

```
App launches → NavigationStateService.LoadAsync() → restore last view/date
→ CalendarQueryService.GetEventsForRangeAsync(from, to)
    → Query gcal_event WHERE start_datetime BETWEEN from AND to AND is_deleted = FALSE
    → Query pending_event WHERE start_datetime BETWEEN from AND to
    → For each gcal_event ID: check if pending_event row exists
        → If pending row: use pending Summary/Description/Start/End, Opacity = 0.6
        → If no pending row: use gcal_event data, Opacity = 1.0
    → Include pending rows with GcalEventId = null (brand-new drafts) at Opacity = 0.6
    → Return union ordered by StartUtc
→ Calendar views render mixed published + pending events
```

---

## Non-Functional Requirements

### Performance

| Target | Metric | Source |
|---|---|---|
| Edit mode activation (click Edit → fields editable) | < 100 ms | Story 4.1 AC-4.1.1 |
| Auto-save write to SQLite (debounce fires → DB write completes) | < 50 ms | NFR-P1 |
| Color picker open (click swatch → Flyout visible) | < 100 ms | Story 4.3 AC-4.3.1 |
| Color change: Flyout close → calendar repaint | < 200 ms | Story 4.3 AC-4.3.6 |
| Calendar view refresh after any save (EventUpdatedMessage) | Single CalendarEventDisplayModel update, no full reload | Story 4.1 AC-4.1.13 |
| Drag preview rectangle render while dragging | 60 FPS sustained | Story 4.2 |

All `IPendingEventRepository` and `IGcalEventRepository` calls are fully `async`. `DispatcherTimer` is used for the debounce (never `Task.Delay` on the UI thread). The `EventUpdatedMessage` handler in `MainViewModel` updates only the single affected model — it does not trigger a full `GetEventsForRangeAsync` re-query.

### Memory

`pending_event` rows are few in practice (one per in-progress edit or new draft). `GetForRangeAsync` returns only rows whose `start_datetime` falls within the current view range. No caching layer is added in Epic 4 — SQLite queries are fast enough for the data volumes anticipated.

### Data Integrity

- All times stored UTC in `pending_event`; converted to local time in `CalendarEventDisplayModel` (never in the view)
- `pending_event.GcalEventId` FK uses `DeleteBehavior.Restrict` — a `gcal_event` row cannot be hard-deleted while a pending edit exists against it
- Version history snapshot (`gcal_event_version`) is written in the same EF `SaveChangesAsync` call as the `gcal_event` update for color changes — both succeed or both fail
- `UpsertAsync` uses optimistic update-else-insert pattern (not raw SQL `UPSERT`) to preserve EF change tracking

### Resilience

- If `IPendingEventRepository.UpsertAsync` throws, `EventDetailsPanelViewModel` catches, logs, and shows "Save failed" in the status indicator — the panel remains open in edit mode
- If the app restarts while a debounce timer is pending, the unsaved in-memory changes are lost; the pending row from the last successful auto-save is preserved and reloaded on restart
- Color picker: if `GcalEventId` is null (brand-new draft), the color save path falls back to updating `pending_event.ColorId` instead of `gcal_event.color_id`, since there is no `gcal_event` row to update

---

## Dependencies and Integrations

**NuGet Packages — no new packages required.** All needed packages are present from Epics 1–3.

| Package | Usage in Epic 4 |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` 9.x | `PendingEvent` entity, migrations, repository queries |
| `CommunityToolkit.Mvvm` 8.x | `ObservableProperty`, `RelayCommand`, `WeakReferenceMessenger`, `DispatcherTimer` indirectly |
| `Microsoft.WindowsAppSDK` 1.8.x | `Flyout`, `ContentDialog`, `TimePicker`, `DatePicker`, `VisualStateManager`, `Canvas` overlay |
| `Serilog` / `Serilog.Sinks.File` | Save/revert/create/color-change events logged |

**Internal cross-epic dependencies:**

| Dependency | Direction | Contract |
|---|---|---|
| Epic 3: `EventDetailsPanelControl.xaml` + `EventDetailsPanelViewModel` | Extended (Epic 4) | `EditButton` was disabled; Epic 4 enables it and adds edit mode states |
| Epic 3: `ICalendarQueryService` / `CalendarQueryService` | Extended (Epic 4) | Union query added; `GetEventByIdAsync` made source-agnostic |
| Epic 3: `ICalendarSelectionService` | Extended (Epic 4) | `SelectedEventId` type widened; `SourceKind` added to `EventSelectedMessage` |
| Epic 3: `IColorMappingService` / `ColorMappingService` | Extended (Epic 4) | `PickerColors`, `GetDisplayName`, `NormalizeColorKey` added |
| Epic 3: all four view controls | Modified (Epic 4) | `Opacity` binding added to event blocks; drag interaction added to Week/Day |
| Epic 2: `gcal_event` table | Reads + color writes (Epic 4) | Color update path in Story 4.3 |
| Epic 2: `IGoogleCalendarApiService` write methods | Used by Story 4.4 (publish) and 4.5 (delete) | `InsertAsync`, `UpdateAsync`, `DeleteAsync` against Google Calendar API |

---

## Acceptance Criteria (Authoritative)

The authoritative AC for each story is in its story file. This section summarises the cross-cutting integration outcomes that span all three stories:

1. **Given** the app has both synced `gcal_event` rows and `pending_event` rows in the visible date range, **when** any calendar view loads, **then** all events appear in the correct order by start time, synced events at 100% opacity and pending events at 60% opacity.

2. **Given** an event has a `pending_event` row, **when** the user clicks Revert in the details panel, **then** the pending row is deleted and the event returns to 100% opacity in all views within one render cycle.

3. **Given** edit mode is active and the user types in the title field, **when** 500 ms elapses with no further input, **then** a `pending_event` row is created or updated in SQLite with no observable lag on the UI thread.

4. **Given** a new draft is created (drag or button), **when** the event list is re-queried, **then** the draft appears in Week, Day, and Month views with Azure color and 60% opacity without a full app reload.

5. **Given** the color picker is open and the user selects a color, **when** the flyout closes, **then** all currently visible calendar view blocks for that event repaint with the new color within 200 ms.

6. **Given** a color change is persisted via the picker, **when** `pending_event` is queried for that event, **then** the `color_id` field reflects the newly selected canonical color key and the event renders at 60% opacity in all views.

7. **Given** the app is restarted after edits were made, **when** the calendar loads, **then** pending events (both edits and new drafts) are still visible at 60% opacity using the data from `pending_event`.

---

## Traceability Mapping

| AC # | Story | Component(s) | Test Idea |
|---|---|---|---|
| 1 | 4.1, 4.2 | `CalendarQueryService.GetEventsForRangeAsync` | Integration: seed 2 gcal_event rows + 1 pending row; assert union returns 3 models with correct Opacity values |
| 2 | 4.1 | `IPendingEventRepository.DeleteByGcalEventIdAsync`, `EventUpdatedMessage` | Unit: DeleteByGcalEventId → ViewModel refreshes model with Opacity=1.0 |
| 3 | 4.1 | `EventDetailsPanelViewModel.StartDebounce`, `IPendingEventRepository.UpsertAsync` | Integration: call UpsertAsync → query pending_event → assert row created with correct Summary |
| 4 | 4.2 | `PendingEventRepository.UpsertAsync`, `CalendarQueryService` union | Integration: insert pending row with null GcalEventId → GetEventsForRangeAsync includes it |
| 5 | 4.3 | `SelectColorCommand`, `EventUpdatedMessage`, view XAML `Opacity` binding | Unit: SelectColorCommand → EventUpdatedMessage sent → model ColorHex updated |
| 6 | 4.3 | `IPendingEventRepository.UpsertAsync`, `SelectColorCommand` | Integration: change color → query pending_event → assert color_id = selected key; assert event renders at 0.6 opacity |
| 7 | 4.1, 4.2 | `CalendarQueryService`, `PendingEventRepository` | Integration: seed pending rows → restart context → GetEventsForRangeAsync still returns them |

---

## Risks, Assumptions, Open Questions

| # | Type | Item | Mitigation / Next Step |
|---|---|---|---|
| R1 | Risk | **Conflict resolution on push** — a `pending_event` row may be stale if Google Calendar was also modified externally since the last sync. When pushing, if Google returns a 412 ETag mismatch, the app must decide which version wins. | **Decision (MergeTimestamp):** The edit with the later timestamp wins. Compare `pending_event.updated_at` (local last-modified) vs. `gcal_event.gcal_updated_at` (Google's last-modified at last sync). If local is newer, proceed with push and accept the overwrite. If Google is newer, surface a per-event warning in the push results ("Google was modified more recently — overwrite anyway?") and let the user decide. Do not silently auto-resolve. |
| R2 | Risk | Story 4.1 created `PendingEvent` with non-nullable `GcalEventId`. New drafts (Story 4.2) have no Google ID yet — so `pending_event.GcalEventId` must allow null for drafts. However, `gcal_event.gcal_event_id` (the string PK on the gcal table) must remain non-nullable — only real, Google-assigned IDs go into that table. The row is written to `gcal_event` only after Google responds with an ID on successful insert. On failure, the pending row stays in `pending_event` and the user is notified. | **Action before Story 4.2:** Add migration `ExtendPendingEventForCreation` — make `pending_event.GcalEventId` nullable; add `CalendarId`, `AppCreated`, `SourceSystem`, `ReadyToPublish` columns. The unique index on `GcalEventId` must be a partial/filtered index (null values allowed, but no two non-null rows share the same `GcalEventId`). Do NOT change `gcal_event.gcal_event_id` nullability. |
| R3 | Risk | `WeekViewControl` and `DayViewControl` currently use hour-granular row math. 15-minute snapping requires a quarter-hour `Canvas` overlay. Adding this overlay must not break the existing event block selection and resize interactions added in Story 4.1. | **Action before Story 4.2:** Spike the foreground `Canvas` overlay approach in isolation to verify it does not interfere with `PointerPressed` routing on existing `EventBlock` controls. |
| R4 | Risk | `ICalendarSelectionService` currently tracks `SelectedGcalEventId` as a string. Widening to source-agnostic `(EventId, SourceKind)` is a breaking change across all four view controls and `EventDetailsPanelViewModel`. | **Action at start of Story 4.2:** Update `ICalendarSelectionService` interface, all four view controls, `EventDetailsPanelViewModel`, and `EventSelectedMessage` atomically in Task 2 of Story 4.2 before implementing creation UI. |
| R5 | Risk | ~~Color save path split~~ | **Resolved:** Story 4.3 writes all color changes to `pending_event.ColorId` (same path as text/time edits). No `gcal_event` write occurs. A color change on a published event with no pending row creates a new pending row (event drops to 60% opacity). No fallback or guard needed. |
| A1 | Assumption | `pending_event.GcalEventId` is currently non-nullable in the Story 4.1 migration. Story 4.2 requires it to be nullable for new drafts. `gcal_event.gcal_event_id` (PK) remains non-nullable — that column is only ever populated with real Google IDs. | **Action before Story 4.2:** Inspect `Data/Migrations/...AddPendingEventTable.cs` and add `ExtendPendingEventForCreation` migration to make `pending_event.GcalEventId` nullable with a partial unique index. |
| A2 | Assumption | Only one `pending_event` row can exist per `GcalEventId` at a time (partial unique index enforces this for non-null values). If a second edit is started on an already-pending event, `UpsertAsync` updates the existing row. Multiple pure draft rows (all with null `GcalEventId`) are all allowed. | Confirmed by Story 4.1 upsert logic; partial index design must be explicit in the migration. |
| A3 | Assumption | `ReadyToPublish` remains `false` for all events created or edited in Stories 4.1–4.3. Story 4.4 sets it `true` in the push workflow; on failure it reverts to `false`. | Schema field already present on `pending_event`; no additional migration needed for Story 4.4. |
| Q1 | Risk | If a background sync discovers that Google deleted an event that has a `pending_event` edit row locally, the FK `DeleteBehavior.Restrict` prevents the `gcal_event` soft-delete from propagating. | **Decision:** Surface an explicit error to the user: "This event was deleted from Google Calendar. Your local edits cannot be pushed. Discard local changes?" Require explicit confirmation before deleting the pending row. Never silently discard. |
| Q2 | Risk | A new draft is created (drag or button) but the user closes the panel without typing a title. | **Decision:** Discard the `pending_event` row immediately on panel close if `Summary` is still the default placeholder ("New Event") and the user has made no other edits. No confirmation dialog — the event simply disappears. |

---

## Test Strategy Summary

| Level | Framework | Scope | Target |
|---|---|---|---|
| Unit | xUnit + Moq + FluentAssertions | `EventDetailsPanelViewModel` (edit mode state, validation, debounce, undo, revert), `ColorMappingService` (9 keys, normalization, display names, fallback), `CalendarQueryService` (pending + gcal union logic, opacity assignment) | All public methods; all save/revert/undo paths |
| Integration | xUnit + in-memory SQLite | `PendingEventRepository` (upsert creates on first call, updates on second; delete removes row; range query returns correct rows); `CalendarQueryService` (union of gcal + pending with correct Opacity; `GetEventByIdAsync` for both source kinds); color-change persistence (upserts pending row with new `color_id`) | Full data pipeline: seed → operate → assert DB state |
| Manual | Developer | Drag-to-create in Week and Day views; cancel via Esc; color picker 2×6 grid keyboard navigation; 60% opacity visible across all four views; panel slide-in after drag creation | Story 4.2 drag gesture flows; Story 4.3 picker accessibility |

**Key test scenarios:**

- `PendingEventRepository_UpsertAsync_WhenCalledTwice_UpdatesExistingRow`
- `PendingEventRepository_DeleteByGcalEventId_WhenRowExists_RemovesRow`
- `CalendarQueryService_GetEventsForRangeAsync_IncludesBothGcalAndPendingEvents`
- `CalendarQueryService_GetEventsForRangeAsync_SetsPendingOpacityToPoint6`
- `CalendarQueryService_GetEventsForRangeAsync_UsesPendingDataWhenRowExists`
- `EventDetailsPanelViewModel_WhenTitleCleared_BlocksSave`
- `EventDetailsPanelViewModel_WhenEndBeforeStart_BlocksSave`
- `EventDetailsPanelViewModel_UndoLastChange_RevertsFieldToPreKeystrokeValue`
- `ColorMappingService_GetHexColor_ReturnsAzureForNullAndUnknownKeys`
- `ColorMappingService_NormalizeColorKey_MapsGoogleNumericIdsToCanonicalKeys`
- `ColorMappingService_PickerColors_ContainsExactlyNineEntriesInOrder`
- `PendingEventRepository_UpsertAsync_WhenColorChanged_UpdatesColorIdInPendingRow` (integration)

**Test data:** Reuse `GoogleCalendarManagement.Tests/TestData/` fixtures from Epic 3. Add `sample_pending_events.json` with at least: one pending edit row (GcalEventId set), one brand-new draft row (GcalEventId null).

---

## Future Story Map (Stories 4.4–4.9)

These stories are backlog and not yet fully drafted. The descriptions below are intentionally rough — enough to sequence and scope work, not implementation-ready.

---

### Story 4.4 — Push Pending Events to Google Calendar

**Goal:** Publish all `pending_event` rows to Google Calendar in a single batch action.

**Core flow:**
- "Push to GCal" button in top toolbar shows count badge of pending events; clicking badge opens a dropdown list of pending events (title, date, operation type)
- User can multi-select which pending rows to push, leaving the rest staged; "Select All" button available
- Confirmation dialog lists the selected events before pushing
- For each selected pending row:
  - If `GcalEventId` is null (new draft): call `Events.Insert` → receive Google ID → move row to `gcal_event`, delete from `pending_event`
  - If `GcalEventId` is set (edit): merge all `pending_event` fields → call `Events.Update` with ETag → update `gcal_event`, delete `pending_event` row
  - If `OperationType = 'delete'`: call `Events.Delete` → move `gcal_event` row to `deleted_event`, delete `pending_event` row
- Success: event transitions from 60% → 100% opacity with a **300 ms** animated fade
- Failure: `publish_error` recorded on `pending_event` row; user notified per-event with inline error; **no automatic retry** — failed events remain in pending list; user can retry manually
- Progress indicator for batch operations (n / total)
- **Recurring events (Story 4.4 scope):** If a recurring instance is in the push list, default to "change only this instance" and show an informational popup explaining the scope. Full "this and following" and "edit all" push paths are deferred to Story 4.9.

**Key design decisions resolved:**
- R1 (MergeTimestamp): `pending_event.updated_at` vs `gcal_event.gcal_updated_at` — later wins; surface per-event warning to user when Google version is newer
- ETag conflict handling: use MergeTimestamp strategy from `_database-schemas.md` §Tier 2
- No automatic retry on failure; failed events stay in pending list
- `app_published`, `app_published_at` fields updated on `gcal_event` after successful push

**Schema impact:** None — `pending_event.ready_to_publish` already exists; `gcal_event` publish fields already exist.

---

### Story 4.5 — Event Deletion

**Goal:** Allow users to delete events — both local-only drafts and previously published events.

**Core flow:**
- Delete button in the event details panel (read-only and edit mode)
- **Local draft** (`GcalEventId = null`): confirmation dialog → `IPendingEventRepository.DeleteByIdAsync` → event removed from all views immediately
- **Pending edit** (`GcalEventId` set, pending row exists): offer two sub-options: (a) revert only (same as existing Revert), (b) stage deletion — upsert `pending_event` row with `OperationType = 'delete'`; event renders at 60% opacity with a deletion indicator until pushed
- **Published event** (no pending row): confirmation dialog → upsert `pending_event` row with `OperationType = 'delete'`; event renders at 60% opacity; deletion is executed against Google Calendar API in Story 4.4 push flow
- On successful push of a delete: `gcal_event` row is moved to `deleted_event` table (soft-delete); `pending_event` row deleted
- **Do NOT delete `pending_event` rows without user confirmation** — always show a confirmation dialog before any removal; on dismissal, the pending row is preserved
- Batch delete: if multi-select active (Story 4.6), delete all selected events via same flow

**Key design decisions resolved:**
- Soft-delete via `deleted_event` table (not `is_deleted` column on `gcal_event`) — `gcal_event` stays strictly live events
- Deletion staged through `pending_event.operation_type = 'delete'` so the push list is the single source of truth for all outbound operations
- Sync guard: on re-sync, if an event exists in `deleted_event`, skip re-insertion (use `gcal_event_id` as guard key)

**Schema impact:** `deleted_event` table (see Schema Changes section).

---

### Story 4.6 — Multi-Select and Batch Operations

**Goal:** Let users select multiple events to apply batch color changes or batch delete.

**Core flow:**
- **Shift+click:** selects all timed (non-all-day) events between the first clicked event and the shift-clicked event (inclusive, ordered by start time)
- **Ctrl+click:** toggles individual event selection without affecting others
- **No drag-select rectangle** — drag gesture is reserved for event creation and duration editing
- Selection counter badge ("3 events selected") in top bar with clear button
- Selection persists across view mode switches (Month → Week → Day etc.)
- Multi-select automatically populates the selected events in the pending events dropdown (Story 4.4)
- Batch operations available when >1 event selected:
  - **Color:** opens same 2×6 picker; applies color to all selected events immediately
  - **Delete:** confirmation dialog listing all selected events
  - **Push to GCal:** pushes any selected events that have a `pending_event` row (even if some selected events are `gcal_event` only)
- Red outline extended to all selected events (same 2 px style as single-select)
- **Edit panel behavior during multi-select:**
  - Panel stays open; unsupported fields are greyed out and disabled
  - Title field: shows "X events selected" placeholder (read-only)
  - Time fields: shows "Various" placeholder (read-only)
  - Description field: blank; shows tooltip "Multi events selected — field does not support multi-editing"
  - Color field, Delete button, Push button: remain active for batch operations
- Esc clears multi-selection

**Key design decisions resolved:**
- No drag-select — Shift+click range-selects timed events; Ctrl+click toggles individuals
- Edit panel stays open but disables unsupported fields rather than collapsing
- Selection persists across view switches
- Multi-select pre-populates the pending events push list

**Schema impact:** None.

---

### Story 4.7 — Drag-to-Reschedule Existing Events

**Goal:** Allow users to drag an existing timed event block to a new time slot in Week or Day view.

**Core flow:**
- User clicks and holds an existing `EventBlock` body (published `gcal_event` or pending draft)
- **Drag from bottom edge only = duration resize** (Story 4.1 affordance); drag from anywhere else on the block body = reschedule
- Block follows pointer; snaps to 15-minute grid; **duration is preserved** (start time shifts, end time shifts by same delta)
- **No ghost placeholder** — the original slot is immediately vacated; the dragged block is rendered translucent (60% opacity, same as any pending event) at the current drag position
- **Cross-day dragging in Week view:** dragging above the top of the visible area wraps to the previous day; dragging below the bottom wraps to the next day (both directions)
- **All-day ↔ timed boundary is hard:** it is impossible to drag an event from the all-day header strip into the timed area or vice versa; drag gesture is rejected at the boundary
- On release: compute new UTC start/end → upsert `pending_event` row → event stays at 60% opacity (now a pending change)
- `EventUpdatedMessage` triggers calendar refresh
- **Esc while dragging:** cancel, restore original position
- **Ctrl+Z after release:** undo the reschedule — removes the `pending_event` row if it was newly created, or restores prior `pending_event` values if it already existed
- **Drag does not change selection status:** dragging an event does not select it, deselect it, or affect other selected events; the event simply becomes pending

**Key design decisions resolved:**
- Drag from body = reschedule; drag from bottom edge = resize (Story 4.1)
- No ghost — original slot immediately vacated; block renders translucent while dragging
- Wrap-around both directions in Week view
- All-day ↔ timed drag is blocked
- Ctrl+Z undo available after drop

**Schema impact:** None — uses existing `pending_event` upsert path.

---

### Story 4.8 — Year View Drag-to-Create

**Goal:** Extend drag-to-create to Year and Month views, producing all-day events.

**Core flow:**
- User drags across day cells in Year view mini-month grids or Month view day cells
- Drag across multiple cells creates a multi-day all-day event
- Same outcome as drag-to-create in Week/Day view: `pending_event` row created, details panel opens in edit mode
- Default: `IsAllDay = true`, duration = number of days dragged, `ColorId = "azure"`
- Minimum 1 day; snap to whole days only

**Key design decisions to resolve:**
- Whether Year view cell size supports accurate day-cell hit testing during drag (may require a tap-to-create fallback if cells are too small)
- Month view: included in this story (whole-day cells, same drag mechanic)

**Schema impact:** None — uses existing `pending_event` create path.

---

### Story 4.9 — Recurring Event Editing (All Scopes)

**Goal:** Allow editing recurring event instances with full scope control: single instance, this and following, or all events.

> **IMPORTANT: Begin this story with an API spike against a live Google Calendar account before writing any application code.** The spike must confirm exact API behavior for each edit scope, ETag semantics for recurring instances, and the two-call sequence required for "this and following." Do not estimate or plan implementation tasks until the spike is complete.

**Core flow:**
- User selects a recurring event instance (identified by `is_recurring_instance = true` on `gcal_event`)
- Edit panel shows a scope selector: "Only this event" | "This and following" | "All events"
- **"Only this event":** normal edit flow (Story 4.1 path); `Events.Update` on the specific instance ID (includes date suffix in `gcal_event_id`)
- **"This and following":** requires two sequential API calls — truncate master RRULE at the original instance date, then insert a new series starting from the edited instance. Spike must confirm rollback behavior if the second call fails.
- **"All events":** `Events.Update` on the series master ID; all instances reflect the change after next sync

**Spike questions to answer before implementation:**
- Exact API shape for each of the three scopes (Google Recurring Events API)
- ETag handling for recurring instance vs. series master
- Recovery path if "this and following" second call fails (series permanently shortened)
- Whether `recurring_event_series` table needs additional fields after the spike findings
- How `gcal_event_version` snapshots interact with instance vs. series master rows

**Schema impact:** `recurring_event_series` table (see Schema Changes section); `pending_event.recurrence_edit_scope` and `pending_event.series_master_gcal_event_id` fields; migration `AddPendingEventRecurringFields`.

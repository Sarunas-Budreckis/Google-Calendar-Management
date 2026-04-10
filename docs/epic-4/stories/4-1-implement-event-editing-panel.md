# Story 4.1: Implement Event Editing Panel

Status: ready-for-dev

> **Moved from Epic 3:** This story was originally Story 3.5. It has been moved to Epic 4 (Event Editing — Tier 2) as event editing is out of scope for Tier 1 (read-only).
>
> **Revised (2026-04-06):** Two architectural decisions updated — (1) edits are written to a new `pending_event` table, NOT directly to `gcal_event`; (2) events with a pending edit row are rendered at **60% opacity** immediately, becoming fully opaque only after a successful Push to GCal.

## Story

As a **user**,
I want **to edit event details directly in the application**,
so that **I can modify events before publishing to Google Calendar**.

## Acceptance Criteria

1. **AC-4.1.1 — Edit Mode Activation:** Given the user selects an event and the details panel is open (Story 3.4), when the user clicks the "Edit" button (previously disabled in Story 3.4), the panel transitions to edit mode within 100 ms — all fields become editable in-place.

2. **AC-4.1.2 — Title Field:** Given edit mode is active, the title field renders as a `TextBox` with the current title pre-filled. A required-field validation error ("Title is required") appears inline below the field if the user clears it. The Save cannot complete with an empty title.

3. **AC-4.1.3 — Start/End Date+Time Pickers:** Given edit mode is active, start and end date/time each render as a `DatePicker` + `TimePicker` pair. The `TimePicker` has `MinuteIncrement = 15`. Both are pre-filled with current event values (UTC converted to local time for display). Validation error appears inline if end time is not after start time.

4. **AC-4.1.4 — Description Field:** Given edit mode is active, the description field renders as a multi-line `TextBox` (`AcceptsReturn="True"`, scrollable) pre-filled with the current description (empty string if null).

5. **AC-4.1.5 — Color Picker Stub:** Given edit mode is active, a color swatch placeholder is visible (labelled "Color: [current color name]") with a "Coming soon" tooltip. Story 4.3 will replace this stub with a full color picker — the layout slot must exist in Story 4.1 but must NOT implement color editing logic.

6. **AC-4.1.6 — Auto-Save (Debounced):** Given the user modifies any field and no validation errors exist, changes are saved to the local `pending_event` database row automatically after 500 ms of inactivity (debounced). No explicit "Save" button is needed or shown.

7. **AC-4.1.7 — Unsaved Indicator:** Given the user is actively typing (within the 500 ms debounce window), a subtle indicator ("Saving…" text or pulsing dot) is visible. Once auto-save completes, the indicator shows "Saved" (or disappears) within 300 ms.

8. **AC-4.1.8 — Database Write:** Given auto-save fires, a `pending_event` row is created (if none exists for this `gcal_event_id`) or updated (if one already exists). The `pending_event` row stores: `gcal_event_id` (FK referencing the original event), `summary`, `description`, `start_datetime`, `end_datetime` (stored as UTC), `color_id` (copied from original on creation, unchanged here), `created_at`, and `updated_at`. The original `gcal_event` row is **NOT modified**.

9. **AC-4.1.9 — Validation — End Before Start:** Given the user sets end time before start time, an inline validation message ("End time must be after start time") appears immediately and auto-save is blocked until corrected.

10. **AC-4.1.10 — Undo (Ctrl+Z):** Given the user presses Ctrl+Z while in edit mode, the last field change is reverted. Undo operates per-field (reverts the field to its value before the last keystroke batch). Only one undo level is required for Tier 2.

11. **AC-4.1.11 — Esc Closes Panel:** Given edit mode is active and changes have been auto-saved, pressing Esc closes the panel (same behavior as Story 3.4). If the debounce timer is still pending (changes NOT yet saved), pressing Esc triggers an immediate save before closing.

12. **AC-4.1.12 — Pending State Visual:** Given auto-save creates or updates a `pending_event` row, the corresponding event block in the calendar view (all views) is rendered at **60% opacity** (translucent/pending state). The original `gcal_event` data is no longer displayed for that event — the `pending_event` data is shown instead. Events with no `pending_event` row continue to render at 100% opacity. Opacity transitions to 100% only after a successful Push to GCal (a future story).

13. **AC-4.1.13 — Calendar View Refresh:** Given auto-save completes, the calendar view (Month/Week/Day/Year) refreshes to show the updated event title and any visible field changes without requiring full page reload. Only the affected event's display model is updated.

---

## Scope Boundaries (Tier 2 Only)

**IN SCOPE — this story:**
- Editing `summary`, `description`, `start_datetime`, `end_datetime` on existing `gcal_event` rows via a new `pending_event` row
- `PendingEvent` EF Core entity, `EntityTypeConfiguration`, and database migration
- `IPendingEventRepository` with `UpsertAsync(PendingEvent)` and `GetByGcalEventIdAsync(string)` methods
- Auto-save with 500 ms debounce using `DispatcherTimer`
- Inline validation (empty title, end-before-start)
- Single-level Undo (Ctrl+Z)
- Color picker stub/placeholder (layout slot only — no editing logic)
- `EventDetailsPanelViewModel` edit mode state and save logic
- 60% opacity rendering for event blocks that have a `pending_event` row (all four calendar views)
- Refresh affected event in calendar view via `CalendarSelectionService`/`WeakReferenceMessenger`

**OUT OF SCOPE — do NOT implement:**
- Color picking / color change (Story 4.3)
- Event creation (Story 4.2)
- Pushing edits to Google Calendar (future Push to GCal story)
- Multi-level undo
- Drag-to-reschedule in calendar view

---

## Dev Notes

### CRITICAL: Actual Project Structure

The project is a single flat WinUI 3 project — **NOT** the `src/Core/` hierarchy described in the architecture doc. All new files follow the same structure as existing code:

```
GoogleCalendarManagement/             ← project root (this IS the csproj root)
├── App.xaml / App.xaml.cs            ← DI registration in ConfigureServices()
├── Views/                            ← XAML + code-behind
├── ViewModels/                       ← ViewModels
├── Services/                         ← All interfaces + implementations (flat, no sub-folders)
├── Messages/                         ← WeakReferenceMessenger messages
├── Models/                           ← CalendarEventDisplayModel, NavigationState, ViewMode
├── Data/                             ← EF Core (CalendarDbContext, Entities, Configurations, Migrations)
└── GoogleCalendarManagement.Tests/   ← Unit + Integration tests
```

**Namespace:** `GoogleCalendarManagement.Services`, `GoogleCalendarManagement.ViewModels`, `GoogleCalendarManagement.Views`, `GoogleCalendarManagement.Models`

**NEVER create a `Core/` folder.** Services live flat in `Services/`.

### Dependency: Stories 3.2–3.4 Must Be Complete First

Story 4.1 requires the following to exist before implementation begins:

| Prerequisite | Where it lives | What Story 4.1 needs from it |
|---|---|---|
| Story 3.2 | `ColorMappingService` fully implemented with all 9 colours | `IColorMappingService.AllColors` to display current colour name in stub |
| Story 3.3 | `CalendarSelectionService`, `EventSelectedMessage` | Event selection messaging to trigger edit mode |
| Story 3.4 | `EventDetailsPanelControl.xaml` + `EventDetailsPanelViewModel` | The read-only panel to EXTEND to edit mode |

**Do NOT reinvent `EventDetailsPanelControl` or `EventDetailsPanelViewModel`** — Story 4.1 EXTENDS them.

### PendingEvent Entity (NEW in Story 4.1)

Create `Data/Entities/PendingEvent.cs`:

```csharp
public class PendingEvent
{
    public Guid Id { get; set; }
    public string GcalEventId { get; set; } = null!;  // FK to GcalEvent — always set for edits (null for new events in Story 4.2)
    public string Summary { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartDatetime { get; set; }   // UTC
    public DateTime EndDatetime { get; set; }     // UTC
    public string ColorId { get; set; } = null!;  // copied from GcalEvent on creation
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public GcalEvent? GcalEvent { get; set; }     // navigation property
}
```

Add EF configuration in `Data/Configurations/PendingEventConfiguration.cs` (index on `GcalEventId`). Add migration: `dotnet ef migrations add AddPendingEventTable`.

**Upsert logic** — when auto-save fires:
1. Query `IPendingEventRepository.GetByGcalEventIdAsync(gcalEventId)`.
2. If `null`: create new `PendingEvent`, copying `ColorId` from the original `GcalEvent`.
3. If found: update `Summary`, `Description`, `StartDatetime`, `EndDatetime`, `UpdatedAt`.
4. Call `UpsertAsync(pendingEvent)`.

**UTC conversion:** `DateTime.SpecifyKind(localPicker.Date.DateTime + localPicker.Time, DateTimeKind.Local).ToUniversalTime()`.

### New Repository — IPendingEventRepository

Create `Services/IPendingEventRepository.cs`:

```csharp
public interface IPendingEventRepository
{
    Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
    Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default);
}
```

Implement in `Services/PendingEventRepository.cs` using `IDbContextFactory<CalendarDbContext>`.

### Calendar View Opacity — Pending State

All four calendar view controls query `PendingEvent` rows alongside `GcalEvent` rows. When building `CalendarEventDisplayModel`, set `Opacity = 0.6` if a `PendingEvent` row exists for that `GcalEventId`, and use the `PendingEvent` field values (summary, times, color) for display instead of the `GcalEvent` values.

The recommended approach: add a `bool IsPending` and `double Opacity` property to `CalendarEventDisplayModel`, populated by the query layer. View XAML binds `Opacity="{Binding Opacity}"` on event blocks.

### EventDetailsPanelViewModel Changes

Extend the existing `EventDetailsPanelViewModel` (created in Story 3.4):

```csharp
// --- NEW in Story 4.1 ---
[ObservableProperty] private bool isEditMode;
[ObservableProperty] private string editTitle = "";
[ObservableProperty] private DateOnly editStartDate;
[ObservableProperty] private TimeOnly editStartTime;
[ObservableProperty] private DateOnly editEndDate;
[ObservableProperty] private TimeOnly editEndTime;
[ObservableProperty] private string editDescription = "";
[ObservableProperty] private string titleError = "";
[ObservableProperty] private string dateTimeError = "";
[ObservableProperty] private string saveStatusText = "";

[RelayCommand] private void EnterEditMode() { ... }
[RelayCommand] private async Task SaveNowAsync() { ... }
private void StartDebounce() { ... }
public void UndoLastChange() { ... }
```

**Debounce using `DispatcherTimer`** (do NOT use `Task.Delay` on UI thread):

```csharp
private void StartDebounce()
{
    SaveStatusText = "Saving…";
    _debounceTimer?.Stop();
    if (_debounceTimer == null)
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await SaveNowAsync();
        };
    }
    _debounceTimer.Start();
}
```

### Calendar View Refresh After Save

```csharp
// Messages/EventUpdatedMessage.cs (NEW)
public record EventUpdatedMessage(string GcalEventId);
```

`MainViewModel` subscribes and refreshes only the affected `CalendarEventDisplayModel`.

### Previous Story Learnings

- **Use `IDbContextFactory`, not scoped `CalendarDbContext`** in singleton services.
- **GcalEvent PK is `GcalEventId` (string)** — not an int.
- **All DB timestamps are UTC.**
- **Filter `IsDeleted == false`** in all GcalEvent queries.
- **Build:** `dotnet build -p:Platform=x64`

---

## Tasks / Subtasks

- [ ] **Task 1: Create `PendingEvent` entity, repository, and migration** (AC: 4.1.8, 4.1.12)
  - [ ] `Data/Entities/PendingEvent.cs` — entity class
  - [ ] `Data/Configurations/PendingEventConfiguration.cs` — EF config with index on `GcalEventId`
  - [ ] Register `DbSet<PendingEvent>` in `CalendarDbContext`
  - [ ] `Services/IPendingEventRepository.cs` — `GetByGcalEventIdAsync`, `UpsertAsync`
  - [ ] `Services/PendingEventRepository.cs` — implementation using `IDbContextFactory`
  - [ ] Register repository in DI (`App.xaml.cs`)
  - [ ] `dotnet ef migrations add AddPendingEventTable`
  - [ ] Add `EventUpdatedMessage` to `Messages/EventUpdatedMessage.cs`

- [ ] **Task 2: Create value converters** (AC: 4.1.3)
  - [ ] `Views/Converters/DateOnlyToDateTimeOffsetConverter.cs`
  - [ ] `Views/Converters/TimeOnlyToTimeSpanConverter.cs`

- [ ] **Task 3: Extend `EventDetailsPanelViewModel`** (AC: 4.1.1–4.1.13)
  - [ ] Add edit mode state, editable fields, validation, debounce timer, undo

- [ ] **Task 4: Extend `EventDetailsPanelControl.xaml`** (AC: 4.1.1–4.1.13)
  - [ ] Add `VisualStateManager` ReadOnly/EditMode states
  - [ ] Enable Edit button; add edit-mode controls
  - [ ] Add Ctrl+Z and Esc key handling

- [ ] **Task 5: Calendar view refresh on save + pending opacity** (AC: 4.1.12, 4.1.13)
  - [ ] Add `IsPending` and `Opacity` properties to `CalendarEventDisplayModel`
  - [ ] Update event query logic in all four view controls to check `PendingEvent` table; use pending data + `Opacity = 0.6` when a pending row exists
  - [ ] Bind `Opacity` on event blocks in all four view XAML files
  - [ ] `MainViewModel` subscribes to `EventUpdatedMessage`, refreshes affected display model

- [ ] **Task 6: Unit tests** (AC: 4.1.6, 4.1.8, 4.1.9, 4.1.10, 4.1.12)
  - [ ] `EnterEditMode()`, `ValidateFields()`, `UndoLastChange()` tests
  - [ ] `PendingEventRepository.UpsertAsync()` — creates on first save, updates on second save
  - [ ] `CalendarEventDisplayModel` — `Opacity = 0.6` when `IsPending = true`, `1.0` otherwise

- [ ] **Task 7: Build verification**
  - [ ] `dotnet build -p:Platform=x64` — 0 errors
  - [ ] `dotnet test GoogleCalendarManagement.Tests/` — all pass

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

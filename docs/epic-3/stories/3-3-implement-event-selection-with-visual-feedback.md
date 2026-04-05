# Story 3.3: Implement Event Selection with Visual Feedback

Status: in-progress

## Story

As a **calendar user**,
I want **to click any event and see it visually highlighted with a silvery-white outline, keep that selection while changing views or periods, and clear it only through explicit calendar-selection actions**,
so that **I always know which event is active and can proceed to view its details**.

## Acceptance Criteria

1. **AC-3.3.1 — Silvery-white outline on click:** Given the user clicks an event in any view, the event gains a 2 px solid silvery-white outline within 50 ms of the click.
2. **AC-3.3.2 — Single-select enforcement:** Given an event is already selected, when the user clicks a different event, the silvery-white outline moves to the newly clicked event; the previously selected event returns to its normal appearance.
3. **AC-3.3.3 — Esc clears selection:** Given an event is selected, when the user presses Esc, the selection is cleared and all event outlines are removed.
4. **AC-3.3.4 — Blank calendar area clears selection:** Given an event is selected, when the user clicks any blank area inside the calendar surface (not on an event and not in the control panels), the selection is cleared.
5. **AC-3.3.5 — Hover tooltip:** Given the user hovers over any event without clicking it, a tooltip appears showing the event title and formatted start/end time.
6. **AC-3.3.6 — Selection persists across navigation:** Given an event is selected, when the user switches calendar views or navigates to a previous or next period, the selected event remains the active selection until the user presses Esc, clicks blank calendar space, or uses the close button that Story 3.4 will wire to `ClearSelection()`. If the selected event is visible in the resulting view or period, its silvery-white outline remains applied without requiring reselection.

## Tasks / Subtasks

- [x] **Task 1: `ICalendarSelectionService` interface** — DONE
  - File: `Services/ICalendarSelectionService.cs`
  - Uses `string? SelectedGcalEventId`, `Select(string gcalEventId)`, `ClearSelection()`

- [x] **Task 2: `CalendarSelectionService` implementation** — DONE
  - File: `Services/CalendarSelectionService.cs`
  - Sends `EventSelectedMessage(gcalEventId)` via `WeakReferenceMessenger.Default`

- [x] **Task 3: `EventSelectedMessage`** — DONE
  - File: `Messages/EventSelectedMessage.cs`
  - `public sealed record EventSelectedMessage(string? GcalEventId)`

- [x] **Task 4: DI registration** — DONE
  - `services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();` in `App.xaml.cs`

- [x] **Task 5: Wire tap + selection visual state into `MonthViewControl`** (AC: 3.3.1, 3.3.2, 3.3.4, 3.3.5, 3.3.6)
  - Add `_selectionService = App.GetRequiredService<ICalendarSelectionService>()` in constructor
  - Add `_eventBorders = new Dictionary<string, Border>()` field (GcalEventId → Border)
  - In `BuildDayCell()`: add `Tapped` handler on each event `Border` → calls `_selectionService.Select(item.GcalEventId)`
  - In `BuildDayCell()`: set `e.Handled = true` in Tapped handler to stop event bubbling to background
  - In `BuildDayCell()`: add `ToolTipService.ToolTip` on each event `Border`
  - Register `WeakReferenceMessenger.Default` for `EventSelectedMessage` in `Loaded` handler
  - On `EventSelectedMessage`: reset all borders to normal, then apply the silvery-white outline to the matching border
  - After every `Rebuild()`, reapply `_selectionService.SelectedGcalEventId` so selection survives view/period changes and data refreshes
  - Unregister message in `Unloaded` handler
  - Add `Tapped` on `MonthGrid` (background) → calls `_selectionService.ClearSelection()`

- [x] **Task 6: Wire tap + selection visual state into `WeekViewControl`** (AC: 3.3.1, 3.3.2, 3.3.4, 3.3.5, 3.3.6)
  - Same pattern as Task 5
  - Apply to both `CreateEventChip()` (all-day) and `CreateTimedEventBlock()` (timed) borders
  - After every `Rebuild()`, reapply `_selectionService.SelectedGcalEventId` so selection survives view/period changes and data refreshes
  - Add `Tapped` on `WeekGrid` (background) → calls `_selectionService.ClearSelection()`
  - Register/unregister `EventSelectedMessage` in `Loaded`/`Unloaded`

- [x] **Task 7: Wire tap + selection visual state into `DayViewControl`** (AC: 3.3.1, 3.3.2, 3.3.4, 3.3.5, 3.3.6)
  - Same pattern as Task 5
  - Apply to all-day `Border` elements and timed `eventBlock` `Border` elements in `Rebuild()`
  - After every `Rebuild()`, reapply `_selectionService.SelectedGcalEventId` so selection survives view/period changes and data refreshes
  - Add `Tapped` on `DayGrid` (background) → calls `_selectionService.ClearSelection()`
  - Register/unregister `EventSelectedMessage` in `Loaded`/`Unloaded`

- [x] **Task 8: Year view — no event selection needed** (AC: 3.3.4, 3.3.6)
  - `YearViewControl` shows day-number buttons and sync dots, NOT event chips
  - Year view day-buttons already handle Click → navigate to Month view (`DayButton_Click`)
  - No event tap handlers needed; add background `Tapped` on `MonthsGrid` → `_selectionService.ClearSelection()` only for blank year-view space
  - Do not clear selection from year-view day buttons; selection must persist through their navigation action

- [x] **Task 9: Esc key handler on `MainPage`** (AC: 3.3.3, 3.3.6)
  - Inject `ICalendarSelectionService` into `MainPage` constructor
  - Add `KeyboardAccelerator` for `VirtualKey.Escape` in `MainPage.xaml` or wire `KeyDown` in code-behind
  - Handler calls `_selectionService.ClearSelection()`
  - Esc must work regardless of which view is active (Year/Month/Week/Day)
  - Previous/Next buttons, view-mode buttons, and other control-panel interactions must not clear selection

- [x] **Task 10: Unit tests for `CalendarSelectionService`** (AC: 3.3.1–3.3.4)
  - Create `GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs`
  - See exact test patterns in Dev Notes below

- [ ] **Task 11: Final validation**
  - Run `dotnet build -p:Platform=x64`
  - Run `dotnet test`
  - Manual: click event → silvery-white outline, no other event outlined
  - Manual: click second event → first outline gone, second outlined
  - Manual: switch view or use previous/next period → selection is still active and outline reapplies whenever the selected event is visible
  - Manual: press Esc → outline disappears
  - Manual: click blank calendar area → outline disappears
  - Manual: click blank control-panel space or use navigation controls → selection remains active
  - Manual: hover event → tooltip shows title and time

## Dev Notes

### CRITICAL: String ID, Not Integer

**The codebase uses `string GcalEventId`, NOT `int eventId`.**

| Spec says | Actual codebase |
|---|---|
| `int eventId` | `string gcalEventId` |
| `int? EventId` | `string? GcalEventId` |
| `GetEventByIdAsync(int id)` | `GetEventByGcalIdAsync(string gcalEventId)` |

Every reference to integer IDs in older planning documents is wrong. Use `string GcalEventId` throughout.

### Actual Interface (already exists)

```csharp
// Services/ICalendarSelectionService.cs
namespace GoogleCalendarManagement.Services;

public interface ICalendarSelectionService
{
    string? SelectedGcalEventId { get; }
    void Select(string gcalEventId);
    void ClearSelection();
}
```

### Actual Implementation (already exists)

```csharp
// Services/CalendarSelectionService.cs
public sealed class CalendarSelectionService : ICalendarSelectionService
{
    public string? SelectedGcalEventId { get; private set; }

    public void Select(string gcalEventId)
    {
        if (string.IsNullOrWhiteSpace(gcalEventId))
        {
            throw new ArgumentException("Event ID must be a non-empty, non-whitespace string.", nameof(gcalEventId));
        }
        if (string.Equals(SelectedGcalEventId, gcalEventId, StringComparison.Ordinal)) return; // ← no duplicate message

        SelectedGcalEventId = gcalEventId;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(gcalEventId));
    }

    public void ClearSelection()
    {
        if (SelectedGcalEventId is null) return; // ← no duplicate message

        SelectedGcalEventId = null;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
    }
}
```

**Service behavior (differs from original spec):**
- `Select(sameId)` twice: second call is silently swallowed — no message sent
- `ClearSelection()` when already null: silently swallowed — no message sent
- `Select(null/empty/whitespace)` throws `ArgumentException`; blank UI clicks must call `ClearSelection()` directly instead of sending an empty ID

### Actual Message (already exists)

```csharp
// Messages/EventSelectedMessage.cs
namespace GoogleCalendarManagement.Messages;
public sealed record EventSelectedMessage(string? GcalEventId);
```

### Actual Display Model

```csharp
// Models/CalendarEventDisplayModel.cs
public sealed record CalendarEventDisplayModel(
    string GcalEventId,   // ← key for selection lookup
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime StartLocal,  // ← use for display (already local)
    DateTime EndLocal,
    bool IsAllDay,
    string ColorHex,
    bool IsRecurringInstance,
    string? Description,
    DateTime? LastSyncedAt);
```

### View Architecture — No Separate UserControls

**There are NO `EventChip` or `EventBlock` user control files.** Events are built inline in each view's `Rebuild()` code-behind. The plan must work with inline `Border` elements.

Pattern for each view:

```csharp
// At class level
private ICalendarSelectionService _selectionService = null!;
private readonly Dictionary<string, Border> _eventBorders = new();

// In constructor
_selectionService = App.GetRequiredService<ICalendarSelectionService>();

// In Loaded handler
WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, OnEventSelected);

// In Unloaded handler
WeakReferenceMessenger.Default.UnregisterAll(this);

// Message handler (runs on background thread — dispatch to UI thread)
private void OnEventSelected(object recipient, EventSelectedMessage message)
{
    DispatcherQueue.TryEnqueue(() =>
    {
        foreach (var (id, border) in _eventBorders)
        {
            if (message.GcalEventId is not null && id == message.GcalEventId)
            {
                border.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE8, 0xEC, 0xF1));
                border.BorderThickness = new Thickness(2);
            }
            else
            {
                border.BorderBrush = new SolidColorBrush(Colors.Transparent);
                border.BorderThickness = new Thickness(0);
            }
        }
    });
}
```

**In `Rebuild()`, clear and repopulate the dictionary:**
```csharp
_eventBorders.Clear();
// ... then for each event border built:
var eventBorder = new Border { ... };
_eventBorders[item.GcalEventId] = eventBorder;
eventBorder.Tapped += (_, e) =>
{
    _selectionService.Select(item.GcalEventId);
    e.Handled = true; // prevents bubbling to background Tapped handler
};
```

**Background Tapped handler (clear selection when clicking empty calendar space only):**
```csharp
// In Loaded handler or constructor
MonthGrid.Tapped += (_, _) => _selectionService.ClearSelection();
// (WeekGrid.Tapped, DayGrid.Tapped similarly)
```

Because event `Tapped` handlers set `e.Handled = true`, clicks on events will NOT bubble to the grid's `Tapped` handler.
Do not wire selection clearing to shell controls like view-mode buttons, previous/next buttons, or jump-to-date controls.

### Tooltip Pattern

```csharp
var tooltipText = item.IsAllDay
    ? item.Title
    : $"{item.Title}\n{item.StartLocal:HH:mm} – {item.EndLocal:HH:mm}";

ToolTipService.SetToolTip(eventBorder, tooltipText);
```

Do NOT set `ToolTipService.ToolTip` via XAML (borders are built in code-behind). Use the static `ToolTipService.SetToolTip(element, content)` API.

### Esc Key Handler on MainPage

`MainPage.xaml.cs` is where the Esc key handler belongs (not `MainWindow`). Currently `MainPage` only takes `MainViewModel` in its constructor. Add `ICalendarSelectionService`:

```csharp
// MainPage.xaml.cs — updated constructor
public MainPage(MainViewModel viewModel, ICalendarSelectionService selectionService)
{
    _selectionService = selectionService;
    ViewModel = viewModel;
    InitializeComponent();
    ...
}
```

And update DI registration in `App.xaml.cs` — `services.AddTransient<MainPage>()` relies on DI to resolve the constructor. Since `ICalendarSelectionService` is already registered as singleton, DI will inject it automatically — no changes to registration needed.

**Esc handler via `KeyboardAccelerator` in `MainPage.xaml`:**
```xml
<Page.KeyboardAccelerators>
    <KeyboardAccelerator Key="Escape" Invoked="OnEscapePressed"/>
</Page.KeyboardAccelerators>
```
```csharp
private void OnEscapePressed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
{
    _selectionService.ClearSelection();
    e.Handled = true;
}
```

`MainPage` navigation and view-switch controls must not clear selection. The service remains the single source of truth until an explicit clear action occurs.

### Year View — No Events, No Selection

`YearViewControl` shows day-number buttons + sync indicator dots only. There are no event elements to select. The year view day buttons navigate to Month view (`DayButton_Click`). Do NOT add event tap handlers to year view.

Add background Tapped to clear selection when user clicks in the year view area:
```csharp
MonthsGrid.Tapped += (_, _) => _selectionService.ClearSelection();
```

### WeekReferenceMessenger Registration Pattern

From `SettingsViewModel.cs` (confirmed working pattern):
```csharp
WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, OnEventSelected);
// ...unregister:
WeakReferenceMessenger.Default.UnregisterAll(this);
```

Register in `Loaded`, unregister in `Unloaded` to avoid leaks when Frame navigates away.

### Unit Tests for CalendarSelectionService

File: `GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs`

Follow xUnit + FluentAssertions pattern (see `MainViewModelTests.cs` for style reference).

```csharp
public sealed class CalendarSelectionServiceTests : IDisposable
{
    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private EventSelectedMessage? Capture()
    {
        EventSelectedMessage? received = null;
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, m) => received = m);
        return received; // returned AFTER action in each test
    }
}
```

**Tests to write:**

```csharp
[Fact]
public void Select_NewId_UpdatesSelectedIdAndSendsMessage()
{
    EventSelectedMessage? received = null;
    WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, m) => received = m);
    var sut = new CalendarSelectionService();

    sut.Select("evt-1");

    sut.SelectedGcalEventId.Should().Be("evt-1");
    received.Should().NotBeNull();
    received!.GcalEventId.Should().Be("evt-1");
}

[Fact]
public void Select_DifferentId_MovesSelectionAndSendsMessage()
{
    var sut = new CalendarSelectionService();
    sut.Select("evt-1");

    EventSelectedMessage? received = null;
    WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, m) => received = m);

    sut.Select("evt-2");

    sut.SelectedGcalEventId.Should().Be("evt-2");
    received!.GcalEventId.Should().Be("evt-2");
}

[Fact]
public void Select_SameId_SuppressesMessage()
{
    // CalendarSelectionService short-circuits on same ID — NO message sent
    var sut = new CalendarSelectionService();
    sut.Select("evt-1");

    var messageCount = 0;
    WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, _) => messageCount++);

    sut.Select("evt-1"); // same ID again

    messageCount.Should().Be(0);
    sut.SelectedGcalEventId.Should().Be("evt-1");
}

[Fact]
public void ClearSelection_WhenSelected_ClearsAndSendsNullMessage()
{
    var sut = new CalendarSelectionService();
    sut.Select("evt-1");

    EventSelectedMessage? received = null;
    WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, m) => received = m);

    sut.ClearSelection();

    sut.SelectedGcalEventId.Should().BeNull();
    received!.GcalEventId.Should().BeNull();
}

[Fact]
public void ClearSelection_WhenAlreadyClear_SuppressesMessage()
{
    // CalendarSelectionService short-circuits when already null — NO message sent
    var sut = new CalendarSelectionService();

    var messageCount = 0;
    WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (_, _) => messageCount++);

    sut.ClearSelection(); // nothing selected

    messageCount.Should().Be(0);
}

[Theory]
[InlineData("")]
[InlineData("   ")]
public void Select_EmptyOrWhitespace_ThrowsArgumentException(string invalidId)
{
    var sut = new CalendarSelectionService();

    Action act = () => sut.Select(invalidId);

    act.Should().Throw<ArgumentException>();
}
```

### File Changes Summary

**Already exists (do not recreate):**
```
Services/ICalendarSelectionService.cs    ✅ done
Services/CalendarSelectionService.cs     ✅ done
Messages/EventSelectedMessage.cs         ✅ done
App.xaml.cs (DI registration)           ✅ done
```

**Files to modify:**
```
Views/MonthViewControl.xaml.cs   — add selection + tooltip + persistence reapply
Views/WeekViewControl.xaml.cs    — add selection + tooltip
Views/DayViewControl.xaml.cs     — add selection + tooltip
Views/YearViewControl.xaml.cs    — add background Tapped only (no events to select)
Views/MainPage.xaml              — add KeyboardAccelerator for Esc
Views/MainPage.xaml.cs           — inject ICalendarSelectionService, add Esc handler, preserve selection across shell navigation
```

**Files to create:**
```
GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs
```

**Do NOT create:**
- No EventChip.xaml or EventBlock.xaml user controls
- No changes to MainViewModel (selection state lives in ICalendarSelectionService, not VM)
- No changes to Models/ or Services/ beyond what's listed

### Previous Story / Git Intelligence

- **Story 3.1 "first draft" commit** (f83707e) established `MonthViewControl`, `WeekViewControl`, `DayViewControl`, `YearViewControl` with inline event rendering via `Border` elements in code-behind. These views are the target for selection wiring.
- **`DispatcherQueue.TryEnqueue()`** is the WinUI 3 pattern for marshal-to-UI-thread calls from `WeakReferenceMessenger` callbacks (messenger callbacks may arrive on any thread).
- **Test file location**: `GoogleCalendarManagement.Tests/Unit/Services/` — existing tests at `ColorMappingServiceTests.cs` and `NavigationStateServiceTests.cs` in this folder confirm the location.
- **FluentAssertions** is already referenced in the test project.
- **No Moq needed** for `CalendarSelectionService` tests — testing the concrete class directly.

### References

- [Services/ICalendarSelectionService.cs](Services/ICalendarSelectionService.cs)
- [Services/CalendarSelectionService.cs](Services/CalendarSelectionService.cs)
- [Messages/EventSelectedMessage.cs](Messages/EventSelectedMessage.cs)
- [Views/MonthViewControl.xaml.cs](Views/MonthViewControl.xaml.cs) — Rebuild() pattern to replicate
- [Views/WeekViewControl.xaml.cs](Views/WeekViewControl.xaml.cs)
- [Views/DayViewControl.xaml.cs](Views/DayViewControl.xaml.cs)
- [Views/MainPage.xaml.cs](Views/MainPage.xaml.cs) — constructor to extend
- [App.xaml.cs](App.xaml.cs) — existing DI registrations
- [Epic 3 tech spec](docs/epic-3/tech-spec.md) — AC and flow definitions (note: spec uses `int Id`, actual code uses `string GcalEventId`)

## Dev Agent Record

### Context Reference

- [Story Context XML](3-3-implement-event-selection-with-visual-feedback.context.xml) - Generated 2026-03-31

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- 2026-04-02: Updated Story 3.3 acceptance criteria and generated context to use a silvery-white selection outline and persistent selection across view/period navigation.
- 2026-04-02: Implemented selection visuals, messenger subscriptions, tooltip wiring, and selection reapply logic in Month/Week/Day views; added blank-calendar clear handling without clearing from shell controls.
- 2026-04-02: Preserved selection through year-view day-button navigation and wired `Escape` clearing in `MainPage`.
- 2026-04-02: `dotnet build -p:Platform=x64` passed locally.
- 2026-04-02: User-reported `dotnet test` passed locally with 101/101 tests successful.

### Completion Notes List

- Added silvery-white selection outlines to rendered event borders in Month, Week, and Day views while preserving each border's original non-selected outline state.
- Reapplied the current `ICalendarSelectionService.SelectedGcalEventId` after each view rebuild so selection persists through view switches, previous/next navigation, and other data refreshes.
- Restricted automatic clearing to explicit calendar-surface interactions and `Escape`; main shell navigation controls do not clear selection.
- Added `CalendarSelectionServiceTests` covering new selection, selection changes, duplicate suppression, clear behavior, and invalid ID argument validation.
- Story remains `in-progress` because Task 11 manual UI verification has not been completed in a live WinUI session.

### File List

- App.xaml.cs
- GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs
- Views/DayViewControl.xaml.cs
- Views/MainPage.xaml.cs
- Views/MonthViewControl.xaml.cs
- Views/WeekViewControl.xaml.cs
- Views/YearViewControl.xaml.cs
- docs/epic-3/stories/3-3-implement-event-selection-with-visual-feedback.context.xml
- docs/epic-3/stories/3-3-implement-event-selection-with-visual-feedback.md
- docs/sprint-status.yaml

### Change Log

- 2026-04-02: Updated Story 3.3 to require a silvery-white outline and persistent selection across view and period navigation.
- 2026-04-02: Implemented event selection visuals and persistence across Month, Week, Day, Year, and MainPage interactions.
- 2026-04-02: Added unit coverage for `CalendarSelectionService` and validated with successful build and test runs.

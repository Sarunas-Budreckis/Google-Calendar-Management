# Story 3.3: Implement Event Selection with Visual Feedback

Status: ready-for-dev

## Story

As a **calendar user**,
I want **to click any event and see it visually highlighted with a red outline, with only one event selected at a time, and clear selection by pressing Esc or clicking an empty area**,
so that **I always know which event is active and can proceed to view its details**.

## Acceptance Criteria

1. **AC-3.3.1 — Red outline on click:** Given the user clicks an event in any view, the event gains a 2 px solid red outline within 50 ms of the click.
2. **AC-3.3.2 — Single-select enforcement:** Given an event is already selected, when the user clicks a different event, the red outline moves to the newly clicked event; the previously selected event returns to its normal appearance.
3. **AC-3.3.3 — Esc clears selection:** Given an event is selected, when the user presses Esc, the selection is cleared and all event outlines are removed.
4. **AC-3.3.4 — Click-empty-area clears selection:** Given an event is selected, when the user clicks any empty calendar area (not on an event), the selection is cleared.
5. **AC-3.3.5 — Hover tooltip:** Given the user hovers over any event without clicking it, a tooltip appears showing the event title and formatted start/end time.

## Tasks / Subtasks

- [ ] **Task 1: Create `ICalendarSelectionService` interface** (AC: 3.3.1, 3.3.2, 3.3.3)
  - [ ] Create `Services/ICalendarSelectionService.cs` with `SelectedEventId`, `Select(int)`, `ClearSelection()`
  - [ ] Namespace: `GoogleCalendarManagement.Services`

- [ ] **Task 2: Create `CalendarSelectionService` implementation** (AC: 3.3.1, 3.3.2, 3.3.3)
  - [ ] Create `Services/CalendarSelectionService.cs` implementing `ICalendarSelectionService`
  - [ ] On `Select(id)`: update `SelectedEventId`, send `EventSelectedMessage(id)` via `WeakReferenceMessenger.Default`
  - [ ] On `ClearSelection()`: set `SelectedEventId = null`, send `EventSelectedMessage(null)`
  - [ ] Calling `Select(id)` when `id` is already selected must still send the message (idempotent re-select is fine; do not suppress)

- [ ] **Task 3: Create `EventSelectedMessage`** (AC: 3.3.1, 3.3.2, 3.3.3, 3.3.4)
  - [ ] Create `Messages/EventSelectedMessage.cs` as `public sealed record EventSelectedMessage(int? EventId);`
  - [ ] Namespace: `GoogleCalendarManagement.Messages`

- [ ] **Task 4: Register service in DI** (all ACs)
  - [ ] Add `services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();` to `App.xaml.cs → ConfigureServices()`
  - [ ] Position after existing singleton registrations, before ViewModel registrations

- [ ] **Task 5: Wire selection into `EventChip` user control** (AC: 3.3.1, 3.3.2, 3.3.4, 3.3.5)
  - [ ] **Prerequisite: Story 3.1 must be complete** — `EventChip` user control must exist
  - [ ] Add a `SelectedEventId` dependency property (or bind to `MainViewModel.SelectedEventId`) to drive `VisualState`
  - [ ] Add `VisualStateGroup` with states `Normal` and `Selected`; `Selected` state: `Border.BorderBrush="#FF0000"`, `Border.BorderThickness="2"`
  - [ ] Handle `Tapped` event on EventChip → call `ICalendarSelectionService.Select(EventId)` (inject via ViewModel binding or constructor if UserControl)
  - [ ] Subscribe to `EventSelectedMessage` in EventChip or parent ViewModel to trigger `VisualStateManager.GoToState`
  - [ ] `ToolTipService.ToolTip` bound to a formatted string: `"{Title}\n{LocalStart} – {LocalEnd}"`

- [ ] **Task 6: Wire selection into `EventBlock` user control (week/day views)** (AC: 3.3.1, 3.3.2, 3.3.4, 3.3.5)
  - [ ] Same VisualState and tooltip pattern as EventChip (Task 5)
  - [ ] **Prerequisite: Story 3.1 must be complete** — `EventBlock` user control must exist

- [ ] **Task 7: Esc key handler** (AC: 3.3.3)
  - [ ] In `MainWindow.xaml` (or `MainViewModel`), add a `KeyboardAccelerator` for `VirtualKey.Escape` that calls `ICalendarSelectionService.ClearSelection()`
  - [ ] Use `KeyboardAccelerator` in XAML (preferred) or `KeyDown` handler on the root `Page`/`Grid`
  - [ ] Esc must work regardless of which view is active (Year/Month/Week/Day)

- [ ] **Task 8: Click-empty-area handler** (AC: 3.3.4)
  - [ ] In each view control (Year, Month, Week, Day), handle `Tapped` on the root container (not on EventChip/EventBlock — those stop propagation)
  - [ ] Call `ICalendarSelectionService.ClearSelection()` from the tapped handler
  - [ ] Use `e.Handled = true` in EventChip/EventBlock `Tapped` to prevent bubbling to the container

- [ ] **Task 9: Unit tests for `CalendarSelectionService`** (AC: 3.3.1–3.3.4)
  - [ ] Create `GoogleCalendarManagement.Tests/Unit/CalendarSelectionServiceTests.cs`
  - [ ] Test: `Select(1)` → `SelectedEventId == 1` and `EventSelectedMessage(1)` received
  - [ ] Test: `Select(2)` while `1` selected → `SelectedEventId == 2` and `EventSelectedMessage(2)` received (only one message, eventId=2)
  - [ ] Test: `ClearSelection()` → `SelectedEventId == null` and `EventSelectedMessage(null)` received
  - [ ] Test: `ClearSelection()` when nothing selected → `EventSelectedMessage(null)` sent (idempotent)

- [ ] **Task 10: Final validation**
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual: click event → red outline appears, no other event outlined
  - [ ] Manual: click second event → first outline gone, second outlined
  - [ ] Manual: press Esc → outline disappears
  - [ ] Manual: click empty calendar area → outline disappears
  - [ ] Manual: hover event → tooltip shows title and time

## Dev Notes

### Architecture Patterns and Constraints

**Project structure is flat (single-project), NOT the multi-library layout in architecture.md:**
- No `src/` directory, no `GoogleCalendarManagement.Core/` or `GoogleCalendarManagement.Data/` class libraries
- All services, interfaces, ViewModels, Views, Messages co-exist in the root `GoogleCalendarManagement` project
- `Services/` contains both interfaces and implementations (e.g., `IContentDialogService.cs` + `ContentDialogService.cs` side-by-side)
- When the epic-3 tech spec says `GoogleCalendarManagement.Core/Interfaces/ICalendarSelectionService.cs`, the **actual target path is `Services/ICalendarSelectionService.cs`**

**DI registration pattern (from `App.xaml.cs`):**
```csharp
services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();
```
Register in `ConfigureServices()`, alongside existing singleton registrations like `IContentDialogService`, `ISyncManager`.

**WeakReferenceMessenger pattern (confirmed in `SettingsViewModel.cs`):**
```csharp
WeakReferenceMessenger.Default.Send(new AuthenticationSucceededMessage());
```
Use `WeakReferenceMessenger.Default` (not constructor-injected messenger). No need to register a channel.

**Message pattern (from `Messages/AuthenticationSucceededMessage.cs`):**
```csharp
namespace GoogleCalendarManagement.Messages;
public sealed class AuthenticationSucceededMessage;
```
For `EventSelectedMessage`, use a record with a payload:
```csharp
namespace GoogleCalendarManagement.Messages;
public sealed record EventSelectedMessage(int? EventId);
```

**MVVM pattern (from `SettingsViewModel.cs`):**
- ViewModels extend `ObservableObject` from `CommunityToolkit.Mvvm.ComponentModel`
- Commands are `RelayCommand` / `AsyncRelayCommand` from `CommunityToolkit.Mvvm.Input`
- Properties use `SetProperty(ref _field, value)` for change notification
- `OnPropertyChanged(nameof(Prop))` for computed properties

**LAYERING CONSTRAINT (from epic-3 tech spec):**
- All business logic lives in services with no WinUI 3 dependency
- `CalendarSelectionService` must NOT import any `Microsoft.UI.*` namespace
- ViewModels translate service state into observable properties
- Views bind to ViewModels via XAML — no code-behind business logic

**Namespace structure:**
```
GoogleCalendarManagement.Services        → ICalendarSelectionService, CalendarSelectionService
GoogleCalendarManagement.Messages        → EventSelectedMessage
GoogleCalendarManagement.ViewModels      → (subscribe to EventSelectedMessage in MainViewModel)
GoogleCalendarManagement.Views           → (view controls wire tap/key handlers)
```

### Interface Specification

```csharp
// Services/ICalendarSelectionService.cs
namespace GoogleCalendarManagement.Services;

public interface ICalendarSelectionService
{
    int? SelectedEventId { get; }
    void Select(int eventId);
    void ClearSelection();
}
```

### Implementation Specification

```csharp
// Services/CalendarSelectionService.cs
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarSelectionService : ICalendarSelectionService
{
    public int? SelectedEventId { get; private set; }

    public void Select(int eventId)
    {
        SelectedEventId = eventId;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(eventId));
    }

    public void ClearSelection()
    {
        SelectedEventId = null;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
    }
}
```

### XAML Visual Feedback

**VisualState pattern for `EventChip` and `EventBlock`:**
```xml
<VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="SelectionStates">
        <VisualState x:Name="Normal"/>
        <VisualState x:Name="Selected">
            <VisualState.Setters>
                <Setter Target="RootBorder.BorderBrush" Value="#FF0000"/>
                <Setter Target="RootBorder.BorderThickness" Value="2"/>
            </VisualState.Setters>
        </VisualState>
    </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

**Trigger VisualState from code-behind or ViewModel:**
```csharp
VisualStateManager.GoToState(this, isSelected ? "Selected" : "Normal", false);
```

**Tooltip binding:**
```xml
<ToolTipService.ToolTip>
    <ToolTip Content="{x:Bind TooltipText}"/>
</ToolTipService.ToolTip>
```
`TooltipText` is a computed property on the event VM: `$"{Title}\n{LocalStart:HH:mm} – {LocalEnd:HH:mm}"`.

**Esc keyboard accelerator in MainWindow.xaml:**
```xml
<Window.KeyboardAccelerators>
    <KeyboardAccelerator Key="Escape" Invoked="OnEscapePressed"/>
</Window.KeyboardAccelerators>
```
```csharp
private void OnEscapePressed(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs e)
{
    _selectionService.ClearSelection();
    e.Handled = true;
}
```

**Click-empty-area pattern:**
```xml
<!-- In each view root container -->
<Grid Tapped="OnBackgroundTapped">
    <!-- EventChip elements stop propagation with e.Handled = true -->
</Grid>
```

### Project Structure Notes

**Files to create:**
```
Services/
├── ICalendarSelectionService.cs     (new)
└── CalendarSelectionService.cs      (new)

Messages/
└── EventSelectedMessage.cs          (new)

App.xaml.cs                          (update — add DI registration)

GoogleCalendarManagement.Tests/Unit/
└── CalendarSelectionServiceTests.cs (new)
```

**Files to update (require Story 3.1 views to exist first):**
```
Views/Controls/
├── EventChip.xaml + EventChip.xaml.cs      (add VisualState, Tapped handler, tooltip)
└── EventBlock.xaml + EventBlock.xaml.cs    (same)

Views/
├── MainWindow.xaml                          (add Esc KeyboardAccelerator)
└── MainWindow.xaml.cs                       (add EscapePressed handler)

Views/Controls/
├── YearViewControl.xaml.cs                  (add background Tapped handler)
├── MonthViewControl.xaml.cs                 (add background Tapped handler)
├── WeekViewControl.xaml.cs                  (add background Tapped handler)
└── DayViewControl.xaml.cs                   (add background Tapped handler)
```

**Actual path vs. tech spec path:**

| Tech spec says | Actual path |
|---|---|
| `GoogleCalendarManagement.Core/Interfaces/ICalendarSelectionService.cs` | `Services/ICalendarSelectionService.cs` |
| `GoogleCalendarManagement.Core/Services/CalendarSelectionService.cs` | `Services/CalendarSelectionService.cs` |

### Previous Story Intelligence

- **Story 2.3A (latest done)** established `WeakReferenceMessenger.Default.Send(...)` as the cross-ViewModel messaging pattern — confirmed in `SettingsViewModel.cs:147`. Use the same pattern.
- **Story 2.3A** established all async methods suffixed `Async` — but selection methods are synchronous by design (in-memory state + messaging).
- Tests live in `GoogleCalendarManagement.Tests/Unit/` (e.g., `SettingsViewModelTests.cs`). Follow xUnit test class naming: `[ClassName]Tests.cs`.
- DI registration order in `ConfigureServices()`: infrastructure singletons first, then domain services, then ViewModels, then Views.

### Story Dependency

**Story 3.3 has a split implementation scope:**

| Scope | Depends on | Can start |
|---|---|---|
| `ICalendarSelectionService`, `CalendarSelectionService`, `EventSelectedMessage`, DI registration, unit tests | Nothing (pure C#) | Immediately |
| View wiring (VisualState, Tapped, Esc, tooltips) | Story 3.1 (EventChip, EventBlock, view controls must exist) | After Story 3.1 |

If implementing Story 3.3 before Story 3.1 is done: complete Tasks 1–4 and Task 9, and stub the view integration with TODO comments in the target view files.

### Testing Pattern

Test `CalendarSelectionService` by subscribing to `WeakReferenceMessenger.Default` before calling methods:

```csharp
// Arrange
EventSelectedMessage? received = null;
WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (r, m) => received = m);
var sut = new CalendarSelectionService();

// Act
sut.Select(42);

// Assert
Assert.Equal(42, sut.SelectedEventId);
Assert.NotNull(received);
Assert.Equal(42, received!.EventId);

// Cleanup
WeakReferenceMessenger.Default.UnregisterAll(this);
```

Always call `WeakReferenceMessenger.Default.UnregisterAll(this)` in test teardown to avoid cross-test message leakage.

### References

- [Epic 3 tech spec — Story 3.3 ACs](docs/epic-3/tech-spec.md#story-33--event-selection-with-visual-feedback) — authoritative AC source
- [Epic 3 tech spec — CalendarSelectionService spec](docs/epic-3/tech-spec.md#services-and-modules) — interface + implementation design
- [Epic 3 tech spec — Flow 3 and Flow 4](docs/epic-3/tech-spec.md#workflows-and-sequencing) — selection and clear-selection workflows
- [Epic 3 tech spec — Traceability 13–17](docs/epic-3/tech-spec.md#traceability-mapping) — test ideas per AC
- [SettingsViewModel.cs](ViewModels/SettingsViewModel.cs) — confirmed WeakReferenceMessenger.Default usage pattern
- [Messages/AuthenticationSucceededMessage.cs](Messages/AuthenticationSucceededMessage.cs) — message class pattern
- [App.xaml.cs](App.xaml.cs) — DI registration pattern and ConfigureServices() location
- [Architecture.md](docs/architecture.md) — project structural overview (note: actual structure diverges from multi-project layout described here)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

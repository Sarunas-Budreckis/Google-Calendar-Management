# Story 3.4: Create Event Details Panel (Read-Only)

Status: ready-for-dev

## Story

As a **calendar user**,
I want **a read-only details panel that slides in from the right when I select an event, showing all its details, and closes when I press Esc or click the close button**,
so that **I can read the full event information without leaving the calendar view**.

## Acceptance Criteria

1. **AC-3.4.1 — Panel slides in with full event data:** Given the user selects an event, the event details panel slides in from the right within 200 ms and displays: title, start/end date and time (local timezone), colour indicator swatch, description (scrollable), source label ("From Google Calendar"), and last synced timestamp.
2. **AC-3.4.2 — Edit button is disabled with Tier 2 tooltip:** Given the details panel is visible, clicking the "Edit" button shows the tooltip "Coming in Tier 2" and takes no further action; the button is visually disabled.
3. **AC-3.4.3 — Esc or close button dismisses the panel:** Given the details panel is visible, pressing Esc or clicking the close button slides the panel out and clears the event selection.
4. **AC-3.4.4 — Panel persists across view mode switches:** Given the details panel is open and the user switches view mode (e.g. Month → Week), the panel remains visible and continues showing the same selected event's details.

## Tasks / Subtasks

- [ ] **Task 1: Create `EventDetailsPanelViewModel`** (AC: 3.4.1, 3.4.2, 3.4.3, 3.4.4)
  - [ ] Create `ViewModels/EventDetailsPanelViewModel.cs` extending `ObservableObject`
  - [ ] Constructor: inject `ICalendarSelectionService`, `ICalendarQueryService`, `ILogger<EventDetailsPanelViewModel>`
  - [ ] Subscribe to `EventSelectedMessage` via `WeakReferenceMessenger.Default.Register<EventSelectedMessage>(...)`
  - [ ] On `EventSelectedMessage(id != null)`: call `ICalendarQueryService.GetEventByIdAsync(id)`, populate observable properties, set `IsPanelVisible = true`
  - [ ] On `EventSelectedMessage(null)`: set `IsPanelVisible = false`, clear properties
  - [ ] Implement `CloseCommand` (RelayCommand): calls `ICalendarSelectionService.ClearSelection()` which sends `EventSelectedMessage(null)` → panel closes via message handler
  - [ ] Observable properties: `IsPanelVisible`, `Title`, `DateTimeRange` (formatted string), `ColorHex`, `ColorName`, `Description`, `SourceLabel`, `LastSyncedText`
  - [ ] `DateTimeRange`: formatted as `"Monday, 15 January 2026, 09:00 – 10:30"` (local time) for timed events; `"Monday, 15 January 2026 (All day)"` for all-day events
  - [ ] `LastSyncedText`: `"Synced: {LastSyncedAt.ToLocalTime():g}"` or `"Sync time unavailable"` if null
  - [ ] `SourceLabel`: always `"From Google Calendar"` in Tier 1
  - [ ] Implement `IDisposable` or `IRecipient<EventSelectedMessage>` to unregister on disposal

- [ ] **Task 2: Create `EventDetailsPanelControl.xaml`** (AC: 3.4.1, 3.4.2, 3.4.3)
  - [ ] Create `Views/Controls/EventDetailsPanelControl.xaml` + `EventDetailsPanelControl.xaml.cs`
  - [ ] Panel: ~375 px wide, full height, right-anchored, overlays the calendar (no layout push)
  - [ ] Layout (top to bottom): title (`TextBlock`, large font), date/time range (`TextBlock`), colour swatch (`Rectangle` 16×16 + colour name `TextBlock`), description (`ScrollViewer` → `TextBlock`), source label (`TextBlock`), last synced timestamp (`TextBlock`), disabled "Edit" button with `ToolTipService.ToolTip="Coming in Tier 2"`, close button (×)
  - [ ] Slide-in animation: `TranslateTransform` from `X=375` to `X=0` in ≤200 ms using `DoubleAnimation` in a `Storyboard`, triggered when `IsPanelVisible` changes to `true`
  - [ ] Slide-out animation: `X=0` to `X=375`, triggered when `IsPanelVisible` changes to `false`
  - [ ] Alternatively use `EntranceThemeTransition` or `SlideNavigationTransitionInfo` if simpler in WinUI 3
  - [ ] `DataContext` bound to `EventDetailsPanelViewModel` (injected via `App.xaml.cs`)
  - [ ] Panel `Visibility` controlled by `IsPanelVisible` via `BoolToVisibilityConverter` (already in `Converters/`)
  - [ ] Close button `Command` bound to `CloseCommand`
  - [ ] Edit button: `IsEnabled="False"`, `ToolTipService.ToolTip="Coming in Tier 2"`
  - [ ] Description `TextBlock` inside a `ScrollViewer` with `MaxHeight` set (e.g. 200 px) so it doesn't push other content off screen

- [ ] **Task 3: Integrate panel into `MainWindow.xaml`** (AC: 3.4.1, 3.4.4)
  - [ ] Add `EventDetailsPanelControl` to `MainWindow.xaml` as a right-side overlay inside a root `Grid`
  - [ ] Use `HorizontalAlignment="Right"` on the panel so it overlays the calendar content
  - [ ] Panel must remain in the visual tree during view mode switches (do NOT unload/reload it on tab change) so `IsPanelVisible = true` persists across view switches

- [ ] **Task 4: Wire `ICalendarQueryService` (dependency on Story 3.1)** (AC: 3.4.1)
  - [ ] **Prerequisite: Story 3.1 must define `ICalendarQueryService` and `CalendarEventDisplayModel`**
  - [ ] `EventDetailsPanelViewModel` calls `ICalendarQueryService.GetEventByIdAsync(int id)` to fetch `CalendarEventDisplayModel`
  - [ ] If `GetEventByIdAsync` returns `null` (event not found), log a warning and call `ClearSelection()` — do not leave panel in broken state
  - [ ] `CalendarEventDisplayModel.LastSyncedAt` (nullable) drives `LastSyncedText`
  - [ ] `CalendarEventDisplayModel.ColorHex` drives the colour swatch (`Rectangle.Fill`)
  - [ ] `CalendarEventDisplayModel.Description` may be null — show empty `TextBlock` with `Visibility=Collapsed` if null/empty

- [ ] **Task 5: Register DI and wire Esc handler** (AC: 3.4.3)
  - [ ] Add `services.AddSingleton<EventDetailsPanelViewModel>();` to `App.xaml.cs → ConfigureServices()`
  - [ ] Add `services.AddTransient<EventDetailsPanelControl>();` (or resolve at window creation time)
  - [ ] Esc key handler (already defined in Story 3.3 in `MainWindow.xaml`) calls `ICalendarSelectionService.ClearSelection()` → `EventSelectedMessage(null)` → panel closes via ViewModel message handler. **Do NOT duplicate the Esc handler** — the Story 3.3 Esc handler is sufficient.

- [ ] **Task 6: Unit tests for `EventDetailsPanelViewModel`** (AC: 3.4.1–3.4.4)
  - [ ] Create `GoogleCalendarManagement.Tests/Unit/EventDetailsPanelViewModelTests.cs`
  - [ ] Test: `EventSelectedMessage(id)` → `IsPanelVisible = true`, all 6 fields populated from mock `CalendarEventDisplayModel`
  - [ ] Test: `EventSelectedMessage(null)` → `IsPanelVisible = false`
  - [ ] Test: `CloseCommand.Execute()` → `ICalendarSelectionService.ClearSelection()` called once
  - [ ] Test: `EventSelectedMessage(id)` received while panel already open → panel stays visible, fields updated to new event (view mode switch scenario)
  - [ ] Test: `GetEventByIdAsync` returns null → `IsPanelVisible = false`, warning logged
  - [ ] Mock `ICalendarQueryService` with Moq; mock `ICalendarSelectionService`

- [ ] **Task 7: Final validation**
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual: click event → panel slides in from right, all 6 fields populated
  - [ ] Manual: click Edit → tooltip "Coming in Tier 2" shows, no navigation
  - [ ] Manual: press Esc → panel slides out, event deselected
  - [ ] Manual: click close (×) → panel slides out, event deselected
  - [ ] Manual: switch Month → Week while panel open → panel stays visible with same event

## Dev Notes

### Architecture Patterns and Constraints

**Project structure is flat (single-project), NOT the multi-library layout in architecture.md:**
- No `src/` directory, no separate `Core`/`Data` class libraries
- `ViewModels/` contains all ViewModels (e.g., `SettingsViewModel.cs`)
- `Views/` contains XAML + code-behind; `Views/Controls/` for UserControls (convention from Epic 3 spec)
- `Services/` contains all service interfaces and implementations
- When tech spec says `EventDetailsPanelViewModel` in `UI/ViewModels` — actual path is `ViewModels/EventDetailsPanelViewModel.cs`

**MVVM pattern (confirmed in `SettingsViewModel.cs`):**
- Extend `ObservableObject` from `CommunityToolkit.Mvvm.ComponentModel`
- Properties use `SetProperty(ref _field, value)` for change notification
- Commands: `RelayCommand` / `AsyncRelayCommand` from `CommunityToolkit.Mvvm.Input`
- No business logic in code-behind — only bindings and animation triggers

**WeakReferenceMessenger pattern (confirmed in `SettingsViewModel.cs:147`):**
```csharp
// Subscribe
WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (r, m) => OnEventSelected(m));
// Unsubscribe (in Dispose or page unload)
WeakReferenceMessenger.Default.UnregisterAll(this);
```

**DI pattern (from `App.xaml.cs`):**
```csharp
services.AddSingleton<EventDetailsPanelViewModel>();
services.AddTransient<EventDetailsPanelControl>();
```

**Layering constraint:** `EventDetailsPanelViewModel` must NOT import `Microsoft.UI.*`. All WinUI 3 concerns stay in `EventDetailsPanelControl.xaml.cs`.

### ViewModel Specification

```csharp
// ViewModels/EventDetailsPanelViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.ViewModels;

public sealed class EventDetailsPanelViewModel : ObservableObject
{
    private readonly ICalendarSelectionService _selectionService;
    private readonly ICalendarQueryService _queryService;
    private readonly ILogger<EventDetailsPanelViewModel> _logger;

    private bool _isPanelVisible;
    private string _title = string.Empty;
    private string _dateTimeRange = string.Empty;
    private string _colorHex = "#0088CC";
    private string _colorName = string.Empty;
    private string _description = string.Empty;
    private string _sourceLabel = "From Google Calendar";
    private string _lastSyncedText = string.Empty;

    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        private set => SetProperty(ref _isPanelVisible, value);
    }

    // ... other properties with SetProperty

    public IRelayCommand CloseCommand { get; }

    public EventDetailsPanelViewModel(
        ICalendarSelectionService selectionService,
        ICalendarQueryService queryService,
        ILogger<EventDetailsPanelViewModel> logger)
    {
        _selectionService = selectionService;
        _queryService = queryService;
        _logger = logger;
        CloseCommand = new RelayCommand(() => _selectionService.ClearSelection());
        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, OnEventSelected);
    }

    private async void OnEventSelected(object recipient, EventSelectedMessage message)
    {
        if (message.EventId is null)
        {
            IsPanelVisible = false;
            return;
        }
        var model = await _queryService.GetEventByIdAsync(message.EventId.Value);
        if (model is null)
        {
            _logger.LogWarning("Event {Id} not found; closing panel.", message.EventId);
            _selectionService.ClearSelection();
            return;
        }
        // populate observable properties from model
        IsPanelVisible = true;
    }
}
```

### XAML Slide Animation Pattern

**Option A — Storyboard (more control):**
```xml
<UserControl.Resources>
    <Storyboard x:Name="SlideInStoryboard">
        <DoubleAnimation Storyboard.TargetName="PanelTransform"
                         Storyboard.TargetProperty="X"
                         From="375" To="0" Duration="0:0:0.2"
                         EasingFunction="{StaticResource FastInFastOutEasing}"/>
    </Storyboard>
    <Storyboard x:Name="SlideOutStoryboard">
        <DoubleAnimation Storyboard.TargetName="PanelTransform"
                         Storyboard.TargetProperty="X"
                         From="0" To="375" Duration="0:0:0.2"/>
    </Storyboard>
</UserControl.Resources>

<Border x:Name="PanelRoot" Width="375">
    <Border.RenderTransform>
        <TranslateTransform x:Name="PanelTransform" X="375"/>
    </Border.RenderTransform>
    ...
</Border>
```
Trigger from code-behind via `IsPanelVisible` property change notification.

**Option B — EntranceThemeTransition (simpler, less control):**
```xml
<Border>
    <Border.Transitions>
        <TransitionCollection>
            <EntranceThemeTransition FromHorizontalOffset="375"/>
        </TransitionCollection>
    </Border.Transitions>
</Border>
```
`EntranceThemeTransition` only fires on element visibility change (Visibility.Visible). For slide-out use a `RepositionThemeTransition` or fallback to Option A.

**Recommendation:** Use Option A (Storyboard) for full control over both in and out animations.

### Panel Layout Structure

```
EventDetailsPanelControl (UserControl, Width=375, full height, right-aligned overlay)
├── Border (shadow/background, e.g. card-like background from WinUI 3 resources)
│   └── StackPanel (Padding="16")
│       ├── Grid (close button row)
│       │   ├── TextBlock "Event Details" (header)
│       │   └── Button CloseCommand (×, right-aligned)
│       ├── TextBlock Title (FontSize=20, bold, TextWrapping=Wrap)
│       ├── TextBlock DateTimeRange (FontSize=14, muted)
│       ├── StackPanel (Orientation=Horizontal, colour swatch row)
│       │   ├── Rectangle (16×16, Fill=ColorHex, CornerRadius=2)
│       │   └── TextBlock ColorName (margin-left=8)
│       ├── TextBlock "Description" (section label, if description non-empty)
│       ├── ScrollViewer (MaxHeight=200, visible only if description non-empty)
│       │   └── TextBlock Description (TextWrapping=Wrap)
│       ├── TextBlock SourceLabel ("From Google Calendar", muted, small)
│       ├── TextBlock LastSyncedText (muted, small)
│       └── Button "Edit" (IsEnabled=False, ToolTipService.ToolTip="Coming in Tier 2")
```

### Color Swatch Binding

`CalendarEventDisplayModel.ColorHex` is a string (e.g., `"#0088CC"`). To bind to `Rectangle.Fill`:

```xml
<Rectangle Fill="{x:Bind ColorHex, Converter={StaticResource StringToSolidColorBrushConverter}}"/>
```

Create `Converters/StringToSolidColorBrushConverter.cs` if it doesn't exist from Story 3.2 (Story 3.2 may have created a `ColorConverter.cs`). Check first before creating a new one.

### Dependency on Other Stories

| Dependency | What it provides | Story |
|---|---|---|
| `ICalendarQueryService.GetEventByIdAsync` | Fetch `CalendarEventDisplayModel` by id | Story 3.1 |
| `CalendarEventDisplayModel` record | All event fields (title, times, colorHex, description, lastSyncedAt) | Story 3.1 |
| `IColorMappingService` (via display model) | `ColorHex` already resolved in the display model | Story 3.2 |
| `ICalendarSelectionService`, `EventSelectedMessage` | Receive selection events, call ClearSelection | Story 3.3 |
| Esc `KeyboardAccelerator` in `MainWindow` | Already handles Esc → ClearSelection | Story 3.3 |

**Do NOT duplicate the Esc handler.** Story 3.3 wires Esc → `ClearSelection()` → `EventSelectedMessage(null)` → panel closes via ViewModel. No additional Esc logic needed in Story 3.4.

### Project Structure Notes

**Files to create:**
```
ViewModels/
└── EventDetailsPanelViewModel.cs           (new)

Views/Controls/
├── EventDetailsPanelControl.xaml           (new)
└── EventDetailsPanelControl.xaml.cs        (new)

GoogleCalendarManagement.Tests/Unit/
└── EventDetailsPanelViewModelTests.cs      (new)

Converters/
└── StringToSolidColorBrushConverter.cs     (new, ONLY if not already created in Story 3.2)
```

**Files to update:**
```
App.xaml.cs             (add DI registrations for VM + control)
Views/MainWindow.xaml   (add EventDetailsPanelControl as right-aligned overlay)
```

**Actual path vs. tech spec path:**

| Tech spec says | Actual path |
|---|---|
| `EventDetailsPanelControl.xaml` in root UI | `Views/Controls/EventDetailsPanelControl.xaml` |
| `EventDetailsPanelViewModel` in `UI/ViewModels` | `ViewModels/EventDetailsPanelViewModel.cs` |

### Testing Pattern

```csharp
// Mock setup
var mockQuery = new Mock<ICalendarQueryService>();
mockQuery.Setup(q => q.GetEventByIdAsync(42, default))
         .ReturnsAsync(new CalendarEventDisplayModel(
             Id: 42, GcalEventId: "abc", Title: "Test",
             StartUtc: DateTime.UtcNow, EndUtc: DateTime.UtcNow.AddHours(1),
             IsAllDay: false, ColorHex: "#0088CC",
             IsRecurringInstance: false, Description: "Desc",
             LastSyncedAt: DateTime.UtcNow));

var mockSelection = new Mock<ICalendarSelectionService>();
var vm = new EventDetailsPanelViewModel(mockSelection.Object, mockQuery.Object, logger);

// Trigger via message (simulate story 3.3 service sending the message)
WeakReferenceMessenger.Default.Send(new EventSelectedMessage(42));
await Task.Yield(); // allow async handler to complete

Assert.True(vm.IsPanelVisible);
Assert.Equal("Test", vm.Title);

// Cleanup
WeakReferenceMessenger.Default.UnregisterAll(vm);
```

Always call `WeakReferenceMessenger.Default.UnregisterAll(vm)` after each test to prevent message leakage.

### Previous Story Intelligence

- **Story 2.3A (latest done):** All patterns established — `ObservableObject`, `SetProperty`, `WeakReferenceMessenger.Default`, `IDbContextFactory`, `async/await`, Serilog `ILogger<T>`. Follow exactly.
- **SettingsViewModel.cs** is the best existing reference for ViewModel patterns in this project.
- **`BoolToVisibilityConverter`** referenced in architecture doc (`Converters/BoolToVisibilityConverter.cs`) — check if it already exists before creating. If it does, use it for `IsPanelVisible → Visibility`.
- Build command: `dotnet build -p:Platform=x64`. Test command: `dotnet test`.
- Tests use xUnit + Moq + FluentAssertions (per epic-3 tech spec test strategy).

### References

- [Epic 3 tech spec — Story 3.4 ACs](docs/epic-3/tech-spec.md#story-34--event-details-panel-read-only) — authoritative ACs 18–21
- [Epic 3 tech spec — EventDetailsPanelViewModel spec](docs/epic-3/tech-spec.md#services-and-modules) — ViewModel responsibilities
- [Epic 3 tech spec — Flow 3 and Flow 4](docs/epic-3/tech-spec.md#workflows-and-sequencing) — selection → panel open → close flows
- [Epic 3 tech spec — Traceability 18–21](docs/epic-3/tech-spec.md#traceability-mapping) — test ideas per AC
- [Epic 3 tech spec — XAML layout](docs/epic-3/tech-spec.md#xaml-view-composition) — `EventDetailsPanelControl` layout spec
- [SettingsViewModel.cs](ViewModels/SettingsViewModel.cs) — confirmed ViewModel pattern, WeakReferenceMessenger usage
- [App.xaml.cs](App.xaml.cs) — DI registration pattern
- [Story 3.3 story file](_bmad-output/implementation-artifacts/3-3-implement-event-selection-with-visual-feedback.md) — `ICalendarSelectionService`, `EventSelectedMessage` definitions
- [Architecture.md](docs/architecture.md) — project structure overview (note: flat single-project, not multi-library)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

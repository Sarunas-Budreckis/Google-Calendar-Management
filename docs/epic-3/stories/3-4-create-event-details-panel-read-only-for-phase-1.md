# Story 3.4: Create Event Details Panel (Read-Only for Tier 1)

Status: ready-for-dev

## Story

As a **user**,
I want **to view full event details when I select an event**,
So that **I can see all information about the event without leaving the calendar view**.

## Acceptance Criteria

1. **AC-3.4.1 — Panel Appears on Selection:** Given a calendar view is displayed and an event is selected (via `ICalendarSelectionService.Select`), the `EventDetailsPanelControl` slides in from the right within 200 ms and becomes visible.

2. **AC-3.4.2 — Panel Displays All Required Fields:** Given the panel is visible, it shows: event title (large), start and end date/time (local timezone), colour indicator swatch with colour name, description (scrollable `ScrollViewer`, empty state handled), source label ("From Google Calendar"), and last synced timestamp (from `GcalEvent.LastSyncedAt`, formatted as local time or "Never" if null).

3. **AC-3.4.3 — Edit Button Disabled with Tooltip:** Given the panel is visible, it contains an "Edit" button that is `IsEnabled="False"` with `ToolTipService.ToolTip="Coming in Tier 2"` and clicking it takes no action.

4. **AC-3.4.4 — Panel Closes on Esc or Close Button:** Given the panel is visible, pressing Esc or clicking the close (×) button slides the panel out, sets `IsPanelVisible = false`, and calls `ICalendarSelectionService.ClearSelection()`.

5. **AC-3.4.5 — Panel Persists Across View Mode Switches:** Given the panel is open showing event details, when the user switches view mode (Year/Month/Week/Day), `IsPanelVisible` remains `true` and the same event details remain displayed.

6. **AC-3.4.6 — Panel Hides When Selection Cleared:** Given the panel is visible, when `EventSelectedMessage(null)` is received (selection cleared by Esc, empty-area click, or `ClearSelection()`), the panel slides out.

7. **AC-3.4.7 — No Crash on Missing Data:** Given an event has null `Description`, null `LastSyncedAt`, or an unrecognised `ColorId`, the panel renders gracefully with empty/fallback values (no exception thrown, no empty white box).

## Tasks / Subtasks

- [ ] **Task 1: Define shared data contracts (if not yet created by Story 3.3)** (AC: all)
  - [ ] Create `Messages/EventSelectedMessage.cs` — `public record EventSelectedMessage(string? GcalEventId);` (null = clear selection)
  - [ ] Create `Core/Interfaces/ICalendarSelectionService.cs` with `string? SelectedGcalEventId`, `void Select(string gcalEventId)`, `void ClearSelection()`
  - [ ] Create `Core/Services/CalendarSelectionService.cs` — holds selected ID, sends `EventSelectedMessage` via `WeakReferenceMessenger.Default`
  - [ ] Create `Core/Models/CalendarEventDisplayModel.cs` (record, see Dev Notes for exact fields)
  - [ ] Create `Core/Interfaces/IColorMappingService.cs` with `string GetHexColor(string? colorId)` and `string GetColorName(string? colorId)`
  - [ ] Create `Core/Services/ColorMappingService.cs` — hardcoded dictionary, Azure fallback `#0088CC` (see Dev Notes)
  - [ ] Create `Core/Interfaces/ICalendarQueryService.cs` — include at minimum `Task<CalendarEventDisplayModel?> GetEventByIdAsync(string gcalEventId, CancellationToken ct = default)`
  - [ ] Create `Core/Services/CalendarQueryService.cs` — implements `GetEventByIdAsync` by querying `gcal_event` via `IDbContextFactory<CalendarDbContext>`

- [ ] **Task 2: Implement EventDetailsPanelViewModel** (AC: 3.4.1, 3.4.2, 3.4.3, 3.4.4, 3.4.5, 3.4.6)
  - [ ] Create `ViewModels/EventDetailsPanelViewModel.cs` extending `ObservableObject`
  - [ ] Subscribe to `EventSelectedMessage` via `WeakReferenceMessenger.Default.Register<EventSelectedMessage>` in constructor
  - [ ] On message with non-null `GcalEventId`: call `ICalendarQueryService.GetEventByIdAsync`, populate observable properties, set `IsPanelVisible = true`
  - [ ] On message with null `GcalEventId`: set `IsPanelVisible = false`, clear properties
  - [ ] Expose `IsPanelVisible`, `Title`, `StartEndDisplay`, `ColorHex`, `ColorName`, `Description`, `LastSyncedDisplay` as observable properties
  - [ ] Implement `CloseCommand` (RelayCommand): calls `_selectionService.ClearSelection()` (triggers `EventSelectedMessage(null)`)
  - [ ] Unsubscribe in `IDisposable.Dispose` (implement `IDisposable`)

- [ ] **Task 3: Implement EventDetailsPanelControl XAML** (AC: 3.4.1, 3.4.2, 3.4.3, 3.4.4)
  - [ ] Create `Views/EventDetailsPanelControl.xaml` + `Views/EventDetailsPanelControl.xaml.cs`
  - [ ] ~375 px fixed width, full height, right-side panel
  - [ ] Slide-in animation via `ThemeTransition` or `TranslationTransition` on `Visibility` change
  - [ ] Bind `DataContext` to `EventDetailsPanelViewModel`
  - [ ] Handle Esc key: call `ViewModel.CloseCommand` (wire in code-behind via `KeyDown` on parent or `Page.KeyboardAccelerators`)
  - [ ] Display: large `TextBlock` for title, date/time row, `Rectangle` colour swatch + colour name `TextBlock`, scrollable description, source label, last-synced label
  - [ ] Disabled "Edit" `Button` with `IsEnabled="False"` and `ToolTipService.ToolTip="Coming in Tier 2"`
  - [ ] Close `Button` (×) bound to `CloseCommand`

- [ ] **Task 4: Register in DI and wire into App shell** (AC: all)
  - [ ] In `App.xaml.cs` `ConfigureServices`: register `ICalendarSelectionService` (singleton), `IColorMappingService` (singleton), `ICalendarQueryService` (singleton), `EventDetailsPanelViewModel` (singleton), `EventDetailsPanelControl` (transient)
  - [ ] Panel is a child control inside the future `MainWindow`; for now it can be placed inside `SettingsPage` shell or a new `MainPage` as a stub host — confirm placement with existing shell structure

- [ ] **Task 5: Write unit tests** (AC: 3.4.1, 3.4.2, 3.4.4, 3.4.5, 3.4.6, 3.4.7)
  - [ ] `GoogleCalendarManagement.Tests/Unit/EventDetailsPanelViewModelTests.cs`
  - [ ] Test: `EventSelectedMessage("gcal_123")` → `IsPanelVisible = true`, all display properties populated
  - [ ] Test: `EventSelectedMessage(null)` → `IsPanelVisible = false`
  - [ ] Test: View mode switch does NOT close panel (simulate by not sending null message → `IsPanelVisible` stays true)
  - [ ] Test: null `Description` → `Description` property is empty string, no exception
  - [ ] Test: null `LastSyncedAt` → `LastSyncedDisplay` is "Never"
  - [ ] Test: `CloseCommand.Execute()` → `ClearSelection()` called, `IsPanelVisible = false`
  - [ ] `GoogleCalendarManagement.Tests/Unit/CalendarSelectionServiceTests.cs`
  - [ ] Test: `Select("id")` sends `EventSelectedMessage("id")` via messenger
  - [ ] Test: `ClearSelection()` sends `EventSelectedMessage(null)`
  - [ ] `GoogleCalendarManagement.Tests/Unit/ColorMappingServiceTests.cs`
  - [ ] Test: null/unknown `colorId` returns `#0088CC` with no exception
  - [ ] Test: `"1"` / `"azure"` returns `#0088CC`

- [ ] **Task 6: Build and test** (AC: all)
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test`
  - [ ] Manual: select an event → panel slides in with correct data, close × → panel slides out

## Dev Notes

### CRITICAL: Schema Discrepancy — Integer vs String PK

The epic tech-spec and architecture doc describe `CalendarEventDisplayModel.Id` as `int` mapping to `gcal_event.id (PK)`. **This is wrong.** The actual implemented schema has:

```csharp
// GcalEventConfiguration.cs — GcalEventId IS the PK (string, Google Calendar's event ID)
builder.HasKey(e => e.GcalEventId);
// There is NO integer 'id' column in gcal_event
```

**Resolution for all Epic 3 stories:** Use `string GcalEventId` as the event key throughout. All service interfaces, messages, and display models must use `string` not `int`.

```csharp
// CORRECT CalendarEventDisplayModel
public record CalendarEventDisplayModel(
    string GcalEventId,          // gcal_event.gcal_event_id — use this as the unique key
    string Title,                // gcal_event.summary (empty string "" if null)
    DateTime? StartUtc,          // gcal_event.start_datetime (nullable in DB)
    DateTime? EndUtc,            // gcal_event.end_datetime (nullable in DB)
    bool IsAllDay,               // gcal_event.is_all_day ?? false
    string ColorHex,             // resolved by IColorMappingService
    string ColorName,            // resolved by IColorMappingService (e.g. "Azure")
    bool IsRecurringInstance,    // gcal_event.is_recurring_instance
    string? Description,         // gcal_event.description
    DateTime? LastSyncedAt       // gcal_event.last_synced_at (directly on GcalEvent entity)
);

// CORRECT ICalendarSelectionService
public interface ICalendarSelectionService
{
    string? SelectedGcalEventId { get; }
    void Select(string gcalEventId);
    void ClearSelection();
}

// CORRECT EventSelectedMessage
public record EventSelectedMessage(string? GcalEventId);  // null = cleared
```

### LastSyncedAt Source

`GcalEvent` has a `LastSyncedAt` property directly (`gcal_event.last_synced_at`). Use this field directly when projecting to `CalendarEventDisplayModel`. **Do NOT query `DataSourceRefresh` for individual event panel display** — that table tracks bulk sync operations, not per-event sync timestamps. The `DataSourceRefresh` table is used in `SettingsViewModel` for "Last Sync" overall display.

```csharp
// CalendarQueryService.GetEventByIdAsync projection
var ev = await context.GcalEvents
    .AsNoTracking()
    .FirstOrDefaultAsync(e => e.GcalEventId == gcalEventId && !e.IsDeleted, ct);

if (ev == null) return null;

return new CalendarEventDisplayModel(
    GcalEventId: ev.GcalEventId,
    Title: ev.Summary ?? "",
    StartUtc: ev.StartDatetime,
    EndUtc: ev.EndDatetime,
    IsAllDay: ev.IsAllDay ?? false,
    ColorHex: _colorService.GetHexColor(ev.ColorId),
    ColorName: _colorService.GetColorName(ev.ColorId),
    IsRecurringInstance: ev.IsRecurringInstance,
    Description: ev.Description,
    LastSyncedAt: ev.LastSyncedAt
);
```

### Project Structure — No "Core" Folder Exists Yet

The architecture describes a `Core` layer but **no such folder exists**. Create it for Epic 3 services:

```
GoogleCalendarManagement/
├── Core/
│   ├── Interfaces/
│   │   ├── ICalendarSelectionService.cs
│   │   ├── IColorMappingService.cs
│   │   └── ICalendarQueryService.cs
│   ├── Services/
│   │   ├── CalendarSelectionService.cs
│   │   ├── ColorMappingService.cs
│   │   └── CalendarQueryService.cs
│   └── Models/
│       └── CalendarEventDisplayModel.cs
├── Messages/
│   └── EventSelectedMessage.cs        ← new (AuthenticationSucceededMessage.cs exists as pattern)
├── ViewModels/
│   └── EventDetailsPanelViewModel.cs  ← new (SettingsViewModel.cs exists as pattern)
└── Views/
    ├── EventDetailsPanelControl.xaml   ← new
    └── EventDetailsPanelControl.xaml.cs ← new
```

**Namespace convention** (matches existing code):
- `GoogleCalendarManagement.Core.Interfaces`
- `GoogleCalendarManagement.Core.Services`
- `GoogleCalendarManagement.Core.Models`
- `GoogleCalendarManagement.Messages`
- `GoogleCalendarManagement.ViewModels`
- `GoogleCalendarManagement.Views`

### Existing WeakReferenceMessenger Pattern

Follow exactly the pattern in `Messages/AuthenticationSucceededMessage.cs` and `ViewModels/SettingsViewModel.cs`:

```csharp
// Send (in CalendarSelectionService)
WeakReferenceMessenger.Default.Send(new EventSelectedMessage(gcalEventId));

// Register (in EventDetailsPanelViewModel constructor)
WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (r, m) =>
{
    // Update UI on UI thread if needed
    _ = LoadEventDetailsAsync(m.GcalEventId);
});
```

### ColorMappingService — Confirmed Hex Values

Only Azure is confirmed: `#0088CC`. Other hex values are NOT yet defined in `_color-definitions.md`. For this story, implement the service with Azure confirmed + TBD placeholders for the other 8 colors (Story 3.2 owns finalizing all 9 hex values). Use Azure as fallback for any unrecognised `ColorId`.

```csharp
// GoogleCalendarId → (HexColor, DisplayName) mapping
// ColorId values from Google Calendar API are "1"-"11" numeric strings
private static readonly Dictionary<string, (string Hex, string Name)> _map = new()
{
    { "1",          ("#0088CC", "Azure") },        // Eudaimonia — confirmed
    { "azure",      ("#0088CC", "Azure") },        // alias
    { "2",          ("#TBD",    "Navy") },          // Personal Engineering — TBD Story 3.2
    { "lavender",   ("#TBD",    "Lavender") },      // alias — TBD
    { "3",          ("#TBD",    "Lavender") },
    { "flamingo",   ("#TBD",    "Flamingo") },
    { "4",          ("#TBD",    "Flamingo") },
    { "5",          ("#TBD",    "Yellow") },
    { "banana",     ("#TBD",    "Yellow") },
    { "6",          ("#TBD",    "Orange") },
    { "tangerine",  ("#TBD",    "Orange") },
    { "7",          ("#TBD",    "Sage") },
    { "sage",       ("#TBD",    "Sage") },
    { "8",          ("#TBD",    "Grey") },
    { "graphite",   ("#TBD",    "Grey") },
    { "9",          ("#TBD",    "Purple") },
    { "blueberry",  ("#TBD",    "Purple") },
    { "10",         ("#TBD",    "Sage") },          // Google "basil" → Sage in custom taxonomy
    { "11",         ("#TBD",    "Flamingo") },      // Google "tomato" → Flamingo in custom taxonomy
};
// Fallback for null or unknown:
public string GetHexColor(string? colorId) =>
    colorId != null && _map.TryGetValue(colorId, out var v) ? v.Hex : "#0088CC";
public string GetColorName(string? colorId) =>
    colorId != null && _map.TryGetValue(colorId, out var v) ? v.Name : "Azure";
```

### ViewModel Pattern — Match SettingsViewModel

```csharp
// EventDetailsPanelViewModel.cs
public sealed class EventDetailsPanelViewModel : ObservableObject, IDisposable
{
    private readonly ICalendarQueryService _queryService;
    private readonly ICalendarSelectionService _selectionService;
    private bool _isPanelVisible;
    private string _title = "";
    // ... other backing fields

    public EventDetailsPanelViewModel(
        ICalendarQueryService queryService,
        ICalendarSelectionService selectionService)
    {
        _queryService = queryService;
        _selectionService = selectionService;
        CloseCommand = new RelayCommand(ExecuteClose);

        WeakReferenceMessenger.Default.Register<EventSelectedMessage>(this, (r, m) =>
            _ = HandleEventSelectedAsync(m.GcalEventId));
    }

    public bool IsPanelVisible
    {
        get => _isPanelVisible;
        private set => SetProperty(ref _isPanelVisible, value);
    }

    // ... observable properties for Title, StartEndDisplay, ColorHex, ColorName, Description, LastSyncedDisplay

    public IRelayCommand CloseCommand { get; }

    private async Task HandleEventSelectedAsync(string? gcalEventId)
    {
        if (gcalEventId == null)
        {
            IsPanelVisible = false;
            return;
        }
        var ev = await _queryService.GetEventByIdAsync(gcalEventId);
        if (ev == null) return;  // event disappeared (soft-deleted); ignore

        Title = ev.Title;
        // ... populate remaining properties
        IsPanelVisible = true;
    }

    private void ExecuteClose() => _selectionService.ClearSelection();

    public void Dispose() =>
        WeakReferenceMessenger.Default.UnregisterAll(this);
}
```

### Date/Time Display

All UTC datetimes must be converted to local time for display:
```csharp
// StartEndDisplay computed property
private string FormatStartEnd(CalendarEventDisplayModel ev)
{
    if (ev.IsAllDay)
    {
        var start = ev.StartUtc?.ToLocalTime().ToString("ddd, MMM d, yyyy") ?? "";
        return ev.EndUtc.HasValue
            ? $"{start} (All Day)"
            : start;
    }
    var s = ev.StartUtc?.ToLocalTime().ToString("ddd, MMM d, yyyy h:mm tt") ?? "Unknown";
    var e = ev.EndUtc?.ToLocalTime().ToString("h:mm tt") ?? "";
    return $"{s} – {e}";
}
```

### XAML Slide Animation

Use `TranslationTransition` (WinUI 3 / Windows App SDK 1.8):
```xml
<UserControl.Resources>
    <TransitionCollection x:Key="SlideTransitions">
        <EntranceThemeTransition FromHorizontalOffset="375" IsStaggeringEnabled="False"/>
    </TransitionCollection>
</UserControl.Resources>

<!-- Panel visibility binding with transition -->
<Grid x:Name="PanelRoot"
      Width="375"
      Visibility="{x:Bind ViewModel.IsPanelVisible, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}"
      Transitions="{StaticResource SlideTransitions}">
```

If `EntranceThemeTransition` alone doesn't produce the desired slide on hide, use `Connected Animation` or `Storyboard` as fallback (target < 200 ms).

### DI Registration (App.xaml.cs ConfigureServices)

```csharp
// Add after existing service registrations:
services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();
services.AddSingleton<IColorMappingService, ColorMappingService>();
services.AddSingleton<ICalendarQueryService, CalendarQueryService>();
services.AddSingleton<EventDetailsPanelViewModel>();
services.AddTransient<EventDetailsPanelControl>();
```

### Story 3.3 Dependency

Story 3.3 owns `ICalendarSelectionService` and `EventSelectedMessage` as part of implementing event click selection. If story 3.4 is being implemented before 3.3 is done, you must create these contracts in this story. They MUST match the contracts that story 3.3 will depend on — do not create incompatible parallel definitions.

The `EventDetailsPanelViewModel` must NOT assume it is the only consumer of `EventSelectedMessage`. Stories 3.1–3.3 view controls will also subscribe to selection changes.

### CalendarQueryService DI

Use `IDbContextFactory<CalendarDbContext>` (already registered as `AddDbContextFactory` in App.xaml.cs) for thread-safe async DB access:

```csharp
public class CalendarQueryService : ICalendarQueryService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly IColorMappingService _colorService;

    public CalendarQueryService(IDbContextFactory<CalendarDbContext> contextFactory, IColorMappingService colorService)
    {
        _contextFactory = contextFactory;
        _colorService = colorService;
    }

    public async Task<CalendarEventDisplayModel?> GetEventByIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var ev = await context.GcalEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GcalEventId == gcalEventId && !e.IsDeleted, ct);
        if (ev == null) return null;
        return Project(ev);
    }
    // ...
}
```

### Anti-Patterns to Avoid

- **DO NOT** use `DbContext` directly (use `IDbContextFactory<CalendarDbContext>` for thread-safety)
- **DO NOT** use integer `Id` for event identity — there is no integer PK on `gcal_event`
- **DO NOT** put business logic in XAML code-behind — keep code-behind only for wiring (KeyDown handlers, setting DataContext)
- **DO NOT** subscribe to `WeakReferenceMessenger` without unsubscribing in `Dispose` (memory leak)
- **DO NOT** call `DataSourceRefresh` for per-event last-synced — use `GcalEvent.LastSyncedAt` directly
- **DO NOT** import WinUI 3 namespaces (`Microsoft.UI.*`) into Core layer classes

### Test Data Guidance

Use `SettingsViewModelTests.cs` as reference for test setup patterns. For `EventDetailsPanelViewModelTests`:
- Mock `ICalendarQueryService` returning a seeded `CalendarEventDisplayModel`
- Use `WeakReferenceMessenger.Default` directly to send test messages (same messenger the ViewModel registers with)
- Verify ViewModel observable property changes after message delivery

## References

- [Epic 3 Tech Spec](../tech-spec.md) — authoritative service contracts, AC, and XAML composition
- [Architecture.md](../../architecture.md) — layering rules, WeakReferenceMessenger usage
- [GcalEvent entity](../../../Data/Entities/GcalEvent.cs) — actual DB fields (`GcalEventId` is string PK)
- [GcalEventConfiguration.cs](../../../Data/Configurations/GcalEventConfiguration.cs) — confirms `HasKey(e => e.GcalEventId)`
- [SettingsViewModel.cs](../../../ViewModels/SettingsViewModel.cs) — ObservableObject + AsyncRelayCommand + WeakReferenceMessenger pattern
- [App.xaml.cs](../../../App.xaml.cs) — DI registration pattern, `IDbContextFactory` usage
- [AuthenticationSucceededMessage.cs](../../../Messages/AuthenticationSucceededMessage.cs) — message record pattern
- [_color-definitions.md](../../_color-definitions.md) — confirmed Azure = #0088CC; other hex TBD (Story 3.2)
- [Sprint Status](../../sprint-status.yaml) — story 3.3 (prerequisite) still backlog; create `ICalendarSelectionService` here if needed

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

### Change Log

<!-- to be filled by dev agent -->

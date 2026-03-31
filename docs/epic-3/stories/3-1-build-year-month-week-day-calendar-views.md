# Story 3.1: Build Year/Month/Week/Day Calendar Views

Status: in-progress

## Story

As a **user**,
I want **to view my calendar in year, month, week, and day perspectives**,
So that **I can see my synced Google Calendar events at different levels of detail**.

## Acceptance Criteria

1. **AC-3.1.1 — Year View on Launch:** Given the app launches with a populated `gcal_event` table, the year view is displayed showing 12 months with day cells for the current year.

2. **AC-3.1.2 — Month View Switch:** Given the user clicks the "Month" toggle, the view switches to a month grid displaying all events for the current month with event titles visible on each day cell.

3. **AC-3.1.3 — Week View Switch:** Given the user clicks the "Week" toggle, the view switches to a 7-column hourly timeline for the current week, with events positioned at their correct time slots.

4. **AC-3.1.4 — Day View Switch:** Given the user clicks the "Day" toggle, the view switches to a single-day hourly timeline with full-width event blocks.

5. **AC-3.1.5 — Animation Speed:** Given any view is active, switching between views completes with a smooth animation in under 300 ms.

6. **AC-3.1.6 — State Persistence:** Given the app restarts, it opens to the last viewed view mode and date (restored from `system_state` table using keys `current_view_mode` and `current_view_date`).

7. **AC-3.1.7 — Performance:** Given a month view with 200+ events across the month, the view renders in under 1 second.

8. **AC-3.1.8 — Empty State:** Given no events are synced yet (`gcal_event` table is empty or all rows have `is_deleted = 1`), all calendar views show an empty state message: *"No events synced yet. Go to Settings → Connect Google Calendar to sync."* No crash, no unhandled exception.

## Scope Boundaries (Tier 1 Only)

**IN SCOPE — this story:**
- `MainWindow.xaml` shell with toolbar (view mode toggle, prev/next/today buttons, breadcrumb label, jump-to-date)
- `YearViewControl`, `MonthViewControl`, `WeekViewControl`, `DayViewControl`
- `MainViewModel` with navigation commands
- `ICalendarQueryService` + `CalendarQueryService`
- `IColorMappingService` + `ColorMappingService` (Azure-only stub — Story 3.2 completes the 9-colour mapping)
- `INavigationStateService` + `NavigationStateService`
- `ICalendarSelectionService` + `CalendarSelectionService` (service + message only — visual selection outline is Story 3.3)
- `GcalEventRepository` (read-only queries)
- `ISystemStateRepository` + `SystemStateRepository`
- Data models: `CalendarEventDisplayModel`, `NavigationState`, `ViewMode`, `EventSelectedMessage`
- All DI registrations in `App.xaml.cs`
- Switch app entry point from `SettingsPage` to `MainWindow` (keeping SettingsPage accessible via navigation)

**OUT OF SCOPE — do NOT implement:**
- Colour-coded rendering (Story 3.2) — use Azure `#0088CC` as placeholder for all events
- Event selection red outline / click-to-select (Story 3.3)
- Event details panel (Story 3.4)
- Sync status green/grey indicators (Story 2.4 — `ISyncStatusService` is not yet implemented; show no indicators)
- Event editing or creation (Stories 3.5, 3.6, 3.7)
- Multi-select, drag-select, drag-to-create
- Settings page navigation (SettingsPage already exists — wire up access via toolbar button)

---

## Dev Notes

### Critical Context: Project Structure Differs from Architecture Doc

The architecture doc describes a hypothetical `src/` folder structure. The **actual project structure** is flat at root:

```
GoogleCalendarManagement/             ← project root
├── App.xaml / App.xaml.cs           ← application entry
├── Views/                           ← XAML + code-behind (currently only SettingsPage.xaml)
├── ViewModels/                      ← ViewModels (currently only SettingsViewModel.cs)
├── Services/                        ← All services (ISyncManager, IGoogleCalendarService, etc.)
├── Messages/                        ← WeakReferenceMessenger messages (AuthenticationSucceededMessage.cs)
├── Models/                          ← UI display models (currently empty)
├── Data/                            ← EF Core (CalendarDbContext, Entities, Configurations, Migrations)
├── GoogleCalendarManagement.Tests/  ← Unit + Integration tests
└── GoogleCalendarManagement.csproj
```

Place new files in the correct existing folders — **never create a `Core/` folder** or `src/` hierarchy. The project is a single WinUI 3 project (not separated into multiple class libraries despite what the architecture doc implies).

### Critical Context: Current App Entry Point

`App.xaml.cs` line 131 currently sets `window.Content = settingsPage` as the main UI. **This story must change this** to show `MainWindow` (a `Page` or `UserControl`, not a new `Window`) as the primary content, with `SettingsPage` accessible via a settings button in the toolbar.

Recommended approach: Create `MainPage.xaml` as the shell (containing toolbar + `Frame` for view controls), set `window.Content = mainPage`, and the frame navigates between `YearViewControl`, `MonthViewControl`, etc.

### Core Services to Create

**Location: `Services/`** — follow the same namespace `GoogleCalendarManagement.Services`

#### 1. Data Models

Create in `Models/` (namespace `GoogleCalendarManagement.Models`):

```csharp
// Models/CalendarEventDisplayModel.cs
public record CalendarEventDisplayModel(
    string GcalEventId,        // gcal_event.gcal_event_id (natural PK)
    string Title,              // gcal_event.summary (empty string if null)
    DateTime StartUtc,         // gcal_event.start_datetime
    DateTime EndUtc,           // gcal_event.end_datetime
    bool IsAllDay,             // gcal_event.is_all_day
    string ColorHex,           // resolved by IColorMappingService, default "#0088CC"
    bool IsRecurringInstance,  // gcal_event.is_recurring_instance
    string? Description,       // gcal_event.description
    DateTime? LastSyncedAt     // gcal_event.last_synced_at
);

// Models/NavigationState.cs
public record NavigationState(ViewMode ViewMode, DateOnly CurrentDate);

// Models/ViewMode.cs
public enum ViewMode { Year, Month, Week, Day }
```

Create in `Messages/` (existing folder, namespace `GoogleCalendarManagement.Messages`):

```csharp
// Messages/EventSelectedMessage.cs
public record EventSelectedMessage(string? GcalEventId);  // null = selection cleared
```

#### 2. Repository Interfaces and Implementations

Create `Services/IGcalEventRepository.cs` and `Services/GcalEventRepository.cs`:

```csharp
public interface IGcalEventRepository
{
    Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
}
```

Query logic in `GcalEventRepository`: filter where `is_deleted = 0` AND `start_datetime` overlaps the requested range. Use the `IDbContextFactory<CalendarDbContext>` (already registered in DI) — **not** the scoped `CalendarDbContext` — to avoid threading issues from background sync.

Create `Services/ISystemStateRepository.cs` and `Services/SystemStateRepository.cs`:

```csharp
public interface ISystemStateRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
}
```

Use `IDbContextFactory<CalendarDbContext>` here too. Match on `SystemState.StateName` column (mapped as `state_name` in DB).

#### 3. Service Interfaces and Implementations

**`IColorMappingService` / `ColorMappingService`** (stub for Story 3.1 — Story 3.2 fills in all 9 colours):

```csharp
// Services/IColorMappingService.cs
public interface IColorMappingService
{
    string GetHexColor(string? colorId);  // never throws; returns "#0088CC" fallback
}

// Services/ColorMappingService.cs
// For Story 3.1: return "#0088CC" for all inputs.
// Story 3.2 will replace this with the full 9-colour dictionary.
public class ColorMappingService : IColorMappingService
{
    public string GetHexColor(string? colorId) => "#0088CC";
}
```

**`ICalendarQueryService` / `CalendarQueryService`**:

```csharp
// Services/ICalendarQueryService.cs
public interface ICalendarQueryService
{
    Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default);
}
```

`CalendarQueryService` wraps `IGcalEventRepository` and projects `GcalEvent → CalendarEventDisplayModel`:
- `Title` = `event.Summary ?? ""`
- `StartUtc` / `EndUtc` = values from DB (already UTC)
- `IsAllDay` = `event.IsAllDay ?? false`
- `ColorHex` = `_colorMappingService.GetHexColor(event.ColorId)`
- `LastSyncedAt` = `event.LastSyncedAt`

**`INavigationStateService` / `NavigationStateService`**:

```csharp
// Services/INavigationStateService.cs
public interface INavigationStateService
{
    Task<NavigationState> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(NavigationState state, CancellationToken ct = default);
}
```

`NavigationStateService` reads/writes these `system_state` rows:
- Key `"current_view_mode"` → `ViewMode.ToString()` (e.g., `"Year"`)
- Key `"current_view_date"` → ISO date string (e.g., `"2026-03-30"`)

On load failure (missing rows or parse error): **return default** `new NavigationState(ViewMode.Year, DateOnly.FromDateTime(DateTime.Today))` and log a warning via `ILogger`. Do NOT throw.

**`ICalendarSelectionService` / `CalendarSelectionService`**:

```csharp
// Services/ICalendarSelectionService.cs
public interface ICalendarSelectionService
{
    string? SelectedGcalEventId { get; }
    void Select(string gcalEventId);
    void ClearSelection();
}
```

Implementation publishes `EventSelectedMessage` via `WeakReferenceMessenger.Default.Send(new EventSelectedMessage(id))` on selection change.

### MainViewModel Structure

Create `ViewModels/MainViewModel.cs` (namespace `GoogleCalendarManagement.ViewModels`):

```csharp
public partial class MainViewModel : ObservableObject
{
    // Observable properties
    [ObservableProperty] private ViewMode currentViewMode;
    [ObservableProperty] private DateOnly currentDate;
    [ObservableProperty] private string breadcrumbLabel = "";
    [ObservableProperty] private IList<CalendarEventDisplayModel> currentEvents = [];
    [ObservableProperty] private bool isLoading;

    // Commands
    [RelayCommand] private async Task SwitchViewMode(ViewMode mode) { ... }
    [RelayCommand] private async Task NavigatePrevious() { ... }
    [RelayCommand] private async Task NavigateNext() { ... }
    [RelayCommand] private async Task NavigateToday() { ... }
    [RelayCommand] private async Task JumpToDate(DateOnly date) { ... }
    public async Task InitializeAsync() { ... }
}
```

**Date range computation per view mode:**
- Year: Jan 1 – Dec 31 of `CurrentDate.Year`
- Month: first day – last day of `CurrentDate.Month/Year`
- Week: Monday of the week containing `CurrentDate` to Sunday (ISO 8601 — week starts Monday)
- Day: `CurrentDate` to `CurrentDate`

**Breadcrumb computation:**
- Year: `"2026"`
- Month: `"January 2026"`
- Week: `"Jan 15–21, 2026"` (use `CultureInfo.CurrentCulture` for month abbreviation)
- Day: `"Monday, 15 January 2026"`

**On `SwitchViewModeCommand`:** Set `CurrentViewMode`, recompute range, load events, save navigation state, update breadcrumb.

**On init:** Call `NavigationStateService.LoadAsync()` → set view + date → load events → update breadcrumb. All async; use `isLoading = true/false` pattern to allow UI to show a progress ring.

### XAML Structure

**MainPage.xaml** (or MainView.xaml — pick one and be consistent):

```xaml
<Page>
  <Grid RowDefinitions="Auto,*">
    <!-- Row 0: Toolbar -->
    <StackPanel Grid.Row="0" Orientation="Horizontal">
      <!-- View Mode Toggle -->
      <RadioButtons>Year | Month | Week | Day</RadioButtons>
      <!-- Navigation -->
      <Button x:Name="PrevBtn" Content="←" Command="{x:Bind ViewModel.NavigatePreviousCommand}"/>
      <Button x:Name="TodayBtn" Content="Today" Command="{x:Bind ViewModel.NavigateToTodayCommand}"/>
      <Button x:Name="NextBtn" Content="→" Command="{x:Bind ViewModel.NavigateNextCommand}"/>
      <TextBlock Text="{x:Bind ViewModel.BreadcrumbLabel, Mode=OneWay}"/>
      <!-- Jump-to-date: CalendarDatePicker in a Flyout -->
      <Button x:Name="JumpBtn">Jump to Date</Button>
      <!-- Settings access -->
      <Button x:Name="SettingsBtn">⚙</Button>
    </StackPanel>
    <!-- Row 1: Calendar view frame -->
    <Frame x:Name="CalendarFrame" Grid.Row="1"/>
  </Grid>
</Page>
```

**YearViewControl.xaml** — `UniformGrid` 3×4 (12 months). Each month is a `MonthMiniGrid` showing day cells. Day cells: just the day number and a placeholder for sync indicator (grey dot, not wired to real data in 3.1). Click on a day navigates to Month view for that month (call `MainViewModel.SwitchViewModeCommand(Month)` and set date).

**MonthViewControl.xaml** — 7-column `UniformGrid` with 5–6 rows. Each day cell shows the day number and stacked `EventChip` controls (coloured background = `ColorHex`, title text). Overflow: "+N more" flyout label if events exceed 3 per day. All-day events shown at top of cell.

**WeekViewControl.xaml** — `ScrollViewer` containing a 7-column `Grid` with 24 rows (one per hour). `EventBlock` elements positioned using `Grid.Row`/`Grid.RowSpan` computed from UTC start/end times converted to **local time** (`StartUtc.ToLocalTime()`). Left gutter: hour labels 00:00–23:00. Use `ItemsRepeater` with `RecyclingElementFactory` for event blocks to support virtualization.

**DayViewControl.xaml** — Single-column `ScrollViewer`, same 24-row layout. Wider `EventBlock` elements. All-day events in a pinned strip above the scroll area.

### Conversion: UTC → Local Time

Convert DB datetime to local time in `CalendarQueryService` during projection, NOT in the view. Store both in `CalendarEventDisplayModel` or compute local time before binding.

```csharp
// In CalendarQueryService.GetEventsForRangeAsync, project:
var startLocal = (event.StartDatetime ?? DateTime.UtcNow).ToLocalTime();
var endLocal = (event.EndDatetime ?? DateTime.UtcNow).ToLocalTime();
```

### Week Start: Monday (ISO 8601)

```csharp
// Get Monday of week containing 'date':
var dayOfWeek = (int)date.DayOfWeek;
var daysFromMonday = (dayOfWeek == 0) ? 6 : dayOfWeek - 1; // Sunday = 0 in DayOfWeek
var monday = date.AddDays(-daysFromMonday);
```

### DI Registration Changes to App.xaml.cs

Add to `ConfigureServices()` — all new services are `AddSingleton` unless noted:

```csharp
// Repositories (use factory to avoid threading issues)
services.AddSingleton<IGcalEventRepository, GcalEventRepository>();
services.AddSingleton<ISystemStateRepository, SystemStateRepository>();

// Services
services.AddSingleton<IColorMappingService, ColorMappingService>();
services.AddSingleton<ICalendarQueryService, CalendarQueryService>();
services.AddSingleton<INavigationStateService, NavigationStateService>();
services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();

// ViewModels
services.AddSingleton<MainViewModel>();

// Views (Transient — each navigation creates fresh instance)
services.AddTransient<MainPage>();
services.AddTransient<YearViewControl>();
services.AddTransient<MonthViewControl>();
services.AddTransient<WeekViewControl>();
services.AddTransient<DayViewControl>();
```

**Change the window entry point** in `OnLaunched`:

```csharp
// Replace: window.Content = settingsPage;
// With:
var mainPage = serviceProvider.GetRequiredService<MainPage>();
window.Content = mainPage;
await mainPage.ViewModel.InitializeAsync();
```

### SystemState Entity Note

`SystemState` entity uses `StateName` / `StateValue` / `StateId` / `UpdatedAt` columns. Check `Data/Configurations/SystemStateConfiguration.cs` for the actual column name mappings before writing queries.

### ISyncStatusService Dependency

`ISyncStatusService` (from Epic 2, Story 2.4) is **NOT yet implemented** — Story 2.4 is `ready-for-dev` but not done. For Story 3.1:
- Year view day cells: show a static grey dot placeholder — do NOT call any sync status service
- Month view day cells: no sync indicator — just events
- Leave a `// TODO Story 2.4: wire ISyncStatusService here` comment in the year view day cell template
- Do NOT define `ISyncStatusService` in this story; Story 2.4 owns that interface

### Existing Infrastructure to Reuse (Do NOT Reinvent)

| What | Where | Use For |
|------|-------|---------|
| `IDbContextFactory<CalendarDbContext>` | Already registered in DI | All repository DB access |
| `CalendarDbContext` + all entities | `Data/` | GcalEvent, SystemState, DataSourceRefresh queries |
| `WeakReferenceMessenger` (CommunityToolkit.Mvvm) | Already a dependency | `EventSelectedMessage`, `SyncCompletedMessage` publishing |
| `ObservableObject`, `RelayCommand`, `[ObservableProperty]` | CommunityToolkit.Mvvm | ViewModel base, commands, properties |
| `ILogger<T>` via Serilog | Already registered in DI | All logging |
| `AuthenticationSucceededMessage` pattern in `Messages/` | `Messages/AuthenticationSucceededMessage.cs` | Follow same pattern for `EventSelectedMessage` |
| `SettingsViewModel` pattern | `ViewModels/SettingsViewModel.cs` | Follow same code-behind + DI-injected ViewModel pattern |

### Previous Story Learnings (From Stories 2.3 and 2.3A)

- **Use `IDbContextFactory`, not scoped `CalendarDbContext`** in singleton services to avoid context threading issues. The factory creates a fresh context per operation. Pattern: `await using var db = await _dbFactory.CreateDbContextAsync(ct);`
- **GcalEvent PK is `GcalEventId` (string, natural key)** — not an `int id`. Do NOT assume an integer PK. `GcalEventId` is the Google event ID string.
- **All DB timestamps are UTC** — stored and retrieved as UTC. Apply `.ToLocalTime()` only in presentation layer.
- **Soft deletes**: filter `IsDeleted == false` in all GcalEvent queries. Deleted events must never appear in calendar views.
- **EF Core migrations**: if any schema changes are needed (none expected for 3.1 since it uses existing `system_state` table), follow the established migration pattern: `dotnet ef migrations add MigrationName -p GoogleCalendarManagement -- --db-path path`.
- **Build command**: `dotnet build -p:Platform=x64` (x64 required for WinUI 3)
- **Test command**: `dotnet test GoogleCalendarManagement.Tests/`
- **Data files are gitignored** — do not attempt to commit `data/calendar.db` or any `.db-wal`/`.db-shm` files.

### Git History Context (Recent Commits)

| Commit | Work Done |
|--------|-----------|
| `455aa95` finished 2.3a | GcalEventVersion schema hardened; EF migration added |
| `98a7f8e` finished 2.3, fixed bugs, sync working | SyncManager + version history working |
| `234c2ce` finished 2.1 connected to google account | OAuth + token storage |
| `c913f5d` finished epic 1, set up epic 2 | DB schema, migration pipeline, DI wired |

---

## Tasks / Subtasks

- [x] **Task 1: Create data models and messages** (AC: all)
  - [x] `Models/CalendarEventDisplayModel.cs` — record with fields as specified
  - [x] `Models/NavigationState.cs` — record(ViewMode, DateOnly)
  - [x] `Models/ViewMode.cs` — enum {Year, Month, Week, Day}
  - [x] `Messages/EventSelectedMessage.cs` — record(string? GcalEventId)

- [x] **Task 2: Implement repositories** (AC: all)
  - [x] `Services/IGcalEventRepository.cs` interface
  - [x] `Services/GcalEventRepository.cs` — uses `IDbContextFactory<CalendarDbContext>`; filters `IsDeleted == false`; overlapping date range query
  - [x] `Services/ISystemStateRepository.cs` interface
  - [x] `Services/SystemStateRepository.cs` — get/set by `StateName` key; upsert pattern

- [x] **Task 3: Implement core services** (AC: all)
  - [x] `Services/IColorMappingService.cs` + `ColorMappingService.cs` — stub returning `"#0088CC"` for all inputs (Story 3.2 will complete)
  - [x] `Services/ICalendarQueryService.cs` + `CalendarQueryService.cs` — wraps repository; projects GcalEvent → CalendarEventDisplayModel; UTC conversion in projection
  - [x] `Services/INavigationStateService.cs` + `NavigationStateService.cs` — reads/writes `system_state`; graceful fallback to Year view / today on any failure
  - [x] `Services/ICalendarSelectionService.cs` + `CalendarSelectionService.cs` — holds selected ID; publishes `EventSelectedMessage` on change

- [x] **Task 4: Implement MainViewModel** (AC: 3.1.2–3.1.7)
  - [x] `ViewModels/MainViewModel.cs` — all observable properties, commands, date range logic, breadcrumb computation
  - [x] `InitializeAsync()` — loads navigation state, fetches events, updates breadcrumb
  - [x] Navigation commands: Previous, Next, Today, JumpToDate, SwitchViewMode
  - [x] Week start Monday (ISO 8601) — implement date math correctly
  - [x] Month/year wrap-around for prev/next (Dec → Jan, Jan → Dec)

- [x] **Task 5: Build MainPage shell and toolbar** (AC: 3.1.2–3.1.5, 3.1.8)
  - [x] `Views/MainPage.xaml` — Grid with toolbar row and calendar Frame
  - [x] View mode toggle (RadioButtons or SegmentedControl)
  - [x] Prev/Next/Today buttons bound to MainViewModel commands
  - [x] BreadcrumbLabel TextBlock
  - [x] JumpToDate: CalendarDatePicker in a Flyout or Popup
  - [x] Settings button → navigate to SettingsPage
  - [x] Empty state overlay (shown when `CurrentEvents.Count == 0` and not loading)

- [x] **Task 6: Build calendar view controls** (AC: 3.1.1–3.1.5, 3.1.7, 3.1.8)
  - [x] `Views/YearViewControl.xaml` — 3×4 UniformGrid of month mini-grids; day cells with day number and static grey dot placeholder; click day → navigate to Month view for that month
  - [x] `Views/MonthViewControl.xaml` — 7-column grid, EventChip per event (ColorHex background, title); "+N more" overflow label
  - [x] `Views/WeekViewControl.xaml` — ScrollViewer + 7-column × 24-row grid; EventBlock positioned by local time; hour labels in left gutter; ItemsRepeater for event blocks
  - [x] `Views/DayViewControl.xaml` — single-column ScrollViewer; same 24-row layout; all-day events in pinned header strip
  - [x] Ensure all views handle empty event list gracefully (no crash, empty cells only)

- [x] **Task 7: Update App.xaml.cs DI and entry point** (AC: all)
  - [x] Register all 4 new services in `ConfigureServices()`
  - [x] Register `IGcalEventRepository`, `ISystemStateRepository`
  - [x] Register `MainViewModel`, `MainPage` and all view controls
  - [x] Change `window.Content` from `settingsPage` to `mainPage`
  - [x] Call `await mainPage.ViewModel.InitializeAsync()` after setting content

- [x] **Task 8: Write unit tests** (AC: 3.1.6, 3.1.7)
  - [x] `Unit/Services/NavigationStateServiceTests.cs` — mock `ISystemStateRepository`; test load with missing rows returns default; test save round-trip
  - [x] `Unit/ViewModels/MainViewModelTests.cs` — test date range computation for all 4 view modes; test breadcrumb strings; test prev/next at year/month boundaries; test today command
  - [x] `Unit/Services/ColorMappingServiceTests.cs` — test null returns `#0088CC`; test any string returns `#0088CC` (Story 3.2 will expand)

- [x] **Task 9: Integration test** (AC: 3.1.1, 3.1.6, 3.1.8)
  - [x] `Integration/CalendarQueryServiceTests.cs` — seed `gcal_event` with 50 events across a month; assert `GetEventsForRangeAsync` returns correct count; assert `is_deleted` events are excluded
  - [x] `Integration/NavigationStateRoundTripTests.cs` — write system_state rows; read back via `NavigationStateService`; assert identical NavigationState returned

- [ ] **Task 10: Build verification**
  - [x] `dotnet build -p:Platform=x64` — must pass with 0 errors
  - [x] `dotnet test GoogleCalendarManagement.Tests/` — all tests pass
  - [ ] Manual: launch app → verify Year view loads with events; switch all 4 views; kill and relaunch → verify state restored

---

## Test Data

Add to `GoogleCalendarManagement.Tests/TestData/`:

**`sample_gcal_events_month.json`** — 50 events with various `ColorId` values, spread across a full month, mix of all-day and timed events, including some `is_deleted = 1` events to verify exclusion.

**`sample_gcal_events_week.json`** — 20 events within a single week, with overlapping time slots to exercise week view layout.

---

## Open Questions

**Q1:** Should clicking a day cell in year view navigate to Month view for that month, or Day view for that specific day?
- **Proposed answer:** Year cell click → Month view for that month. This matches the "zoom in" mental model. Validate with Sarunas before implementing.

**Q2:** In month view, how many event chips before truncating to "+N more"?
- **Proposed answer:** 3 event chips, then "+N more" flyout. Adjust based on cell height at 1024×768.

**Q3:** Should the event details panel (Story 3.4) be a sliding overlay or push-resize?
- **Proposed answer:** Overlay. Not relevant for 3.1 but design the layout to support it (leave space on the right side, or use a `SplitView`).

## Dev Agent Record

### Debug Log

- 2026-03-30: Implemented the calendar query/navigation model, shell page, and all four calendar view pages in the flat WinUI project structure.
- 2026-03-30: Switched app startup from `SettingsPage` to `MainPage`, added navigation-state persistence, and wired settings access from the calendar toolbar.
- 2026-03-30: Added unit/integration coverage for color fallback, navigation state, calendar query projection, and main calendar navigation logic.
- 2026-03-30: Updated the solution build configuration so `dotnet build -p:Platform=x64` builds the app project cleanly, with `dotnet test GoogleCalendarManagement.Tests/` remaining the separate validation step.
- 2026-03-31: Refined the 3.1 shell UX with a top-right settings action, segmented view selector, arrow navigation buttons, a highlighted date-range pill, and a responsive week layout; drafted Story 3.9 for the larger year-view event-indicator enhancement.

### Implementation Plan

- Add read-only calendar data/query/navigation services on top of the existing EF Core schema and `system_state` table.
- Use a singleton `MainViewModel` as the shared calendar state owner for shell navigation and all calendar views.
- Keep the XAML shells simple and construct the heavier calendar layouts in code-behind to avoid WinUI markup compiler instability while still meeting the story acceptance criteria.
- Cover the new navigation and query behaviors with focused unit/integration tests before closing out the story.

### Completion Notes

- Added `CalendarEventDisplayModel`, `NavigationState`, `ViewMode`, and `EventSelectedMessage`, plus repository/service implementations for event querying, color fallback, selection messaging, and persisted navigation state.
- Implemented `MainPage`, `MainViewModel`, and the year/month/week/day calendar pages with toolbar navigation, jump-to-date, empty-state handling, local-time event placement, and settings access.
- Added JSON calendar fixtures plus 5 new automated test files; current automated validation is green via `dotnet build -p:Platform=x64` and `dotnet test GoogleCalendarManagement.Tests/` (68 passing tests).
- Launched the app briefly and confirmed the executable started, then terminated the spawned process. Full manual interaction verification for view switching and restart-state persistence remains pending, so the story is left `in-progress`.
- Refined the in-scope 3.1 shell interaction model by moving settings to the top-right, replacing the view toggles with a segmented single-select strip, switching previous/next to arrow buttons beneath the selector, and highlighting the current date-range label with a rounded grey pill.
- Updated the week view so its seven-day timeline expands to the available window width instead of leaving unused space on the right, while still preserving horizontal scrolling on narrower windows.
- Drafted follow-up Story 3.9 to handle the out-of-scope year-view event-marker and all-day tooltip behavior separately from 3.1 shell/layout work.

## File List

- `App.xaml.cs`
- `GoogleCalendarManagement.sln`
- `GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj`
- `GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs`
- `GoogleCalendarManagement.Tests/Integration/NavigationStateRoundTripTests.cs`
- `GoogleCalendarManagement.Tests/TestData/sample_gcal_events_month.json`
- `GoogleCalendarManagement.Tests/TestData/sample_gcal_events_week.json`
- `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs`
- `Messages/EventSelectedMessage.cs`
- `Models/CalendarEventDisplayModel.cs`
- `Models/CalendarViewDisplayModels.cs`
- `Models/NavigationState.cs`
- `Models/ViewMode.cs`
- `Services/CalendarQueryService.cs`
- `Services/CalendarSelectionService.cs`
- `Services/ColorMappingService.cs`
- `Services/GcalEventRepository.cs`
- `Services/ICalendarQueryService.cs`
- `Services/ICalendarSelectionService.cs`
- `Services/IColorMappingService.cs`
- `Services/IGcalEventRepository.cs`
- `Services/INavigationStateService.cs`
- `Services/ISystemStateRepository.cs`
- `Services/NavigationStateService.cs`
- `Services/SystemStateRepository.cs`
- `ViewModels/MainViewModel.cs`
- `Views/DayViewControl.xaml`
- `Views/DayViewControl.xaml.cs`
- `Views/MainPage.xaml`
- `Views/MainPage.xaml.cs`
- `Views/MonthViewControl.xaml`
- `Views/MonthViewControl.xaml.cs`
- `Views/WeekViewControl.xaml`
- `Views/WeekViewControl.xaml.cs`
- `Views/YearViewControl.xaml`
- `Views/YearViewControl.xaml.cs`
- `docs/epic-3/stories/3-9-enhance-year-view-with-event-indicators-and-all-day-previews.md`
- `docs/sprint-status.yaml`

## Change Log

- 2026-03-30: Implemented Story 3.1 calendar shell, calendar pages, navigation persistence, read-only query services, automated tests, and x64 solution build validation.
- 2026-03-31: Refined the 3.1 toolbar/view-selector layout, changed previous/next to arrow navigation beneath the selector, highlighted the active date range, made week view responsive to window width, and drafted Story 3.9 for advanced year-view event indicators.

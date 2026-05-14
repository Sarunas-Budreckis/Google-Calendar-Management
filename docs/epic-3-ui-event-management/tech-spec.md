# Epic Technical Specification: Local Calendar UI & Event Management (Tier 1)

Date: 2026-03-30
Author: Sarunas Budreckis
Epic ID: epic-3
Tier: 1 (Read-Only Viewer)
Status: Draft

---

## Overview

Epic 3 delivers the primary user experience of Google Calendar Management: a rich, multi-view calendar interface that renders the locally cached Google Calendar data collected in Epic 2. Building on the `gcal_event` table, `ISyncStatusService`, and `CalendarDbContext` established in Epics 1 and 2, this epic constructs four interchangeable calendar views (year, month, week, day), a colour-coded event rendering pipeline, click-to-select event interaction, a read-only event details panel, and a full date navigation system.

The deliverable at the close of Epic 3 (Tier 1) is an application where the user can open it, see their synced Google Calendar events in any of four views, click any event to read its details, navigate freely through time, and see green/grey sync status indicators per date — the "single pane of glass" for the life-review ritual. **All views are strictly read-only in Tier 1.** Event editing and creation (Stories 3.5–3.7) are Tier 2 scope and are explicitly excluded from this specification.

---

## Objectives and Scope

**In Scope (Tier 1):**

- Year view: 12-month grid, each month showing day cells with event dots/bars and sync status
- Month view: Google Calendar-style month grid with event title blocks per day
- Week view: 7-day columns with hourly time slots and positioned event blocks
- Day view: Single-day detailed hourly timeline with event blocks
- View mode switcher (Year / Month / Week / Day toggle buttons)
- Smooth transitions between views (<300 ms animation)
- Colour-coded event rendering using custom 9-colour taxonomy; `ColorId` → `SolidColorBrush` mapping
- Published events at 100% opacity (Tier 1 only has synced events)
- Event selection: click to select, red 2 px outline, single-select, Esc to clear
- Tooltip on hover: event title and time
- Read-only event details panel: slides in from right, non-modal, full event data display
- Disabled "Edit" button in details panel with "Coming in Tier 2" tooltip
- Sync status indicators (green/grey per date) sourced from `ISyncStatusService` (Epic 2)
- Date navigation: previous/next, Today button, jump-to-date picker, keyboard shortcuts
- Navigation state persistence: last viewed date and view mode restored on app restart
- Breadcrumb label showing current date range
- Current view mode and date persisted to `system_state` table

**Out of Scope (Tier 2+):**

- Event editing (Story 3.5)
- Event creation (Story 3.6)
- Colour picker in editing panel (Story 3.7)
- Translucent unpushed events (60% opacity) — infrastructure slot reserved, not activated
- Multi-select (Shift-click, drag-select)
- Drag-to-create in week/day view
- Save/export/import UI (Epic 8)
- Settings page (Epic 10)

**Dependencies:**

- **Prerequisite:** Epic 2 complete — `gcal_event` table populated; `ISyncStatusService` and `SyncCompletedMessage` available
- **Prerequisite:** Epic 1 — `system_state` table, `config` table, EF Core infrastructure, Serilog logging
- **Enables:** Epic 4 (data source columns in calendar), Epic 6 (approval workflow overlay), Epic 7 (date state indicators), Epic 8 (save/restore UI)
- **External:** No new external API calls in Epic 3

---

## System Architecture Alignment

Epic 3 activates the UI slice of the architecture defined in [architecture.md](../architecture.md):

| Concern | Architecture Component |
|---|---|
| Main calendar shell | `MainWindow.xaml` + `MainViewModel` |
| View mode pages | `YearViewControl.xaml`, `MonthViewControl.xaml`, `WeekViewControl.xaml`, `DayViewControl.xaml` |
| Event details panel | `EventDetailsPanelControl.xaml` + `EventDetailsPanelViewModel` |
| Calendar data queries | `GcalEventRepository` (already in `GoogleCalendarManagement.Data`) |
| Sync status data | `ISyncStatusService` (already implemented in Epic 2) |
| Colour mapping | `IColorMappingService` in `GoogleCalendarManagement.Core/Services/` |
| Navigation state | `INavigationStateService` in `GoogleCalendarManagement.Core/Services/` |
| Event selection | `ICalendarSelectionService` in `GoogleCalendarManagement.Core/Services/` |
| Cross-ViewModel messaging | `CommunityToolkit.Mvvm.WeakReferenceMessenger` |
| Logging | Serilog structured logging; navigation and interaction events logged |

**Layering constraint:** All business logic (`IColorMappingService`, `INavigationStateService`, `ICalendarSelectionService`) lives in the Core layer with no WinUI 3 dependency. ViewModels translate Core data into observable properties. Views bind to ViewModels via XAML; no code-behind business logic.

**Naming alignment:** Same conventions as Epics 1 and 2 — `singular_snake_case` for tables, `PascalCase` for C# classes, all async I/O methods suffixed `Async`, all timestamps UTC.

---

## Detailed Design

### Services and Modules

| Service / Module | Layer | Responsibility | Inputs | Outputs |
|---|---|---|---|---|
| `ICalendarQueryService` | Core/Interfaces | Fetch events for a given date range for display | `DateOnly from`, `DateOnly to` | `IList<CalendarEventDisplayModel>` |
| `CalendarQueryService` | Core/Services | Wraps `IGcalEventRepository`; projects `GcalEvent` → `CalendarEventDisplayModel`; applies colour mapping | `IGcalEventRepository`, `IColorMappingService` | `IList<CalendarEventDisplayModel>` |
| `IColorMappingService` | Core/Interfaces | Maps `ColorId` string → hex color string | `string? colorId` | `string hexColor` |
| `ColorMappingService` | Core/Services | Hardcoded 9-colour dictionary + fallback Azure; returns hex; used by display model projection | Color dictionary | `string` |
| `INavigationStateService` | Core/Interfaces | Persist and restore current view mode and date | `ViewMode`, `DateOnly` | Loaded `NavigationState` |
| `NavigationStateService` | Core/Services | Read/write `system_state` rows with keys `current_view_mode` and `current_view_date` via `ISystemStateRepository` | `ISystemStateRepository` | `NavigationState` |
| `ICalendarSelectionService` | Core/Interfaces | Track currently selected event; notify subscribers | `int? gcalEventId` | Selection changed events |
| `CalendarSelectionService` | Core/Services | Holds `SelectedEventId`; publishes `EventSelectedMessage` via `WeakReferenceMessenger` on change | `WeakReferenceMessenger` | `EventSelectedMessage` |
| `MainViewModel` | UI/ViewModels | Orchestrates view mode, current date, navigation commands, sync status refresh on `SyncCompletedMessage` | All core services | Observable properties for views |
| `EventDetailsPanelViewModel` | UI/ViewModels | Binds to selected `CalendarEventDisplayModel`; controls panel visibility | `ICalendarSelectionService`, `ICalendarQueryService` | Panel content, `IsPanelVisible` |

### Data Models and Contracts

**`CalendarEventDisplayModel`** — UI display model (not a DB entity):

```csharp
public record CalendarEventDisplayModel(
    int Id,                        // gcal_event.id (PK)
    string GcalEventId,            // gcal_event.gcal_event_id
    string Title,                  // gcal_event.summary (empty string if null)
    DateTime StartUtc,
    DateTime EndUtc,
    bool IsAllDay,
    string ColorHex,               // resolved by IColorMappingService, e.g. "#0088CC"
    bool IsRecurringInstance,
    string? Description,
    DateTime? LastSyncedAt         // from data_source_refresh (passed in at query time)
);
```

**`NavigationState`** — persisted to `system_state`:

```csharp
public record NavigationState(
    ViewMode ViewMode,    // Year | Month | Week | Day
    DateOnly CurrentDate  // date within the viewed period
);
```

**`ViewMode`** enum:

```csharp
public enum ViewMode { Year, Month, Week, Day }
```

**`EventSelectedMessage`** — `WeakReferenceMessenger` message:

```csharp
public record EventSelectedMessage(int? EventId);  // null = selection cleared
```

**`SyncStatusRefreshedMessage`** — already defined in Epic 2; `MainViewModel` subscribes to trigger status indicator refresh on sync completion.

**EF entities read (no new tables in Tier 1 Epic 3):**

- `GcalEvent` → `gcal_event` — read-only queries; no writes from Epic 3
- `DataSourceRefresh` → `data_source_refresh` — read `last_synced_at` for tooltip in details panel
- `SystemState` → `system_state` — read/write `current_view_mode` and `current_view_date`

### APIs and Interfaces

**Core service interface signatures:**

```csharp
// GoogleCalendarManagement.Core/Interfaces/ICalendarQueryService.cs
public interface ICalendarQueryService
{
    Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<CalendarEventDisplayModel?> GetEventByIdAsync(
        int id, CancellationToken ct = default);
}

// GoogleCalendarManagement.Core/Interfaces/IColorMappingService.cs
public interface IColorMappingService
{
    string GetHexColor(string? colorId);   // never throws; returns Azure fallback
    IReadOnlyDictionary<string, string> AllColors { get; }  // colorId → hex
}

// GoogleCalendarManagement.Core/Interfaces/INavigationStateService.cs
public interface INavigationStateService
{
    Task<NavigationState> LoadAsync();
    Task SaveAsync(NavigationState state);
}

// GoogleCalendarManagement.Core/Interfaces/ICalendarSelectionService.cs
public interface ICalendarSelectionService
{
    int? SelectedEventId { get; }
    void Select(int eventId);
    void ClearSelection();
}
```

**Colour mapping table (hardcoded in `ColorMappingService`):**

| ColorId key | Colour name | Hex |
|---|---|---|
| `"azure"` / `"1"` / `null` (default) | Azure — Eudaimonia | `#0088CC` |
| `"purple"` / `"9"` | Purple — Professional Work | TBD from `_color-definitions.md` |
| `"grey"` / `"8"` | Grey — Sleep & Recovery | TBD |
| `"yellow"` / `"5"` | Yellow — Passive Consumption | TBD |
| `"navy"` / `"2"` | Navy — Personal Engineering | TBD |
| `"sage"` / `"10"` | Sage — Wisdom & Meta-Reflection | TBD |
| `"flamingo"` / `"4"` | Flamingo — Nerdsniped Deep Reading | TBD |
| `"orange"` / `"6"` | Orange — Physical Training | TBD |
| `"lavender"` / `"3"` | Lavender — In-Between States | TBD |

> **Action before Story 3.2:** Verify all 9 hex values in `_color-definitions.md` and populate `ColorMappingService` dictionary. Azure (`#0088CC`) is the only confirmed value at spec time.

### XAML View Composition

**`MainWindow.xaml` layout:**

```
MainWindow
├── TopToolBar (view mode toggle, navigation, sync status)
│   ├── SegmentedControl: Year | Month | Week | Day
│   ├── PreviousButton (←)
│   ├── TodayButton
│   ├── NextButton (→)
│   ├── BreadcrumbLabel ("January 2026")
│   └── JumpToDateButton (opens DatePicker flyout)
├── ContentPresenter (swaps between view controls)
│   ├── YearViewControl.xaml       (ViewMode.Year)
│   ├── MonthViewControl.xaml      (ViewMode.Month)
│   ├── WeekViewControl.xaml       (ViewMode.Week)
│   └── DayViewControl.xaml        (ViewMode.Day)
└── EventDetailsPanelControl.xaml  (slides in from right, overlay)
```

**`YearViewControl`** — 12 `MonthMiniGrid` panels in a 3×4 `UniformGrid`. Each mini-grid cell is a day button showing a sync indicator dot (green/grey). Click navigates to Month view for that month.

**`MonthViewControl`** — 7-column `UniformGrid` with 5–6 rows. Each cell shows the day number and stacked `EventChip` controls (title, coloured background). Overflow: "+N more" label opening a flyout.

**`WeekViewControl`** — `ScrollViewer` wrapping a 7-column `Grid` with 24 time slot rows. Each slot cell is 60 min / configurable row height. `EventBlock` elements positioned by `Grid.Row`/`Grid.RowSpan` from UTC start/end times converted to local time. Left gutter shows hour labels (00:00–23:00).

**`DayViewControl`** — Single-column `ScrollViewer`, same row layout as week view, one day. Wider `EventBlock` elements. All-day events shown in pinned header strip.

**`EventDetailsPanelControl`** — Right-side panel (~375 px wide, full height). Animated translation via `ThemeAnimation`. Contains: title (large), date/time range, colour swatch + name, description (`ScrollViewer`), source label ("From Google Calendar"), last synced timestamp, disabled "Edit" button with `ToolTipService.ToolTip="Coming in Tier 2"`.

### Workflows and Sequencing

**Flow 1 — App Startup (restoring last state):**

```
App.OnLaunched()
  → NavigationStateService.LoadAsync()
      → SystemStateRepository.GetAsync("current_view_mode") → e.g. "Month"
      → SystemStateRepository.GetAsync("current_view_date") → e.g. "2026-01-15"
  → MainViewModel.Initialize(NavigationState)
  → CalendarQueryService.GetEventsForRangeAsync(from, to)
      → GcalEventRepository.GetByDateRangeAsync(from, to)
      → Project GcalEvent → CalendarEventDisplayModel (ColorMappingService)
  → SyncStatusService.GetSyncStatusAsync(from, to)  [from Epic 2]
  → MainViewModel binds events + status to active view control
  → View renders
```

**Flow 2 — View Mode Switch (e.g. Year → Month):**

```
User clicks "Month" toggle
  → MainViewModel.SwitchViewModeCommand("Month")
  → Compute new date range for Month view (start/end of current month)
  → CalendarQueryService.GetEventsForRangeAsync(from, to)
  → SyncStatusService.GetSyncStatusAsync(from, to)
  → NavigationStateService.SaveAsync(new NavigationState(Month, currentDate))
  → ContentPresenter swaps to MonthViewControl (animated <300ms)
  → BreadcrumbLabel updates: "January 2026"
```

**Flow 3 — Event Click → Selection → Details Panel:**

```
User clicks EventChip / EventBlock
  → View raises EventSelected(eventId)
  → CalendarSelectionService.Select(eventId)
  → WeakReferenceMessenger.Send(new EventSelectedMessage(eventId))
  → EventDetailsPanelViewModel receives message
  → CalendarQueryService.GetEventByIdAsync(eventId)
  → Panel binds CalendarEventDisplayModel properties
  → Panel slides in from right (<200ms ThemeAnimation)
  → Selected event gains 2px red outline (VisualState on EventChip/EventBlock)
```

**Flow 4 — Clear Selection:**

```
User presses Esc OR clicks empty calendar area
  → CalendarSelectionService.ClearSelection()
  → WeakReferenceMessenger.Send(new EventSelectedMessage(null))
  → EventDetailsPanelViewModel: IsPanelVisible = false
  → Panel slides out
  → All event outlines removed
```

**Flow 5 — Sync Completed → Status Refresh:**

```
SyncManager (Epic 2) publishes SyncCompletedMessage
  → MainViewModel.OnSyncCompleted()
  → Re-query CalendarQueryService (events may have changed)
  → Re-query SyncStatusService for current date range
  → Calendar views rebind → status indicators update without user action
```

**Flow 6 — Navigate with Prev/Next:**

```
User clicks PreviousButton (or presses ←)
  → MainViewModel.NavigatePreviousCommand()
  → Shift currentDate by -1 unit (year/month/week/day per active view)
  → Recompute date range
  → Load events + sync status for new range (same pipeline as Flow 2)
  → BreadcrumbLabel updates
  → NavigationStateService.SaveAsync(updated state)
```

**Flow 7 — Jump to Date:**

```
User clicks JumpToDateButton
  → CalendarDatePicker flyout opens
  → User selects date
  → MainViewModel sets currentDate = selected date
  → Recompute range, reload, same pipeline as Flow 6
```

---

## Non-Functional Requirements

### Performance

| Target | Metric | Source |
|---|---|---|
| Month view render with 200+ events | < 1 second | PRD NFR-P1 |
| View mode switch animation | < 300 ms | PRD NFR-P1 |
| Event selection feedback (outline appears) | < 50 ms after click | PRD NFR-P1 |
| Details panel slide-in animation | < 200 ms | PRD NFR-P1 |
| Year view render (12 months) | < 1 second | PRD NFR-P1 |
| Scrolling in week/day views | 60 FPS sustained | PRD NFR-P3 |
| Navigation prev/next date range load | < 500 ms | PRD NFR-P1 |

All `CalendarQueryService` calls are `async`; ViewModels bind to observable properties updated on completion. The UI thread never blocks on database I/O. Event virtualization (WinUI 3 `ItemsRepeater` with `RecyclingElementFactory`) is used in week/day views to avoid rendering all 24×7 time slots simultaneously.

### Memory

Year view holds up to 366 `CalendarEventDisplayModel` references at once (one per day max). Month view holds up to ~200. Week view holds up to 7 days of events. Day view holds up to 24 hours. `CalendarQueryService` does not cache results — each navigation triggers a fresh DB query (SQLite is local, < 10 ms typical). ViewModels are garbage collected when views are unloaded.

### Accessibility

- All interactive elements (event chips, navigation buttons, toggle) have `AutomationProperties.Name` set
- Colour alone does not convey sync status — a tooltip shows "Synced: [date]" or "Not synced" on green/grey indicators
- Keyboard navigation: Tab through events in focused view; Enter selects; Esc clears selection
- Text contrast on coloured event backgrounds: auto-switch between white/black text (WCAG AA minimum — 4.5:1 ratio) using `IColorContrastService` helper

### Resilience

- If `gcal_event` table is empty (no sync yet), all calendar views render empty state: "No events synced yet. Go to Settings → Connect Google Calendar to sync." No crash, no unhandled exception.
- If `NavigationStateService.LoadAsync()` fails (corrupted `system_state` row), fall back to `new NavigationState(ViewMode.Year, DateOnly.FromDateTime(DateTime.Today))` and log a warning.
- If a single event's `ColorId` is unrecognised, `ColorMappingService` returns Azure (`#0088CC`) as fallback without throwing.

---

## Dependencies and Integrations

**NuGet Packages (new in Epic 3):**

| Package | Version | Purpose |
|---|---|---|
| No new packages required | — | All needed packages already present from Epics 1–2 |

**Already present from Epics 1–2 (used in Epic 3):**

| Package | Version | Usage in Epic 3 |
|---|---|---|
| `Microsoft.WindowsAppSDK` | 1.8.3 | WinUI 3 controls: CalendarView, Grid, ScrollViewer, ThemeAnimation |
| `CommunityToolkit.Mvvm` | 8.x | `ObservableObject`, `RelayCommand`, `WeakReferenceMessenger` |
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.12 | `GcalEventRepository` read queries |
| `Serilog` / `Serilog.Sinks.File` | 4.x / 6.x | Navigation and interaction logging |

**Internal (cross-epic) dependencies:**

| Dependency | Direction | Contract |
|---|---|---|
| Epic 1: `gcal_event` table | Reads (Epic 3) | Schema in [_database-schemas.md](../_database-schemas.md) §3 — `id`, `summary`, `start_datetime`, `end_datetime`, `is_all_day`, `color_id`, `is_deleted` |
| Epic 1: `system_state` table | Reads + Writes (Epic 3) | Keys `current_view_mode` (string) and `current_view_date` (ISO date string) |
| Epic 1: `data_source_refresh` table | Reads (Epic 3) | `last_synced_at` for details panel tooltip |
| Epic 2: `ISyncStatusService` | Consumes (Epic 3) | `GetSyncStatusAsync(DateOnly, DateOnly)` → `Dictionary<DateOnly, SyncStatus>` |
| Epic 2: `SyncCompletedMessage` | Subscribes (Epic 3) | `MainViewModel` refreshes events + status on sync complete |
| Epic 4+: `CalendarEventDisplayModel` | Extends | Additional data source columns added to model in Epic 4; Epic 3 model must not assume `GcalEventId` is the only event source |

---

## Acceptance Criteria (Authoritative)

**Story 3.1 — Year/Month/Week/Day Calendar Views**

1. Given the app launches with a populated `gcal_event` table, the year view is displayed showing 12 months with day cells for the current year.
2. Given the user clicks the "Month" toggle, the view switches to a month grid displaying all events for the current month with titles visible on each day cell.
3. Given the user clicks the "Week" toggle, the view switches to a 7-column hourly timeline for the current week, with events positioned at their correct time slots.
4. Given the user clicks the "Day" toggle, the view switches to a single-day hourly timeline with full-width event blocks.
5. Given any view is active, switching between views completes with a smooth animation in under 300 ms.
6. Given the app restarts, it opens to the last viewed view mode and date (restored from `system_state`).
7. Given a month view with 200+ events across the month, the view renders in under 1 second.
8. Given no events are synced yet, all calendar views show an empty state message prompting the user to sync.

**Story 3.2 — Colour-Coded Visual System**

9. Given events in the database with various `color_id` values, each event renders in its assigned custom colour in all four views.
10. Given an event with a null or unrecognised `color_id`, it renders in Azure (`#0088CC`).
11. Given any coloured event block, the text label (event title) is automatically white or black to ensure WCAG AA contrast against the background colour.
12. Given the same event viewed in year, month, week, and day views, the colour rendering is consistent across all four views.

**Story 3.3 — Event Selection with Visual Feedback**

13. Given the user clicks an event in any view, the event gains a 2 px solid red outline within 50 ms of the click.
14. Given an event is selected, only one event is selected at a time — clicking a different event moves the selection outline to the new event.
15. Given an event is selected, pressing Esc clears the selection and removes all outlines.
16. Given an event is selected, clicking any empty calendar area clears the selection.
17. Given the user hovers over any event without clicking, a tooltip appears showing the event title and start/end time.

**Story 3.4 — Event Details Panel (Read-Only)**

18. Given the user selects an event, the event details panel slides in from the right within 200 ms and displays: title, start/end date and time, colour indicator swatch, description (scrollable), source label ("From Google Calendar"), and last synced timestamp.
19. Given the details panel is visible, clicking the "Edit" button shows a tooltip "Coming in Tier 2" and takes no further action.
20. Given the details panel is visible, pressing Esc or clicking the close button slides the panel out and clears the event selection.
21. Given the details panel is open and the user switches view mode, the panel remains visible and continues showing the selected event details.

**Story 3.8 — Date Navigation and Jump-to-Date**

22. Given any view is active, clicking the Previous button shifts the view back by one unit (year in year view, month in month view, week in week view, day in day view).
23. Given any view is active, clicking the Next button shifts the view forward by one unit.
24. Given any view is active, clicking the Today button navigates to the current date.
25. Given the user clicks Jump to Date and selects a date from the picker, the view navigates to that date and the breadcrumb updates to reflect the new range.
26. Given the year view is active, the breadcrumb shows "2026". Month shows "January 2026". Week shows "Jan 15–21, 2026". Day shows "Monday, 15 January 2026".
27. Given navigation to a new date range, the new navigation state is persisted to `system_state` so the next app launch restores to this position.

---

## Traceability Mapping

| AC # | Story | Spec Section | Component(s) | Test Idea |
|---|---|---|---|---|
| 1 | 3.1 | Workflows — Flow 1 | `MainViewModel.Initialize`, `YearViewControl` | Integration: seed gcal_event, launch app, assert YearViewControl bound with 12 month panels |
| 2 | 3.1 | Workflows — Flow 2 | `MainViewModel.SwitchViewModeCommand`, `MonthViewControl` | Unit: switch to Month, assert date range spans full calendar month |
| 3 | 3.1 | Workflows — Flow 2 | `WeekViewControl` | Unit: week view date range = Mon–Sun of current week |
| 4 | 3.1 | Workflows — Flow 2 | `DayViewControl` | Unit: day view date range = single day |
| 5 | 3.1 | View composition — animation | `ContentPresenter` ThemeAnimation | Manual: time view switch animation under 300 ms |
| 6 | 3.1 | Workflows — Flow 1 | `NavigationStateService.LoadAsync` | Integration: write system_state rows, restart, assert correct view and date loaded |
| 7 | 3.1 | NFR — Performance | `MonthViewControl`, `CalendarQueryService` | Integration: seed 200+ events, measure render time < 1s |
| 8 | 3.1 | NFR — Resilience | All view controls, empty state | Integration: empty gcal_event table, assert empty state message visible in all views |
| 9 | 3.2 | Services — `ColorMappingService` | `CalendarEventDisplayModel.ColorHex` | Unit: 9 known ColorIds return correct hex values |
| 10 | 3.2 | Services — `ColorMappingService` | `ColorMappingService.GetHexColor(null)` | Unit: null/unknown colorId returns `#0088CC` without exception |
| 11 | 3.2 | View composition — `EventChip` / `EventBlock` | Text contrast auto-switch | Unit: light colours return black text; dark colours return white text |
| 12 | 3.2 | Workflows — Flow 2 | All four view controls | Manual: verify same event colour in all four views |
| 13 | 3.3 | Workflows — Flow 3 | `CalendarSelectionService.Select`, `EventChip` VisualState | Unit: Select(id) → EventSelectedMessage published; View applies red outline |
| 14 | 3.3 | Workflows — Flow 3 | `CalendarSelectionService` single-select | Unit: Select(id2) while id1 selected → id1 outline removed, id2 outline applied |
| 15 | 3.3 | Workflows — Flow 4 | `ClearSelection()`, Esc handler | Unit: ClearSelection() → EventSelectedMessage(null) → outlines removed |
| 16 | 3.3 | Workflows — Flow 4 | Click-empty-area handler | Manual: click empty cell → selection cleared |
| 17 | 3.3 | View composition — `EventChip` tooltip | `ToolTipService` on EventChip | Manual: hover over event → tooltip with title + time appears |
| 18 | 3.4 | Workflows — Flow 3 | `EventDetailsPanelViewModel`, panel binding | Integration: select event → assert all 6 fields populated in panel |
| 19 | 3.4 | View composition — disabled Edit button | Edit button `IsEnabled=false`, tooltip | Manual: click Edit → no action, tooltip shown |
| 20 | 3.4 | Workflows — Flow 4 | Panel close handler, `IsPanelVisible` | Unit: Esc → IsPanelVisible=false; integration: panel slide-out animation fires |
| 21 | 3.4 | Workflows — Flow 3 | `EventDetailsPanelViewModel`, view switch | Unit: switch view mode while panel open → IsPanelVisible remains true, same event displayed |
| 22 | 3.8 | Workflows — Flow 6 | `MainViewModel.NavigatePreviousCommand` | Unit: Month view, previous → month decrements by 1 |
| 23 | 3.8 | Workflows — Flow 6 | `MainViewModel.NavigateNextCommand` | Unit: Month view, next → month increments by 1 |
| 24 | 3.8 | Workflows — Flow 6 | `MainViewModel.NavigateTodayCommand` | Unit: Today → currentDate = DateOnly.FromDateTime(DateTime.Today) |
| 25 | 3.8 | Workflows — Flow 7 | `MainViewModel.JumpToDateCommand`, `CalendarDatePicker` | Manual: pick date → view and breadcrumb update |
| 26 | 3.8 | Workflows — Flow 6 | `MainViewModel.BreadcrumbLabel` computed property | Unit: 4 view modes × representative dates → assert correct breadcrumb string |
| 27 | 3.8 | Workflows — Flow 6 | `NavigationStateService.SaveAsync` | Integration: navigate → restart → assert same date+view restored |

---

## Risks, Assumptions, Open Questions

| # | Type | Item | Mitigation / Next Step |
|---|---|---|---|
| R1 | Risk | WinUI 3 `CalendarView` control is designed for date-picking, not event rendering. Using it as-is may not support custom event blocks (chips, time-positioned blocks). | **Action before Story 3.1:** Spike custom `ItemsRepeater`-based layouts for month/week views. If native `CalendarView` cell templates are too constrained, use fully custom `UniformGrid` + `Canvas` layouts. Spike should complete in ≤1 day. |
| R2 | Risk | Positioning timed events in the week/day view by pixel offset (`Canvas.Top`) is straightforward but may cause overlap with simultaneous events. | Implement a simple overlap-detection algorithm: divide overlapping events into columns within a day slot. Defer to a follow-up story if complexity is too high for Tier 1. |
| R3 | Risk | `ThemeAnimation` (slide-in for details panel) may require `AnimatedIcon` / `Storyboard` on older Windows 11 builds. | Test on target build (10.0.19041 minimum). Fallback: instant show/hide if animation APIs unavailable. |
| R4 | Risk | Year view performance: rendering 366 day cells with sync status queries could be slow if each cell issues a separate DB query. | `SyncStatusService.GetSyncStatusAsync(from, to)` already returns a bulk `Dictionary<DateOnly, SyncStatus>` in a single query. Year view must call once for the full year, not per-day. |
| R5 | Risk | `system_state` table may not have `current_view_mode` / `current_view_date` keys pre-seeded. | `NavigationStateService.LoadAsync()` must handle missing rows gracefully (return default state). No migration needed — `system_state` uses key-value rows, not schema columns. |
| A1 | Assumption | All events displayed in Tier 1 are synced from Google Calendar (`is_published` = TRUE; `gcal_event_id` not null). No local-only unpushed events exist in Tier 1 schema. | Confirmed by tier-1-requirements.md: "strictly read-only." |
| A2 | Assumption | The `gcal_event` table's `color_id` column stores Google Calendar's numeric color ID strings (e.g., `"1"` through `"11"`). Mapping to the 9 custom colours requires a translation layer in `ColorMappingService`. | **Action:** Verify the exact values stored in `color_id` during Epic 2 integration testing before mapping is hardcoded. |
| A3 | Assumption | Time display uses local system timezone. UTC datetimes from `gcal_event.start_datetime` are converted to local time in `CalendarEventDisplayModel` during projection, not in the View. | Conversion: `startUtc.ToLocalTime()`. Confirmed in architecture doc. |
| A4 | Assumption | Week starts on Monday (ISO 8601). Year view month mini-grids also start Monday columns. | Can be made configurable via `config` table in a later epic if needed. |
| Q1 | Open Question | Should clicking a day cell in year view navigate to month view for that month, or to day view for that specific day? | Proposed: year cell click → month view for that month (consistent with "zoom in" mental model). Validate with user before Story 3.1. |
| Q2 | Open Question | In month view, what is the maximum number of event chips to show per day cell before truncating to "+N more"? | Proposed: 3 event chips, then "+N more" flyout. Adjust based on cell height at typical window size. |
| Q3 | Open Question | Should the event details panel be a sliding overlay (covers part of calendar) or push the calendar to resize? | Proposed: overlay (non-destructive to calendar layout). Reconsider if UX feedback indicates calendar content is too obscured. |

---

## Test Strategy Summary

**Test levels and coverage targets:**

| Level | Framework | Scope | Coverage Target |
|---|---|---|---|
| Unit | xUnit + Moq + FluentAssertions | `ColorMappingService`, `NavigationStateService`, `CalendarSelectionService`, `MainViewModel` navigation commands, `EventDetailsPanelViewModel` visibility logic, breadcrumb computation | All public methods; all navigation edge cases (year wrap, month wrap) |
| Integration | xUnit + in-memory SQLite | `CalendarQueryService` with seeded `gcal_event` rows; `NavigationStateService` round-trip through `SystemStateRepository`; `SyncStatusService` (already covered in Epic 2 — reuse in composite tests) | Full data pipeline: seed → query → display model projection → binding |
| Manual | Developer testing on running app | View switching animations, event click/selection visual states, details panel slide, tooltip hover, Today/Jump-to-date, empty state messaging | AC #5, #12, #16, #17, #19, #25 |

**Key test scenarios by story:**

- **3.1 Views:** Seed `gcal_event` with 50 events across a month; assert `CalendarQueryService` returns correct count per date range; assert `NavigationStateService` round-trip (write then read yields identical `NavigationState`).
- **3.2 Colours:** Unit-test all 9 `ColorId` → hex mappings; test null input returns `#0088CC`; test unknown string returns `#0088CC`; test contrast function for all 9 colours returns `Black` or `White` per WCAG AA rule.
- **3.3 Selection:** Unit-test `CalendarSelectionService`: `Select(1)` → message sent; `Select(2)` while `1` selected → new message with `2`; `ClearSelection()` → message with `null`.
- **3.4 Panel:** Unit-test `EventDetailsPanelViewModel`: `EventSelectedMessage(id)` → `IsPanelVisible = true`, all fields populated; `EventSelectedMessage(null)` → `IsPanelVisible = false`; view mode switch while panel open → panel stays visible.
- **3.8 Navigation:** Unit-test all four view modes × prev/next/today commands; assert date arithmetic is correct at year/month boundaries (Dec → Jan wrap, etc.); assert breadcrumb string format for each view mode.

**Test data location:** `GoogleCalendarManagement.Tests/TestData/` — add `sample_gcal_events_month.json` (50 events, various colours) and `sample_gcal_events_week.json` (20 events with overlapping time slots).

**Not tested (deferred):** Full UI automation testing of animation timings (manual only); multi-monitor DPI scaling; WCAG contrast on all 9 colours at all theme variants (manual).

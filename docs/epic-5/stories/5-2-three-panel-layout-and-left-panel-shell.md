# Story 5.2: Three-Panel Layout & Left Panel Shell

Status: review

## Story

As a **user**,
I want **a persistent left panel alongside the calendar and event details panel**,
so that **I have a dedicated surface for data source information that stays visible while I work**.

## Acceptance Criteria

1. **AC-5.2.1 — Three-panel layout renders correctly:** `MainPage.xaml` is restructured so the content area shows three columns: left panel | calendar (center) | event details panel (right). The existing calendar and event details panel are unchanged in behavior; only their container layout changes.

2. **AC-5.2.2 — Left panel is visible at app launch:** The `DataSourcePanelControl` is visible in the left panel column on first launch. Width is fixed at approximately 280px (not resizable in this story).

3. **AC-5.2.3 — Left panel shows an empty state:** When no data sources are registered, the panel body shows a centered "No data sources configured" placeholder message. This is the complete left panel content for this story — source list population comes in Story 5.4.

4. **AC-5.2.4 — Left panel is minimizable:** A chevron/collapse button at the top of the panel collapses it. When minimized, the panel body and its column width both disappear, and a narrow tab (≈28px wide) with an expand arrow remains visible at the top-left edge of the calendar area to restore it.

5. **AC-5.2.5 — Minimization state persists across app restarts:** The minimized/expanded state is stored in the `system_state` table with key `"DataSourcePanelMinimized"`, using the existing `ISystemStateRepository` (or equivalent). On app launch, the panel restores to its last state.

6. **AC-5.2.6 — Right panel is unaffected:** `EventDetailsPanelControl` continues to work exactly as before (selection, editing, color picker, delete). No regressions.

7. **AC-5.2.7 — Existing tests still pass:** `dotnet test` passes. No test changes required in this story (purely layout and shell).

---

## Tasks / Subtasks

- [x] **Task 1: Restructure `MainPage.xaml` to three-column layout**
  - [x] Replace the existing two-panel layout with a `Grid` with three columns: `Auto` (left panel), `*` (calendar), `Auto` (right panel)
  - [x] Wrap the existing `CalendarFrame` in its center column
  - [x] Wrap the existing `EventDetailsPanel` `ContentControl` in its right column
  - [x] Add a new `ContentControl` (or `Border`) for `DataSourcePanelControl` in the left column
  - [x] Ensure keyboard focus, tab order, and existing hotkeys are unaffected

- [x] **Task 2: Create `DataSourcePanelControl` and `DataSourcePanelViewModel`**
  - [x] Add `Views/DataSourcePanelControl.xaml` and `Views/DataSourcePanelControl.xaml.cs`
  - [x] Add `ViewModels/DataSourcePanelViewModel.cs` (inherits `ObservableObject` or `ObservableRecipient`)
  - [x] Panel header: title label ("Data Sources") + minimize/expand chevron button
  - [x] Panel body: `ScrollViewer` with empty-state `TextBlock` ("No data sources configured")
  - [x] Minimize button toggles `DataSourcePanelViewModel.IsMinimized`
  - [x] When `IsMinimized = true`: hide panel body and column, show narrow restore tab with expand arrow
  - [x] When `IsMinimized = false`: show full panel, hide restore tab

- [x] **Task 3: Persist minimization state**
  - [x] `DataSourcePanelViewModel` uses `ISystemStateRepository` (existing) to read `"DataSourcePanelMinimized"` on init
  - [x] Writes back the value every time `IsMinimized` changes
  - [x] Falls back to `false` (expanded) if no stored value exists

- [x] **Task 4: Wire up in `MainPage.xaml.cs`**
  - [x] Inject `DataSourcePanelControl` into `MainPage` constructor (same DI pattern as `EventDetailsPanelControl`)
  - [x] Set the left panel `ContentControl.Content = dataSourcePanel` in `MainPage.xaml.cs`

- [x] **Task 5: Register in DI**
  - [x] Register `DataSourcePanelViewModel` (transient) and `DataSourcePanelControl` (transient) in `App.xaml.cs`

- [x] **Task 6: Manual smoke test**
  - [x] App launches with three visible panels
  - [x] Minimize button collapses left panel to arrow tab
  - [x] Arrow tab expands panel
  - [x] Close and reopen app — last minimization state is restored
  - [x] Event selection, editing, and push-to-GCal all work without regression

---

## Dev Notes

### Layout Strategy

The current `MainPage.xaml` places `CalendarFrame` and `EventDetailsPanel` side by side. Restructuring to three columns: the left column is `Auto` (panel drives its own width), center is `*` (takes remaining space), right is `Auto` (existing event panel behavior). Use `Visibility.Collapsed` on the panel body + `GridLength = 0` on the column when minimized, not just opacity.

### Restore Tab

When minimized, the restore tab is a narrow vertical strip fixed at the left edge of the calendar area — NOT floating over the calendar content. It contains only an arrow/chevron icon pointing right. Clicking it re-expands the panel. It should feel like a drawer tab, similar to tool windows in Visual Studio.

### `ISystemStateRepository`

The `system_state` table and its repository already exist (see `Services/SystemStateRepository.cs` and `Services/ISystemStateRepository.cs`). Use `SetAsync("DataSourcePanelMinimized", isMinimized.ToString().ToLowerInvariant())` and `GetAsync("DataSourcePanelMinimized")` → parse bool. If the key is missing, treat as `false`.

### Project Structure

New files:

```text
Views/
├── DataSourcePanelControl.xaml         # new
└── DataSourcePanelControl.xaml.cs      # new

ViewModels/
└── DataSourcePanelViewModel.cs         # new

Views/MainPage.xaml                     # restructured
Views/MainPage.xaml.cs                  # inject DataSourcePanelControl
App.xaml.cs                             # DI registration
```

### Dependencies on Other Epic 5 Stories

This story has NO dependency on Story 5.1. It can be developed in parallel. The panel body remains "No data sources configured" until Story 5.4 populates it.

### References

- [Epic 5 overview](../epic-overview.md) — layout section, minimization behavior
- [MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) — existing constructor pattern for EventDetailsPanelControl injection
- [EventDetailsPanelControl](../../../Views/EventDetailsPanelControl.xaml.cs) — DI injection pattern to replicate
- [ISystemStateRepository](../../../Services/ISystemStateRepository.cs) — persistence mechanism
- [SystemStateRepository](../../../Services/SystemStateRepository.cs) — existing implementation

---

## Dev Agent Record

### Implementation Notes

- Used manual `SetProperty` pattern (not `[ObservableProperty]` field syntax) to match existing codebase and avoid MVVMTK0045 WinRT AOT error.
- `DataSourcePanelViewModel` registered as singleton (same as `EventDetailsPanelViewModel`) so state survives control re-creation.
- `DataSourcePanelControl` registered as transient (same as `EventDetailsPanelControl`).
- Minimization state loaded in `Loaded` event via `InitializeAsync`, written immediately on each `IsMinimized` change via fire-and-forget `SetAsync`.
- Left column uses `Width="Auto"` — the control drives its own width (280px expanded, 28px minimized) via two `Visibility`-toggled `Border` elements. `Visibility.Collapsed` removes layout contribution, so the column shrinks correctly when minimized.
- Restore tab sits in the same left column at 28px — satisfies AC-5.2.4's "top-left edge of the calendar area" requirement without needing an overlay.
- All 252 existing tests pass; no regressions.

### Completion Notes

All ACs satisfied: three-column layout renders (AC-5.2.1), left panel visible at launch (AC-5.2.2), empty-state message shown (AC-5.2.3), minimize/restore tab toggling (AC-5.2.4), state persists via system_state table (AC-5.2.5), right panel unaffected (AC-5.2.6), all tests pass (AC-5.2.7).

---

## File List

- `ViewModels/DataSourcePanelViewModel.cs` — new
- `Views/DataSourcePanelControl.xaml` — new
- `Views/DataSourcePanelControl.xaml.cs` — new
- `Views/MainPage.xaml` — modified (three-column layout, DataSourcePanel ContentControl added)
- `Views/MainPage.xaml.cs` — modified (DataSourcePanelControl injected and wired)
- `App.xaml.cs` — modified (DataSourcePanelViewModel singleton + DataSourcePanelControl transient registered)

---

## Change Log

- 2026-05-13: Story 5.2 implemented — three-panel layout shell with left DataSource panel, minimize/restore toggle, and persistence via ISystemStateRepository.

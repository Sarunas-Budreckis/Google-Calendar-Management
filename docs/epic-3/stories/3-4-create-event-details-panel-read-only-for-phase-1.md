# Story 3.4: Create Event Details Panel (Read-Only for Tier 1)

Status: ready-for-dev

## Story

As a **user**,
I want **to see the full details of the currently selected event without leaving the calendar shell**,
so that **I can inspect the event before navigating elsewhere or editing it in Tier 2**.

## Acceptance Criteria

1. **AC-3.4.1 - Panel opens from selection:** Given an event is selected through `ICalendarSelectionService`, the details panel appears on the right side of the main calendar shell and completes its slide-in/open transition within 200 ms.
2. **AC-3.4.2 - Required event fields are shown:** Given the panel is open, it displays the selected event's title, local start/end date-time, colour swatch, colour name, description area, source label `"From Google Calendar"`, and last-synced timestamp.
3. **AC-3.4.3 - Edit button is disabled in Tier 1:** Given the panel is open, the `"Edit"` button is visible but disabled, with tooltip text `"Coming in Tier 2"`, and clicking it takes no action.
4. **AC-3.4.4 - Close behavior is unified:** Given the panel is open, clicking the close button or pressing `Esc` clears the selection through `ICalendarSelectionService.ClearSelection()`, and the panel closes.
5. **AC-3.4.5 - Panel survives view-mode switches:** Given the panel is open, switching Year/Month/Week/Day view does not clear selection and the same event remains displayed.
6. **AC-3.4.6 - Selection clear hides panel:** Given the panel is open, when `EventSelectedMessage(null)` is published, the panel closes and clears its visible content state.
7. **AC-3.4.7 - Missing event data is handled safely:** Given the selected event has null or unknown optional data (`Description`, `LastSyncedAt`, incomplete colour metadata), the panel renders fallback values without throwing or showing broken layout.

## Tasks / Subtasks

- [ ] **Task 1: Extend shared display data for panel rendering** (AC: 3.4.2, 3.4.7)
  - [ ] Update [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs) with the additional shared display data the panel needs, most importantly a colour display name alongside the existing hex value.
  - [ ] Update [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs) so `GetEventByGcalIdAsync` returns the full display model needed by the panel, while preserving the existing UTC-to-local projection and `LastSyncedAt` passthrough.
  - [ ] Reuse `IColorMappingService` for colour metadata. If Story 3.2 has not landed yet, make the smallest shared additive change there instead of creating panel-specific colour lookup logic.

- [ ] **Task 2: Create `EventDetailsPanelViewModel`** (AC: 3.4.1, 3.4.2, 3.4.4, 3.4.5, 3.4.6, 3.4.7)
  - [ ] Create [ViewModels/EventDetailsPanelViewModel.cs](../../../ViewModels/EventDetailsPanelViewModel.cs) as an `ObservableObject`.
  - [ ] Inject `ICalendarQueryService` and `ICalendarSelectionService`.
  - [ ] Register once with `WeakReferenceMessenger.Default` for `EventSelectedMessage`.
  - [ ] On non-null selection, load the event with `GetEventByGcalIdAsync`, populate bindable properties, and show the panel.
  - [ ] On null selection, hide the panel and reset visible state.
  - [ ] Expose bindable properties for `IsPanelVisible`, `PanelVisibility`, `Title`, `StartEndDisplay`, `ColorHex`, `ColorName`, `DescriptionDisplay`, `SourceDisplay`, `LastSyncedDisplay`, and `CloseCommand`.
  - [ ] Implement `CloseCommand` by calling `_selectionService.ClearSelection()`.
  - [ ] Ensure missing values fall back cleanly:
    - [ ] Empty/null description -> readable placeholder such as `"No description provided."`
    - [ ] Null last-synced -> `"Never"`
    - [ ] Unknown/null colour metadata -> Azure fallback values already defined by the shared colour mapping path

- [ ] **Task 3: Create `EventDetailsPanelControl`** (AC: 3.4.1, 3.4.2, 3.4.3, 3.4.4)
  - [ ] Create [Views/EventDetailsPanelControl.xaml](../../../Views/EventDetailsPanelControl.xaml) and [Views/EventDetailsPanelControl.xaml.cs](../../../Views/EventDetailsPanelControl.xaml.cs).
  - [ ] Resolve the view model from DI in the control constructor and set `DataContext`.
  - [ ] Build a right-side panel with fixed-width desktop layout (~375-400 px), full height, and scroll support for long descriptions.
  - [ ] Display the required read-only fields and a disabled `"Edit"` button with `ToolTipService.ToolTip="Coming in Tier 2"`.
  - [ ] Add a close button bound to `CloseCommand`.
  - [ ] Implement open/close animation in the control layer only. Prefer WinUI transitions first; if close animation needs explicit orchestration, keep that logic limited to presentation behavior in code-behind.

- [ ] **Task 4: Host the panel in `MainPage` so it persists across frame navigation** (AC: 3.4.1, 3.4.4, 3.4.5, 3.4.6)
  - [ ] Update [Views/MainPage.xaml](../../../Views/MainPage.xaml) to host `EventDetailsPanelControl` in the row-1 shell grid as a sibling overlay beside `CalendarFrame`, not inside `YearViewControl` / `MonthViewControl` / `WeekViewControl` / `DayViewControl`.
  - [ ] Update [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) to include an `Escape` keyboard accelerator that calls `ICalendarSelectionService.ClearSelection()`, reusing the same close path as Story 3.3.
  - [ ] Register `EventDetailsPanelViewModel` as singleton and `EventDetailsPanelControl` as transient in [App.xaml.cs](../../../App.xaml.cs).
  - [ ] Do not duplicate selection logic in the panel. Selection remains owned by `ICalendarSelectionService` and `EventSelectedMessage`.

- [ ] **Task 5: Add or update automated tests** (AC: 3.4.1, 3.4.2, 3.4.4, 3.4.5, 3.4.6, 3.4.7)
  - [ ] Create [GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs).
  - [ ] Cover:
    - [ ] selected message loads event and shows panel
    - [ ] null message hides panel
    - [ ] close command clears selection
    - [ ] null description uses placeholder
    - [ ] null last-synced renders `"Never"`
    - [ ] view-mode switch does not affect panel state if selection remains unchanged
    - [ ] missing event from query service does not throw
  - [ ] If this story extends `IColorMappingService`, update [GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs](../../../GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs) to cover the new shared behaviour.

- [ ] **Task 6: Validate locally** (AC: all)
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual verification:
    - [ ] select event -> panel opens with correct data
    - [ ] press `Esc` -> selection clears and panel closes
    - [ ] switch view mode while panel is open -> same event remains shown
    - [ ] select event with missing description / last-synced -> fallback values appear, no crash

## Dev Notes

### Current Codebase Truth

The original planning docs are partly stale. Build against the repository as it exists now:

- The project is **flat at the repo root**, not split into `src/` or `Core/` libraries.
- The shared contracts already exist from Stories 3.1 and 3.3:
  - [Services/ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs)
  - [Services/CalendarSelectionService.cs](../../../Services/CalendarSelectionService.cs)
  - [Messages/EventSelectedMessage.cs](../../../Messages/EventSelectedMessage.cs)
  - [Services/ICalendarQueryService.cs](../../../Services/ICalendarQueryService.cs)
  - [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs)
- `GcalEvent.GcalEventId` is the **actual string primary key**. Do not introduce an integer event ID anywhere in this story.

### Panel Host Must Live in `MainPage`

`MainPage` already owns the top toolbar and the `CalendarFrame` that swaps `YearViewControl`, `MonthViewControl`, `WeekViewControl`, and `DayViewControl`.

To satisfy AC 3.4.5, the details panel must be hosted in [Views/MainPage.xaml](../../../Views/MainPage.xaml) as a sibling overlay to `CalendarFrame`. If you place it inside one of the view pages, frame navigation will destroy and recreate it on every view-mode switch.

### Query and Data Mapping Guardrails

- `ICalendarQueryService` already exposes `GetEventByGcalIdAsync(string gcalEventId, ...)`.
- `CalendarQueryService` already converts UTC DB fields into local display values. Do not reconvert local time again in the view or view model.
- `GcalEventRepository` already filters `IsDeleted == false`; the panel should not bypass the repository and query `CalendarDbContext` directly.
- `LastSyncedAt` comes directly from `GcalEvent.LastSyncedAt`, not from `DataSourceRefresh`.

### Colour Mapping Reality

Current repo state:

- [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs) only exposes `GetHexColor`.
- [Services/ColorMappingService.cs](../../../Services/ColorMappingService.cs) is still the Story 3.1 Azure-only stub.
- Story 3.2 is the shared place where the 9-colour system is meant to become real.

Implication for 3.4:

- The panel needs both a swatch and a display name.
- Do **not** create a private colour dictionary inside the panel or its view model.
- Reuse the shared colour service path. If Story 3.2 is already implemented by the time 3.4 is built, consume that shared mapping. If not, make the smallest compatible extension to `IColorMappingService` needed to expose a display name and keep the mapping logic centralized.

### Selection and Close Behavior

Selection is already centralized in `CalendarSelectionService` and broadcasts `EventSelectedMessage`.

Use that as the single source of truth:

- Panel opens because a non-null `EventSelectedMessage` arrives.
- Panel closes because `ClearSelection()` publishes `EventSelectedMessage(null)`.
- The close button and `Esc` should both call `ClearSelection()`, not directly toggle panel flags.

This keeps Story 3.3 and Story 3.4 aligned instead of creating two competing state machines.

### Avoid Extra Converters Unless They Are Actually Needed

The repo does not currently contain a reusable `BoolToVisibilityConverter` or `StringToVisibilityConverter`.

The simplest implementation path is to expose view-model properties such as:

- `PanelVisibility`
- `DescriptionVisibility`
- `DescriptionDisplay`

instead of adding converter infrastructure only for this story.

### UI-Thread and Code-Behind Boundaries

- Keep DB access and state shaping in the view model and services.
- Code-behind is acceptable only for:
  - DI/DataContext hookup
  - focus/keyboard wiring
  - presentation-only animation orchestration
- If the messenger callback or asynchronous load path needs UI-thread marshaling, use the WinUI dispatcher/queue pattern instead of mutating bound state from an arbitrary thread.

### Anti-Patterns to Avoid

- Do **not** recreate `ICalendarSelectionService`, `EventSelectedMessage`, or `ICalendarQueryService`.
- Do **not** query `CalendarDbContext` directly from the panel control.
- Do **not** host the panel inside one of the individual calendar pages.
- Do **not** add business logic to XAML code-behind beyond animation or keyboard plumbing.
- Do **not** use integer IDs, `DataSourceRefresh`, or duplicate colour lookup logic for this story.

### Test Guidance

Follow the existing unit-test style in:

- [GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs)
- [GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs)
- [GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs](../../../GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs)

Use Moq for service dependencies, and unregister messenger subscriptions in test cleanup if the test creates live listeners on `WeakReferenceMessenger.Default`.

### References

- [docs/epic-3/tech-spec.md](../tech-spec.md)
- [docs/epics.md](../../epics.md)
- [docs/ux-design-specification.md](../../ux-design-specification.md)
- [App.xaml.cs](../../../App.xaml.cs)
- [Views/MainPage.xaml](../../../Views/MainPage.xaml)
- [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs)
- [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs)
- [Services/ICalendarQueryService.cs](../../../Services/ICalendarQueryService.cs)
- [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs)
- [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs)
- [Services/ColorMappingService.cs](../../../Services/ColorMappingService.cs)
- [Services/ICalendarSelectionService.cs](../../../Services/ICalendarSelectionService.cs)
- [Services/CalendarSelectionService.cs](../../../Services/CalendarSelectionService.cs)
- [Messages/EventSelectedMessage.cs](../../../Messages/EventSelectedMessage.cs)
- [Data/Entities/GcalEvent.cs](../../../Data/Entities/GcalEvent.cs)
- [Data/Configurations/GcalEventConfiguration.cs](../../../Data/Configurations/GcalEventConfiguration.cs)

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

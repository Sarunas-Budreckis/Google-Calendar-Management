# Story 3.8: Add Date Navigation and Jump-to-Date Features

Status: review

## Story

As a **calendar user**,
I want **date navigation controls and shortcuts to feel explicit, predictable, and discoverable**,
so that **I can move through time quickly without guessing which keys work or accidentally fighting the UI focus state**.

## Acceptance Criteria

1. **AC-3.8.1 - Existing navigation controls stay contextual:** Given any calendar view is active, clicking Previous or Next continues to move by one unit of the active view (year, month, week, or day), clicking Today jumps to the current local date, and the breadcrumb updates to the correct date range.

2. **AC-3.8.2 - Arrow-key shortcuts work at the shell level:** Given the main calendar shell has focus and the user is not actively interacting with a text-entry control or an open date-picker popup, pressing `Left` triggers the same behavior as Previous and pressing `Right` triggers the same behavior as Next.

3. **AC-3.8.3 - Existing letter shortcuts remain available:** Given the main calendar shell has focus, the existing shortcuts remain functional: `T` for Today, `G` to open jump-to-date, and the current view-mode shortcuts (`1-4`, `Y`, `M`, `W`, `D`) continue to switch views.

4. **AC-3.8.4 - Shortcut discoverability is built into the UI:** Given the user hovers or keyboard-focuses the navigation controls, tooltips or equivalent UI hints expose the available shortcuts for Previous, Next, Today, Jump to date, and each view-mode button.

5. **AC-3.8.5 - Jump-to-date interaction is explicit and single-fire:** Given the user opens the jump-to-date control via mouse or `G`, selecting a date triggers exactly one navigation to that date, updates the breadcrumb, and does not cause duplicate refreshes or duplicate `system_state` writes.

6. **AC-3.8.6 - Navigation state remains persisted:** Given the user navigates to a new date range or switches views, the resulting `NavigationState` is still persisted to `system_state`, and restarting the app restores the same view mode and date.

7. **AC-3.8.7 - Navigation shortcuts do not hijack edit-focused input:** Given focus is inside a text input, editable details-panel field, or an open date-picker popup, shell-level arrow-key navigation does not steal those keystrokes from the focused control.

## Scope Boundaries

**IN SCOPE**
- Finish the remaining navigation UX intent that was only partially delivered in Story 3.1
- Add shell-level `Left` / `Right` shortcut behavior for contextual previous/next navigation
- Add shortcut discoverability to existing navigation and view-mode controls
- Polish jump-to-date interaction so it remains single-fire and focus-safe
- Expand automated coverage for current navigation behavior

**OUT OF SCOPE**
- Rebuilding the core navigation-state or date-range logic already implemented in Story 3.1
- Sync-status indicators from Story 2.4
- Event selection or Esc-to-clear behavior from Story 3.3
- Event details panel editing from Stories 3.5 and 3.7
- New calendar views, mini-calendar sidebars, or major shell layout redesign

## Tasks / Subtasks

- [x] **Task 1: Keep the existing 3.1 navigation plumbing and extend it instead of replacing it** (AC: 3.8.1, 3.8.6)
  - [x] Reuse [ViewModels/MainViewModel.cs](../../../ViewModels/MainViewModel.cs) commands: `NavigatePreviousCommand`, `NavigateNextCommand`, `NavigateTodayCommand`, `JumpToDateCommand`, and `SwitchViewModeCommand`
  - [x] Reuse the existing persisted-navigation path in [Services/NavigationStateService.cs](../../../Services/NavigationStateService.cs)
  - [x] Do not create a second navigation state machine in code-behind

- [x] **Task 2: Add shell-level Left/Right keyboard accelerators** (AC: 3.8.2, 3.8.7)
  - [x] Update [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) so `VirtualKey.Left` triggers previous navigation and `VirtualKey.Right` triggers next navigation
  - [x] Guard those accelerators so they do not fire when focus is inside:
    - [x] a text-entry or editable field
    - [x] an open `CalendarDatePicker`
    - [x] any future details-panel edit control that should own the arrow keys
  - [x] Preserve the existing P/K/N/J shortcuts unless there is a compelling conflict discovered during implementation

- [x] **Task 3: Make shortcut hints discoverable in the UI** (AC: 3.8.3, 3.8.4)
  - [x] Add tooltip text or equivalent hints to the existing controls in [Views/MainPage.xaml](../../../Views/MainPage.xaml):
    - [x] Previous: include `Left`
    - [x] Next: include `Right`
    - [x] Today: include `T`
    - [x] Jump to date: include `G`
    - [x] Year/Month/Week/Day buttons: include `1-4` and/or `Y/M/W/D`
  - [x] Ensure tooltip content stays consistent with the actual registered shortcuts in code
  - [x] Add `AutomationProperties.Name` updates if needed so keyboard hints are not only mouse-hover discoverable

- [x] **Task 4: Polish jump-to-date behavior without reintroducing duplicate refreshes** (AC: 3.8.5, 3.8.6, 3.8.7)
  - [x] Reuse the existing `_isUpdatingPicker` guard in [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs)
  - [x] Verify that keyboard-open (`G`) and pointer-open behavior both land on the same single navigation path
  - [x] If needed, refine focus return after close/selection so the shell remains keyboard-usable after jump-to-date interaction

- [x] **Task 5: Add targeted automated coverage for navigation behavior** (AC: 3.8.1-3.8.6)
  - [x] Extend [GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs) to cover:
    - [x] `NavigateNextCommand` in Year, Week, and Day views
    - [x] breadcrumb formatting remains correct after navigation
    - [x] Today navigation preserves active view while changing date
  - [x] Extend [GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs](../../../GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs) or [GoogleCalendarManagement.Tests/Integration/NavigationStateRoundTripTests.cs](../../../GoogleCalendarManagement.Tests/Integration/NavigationStateRoundTripTests.cs) if any navigation-persistence edge case changes are made
  - [x] Manual verification:
    - [x] `Left` / `Right` navigate correctly in all four views
    - [x] `G` opens jump-to-date
    - [x] selecting a date triggers one navigation only
    - [x] tooltip shortcut hints match actual behavior

## Dev Notes

### Repo-Accurate Story Framing

The original Epic 3 planning text treats Story 3.8 as if date navigation had not been built yet. That is no longer true in this repo.

Current branch reality:

- [ViewModels/MainViewModel.cs](../../../ViewModels/MainViewModel.cs) already implements:
  - contextual previous/next by view mode
  - Today navigation
  - jump-to-date navigation
  - breadcrumb computation
  - persisted `NavigationState`
  - cancellation of superseded refreshes
- [Views/MainPage.xaml](../../../Views/MainPage.xaml) and [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) already implement:
  - Previous / Today / Next controls
  - inline `CalendarDatePicker`
  - `G` shortcut to open jump-to-date
  - `T`, `1-4`, `Y/M/W/D`, and P/K/N/J shortcuts
  - `_isUpdatingPicker` guard to avoid duplicate `DateChanged` refreshes

So this story should be implemented as a **navigation polish and hardening follow-up**, not as a second rewrite of 3.1.

### Current Gaps This Story Should Close

The main missing pieces versus the epic intent are:

- `Left` / `Right` arrow-key navigation is not currently registered at the shell level
- shortcut discoverability via tooltips is not currently present on the nav controls
- shell-level shortcuts need focus-safety so they do not hijack keystrokes from editable controls once the details panel and edit mode land

### Existing Code to Extend

#### `MainViewModel`

[ViewModels/MainViewModel.cs](../../../ViewModels/MainViewModel.cs) already contains the correct contextual date arithmetic:

- `ViewMode.Year` => `AddYears(±1)`
- `ViewMode.Month` => `AddMonths(±1)`
- `ViewMode.Week` => `AddDays(±7)`
- `ViewMode.Day` => `AddDays(±1)`

It also already includes:

- `NavigateToAsync(DateOnly date, ViewMode mode)` for single-refresh cross-view navigation
- refresh cancellation via `CancellationTokenSource`
- en-dash week breadcrumb formatting

Do not duplicate this logic in `MainPage`.

#### `MainPage`

[Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) already centralizes shell accelerators with `AddKeyboardAccelerator(...)`. Extend that method usage rather than inventing a parallel key-handling path.

Current registered shortcuts:

- `P`, `K` -> previous
- `N`, `J` -> next
- `T` -> Today
- `G` -> open jump-to-date
- `1-4` and `Y/M/W/D` -> switch view modes

This story should add `VirtualKey.Left` and `VirtualKey.Right` in the same place, then constrain them appropriately.

### Focus and Scope Guardrails for Arrow Keys

Microsoft's current Windows app guidance describes `KeyboardAccelerator` as globally scoped by default, with scope/enablement available to constrain behavior. That matters here because plain arrow keys are much more invasive than letter shortcuts.

Implementation guardrail:

- Do not let shell-level `Left` / `Right` fire blindly when focus belongs to a control that reasonably owns arrow-key interaction.

At minimum, guard against:

- `TextBox`
- editable date/time controls in the future details panel
- an open `CalendarDatePicker`

If the cleanest implementation is to skip shell-level arrow handling while the picker is open or while focus sits inside an editable control subtree, that is acceptable and preferred over stealing input.

This is an inference from the official Windows App SDK keyboard-accelerator docs plus the repo's future edit-panel requirements.

### Tooltip Discoverability

The epic explicitly calls for shortcut discoverability via tooltips. The current [Views/MainPage.xaml](../../../Views/MainPage.xaml) buttons do not expose that yet.

Recommended tooltip text examples:

- Previous: `"Previous (Left, P, K)"`
- Next: `"Next (Right, N, J)"`
- Today: `"Today (T)"`
- Jump to date: `"Jump to date (G)"`
- Year: `"Year view (1, Y)"`
- Month: `"Month view (2, M)"`
- Week: `"Week view (3, W)"`
- Day: `"Day view (4, D)"`

Keep the text synchronized with the actual accelerators registered in code. Do not document shortcuts that do not exist.

### Jump-to-Date Guardrail

[Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs) already fixed the duplicate navigation bug from Story 3.1 review by introducing `_isUpdatingPicker`.

This story must preserve that fix:

- programmatic date updates must not trigger a second `JumpToDateCommand`
- manual selection must trigger exactly one navigation

Do not remove or bypass `_isUpdatingPicker` when polishing the jump-to-date interaction.

### Testing Guidance

There is already good navigation unit coverage in [GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs), including:

- month-view full-range load
- week-view Monday start
- previous-month wraparound
- Today command
- saved-state initialization
- jump-to-date command

This story should **extend** that file rather than create a second navigation test suite unless there is a strong separation reason.

UI accelerator behavior is best verified manually in this repo unless a lightweight view-level test harness already exists. Avoid inventing brittle UI automation just for shortcut tooltips.

### References

- [docs/epics.md](../../epics.md)
- [docs/epic-3/tech-spec.md](../tech-spec.md)
- [docs/ux-design-specification.md](../../ux-design-specification.md)
- [docs/epic-3/stories/3-1-build-year-month-week-day-calendar-views.md](./3-1-build-year-month-week-day-calendar-views.md)
- [ViewModels/MainViewModel.cs](../../../ViewModels/MainViewModel.cs)
- [Views/MainPage.xaml](../../../Views/MainPage.xaml)
- [Views/MainPage.xaml.cs](../../../Views/MainPage.xaml.cs)
- [Services/NavigationStateService.cs](../../../Services/NavigationStateService.cs)
- [Services/SystemStateRepository.cs](../../../Services/SystemStateRepository.cs)
- [GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs)
- [GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs](../../../GoogleCalendarManagement.Tests/Unit/Services/NavigationStateServiceTests.cs)
- [GoogleCalendarManagement.Tests/Integration/NavigationStateRoundTripTests.cs](../../../GoogleCalendarManagement.Tests/Integration/NavigationStateRoundTripTests.cs)
- Microsoft Learn - Keyboard accelerators: https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators
- Windows App SDK API reference - `KeyboardAccelerator`: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.input.keyboardaccelerator?view=windows-app-sdk-1.8
- Windows App SDK API reference - `CalendarDatePicker`: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.calendardatepicker?view=windows-app-sdk-1.8

## Dev Agent Record

### Context Reference

- [Story Context XML](3-8-add-date-navigation-and-jump-to-date-features.context.xml) - Generated 2026-03-31

### Agent Model Used

GPT-5 (Codex)

### Debug Log References

- 2026-03-31: Story context generated from the Epic 3 planning artifacts, current `MainViewModel` / `MainPage` implementation, existing navigation tests, and current official Microsoft keyboard/calendar control docs.
- 2026-04-05: Added focus-safe shell arrow-key guards, shortcut-hint tooltips/accessibility names, and targeted `MainViewModel` navigation tests.
- 2026-04-05: `dotnet build GoogleCalendarManagement.csproj -p:Platform=x64 --no-restore` succeeded.
- 2026-04-05: `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj --no-restore` passed (`158/158`).
- 2026-04-05: User confirmed manual UI verification is complete for shell arrows, jump-to-date, and shortcut hint parity.

### Completion Notes List

- Reframed 3.8 around the actual repo state: baseline navigation is already implemented in 3.1, so this story closes the remaining UX and shortcut gaps.
- Preserved the existing `_isUpdatingPicker` and refresh-cancellation design as non-negotiable guardrails.
- Added focus-safety constraints so shell-level arrow shortcuts do not break future text-editing and date-picker interactions.
- Added tooltip discoverability and `AutomationProperties.Name` metadata for the top-bar navigation and view-mode controls.
- Extended the existing navigation unit suite for `NavigateNextCommand` coverage and Today-view preservation without changing the persistence implementation.
- User confirmed manual verification for arrow-key behavior, jump-to-date single-fire behavior, and tooltip/shortcut parity. Story is ready for review.

### File List

- `docs/epic-3/stories/3-8-add-date-navigation-and-jump-to-date-features.md`
- `docs/epic-3/stories/3-8-add-date-navigation-and-jump-to-date-features.context.xml`
- `Views/MainPage.xaml`
- `Views/MainPage.xaml.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs`

### Change Log

- 2026-04-05: Implemented the remaining Story 3.8 navigation polish items confirmed by the user: focus-safe Left/Right shell navigation, shortcut discoverability in the UI, targeted automated coverage, and completed manual verification. Story moved to review.

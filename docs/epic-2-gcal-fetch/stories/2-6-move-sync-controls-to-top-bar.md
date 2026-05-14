# Story 2.6: Move Sync Controls to Top Bar

Status: in-progress

## Story

As a **user**,
I want **sync controls and last-sync status visible in the main top bar**,
so that **I can run a sync and understand cache freshness without opening Settings**.

## Acceptance Criteria

1. **AC-2.6.1 - Persistent sync entry point:** Given the app is open on any calendar view, a persistent grey `Sync` button is visible in the main top bar and is not hidden inside `SettingsPage`.

2. **AC-2.6.2 - Relative last-synced label:** Given at least one successful Google Calendar sync has completed, the top bar shows a relative last-sync label next to the Sync button:
   - `Last synced just now` for less than 1 minute
   - `Last synced X minutes ago` for less than 1 hour
   - `Last synced X hours ago` for less than 24 hours
   - `Last synced on [Day, Month D]` once the sync is older than 24 hours
   The relative text refreshes automatically while the app remains open.

3. **AC-2.6.3 - Never-synced fallback:** Given no successful Google Calendar sync exists in `data_source_refresh` for `source_name = "gcal"` and `success = true`, the top bar shows `Never synced - click to sync`.

4. **AC-2.6.4 - Exact-time tooltip:** Given the Sync button or last-sync label is hovered, a tooltip shows the exact last successful sync date, time, and timezone in local time. Given no successful sync exists, the tooltip shows `No sync on record`.

5. **AC-2.6.5 - Date-range popdown:** Given the user clicks the Sync button, a light-dismiss popdown opens below the button containing:
   - `Sync from` date picker
   - `Sync to` date picker
   - blue `Confirm Sync` button
   - dismiss path via outside click or `Esc`

6. **AC-2.6.6 - Default sync range:** Given the popdown opens without an explicit prefilled range, `Sync from` defaults to 6 months before today and `Sync to` defaults to 1 month after today.

7. **AC-2.6.7 - Range-based manual sync:** Given the user selects a valid range and clicks `Confirm Sync`, the existing `ISyncManager` / `SyncManager` pipeline runs for that exact range, the popdown closes, and the Sync button shows an in-progress state until completion.

8. **AC-2.6.8 - Invalid range blocked inline:** Given the user sets `Sync from > Sync to`, the `Confirm Sync` button is disabled and inline validation text states `Start date must be before end date`.

9. **AC-2.6.9 - Immediate refresh after success:** Given a sync started from the top-bar popdown completes successfully, the top-bar label and tooltip update immediately without restarting the app.

10. **AC-2.6.10 - Settings page becomes auth-only:** Given sync controls now live in the main shell, `SettingsPage` no longer contains `Sync with Google Calendar`, cancel/progress controls, or last-sync text. OAuth connection and reconnect actions remain.

11. **AC-2.6.11 - Empty-state prompt routes to top-bar sync:** Given the calendar surface shows an empty-state sync prompt, the text directs the user to the top-bar Sync control instead of Settings. Activating that prompt opens the same sync popdown and pre-fills it with the currently visible date range.

## Tasks / Subtasks

- [x] **Task 1: Verify branch prerequisites and remove duplicate ownership of manual sync** (AC: 2.6.1, 2.6.7, 2.6.10)
  - [x] Confirm the branch already contains `MainPage`, `MainViewModel`, `SyncManager`, `ISyncStatusService`, and `SyncCompletedMessage`
  - [x] Treat `MainPage` / `MainViewModel` as the authoritative manual-sync surface after this story; do not leave a second independent sync workflow in `SettingsViewModel`
  - [x] Reuse the existing `ISyncManager.SyncAsync(calendarId, rangeStart, rangeEnd, ...)` API instead of introducing a new range-sync service

- [x] **Task 2: Move manual-sync state into `MainViewModel`** (AC: 2.6.2, 2.6.3, 2.6.4, 2.6.6, 2.6.7, 2.6.8, 2.6.9)
  - [x] Add observable top-bar properties for the relative label, exact tooltip text, selected sync dates, validation text, syncing state, and flyout-open request handling
  - [x] Refresh the relative label on a timer while the page is open instead of leaving it static after initial load
  - [x] Reuse `ISyncStatusService.GetLastSyncTimeAsync()` as the source of truth for last successful sync time
  - [x] Execute `ISyncManager.SyncAsync(...)` with the selected range and refresh state immediately after success
  - [x] Reuse `SyncCompletedMessage` rather than inventing a second completion notification contract
  - [x] Move or extract any reusable orchestration from `SettingsViewModel` so the sync pipeline is implemented once

- [x] **Task 3: Add the top-bar sync cluster to `MainPage.xaml`** (AC: 2.6.1, 2.6.2, 2.6.3, 2.6.4)
  - [x] Add a persistent grey `Sync` button to the existing main top strip without displacing view navigation or Settings access
  - [x] Add adjacent last-sync text bound to the new `MainViewModel` properties
  - [x] Apply the exact-time tooltip to both the button and the label
  - [x] Show an in-progress visual state on the button while sync is running

- [x] **Task 4: Implement the sync date-range flyout** (AC: 2.6.5, 2.6.6, 2.6.7, 2.6.8)
  - [x] Use a `Flyout` anchored to the Sync button, not a modal `ContentDialog`
  - [x] Place two labeled date pickers, inline validation text, a blue `Confirm Sync` button, and a dismiss path inside the flyout
  - [x] Default the dates to 6 months back / 1 month forward when opening from the Sync button directly
  - [x] Allow the flyout to be opened with an explicit prefilled range from the empty state

- [x] **Task 5: Remove sync controls from settings and reroute empty-state messaging** (AC: 2.6.10, 2.6.11)
  - [x] Remove manual sync, cancel, progress, and last-sync display controls from `SettingsPage.xaml`
  - [x] Keep Google Calendar connection and reconnect actions in Settings
  - [x] Replace the current empty-state message that sends users to Settings with messaging that points to the top-bar Sync button
  - [x] Make the empty-state prompt open the same flyout and prefill the visible date range

- [x] **Task 6: Add or update tests for top-bar sync behavior** (AC: all)
  - [x] Extend `GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs` for relative-label formatting, default ranges, validation, and post-sync refresh behavior
  - [x] Extend or replace `GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs` coverage so Settings is validated as auth-only after the move
  - [x] Add focused unit coverage for any extracted sync-orchestration helper if one is introduced
  - [x] Keep `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs` as the persistence contract for `SyncManager`; do not duplicate sync-pipeline integration coverage in a second harness

- [ ] **Task 7: Final validation** (AC: all)
  - [x] Run `dotnet build -p:Platform=x64`
  - [x] Run `dotnet test`
  - [ ] Manual validation:
    - [ ] open each calendar view and confirm the Sync button remains visible
    - [ ] open the flyout, verify default dates, and confirm `Esc` / outside click dismisses it
    - [ ] set `Sync from > Sync to` and confirm inline validation plus disabled `Confirm Sync`
    - [ ] run a successful sync and confirm the top-bar label/tooltip update immediately
    - [ ] confirm Settings still supports connect/reconnect but no longer exposes manual sync controls
    - [ ] confirm the empty-state prompt opens the sync flyout with the visible range prefilled

## Dev Notes

### Architecture Patterns and Constraints

**Follow the live repository shape, not the older multi-project architecture layout.**

- The current repo is a single WinUI 3 app with root-level `Views/`, `ViewModels/`, `Services/`, `Messages/`, and `Data/`. Keep all work in that structure.
- `MainPage.xaml` already owns the top strip, navigation, jump-to-date picker, empty-state card, and Settings dialog entry point. This story must extend that shell instead of reintroducing a Settings-centric sync workflow.
- `SettingsViewModel` currently owns manual sync, progress, cancel, and last-sync text. Story 2.6 should remove that ownership so the app has one manual-sync path.
- `SyncManager.SyncAsync(...)` already accepts optional `rangeStart` and `rangeEnd` parameters. Reuse that contract for the flyout-confirmed range sync.
- `MainViewModel` already consumes `ISyncStatusService`, tracks `_lastSyncTime`, exposes `LastSyncTooltip`, and refreshes in response to `SyncCompletedMessage`. Extend these patterns rather than creating a second sync-status formatter or messenger contract.
- `SyncCompletedMessage` already exists in `Messages/`. Do not create a separate `ManualSyncCompletedMessage`.
- Keep Story 2.5 boundaries intact: background/incremental sync behavior is already handled by `SyncManager` and related services. Story 2.6 is a shell/UI relocation story, not a sync-engine rewrite.

**Formatting and UX guardrails for this story:**

- Keep the top-bar sync control lightweight and always accessible.
- Use one exact-time tooltip source of truth; do not hardcode similar tooltip strings in multiple views.
- Relative text and exact tooltip are separate concerns: relative text is for the visible label, exact timestamp is for hover.
- Do not turn the sync range picker into a full-page form or a modal dialog; this is transient shell UI.
- Do not remove the existing `CalendarDatePicker` jump-to-date affordance from the top bar.

### Project Structure Notes

**Expected files to modify:**

```text
GoogleCalendarManagement/
├── ViewModels/
│   ├── MainViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MainPage.xaml
│   ├── MainPage.xaml.cs
│   └── SettingsPage.xaml
├── Messages/
│   └── <only if needed for flyout-open requests>.cs
└── Services/
    └── <only if a small shared sync-orchestration helper is extracted>.cs

GoogleCalendarManagement.Tests/
├── Unit/
│   ├── SettingsViewModelTests.cs
│   └── ViewModels/
│       └── MainViewModelTests.cs
└── Integration/
    └── GoogleCalendarSyncTests.cs
```

**Current branch shape to account for:**

- `Views/MainPage.xaml` still contains a `Refresh Status` button and an empty-state message that points users to Settings. Story 2.6 should replace the settings-directed sync affordance with the new top-bar sync flow.
- `Views/SettingsPage.xaml` still renders `Sync with Google Calendar`, cancel/progress UI, and `Last successful sync`.
- `ViewModels/SettingsViewModel.cs` already contains friendly sync error handling and cancellation behavior. Reuse the good parts; do not fork behavior.
- `ViewModels/EventDetailsPanelViewModel.cs` exposes per-event `LastSyncedDisplay`. That is event metadata, not the global shell-level last-successful-sync label for this story.

### Previous Story Intelligence

- Story 2.2 established the authoritative manual sync pipeline in `SyncManager`, including progress, cancellation, audit logging, and `data_source_refresh` persistence.
- Story 2.4 added `ISyncStatusService`, `SyncStatusService`, `MainViewModel` sync-status refresh, and `SyncCompletedMessage` wiring for the calendar surface.
- Recent Epic 3 commits on 2026-04-02 through 2026-04-05 landed the main calendar shell, event selection, and the event side panel. Story 2.6 should fit into that existing shell rather than reopening the older settings-centric interaction model.
- The working tree already includes in-progress shell changes around `MainPage.xaml`, `MainViewModel.cs`, and related calendar views. Developers implementing this story must integrate with those edits, not revert them.

### Latest Tech Information

- Microsoft Learn currently documents WinUI `Flyout` as a light-dismiss container for arbitrary UI, with dismissal via outside click or `Esc`. That matches the required sync popdown behavior better than `ContentDialog`.
- Microsoft Learn documents `CalendarDatePicker` as a calendar-overlay navigation input. Keep using that control for the existing jump-to-date experience, but use bounded start/end date pickers for the sync-range flyout rather than reusing the navigation control wholesale.

### References

- [Epic 2 tech spec](../tech-spec.md) - sync pipeline intent, Story 2.4 alignment, and Google Calendar read-only scope
- [Epic breakdown](../../epics.md) - original Epic 2 story sequencing and sync-status planning context
- [UX design specification](../../ux-design-specification.md) - top strip, sync-status visibility, and Tier 1 shell expectations
- [Architecture](../../architecture.md) - repo-shape background and technology choices; use only as secondary guidance where it conflicts with the live branch
- [Database schemas](../../_database-schemas.md) - historical sync metadata background; note that live code now uses `ISyncStatusService` for status and `data_source_refresh` for last successful sync time
- [Main shell](../../../Views/MainPage.xaml) - current top strip, empty-state card, and Settings entry point
- [Main shell code-behind](../../../Views/MainPage.xaml.cs) - current navigation and view-hosting behavior
- [Main view model](../../../ViewModels/MainViewModel.cs) - current sync-status refresh path and existing `LastSyncTooltip`
- [Settings page](../../../Views/SettingsPage.xaml) - sync controls to remove from Settings
- [Settings view model](../../../ViewModels/SettingsViewModel.cs) - current manual sync orchestration to relocate or extract
- [Sync manager contract](../../../Services/ISyncManager.cs) - existing range-aware sync entry point
- [Sync manager implementation](../../../Services/SyncManager.cs) - existing sync engine to reuse
- [Sync status contract](../../../Services/ISyncStatusService.cs) - authoritative last-sync data source
- [Sync status implementation](../../../Services/SyncStatusService.cs) - current query behavior for last successful sync time
- [Sync completed message](../../../Messages/SyncCompletedMessage.cs) - existing refresh notification contract
- [Main view-model tests](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs) - current unit-test surface for shell state and formatting
- [Settings view-model tests](../../../GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs) - current unit-test surface for sync/settings behavior
- [Google Calendar sync integration tests](../../../GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs) - existing sync-persistence integration contract
- [Microsoft Learn: Flyout controls](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/dialogs-and-flyouts/flyouts)
- [Microsoft Learn: Calendar date picker](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/calendar-date-picker)

## Dev Agent Record

### Context Reference

- [Story Context XML](2-6-move-sync-controls-to-top-bar.context.xml) - Generated 2026-04-05

### Agent Model Used

gpt-5

### Debug Log References

- 2026-04-05: `dotnet build GoogleCalendarManagement.csproj -p:Platform=x64 --no-restore` succeeded.
- 2026-04-05: `dotnet test GoogleCalendarManagement.Tests/GoogleCalendarManagement.Tests.csproj --no-restore` passed (`154/154`).
- 2026-04-05: User-provided `dotnet test GoogleCalendarManagement.sln --no-restore` output passed (`154/154`).

### Completion Notes List

- Moved manual sync ownership from `SettingsViewModel` into `MainViewModel` and reused the existing `ISyncManager` range-aware sync pipeline.
- Added a top-bar sync cluster with a persistent Sync button, relative last-sync label, exact tooltip text, and in-progress visual state.
- Implemented a light-dismiss flyout with `Sync from` / `Sync to` date pickers, inline range validation, default date prefill, and visible-range prefill from the empty-state prompt.
- Reduced Settings to Google Calendar connect/reconnect only and rerouted empty-state messaging to the shared top-bar sync flow.
- Added and updated unit coverage for the new shell sync behavior, formatting, validation, and auth-only Settings state.
- Manual UI validation remains pending before the story can move from `in-progress` to `review`.

### File List

- ViewModels/MainViewModel.cs
- ViewModels/SettingsViewModel.cs
- Views/MainPage.xaml
- Views/MainPage.xaml.cs
- Views/SettingsPage.xaml
- GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/SettingsViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/SyncStatusServiceTests.cs

### Change Log

- 2026-04-05: Implemented top-bar manual sync flow, moved sync ownership out of Settings, updated shell/test coverage, and recorded passing automated validation. Manual UI validation is still pending.

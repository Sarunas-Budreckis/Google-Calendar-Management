# Story 2.5: Implement Background Sync and Cache Management

Status: ready-for-dev

## Story

As a **user**,
I want **the app to keep my Google Calendar cache up to date in the background**,
so that **I always have current data without needing to trigger manual sync repeatedly**.

## Acceptance Criteria

1. **AC-2.5.1 - Configurable Background Incremental Sync:** Given the app is running and the device has network connectivity, the background sync timer fires every 30 minutes (configurable via `config` table key `gcal_background_sync_interval_minutes`) and triggers an incremental sync.

2. **AC-2.5.2 - Offline Skips Silently:** Given the device has no network connectivity when the timer fires, the sync is skipped silently and retried at the next timer interval; no error is shown to the user.

3. **AC-2.5.3 - Expired Sync Token Falls Back Cleanly:** Given an incremental sync returns HTTP 410 because the Google sync token expired, `SyncManager` clears the stored token and automatically falls back to a full re-sync for the default date range.

4. **AC-2.5.4 - Refresh Metadata And UI Update After Background Sync:** Given a background sync completes, the UI sync status indicators update and `data_source_refresh` is updated with the new sync token and timestamp.

## Tasks / Subtasks

- [ ] **Task 1: Verify prerequisites and enforce story boundaries** (AC: 2.5.1, 2.5.3, 2.5.4)
  - [ ] Confirm Story 2.2's full-sync pipeline exists on the working branch before starting: `SyncManager`, `SyncResult`, `GcalEventDto`, `DataSourceRefresh.SyncToken`, and the manual sync persistence path
  - [ ] Confirm Story 2.3's version-history behavior exists for update/delete paths; reuse it rather than creating a second delta-write path in this story
  - [ ] Confirm Story 2.4's sync-status refresh surface exists or, if it does not, limit this story to publishing the refresh message/contract rather than inventing indicator rendering here
  - [ ] Do not implement a second sync stack just because prerequisite stories are not merged; finish or merge the prerequisite sync stories first

- [ ] **Task 2: Add a machine-readable incremental-sync failure signal** (AC: 2.5.3)
  - [ ] Extend the sync result contract so `SyncManager` can distinguish `sync_token_expired` from generic failures without parsing friendly error strings
  - [ ] Keep user-facing `ErrorMessage` values for logs/UI, but use a separate enum/code/flag for control flow
  - [ ] Apply the structured failure signal to both manual and background sync paths if they share the same orchestration contract

- [ ] **Task 3: Implement incremental Google Calendar fetch support** (AC: 2.5.1, 2.5.3, 2.5.4)
  - [ ] Replace the current `NotImplementedException` in `GoogleCalendarService.FetchIncrementalEventsAsync(...)`
  - [ ] Replace the placeholder `Services/GcalEventDto.cs` record with the actual DTO shape if Story 2.2 has not already done so
  - [ ] Reuse the stored OAuth token from Story 2.1 and the existing `CreateUserCredential(...)` path in `GoogleCalendarService`
  - [ ] Call `Events.List(calendarId)` using the persisted Google `syncToken`, `ShowDeleted = true`, and pagination until all pages are consumed
  - [ ] Return the updated Google sync token from the final page so it can be persisted back to `data_source_refresh`
  - [ ] Surface HTTP 410 as the dedicated failure reason from Task 2 so `SyncManager` can branch deterministically
  - [ ] Preserve Polly-backed retry behavior and friendly `OperationResult` failures for non-410 network/API errors

- [ ] **Task 4: Extend `SyncManager` for incremental background orchestration** (AC: 2.5.1, 2.5.3, 2.5.4)
  - [ ] Add an explicit sync request or sync mode so the orchestrator can differentiate manual full sync from background incremental sync
  - [ ] Background sync must load the most recent `DataSourceRefresh` row for Google and use its stored `SyncToken`
  - [ ] If no sync token exists yet, fall back once to the existing full-sync default range instead of failing indefinitely
  - [ ] If incremental sync returns the dedicated `sync_token_expired` failure, clear the stored token, log a warning, and immediately rerun the full-sync path for the default range
  - [ ] Reuse the existing upsert, audit-log, and version-history logic; do not fork separate persistence rules for background sync
  - [ ] Serialize sync runs with `SemaphoreSlim` or equivalent so background ticks do not overlap with an active manual or prior background sync
  - [ ] On successful background completion, update `data_source_refresh` with the new sync token/timestamp and publish a sync-completed notification for Story 2.4 consumers

- [ ] **Task 5: Add background timer and network-monitoring services** (AC: 2.5.1, 2.5.2)
  - [ ] Create `Services/INetworkMonitor.cs` and `Services/NetworkMonitor.cs`
  - [ ] Use `NetworkInterface.GetIsNetworkAvailable()` for the current state and `NetworkChange.NetworkAvailabilityChanged` to detect restores while the app is open
  - [ ] Create `Services/BackgroundSyncService.cs` using `PeriodicTimer` on a background loop; it must never block the UI thread
  - [ ] Read the interval from config key `gcal_background_sync_interval_minutes`; default to `30` minutes if the value is missing, non-integer, or invalid
  - [ ] If a timer tick occurs while offline, skip silently, log the skip at `Warning` or `Information`, and mark a pending catch-up run
  - [ ] If network connectivity returns after a skipped run, trigger one catch-up incremental sync instead of waiting a full extra interval
  - [ ] If a sync is already active when the timer fires, skip that tick rather than queueing overlapping work

- [ ] **Task 6: Wire startup, shutdown, and config seeding** (AC: 2.5.1, 2.5.2)
  - [ ] Add seeded config row `gcal_background_sync_interval_minutes = 30` in `Data/Configurations/ConfigConfiguration.cs`
  - [ ] Register `INetworkMonitor` and `BackgroundSyncService` in `App.ConfigureServices(...)`
  - [ ] Start `BackgroundSyncService` from `App.OnLaunched(...)` after migrations complete and the main page is available
  - [ ] Dispose the timer loop and unsubscribe from network events when the app/window closes so background tasks do not leak
  - [ ] Do not introduce `IHostedService`; this app currently uses plain `ServiceCollection` without the generic host

- [ ] **Task 7: Hook the UI refresh contract without adding new indicator UI here** (AC: 2.5.4)
  - [ ] Add `Messages/SyncCompletedMessage.cs` (or equivalent) carrying enough metadata for status-refresh consumers
  - [ ] Publish the message from the successful sync path using `WeakReferenceMessenger`
  - [ ] Have the Story 2.4 status surface listen for the message and refresh its data
  - [ ] If the Story 2.4 UI is not on the branch yet, create the message contract in this story but defer indicator rendering to Story 2.4

- [ ] **Task 8: Add unit and integration coverage for timer, offline, and fallback behavior** (AC: all)
  - [ ] Unit-test `BackgroundSyncService` with mocked `ISyncManager` and `INetworkMonitor`:
    - [ ] timer fire while online triggers exactly one incremental sync
    - [ ] timer fire while offline skips with no exception and no dialog
    - [ ] network restore after an offline skip triggers one catch-up sync
    - [ ] active sync during timer fire does not create an overlapping second sync
  - [ ] Unit-test `GoogleCalendarService.FetchIncrementalEventsAsync(...)`:
    - [ ] multi-page delta responses are consumed fully
    - [ ] deleted events remain represented for local `IsDeleted` handling
    - [ ] HTTP 410 maps to the dedicated failure reason
  - [ ] Unit-test `SyncManager`:
    - [ ] missing sync token falls back to full sync
    - [ ] expired sync token clears the stored token and reruns full sync
    - [ ] successful background sync persists the replacement token
  - [ ] Integration-test `data_source_refresh` updates after a background-triggered sync using SQLite-backed test DB
  - [ ] Integration-test seeded config includes `gcal_background_sync_interval_minutes`

- [ ] **Task 9: Final validation** (AC: all)
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual validation with a real Google account:
    - [ ] authenticate via Story 2.1 and complete at least one full sync
    - [ ] lower `gcal_background_sync_interval_minutes` temporarily for testability
    - [ ] confirm background sync runs without blocking the UI
    - [ ] disable network and confirm timer ticks skip silently with no dialog
    - [ ] restore network and confirm a catch-up sync occurs
    - [ ] simulate or force an expired sync token and confirm automatic full re-sync
    - [ ] confirm `data_source_refresh` receives the new token and timestamp after completion

## Dev Notes

### Architecture Patterns and Constraints

**Use the actual repo structure and current implementation state.**

- The app is a single WinUI 3 project with root-level `Services/`, `Data/`, `ViewModels/`, `Views/`, and `Messages/`. Do not create a new Core project or generic-host infrastructure for background sync.
- `App.ConfigureServices(...)` currently uses `ServiceCollection` plus `services.AddLogging(builder => builder.AddSerilog())`. Start the background loop manually from `App.OnLaunched(...)`; do not introduce `IHostedService`.
- `GoogleCalendarService` already exists and currently has `FetchAllEventsAsync(...)` and `FetchIncrementalEventsAsync(...)` signatures, but both fetch methods still throw `NotImplementedException` in the repo today.
- `GcalEventDto` is currently an empty placeholder record in `Services/GcalEventDto.cs`. Story 2.5 must not assume Story 2.2 already filled it out unless that work is actually on the branch.
- `DataSourceRefresh` currently contains no `SyncToken` property or mapped `sync_token` column in the repo. This story depends on Story 2.2 having already added that field and migration support.

**Incremental sync design guardrails:**

- Follow the Epic 2 technical spec's sync-token design for incremental sync. Do not switch to a separate `updatedMin`-only strategy just because the high-level epic text mentions it.
- Once a Google `syncToken` exists, use that token for incremental delta fetches and do not mix it with unsupported date-range filters for the incremental call.
- HTTP 410 is expected when Google expires a stale token. Treat it as a deterministic recovery path, not as a generic error dialog case.
- Background sync is read-only in Epic 2. It refreshes the local cache only and must not introduce Google-side writes or publish logic.

**Concurrency and lifecycle guardrails:**

- Background sync must be non-blocking and must never use the UI dispatcher for network or database work.
- Timer ticks must not start overlapping sync runs. Use a single-flight guard in `SyncManager` or `BackgroundSyncService`.
- Background sync should be unobtrusive: log results, update persisted metadata, and notify interested view models, but do not show modal dialogs for ordinary offline or retry scenarios.
- Because the app stays open on a single window, the timer and `NetworkChange` subscriptions must be disposed/unhooked when the app exits.

**Scope boundaries to prevent story drift:**

- Story 2.5 owns background triggering, incremental delta orchestration, retry/410 recovery, and refresh notifications.
- Story 2.4 owns the green/grey sync-status indicator UI. This story should publish the refresh signal and integrate with that surface, not redesign the indicator visuals.
- The Epic breakdown mentions cache-size warnings and offline indicators, but the authoritative Epic 2 tech spec and traceability map for Story 2.5 only require timer-driven incremental sync, offline skip, 410 fallback, and UI refresh after completion. Do not add speculative database-size warning features unless planning docs are updated.

### Project Structure Notes

**Files expected to change or be created:**

```text
GoogleCalendarManagement/
├── App.xaml.cs
├── Services/
│   ├── GoogleCalendarService.cs
│   ├── IGoogleCalendarService.cs              # keep contract aligned if structured failure metadata is added
│   ├── GcalEventDto.cs                        # if still placeholder on branch
│   ├── ISyncManager.cs                        # expected from Story 2.2
│   ├── SyncManager.cs                         # expected from Story 2.2, extended here
│   ├── INetworkMonitor.cs                     # new
│   ├── NetworkMonitor.cs                      # new
│   └── BackgroundSyncService.cs               # new
├── Data/
│   ├── Entities/
│   │   └── DataSourceRefresh.cs               # SyncToken must already exist from Story 2.2
│   └── Configurations/
│       └── ConfigConfiguration.cs             # seed gcal_background_sync_interval_minutes
└── Messages/
    └── SyncCompletedMessage.cs                # new

GoogleCalendarManagement.Tests/
├── Unit/
│   ├── BackgroundSyncServiceTests.cs          # new
│   ├── GoogleCalendarIncrementalSyncTests.cs  # new or merged into auth/sync tests
│   └── SyncManagerIncrementalTests.cs         # new
└── Integration/
    └── BackgroundSyncIntegrationTests.cs      # new or merged into sync integration coverage
```

**Current branch variances to respect:**

- `SettingsViewModel` and `SettingsPage` currently only handle connect/reconnect. Do not create a second settings tree for background sync controls.
- There is no existing `SyncCompletedMessage` type in `Messages/`; only `AuthenticationSucceededMessage` exists today.
- `ConfigConfiguration` currently seeds several integer settings, but not the background sync interval key yet.

### Previous Story Intelligence

- Story 2.1 is partially reflected in the repo already: auth DI registration, token storage, `SettingsViewModel`, and `SettingsPage` exist and should be reused.
- Story 2.1 also established the repo's messaging pattern: `WeakReferenceMessenger.Default.Send(...)` from the view model/service layer is already in use.
- The current repo still lacks the prerequisite sync plumbing from Stories 2.2-2.4:
  - `GoogleCalendarService.FetchAllEventsAsync(...)` is not implemented yet
  - `GoogleCalendarService.FetchIncrementalEventsAsync(...)` is not implemented yet
  - `GcalEventDto` is still empty
  - no `SyncManager` or `BackgroundSyncService` exists yet
  - `DataSourceRefresh` does not yet persist a sync token
- Story 1.6's logging and DI conventions still apply here:
  - use constructor-injected `ILogger<T>`
  - keep `services.AddLogging(builder => builder.AddSerilog())`
  - log operational details, but keep user-visible errors friendly and non-technical

### References

- [Epic 2 tech spec](../tech-spec.md) - `Flow 3 - Incremental Sync`
- [Epic 2 tech spec](../tech-spec.md) - authoritative Story 2.5 AC #21-24 and traceability rows
- [Epic 2 tech spec](../tech-spec.md) - Reliability/Availability section for offline skip, Polly retry, and 410 fallback
- [Epic breakdown](../../epics.md) - Story 2.5 business intent and prerequisite note
- [Architecture](../../architecture.md) - cache strategy and incremental-sync token decision
- [Database schemas](../../_database-schemas.md) - `config`, `gcal_event`, and `data_source_refresh` table intent
- [App startup and DI](../../../App.xaml.cs) - actual startup and service-registration pattern in this repo
- [Current Google calendar service](../../../Services/GoogleCalendarService.cs) - existing auth implementation plus fetch stubs to extend
- [Current Google calendar contract](../../../Services/IGoogleCalendarService.cs) - existing full/incremental fetch method signatures
- [Current DTO placeholder](../../../Services/GcalEventDto.cs) - still empty on this branch
- [Current refresh entity](../../../Data/Entities/DataSourceRefresh.cs) - current schema gap to verify against Story 2.2
- [Current config seed](../../../Data/Configurations/ConfigConfiguration.cs) - where `gcal_background_sync_interval_minutes` must be seeded
- [Settings view model](../../../ViewModels/SettingsViewModel.cs) - existing messaging/UI pattern to reuse
- [Story 1.6 logging conventions](../../epic-1/stories/1-6-implement-application-logging-and-error-handling-infrastructure.md) - DI and logging guardrails

## Dev Agent Record

### Context Reference

- [Story Context XML](2-5-implement-background-sync-and-cache-management.context.xml) - Generated 2026-03-30

### Agent Model Used

gpt-5

### Debug Log References

### Completion Notes List

### File List

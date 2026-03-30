# Story 2.2: Fetch Google Calendar Events and Store Locally

Status: done

## Story

As a **user**,
I want **to fetch my Google Calendar events and store them locally**,
so that **I can view my calendar offline and keep a local cache of my existing events**.

## Acceptance Criteria

1. **AC-2.2.1 - Manual Sync Fetches Configured Range:** Given the user is authenticated with Google Calendar, when they click `Sync with Google Calendar`, the app fetches all events for the default range of 6 months back and 1 month forward.

2. **AC-2.2.2 - Pagination Is Exhaustive:** Given the Google Calendar API returns paginated results, all pages are fetched before the sync is considered complete.

3. **AC-2.2.3 - Progress And Cancellation:** Given a sync is in progress, the UI shows progress and a `Cancel` action that stops further API fetches cleanly while preserving rows already written to the local database.

4. **AC-2.2.4 - Event Mapping Is Correct:** Given events are returned from Google Calendar, timed events are stored with UTC-normalized `StartDatetime` and `EndDatetime`, all-day events are stored with `IsAllDay = true`, and recurring instances are stored with `IsRecurringInstance = true` plus `RecurringEventId`.

5. **AC-2.2.5 - Local Persistence And Sync Metadata:** Given fetched events, the app upserts them into `gcal_event`, updates `last_synced_at`, records a successful `data_source_refresh` entry for the synced range, and persists the returned Google `nextSyncToken` for later incremental sync work.

6. **AC-2.2.6 - Empty Calendars Succeed Gracefully:** Given Google Calendar returns zero events, the sync completes successfully with no user-facing error and the refresh metadata still records a successful run.

## Tasks / Subtasks

- [x] **Task 1: Verify prerequisites and reuse Story 2.1 auth contracts** (AC: 2.2.1, 2.2.3)
  - [x] Confirm Story 2.1's `IGoogleCalendarService`, `GoogleCalendarService`, `OperationResult<T>`, token storage, and Settings sync surface exist on the working branch before starting this story
  - [x] If Story 2.1 is not yet merged, complete it first; do not build a second auth path or duplicate OAuth/token logic in this story
  - [x] Reuse the same settings surface introduced in Story 2.1 for the manual sync command; do not invent a separate sync page

- [x] **Task 2: Add Google event fetch models and service contract extensions** (AC: 2.2.1, 2.2.2, 2.2.4)
  - [x] Create `Services/GcalEventDto.cs` matching the Epic 2 tech spec fields actually needed by local persistence:
    - `GcalEventId`, `CalendarId`, `Summary`, `Description`
    - `StartDateTimeUtc`, `EndDateTimeUtc`, `IsAllDay`
    - `ColorId`, `GcalEtag`, `GcalUpdatedAtUtc`
    - `IsDeleted`, `RecurringEventId`, `IsRecurringInstance`
  - [x] Add `FetchAllEventsAsync(string calendarId, DateTime start, DateTime end, IProgress<int>? progress = null, CancellationToken ct = default)` to `IGoogleCalendarService`
  - [x] Add a stub `FetchIncrementalEventsAsync(...)` signature in `IGoogleCalendarService` for Story 2.5, but leave full implementation for that story if not needed yet

- [x] **Task 3: Implement paginated fetch logic in `GoogleCalendarService`** (AC: 2.2.1, 2.2.2, 2.2.4, 2.2.6)
  - [x] Use the authenticated Google Calendar client from Story 2.1; no new auth mechanism
  - [x] Call `Events.List("primary")` with:
    - `TimeMin = rangeStartUtc`
    - `TimeMax = rangeEndUtc`
    - `SingleEvents = true` so recurring events are expanded
    - `ShowDeleted = true` so cancelled items can be represented
    - `MaxResults = 250`
  - [x] Iterate `nextPageToken` until exhausted and accumulate all items
  - [x] Report page-level progress through `IProgress<int>` after each page
  - [x] Map Google API `Event` objects to `GcalEventDto`:
    - Timed events use `EventDateTime.DateTimeDateTimeOffset` converted to UTC
    - All-day events use the `Date` field and store midnight-bounded UTC values consistently
    - Cancelled events map to `IsDeleted = true`
    - Expanded recurring instances set `IsRecurringInstance = true` and `RecurringEventId`
  - [x] Return `OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>`
  - [x] Catch Google API/network failures and return friendly `OperationResult.Failure(...)` messages instead of throwing to callers

- [x] **Task 4: Extend sync bookkeeping schema for later incremental sync** (AC: 2.2.5)
  - [x] Add nullable `SyncToken` to `Data/Entities/DataSourceRefresh.cs`
  - [x] Update `Data/Configurations/DataSourceRefreshConfiguration.cs` to map the new `sync_token` column
  - [x] Create an EF Core migration adding `sync_token TEXT NULL` to `data_source_refresh`
  - [x] Keep the existing table and index names unchanged; do not introduce a new metadata table

- [x] **Task 5: Implement sync orchestration and local upsert flow** (AC: 2.2.3, 2.2.4, 2.2.5, 2.2.6)
  - [x] Create `Services/SyncProgress.cs` with at minimum `PagesFetched`, `EventsProcessed`, and a user-facing status message
  - [x] Create `Services/SyncResult.cs` with `Success`, `EventsAdded`, `EventsUpdated`, `EventsDeleted`, `NewSyncToken`, and `ErrorMessage`
  - [x] Create `Services/ISyncManager.cs` and `Services/SyncManager.cs`
  - [x] `SyncManager.SyncAsync(...)` should:
    - Use default range `DateTime.UtcNow.AddMonths(-6)` to `DateTime.UtcNow.AddMonths(1)` when none is supplied
    - Call `IGoogleCalendarService.FetchAllEventsAsync(...)`
    - Upsert each returned event into `CalendarDbContext.GcalEvents`
    - Preserve the existing `GcalEvent` schema and naming; do not create a second event entity
  - [x] Map `GcalEventDto` to `GcalEvent` using current entity fields:
    - `AppCreated = false`
    - `AppPublished = false`
    - `SourceSystem = null`
    - `LastSyncedAt = DateTime.UtcNow`
    - Set `CreatedAt` on inserts and `UpdatedAt` on every write
  - [x] For existing rows, update the local entity when incoming Google data differs
  - [x] For cancelled Google events, mark `IsDeleted = true` instead of removing rows
  - [x] Save in batches or per page so cancellation can stop the sync without rolling back already-written data
  - [x] Add an `AuditLog` row for manual sync with `OperationType = "gcal_sync"`
  - [x] Add or update a `DataSourceRefresh` row with:
    - `SourceName = "gcal"`
    - synced date range
    - `LastRefreshedAt = DateTime.UtcNow`
    - `RecordsFetched`
    - `Success`
    - `ErrorMessage`
    - `SyncToken`

- [x] **Task 6: Add the manual sync UI command, progress, and cancel behavior** (AC: 2.2.1, 2.2.3, 2.2.6)
  - [x] Extend the Story 2.1 settings view model rather than creating a new view model tree
  - [x] Add `SyncWithGoogleCalendarCommand`
  - [x] Add `CancelSyncCommand` backed by a `CancellationTokenSource`
  - [x] Add observable state for:
    - `IsSyncing`
    - `SyncStatusText`
    - `SyncProgressValue` or equivalent simple progress display
    - `LastSyncText`
  - [x] Update the existing settings page to show:
    - `Sync with Google Calendar` button
    - `Cancel Sync` button during an active sync
    - progress/status text
    - last successful sync timestamp
  - [x] On successful sync, refresh the displayed last-sync timestamp from `DataSourceRefresh`
  - [x] On cancellation, stop additional page fetches and show a non-error status message

- [x] **Task 7: Write tests for fetch, mapping, and persistence** (AC: all)
  - [x] Add unit tests for `GoogleCalendarService` pagination logic with mocked multi-page responses
  - [x] Add unit tests for all-day vs timed event mapping to UTC
  - [x] Add unit tests verifying recurring instances set `IsRecurringInstance` and `RecurringEventId`
  - [x] Add integration tests for `SyncManager` using SQLite test DB:
    - Full sync inserts new `GcalEvent` rows
    - Re-sync updates existing rows rather than duplicating them
    - Cancelled Google events mark `IsDeleted = true`
    - Empty calendar returns success with zero added rows
    - Successful sync writes `data_source_refresh` including `sync_token`
    - Cancellation preserves already-written rows and exits cleanly

- [x] **Task 8: Final validation** (AC: all)
  - [x] Run `dotnet build -p:Platform=x64`
  - [x] Run `dotnet test`
  - [x] Manual test with a real Google account:
    - Authenticate via Story 2.1
    - Trigger sync from the settings surface
    - Confirm rows appear in `gcal_event`
    - Confirm `data_source_refresh` stores `source_name = gcal`, timestamps, row count, and `sync_token`
    - Confirm empty-range or empty-calendar sync completes without error
    - Confirm `Cancel Sync` stops additional fetching without crashing the app

## Dev Notes

### Architecture Patterns and Constraints

**Use the current repo, not the aspirational architecture layout.**

- The real project is a single app project with `Services/`, `Data/`, `ViewModels/`, and `Views/` at the repo root. Do not create new `Core/` or `Data/Repositories/` projects for this story.
- `CalendarDbContext` already exposes `DbSet<GcalEvent>`, `DbSet<GcalEventVersion>`, `DbSet<DataSourceRefresh>`, `DbSet<AuditLog>`, and `DbSet<Config>`. Reuse these directly.
- `GcalEvent` already contains the fields Story 2.2 needs: `GcalEventId`, `CalendarId`, `Summary`, `Description`, `StartDatetime`, `EndDatetime`, `IsAllDay`, `ColorId`, `GcalEtag`, `GcalUpdatedAt`, `IsDeleted`, `RecurringEventId`, `IsRecurringInstance`, `LastSyncedAt`, `CreatedAt`, and `UpdatedAt`.
- `GcalEventVersion` exists, but version-history writes belong to Story 2.3. Story 2.2 should not add snapshot behavior yet.
- `DataSourceRefresh` currently does **not** have a `SyncToken` property or mapped `sync_token` column. Add it here because Story 2.2 is the first story that needs to persist Google's `nextSyncToken` for future incremental sync work.

**Google API usage rules for this story:**

- Use `SingleEvents = true` so Google expands recurring events into instances for local storage.
- Use `ShowDeleted = true` so locally cached rows can be marked deleted when Google returns cancelled events.
- Use pagination; do not assume a single `Events.List()` call returns the full range.
- Normalize stored times to UTC before writing to SQLite.

**UI sequencing guardrail:**

- The story says the user clicks `Sync with Google Calendar`, but Epic 3's full calendar UI does not exist yet.
- The correct implementation is to extend the same settings/auth surface introduced in Story 2.1 with a sync button, progress text, and cancel button.
- Do not build a second sync trigger in `MainWindow` just because the full calendar UI is not ready.

**Observability requirements:**

- Manual sync writes `AuditLog.OperationType = "gcal_sync"`.
- Use `ILogger<T>` through DI, following the existing `services.AddLogging(builder => builder.AddSerilog())` pattern in `App.xaml.cs`.
- Return friendly error messages to the UI. Do not surface raw Google exceptions or stack traces.

### Project Structure Notes

**Files expected to change or be created:**

```text
GoogleCalendarManagement/
├── Services/
│   ├── GcalEventDto.cs
│   ├── SyncProgress.cs
│   ├── SyncResult.cs
│   ├── ISyncManager.cs
│   ├── SyncManager.cs
│   ├── IGoogleCalendarService.cs           # extend existing contract from Story 2.1
│   └── GoogleCalendarService.cs            # add fetch/pagination implementation
├── Data/
│   ├── Entities/
│   │   └── DataSourceRefresh.cs            # add SyncToken
│   ├── Configurations/
│   │   └── DataSourceRefreshConfiguration.cs
│   └── Migrations/
│       └── <timestamp>_AddDataSourceRefreshSyncToken.cs
├── ViewModels/
│   └── SettingsViewModel.cs                # extend Story 2.1 surface
└── Views/
    └── SettingsPage.xaml                   # add Sync/Cancel/progress UI

GoogleCalendarManagement.Tests/
├── Unit/
│   └── GoogleCalendarServiceTests.cs       # new or extend auth tests file if appropriate
└── Integration/
    └── GoogleCalendarSyncTests.cs          # new integration coverage for SyncManager
```

**Do not introduce these changes in Story 2.2:**

- No version-history snapshot writes to `gcal_event_version` yet
- No background sync timer yet
- No sync status indicator rendering in calendar cells yet
- No pending-event or local-edit publishing logic

### Previous Story Intelligence

- Story 2.1 is currently `ready-for-dev`, not done, so this story assumes its auth contracts and settings surface will exist before implementation proceeds.
- Story 1.6 established the logging/DI conventions that Story 2.2 must follow:
  - `services.AddLogging(builder => builder.AddSerilog())`
  - constructor-injected `ILogger<T>`
  - no `services.AddSerilog()` shortcut
  - user-facing errors remain friendly and non-technical

### References

- [Epic 2 tech spec](../tech-spec.md) - authoritative Story 2.2 acceptance criteria, workflows, and sync design
- [Epic 2 tech spec](../tech-spec.md) - `Flow 2 - Full Sync` and `Flow 3 - Incremental Sync`
- [Epic 2 tech spec](../tech-spec.md) - `Risks, Assumptions, Open Questions` item `R5` and `Q2` about missing `sync_token`
- [Epic 2 tech spec](../tech-spec.md) - `Traceability Mapping` rows for AC #7-12
- [Epic breakdown](../../epics.md) - Story 2.2 definition and prerequisites
- [Database schemas](../../_database-schemas.md) - `gcal_event` and `data_source_refresh` table definitions
- [Architecture](../../architecture.md) - Epic 2 mapping, package choices, and service responsibilities
- [App startup and DI](../../../App.xaml.cs) - actual logging and service registration pattern in this repo
- [Calendar DB context](../../../Data/CalendarDbContext.cs) - actual DbSets already available
- [Current `GcalEvent` entity](../../../Data/Entities/GcalEvent.cs) - local event fields to reuse
- [Current `DataSourceRefresh` entity](../../../Data/Entities/DataSourceRefresh.cs) - current schema gap to address
- [Story 1.6 logging conventions](../../epic-1/stories/1-6-implement-application-logging-and-error-handling-infrastructure.md) - existing DI and logging guardrails

## Dev Agent Record

### Context Reference

- [Story Context XML](2-2-fetch-google-calendar-events-and-store-locally.context.xml) - Generated 2026-03-30

### Agent Model Used

gpt-5

### Debug Log References

- `dotnet build -p:Platform=x64`
- `dotnet ef migrations add AddDataSourceRefreshSyncToken --project GoogleCalendarManagement.csproj --startup-project GoogleCalendarManagement.csproj --context CalendarDbContext --output-dir Data/Migrations`
- User-ran `dotnet test -p:Platform=x64` successfully: 38 tests passed, 0 failed
- User manually verified real Google-account sync flow from Settings and confirmed the story acceptance criteria end to end.

### Completion Notes List

- Implemented authenticated Google Calendar event fetching with exhaustive pagination, UTC/all-day/recurring mapping, friendly failures, and an incremental-sync stub for Story 2.5.
- Added `SyncManager` orchestration for local upsert/delete handling, audit logging, `data_source_refresh` sync metadata persistence, and cancellation-aware partial-write behavior.
- Extended the existing Settings surface with manual sync, cancel, live progress/status text, and last successful sync timestamp refresh.
- Added automated coverage for fetch mapping, pagination, `sync_token` schema migration, sync persistence, empty-calendar success, update/delete behavior, and cancellation.
- Manual live Google-account verification completed; the story is ready for review.

### File List

- `.gitignore`
- `App.xaml.cs`
- `Data/Configurations/DataSourceRefreshConfiguration.cs`
- `Data/Entities/DataSourceRefresh.cs`
- `Data/Migrations/20260330181722_AddDataSourceRefreshSyncToken.cs`
- `Data/Migrations/20260330181722_AddDataSourceRefreshSyncToken.Designer.cs`
- `Data/Migrations/CalendarDbContextModelSnapshot.cs`
- `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs`
- `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs`
- `GoogleCalendarManagement.Tests/Unit/GoogleCalendarServiceTests.cs`
- `Services/FetchAllEventsResultList.cs`
- `Services/GcalEventDto.cs`
- `Services/GoogleCalendarApiClient.cs`
- `Services/GoogleCalendarService.cs`
- `Services/ISyncManager.cs`
- `Services/SyncManager.cs`
- `Services/SyncProgress.cs`
- `Services/SyncResult.cs`
- `ViewModels/SettingsViewModel.cs`
- `Views/SettingsPage.xaml`

### Change Log

- 2026-03-30: Implemented Story 2.2 sync pipeline, added `sync_token` migration support, expanded automated test coverage, completed live Google-account validation, and marked the story ready for review.

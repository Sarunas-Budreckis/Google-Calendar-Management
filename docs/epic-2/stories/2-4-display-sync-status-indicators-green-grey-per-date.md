# Story 2.4: Display Sync Status Indicators (Green/Grey per Date)

Status: ready-for-dev

## Story

As a **user**,
I want **to see which dates are synced with Google Calendar**,
so that **I know whether the dates I am reviewing in the calendar are backed by synced Google data**.

## Acceptance Criteria

1. **AC-2.4.1 - Date Indicators Reflect Local Synced Data:** Given the calendar view is displayed, each date cell shows a green indicator if at least one non-deleted `gcal_event` row exists for that date, and a grey indicator otherwise.

2. **AC-2.4.2 - Indicators Refresh After Sync:** Given a sync completes, the sync status indicators update immediately without requiring a manual page refresh.

3. **AC-2.4.3 - Tooltip Shows Last Sync Time:** Given a date indicator is visible, its tooltip shows `Last synced: X hours ago` using the latest successful Google Calendar refresh timestamp from `data_source_refresh.last_refreshed_at`.

4. **AC-2.4.4 - Manual Refresh Recalculates Status:** Given the user clicks `Refresh Status`, the indicator state is recalculated from the current database state.

## Tasks / Subtasks

- [ ] **Task 1: Verify prerequisites and branch shape before starting UI work** (AC: 2.4.1, 2.4.2, 2.4.3, 2.4.4)
  - [ ] Confirm Story 2.2 has landed on the working branch with event sync writing `gcal_event` rows and successful `data_source_refresh` rows for `source_name = "gcal"`
  - [ ] Confirm the Epic 3 Story 3.1 calendar shell exists on the working branch (`MainWindow` or equivalent calendar page plus a calendar view model that owns the visible date range)
  - [ ] If the app still launches directly into `SettingsPage` with no calendar surface, stop and complete Story 3.1 first; do not bolt sync indicators onto `SettingsPage`
  - [ ] Reuse any sync completion message or notification contract already introduced by Story 2.2; do not create a second competing message path if one already exists

- [ ] **Task 2: Add sync status contracts and supporting types** (AC: 2.4.1, 2.4.3, 2.4.4)
  - [ ] Create `Services/SyncStatus.cs` (or `Models/SyncStatus.cs` if the branch has already introduced UI/domain model separation) with `Synced` and `NotSynced`
  - [ ] Create `Services/ISyncStatusService.cs` with:
    - [ ] `Task<Dictionary<DateOnly, SyncStatus>> GetSyncStatusAsync(DateOnly from, DateOnly to, CancellationToken ct = default)`
    - [ ] `Task<DateTime?> GetLastSyncTimeAsync(CancellationToken ct = default)`
  - [ ] Keep the service read-only and side-effect free; this story reads current database state and does not add any new cache table or status persistence layer

- [ ] **Task 3: Implement `SyncStatusService` against the existing SQLite schema** (AC: 2.4.1, 2.4.3, 2.4.4)
  - [ ] Query `CalendarDbContext.GcalEvents` with `AsNoTracking()` and exclude deleted rows (`IsDeleted = false`)
  - [ ] Compute status from actual event presence for each date in the requested range; do not derive green/grey state from refresh staleness rules in older documentation
  - [ ] Timed events that cross midnight must mark every intersected calendar date within the requested range as `Synced`
  - [ ] All-day events must use date coverage semantics that treat `EndDatetime` as exclusive when it lands on midnight, so a single-day all-day event marks only its actual date
  - [ ] Return a dictionary containing every date in the requested range, defaulting missing dates to `NotSynced`
  - [ ] Implement `GetLastSyncTimeAsync` by reading the latest successful `DataSourceRefresh` row where `SourceName == "gcal"` and `Success == true`
  - [ ] Ignore failed refresh rows and rows for other data sources
  - [ ] Do not introduce a new `date_sync_status`, `date_state`, or similar persistence table in this story

- [ ] **Task 4: Wire sync status refresh into the calendar view model layer** (AC: 2.4.2, 2.4.3, 2.4.4)
  - [ ] Extend the calendar view model introduced in Story 3.1 rather than `SettingsViewModel`
  - [ ] Track the visible date range for the active calendar view and reload statuses whenever the visible range or view mode changes
  - [ ] Add `RefreshStatusCommand` that re-queries `ISyncStatusService` for the currently visible range
  - [ ] Subscribe to the sync-completion notification from Story 2.2 / 2.5 and refresh indicator state plus last-sync text after a successful sync
  - [ ] Expose a bindable last-sync tooltip string or helper property from the view model; do not hardcode `"Last synced: X hours ago"` formatting in multiple XAML locations

- [ ] **Task 5: Render a shared indicator visual in the calendar UI** (AC: 2.4.1, 2.4.2, 2.4.3)
  - [ ] Add a small shared indicator visual or template resource reused by year, month, and week/date-header surfaces rather than duplicating indicator markup in each view
  - [ ] Green indicates `SyncStatus.Synced`; grey indicates `SyncStatus.NotSynced`
  - [ ] Attach a tooltip that shows the last successful sync text in the format `Last synced: X hours ago`
  - [ ] Keep colors and brushes centralized in XAML resources instead of scattering raw color literals through multiple views
  - [ ] Ensure the indicator updates in place after sync completion or `Refresh Status`; no app restart or manual page reload is allowed

- [ ] **Task 6: Add tests for date coverage, tooltip source, and refresh behavior** (AC: all)
  - [ ] Add unit tests in `GoogleCalendarManagement.Tests/Unit/SyncStatusServiceTests.cs`
  - [ ] `SyncStatusService_DateWithNonDeletedEvent_ReturnsSynced`
  - [ ] `SyncStatusService_DateWithOnlyDeletedEvents_ReturnsNotSynced`
  - [ ] `SyncStatusService_MultiDayTimedEvent_MarksEachCoveredDate`
  - [ ] `SyncStatusService_AllDayEventWithMidnightExclusiveEnd_MarksCorrectDates`
  - [ ] `SyncStatusService_GetLastSyncTime_IgnoresFailedOrNonGcalRows`
  - [ ] Add a view-model-level integration test in `GoogleCalendarManagement.Tests/Integration/CalendarSyncStatusRefreshTests.cs` verifying a sync-completion notification refreshes the visible-range status map without reloading the page
  - [ ] Keep UI rendering automation optional unless a calendar UI test harness already exists on the branch; prioritize service and view-model correctness first

- [ ] **Task 7: Final validation** (AC: all)
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual validation on a branch that includes Story 3.1 calendar views:
    - [ ] Open the calendar and confirm unsynced dates show grey indicators
    - [ ] Complete a Google Calendar sync and confirm synced dates turn green without reopening the page
    - [ ] Hover the indicator and confirm tooltip text reads `Last synced: X hours ago`
    - [ ] Click `Refresh Status` and confirm the indicator state recalculates from current database contents

## Dev Notes

### Architecture Patterns and Constraints

**Use the current repository shape, not the older multi-project architecture layout.**

- The live repo currently uses a single app project with root-level `Services/`, `Data/`, `ViewModels/`, and `Views/`. Keep new sync-status files in that structure unless the branch already contains the Epic 3 calendar extraction work.
- `App.xaml.cs` still boots directly into `SettingsPage` today. That is a sequencing constraint, not a reason to place sync indicators on the settings surface. The actual indicator UI belongs in the calendar views introduced by Story 3.1.
- `CalendarDbContext` already exposes `DbSet<GcalEvent>` and `DbSet<DataSourceRefresh>`. Reuse those directly; do not add repositories or a second status table just for this story.
- `DataSourceRefresh` currently stores `SourceName`, date range, `LastRefreshedAt`, `RecordsFetched`, `Success`, and `ErrorMessage`. Story 2.4 reads this metadata for tooltip text but computes green/grey state from non-deleted `gcal_event` presence.

**Authoritative-document guardrail:**

- Follow the Epic 2 tech spec acceptance criteria and Flow 4 as the source of truth for Story 2.4.
- Older docs conflict on the status source:
  - `docs/epics.md` mentions `AppMetadata` key `LastGoogleCalendarSync`
  - `docs/_database-schemas.md` describes green/grey status from `data_source_refresh` overlap/staleness
- For this story, use the Epic 2 tech spec instead:
  - green/grey state comes from whether non-deleted `gcal_event` rows exist for a date
  - last-sync tooltip comes from the latest successful `data_source_refresh.last_refreshed_at` row for Google Calendar
- Do not add a new `AppMetadata` sync timestamp key just to satisfy the older planning text.

**Date-coverage rules the implementation must get right:**

- A timed event that spans multiple dates marks every intersected date as `Synced`.
- An all-day event whose `EndDatetime` is stored at midnight for the following day should be treated as an exclusive end boundary so a one-day all-day event does not incorrectly mark two dates.
- Deleted events do not make a date green.
- An empty-but-successfully-synced range remains grey if there are no non-deleted `gcal_event` rows for those dates. This is expected under the authoritative ACs.

**Messaging and refresh behavior:**

- `CommunityToolkit.Mvvm` and `WeakReferenceMessenger` are already in use in `SettingsViewModel`.
- Story 2.4 should reuse the sync-completion notification that Story 2.2 / 2.5 introduces for manual or background sync completion.
- If Story 2.2 has not yet introduced a sync-completion message, create one message type and treat it as the shared contract for both 2.4 and 2.5. Do not create separate manual-sync and background-sync message types unless there is a demonstrated need.

**Performance and testability guardrails:**

- Visible-range status queries should meet the Epic 2 target of under 100 ms for roughly a month of dates.
- Use `AsNoTracking()` for status reads.
- Load once per visible range and bind from in-memory state; do not hit SQLite separately for each date cell.
- Keep tooltip-string formatting in one place in the view model or a dedicated formatter helper so tests can verify it easily.

### Project Structure Notes

**Expected files to create or update:**

```text
GoogleCalendarManagement/
├── Services/
│   ├── SyncStatus.cs
│   ├── ISyncStatusService.cs
│   └── SyncStatusService.cs
├── ViewModels/
│   └── MainViewModel.cs or equivalent Story 3.1 calendar view model
├── Views/
│   └── MainWindow.xaml or equivalent calendar view introduced by Story 3.1
├── Messages/
│   └── <sync-completed message>.cs   # only if Story 2.2 has not already added one
└── App.xaml.cs                       # DI registration for ISyncStatusService

GoogleCalendarManagement.Tests/
├── Unit/
│   └── SyncStatusServiceTests.cs
└── Integration/
    └── CalendarSyncStatusRefreshTests.cs
```

**Current branch reality to account for:**

- The repo already contains `SettingsPage.xaml`, `SettingsViewModel.cs`, `IGoogleCalendarService.cs`, `GoogleCalendarService.cs`, `GcalEventDto.cs`, and authentication infrastructure from Story 2.1.
- The repo does **not** currently contain `MainViewModel`, `MainWindow.xaml`, month/week/year calendar views, or a sync-status control. That missing UI surface is a real dependency on Story 3.1.
- Story 2.4 therefore must not be implemented as a settings-page feature or as a disconnected placeholder UI. Either land it on top of Story 3.1, or explicitly sequence 3.1 first on the branch.

### Previous Story Intelligence

- Story 2.2 already reserves `DataSourceRefresh` as the sync bookkeeping source and requires successful sync metadata to be written there. Story 2.4 must read that existing metadata rather than inventing a second sync-tracking location.
- Story 2.2 also keeps manual sync on the settings/auth surface because the calendar UI does not exist yet. Story 2.4 should not treat that temporary sync entry point as the permanent home for sync-status rendering.
- Story 1.6 established the logging and DI conventions already in the repo:
  - `services.AddLogging(builder => builder.AddSerilog())`
  - constructor-injected `ILogger<T>`
  - friendly user-facing errors
  - xUnit + FluentAssertions + Moq in the test project

### References

- [Epic 2 tech spec](../tech-spec.md) - authoritative Story 2.4 acceptance criteria, Flow 4, and traceability mapping
- [Epic breakdown](../../epics.md) - original Story 2.4 business intent and dependency note on Epic 3 Story 3.1
- [UX design specification](../../ux-design-specification.md) - Tier 1 year-grid sync indicators and refresh expectations
- [Database schemas](../../_database-schemas.md) - background context for `data_source_refresh` and the older, now-non-authoritative status description
- [Architecture](../../architecture.md) - calendar UI target surfaces and project structure intent
- [App startup](../../../App.xaml.cs) - current app boot path still routes to `SettingsPage`, which is the sequencing blocker for UI integration
- [Calendar DB context](../../../Data/CalendarDbContext.cs) - actual available DbSets for `GcalEvent` and `DataSourceRefresh`
- [Current `GcalEvent` entity](../../../Data/Entities/GcalEvent.cs) - event fields used to determine synced dates and deleted status
- [Current `DataSourceRefresh` entity](../../../Data/Entities/DataSourceRefresh.cs) - last-refresh metadata source for tooltip text
- [Current `DataSourceRefresh` configuration](../../../Data/Configurations/DataSourceRefreshConfiguration.cs) - table and index names already in place
- [Current settings view model](../../../ViewModels/SettingsViewModel.cs) - existing `WeakReferenceMessenger` usage pattern that future sync completion messaging should follow
- [Current settings page](../../../Views/SettingsPage.xaml) - evidence that indicator UI does not belong on the current app shell

## Dev Agent Record

### Context Reference

- [Story Context XML](2-4-display-sync-status-indicators-green-grey-per-date.context.xml) - Generated 2026-03-30

### Agent Model Used

gpt-5

### Debug Log References

### Completion Notes List

### File List

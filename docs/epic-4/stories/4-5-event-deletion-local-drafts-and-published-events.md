# Story 4.5: Event Deletion (Local Drafts and Published Events)

Status: ready-for-dev

## Story

As a **user**,
I want **to delete local drafts immediately and stage deletion of published Google Calendar events**,
so that **I can remove unwanted events from the app without losing control over when Google is changed**.

## Acceptance Criteria

1. **AC-4.5.1 - Delete entry points exist in the details panel:** Given an event is selected and the details panel is visible, both read-only mode and edit mode expose a `Delete` action for the selected event. The action uses the existing right-side panel workflow; do not add a second delete surface elsewhere in the shell for single-event deletion.

2. **AC-4.5.2 - Local draft deletion is immediate after confirmation:** Given the selected event is a local-only draft from Story 4.2 (`pending_event.GcalEventId == null`), when the user confirms deletion, the app deletes that `pending_event` row by its pending ID, clears the panel selection, and removes the event from Month, Week, Day, and Year views without waiting for any Google API call.

3. **AC-4.5.3 - Published event deletion is staged locally, not pushed yet:** Given the selected event exists in `gcal_event` and has no pending row yet, when the user confirms deletion, the app creates or upserts a `pending_event` row for that event with `operation_type = 'delete'`, preserves the event's identifying/display fields needed for the pending list and UI refresh, updates `updated_at`, leaves the original `gcal_event` row untouched, and does not call the Google Calendar delete API in this story.

4. **AC-4.5.4 - Pending edit can be reverted or converted to pending delete:** Given the selected event already has a non-delete `pending_event` row, when the user triggers `Delete`, the confirmation flow offers three outcomes: `Revert Changes`, `Delete Event`, and `Cancel`. `Revert Changes` behaves exactly like the existing Revert path from Story 4.1. `Delete Event` reuses that same pending row and changes it to `operation_type = 'delete'` instead of creating a second row.

5. **AC-4.5.5 - Pending delete stays visible with explicit delete state:** Given an event is staged for deletion (`pending_event.operation_type = 'delete'`), the event remains visible in the calendar at 60% opacity until Story 4.4 pushes it, and the details/pending UI exposes an explicit pending-delete indicator or status label so the user can distinguish "pending edit" from "pending delete".

6. **AC-4.5.6 - Cancel and dismiss are non-destructive:** Given any delete confirmation dialog is dismissed or cancelled, no row is deleted, no delete staging write occurs, and the currently selected event remains in its pre-dialog state.

7. **AC-4.5.7 - Revert clears a staged delete:** Given a published event is currently staged for deletion, when the user clicks the existing `Revert` action, the `pending_event` row is removed, the event returns to the original `gcal_event` display at 100% opacity, and the details panel reflects the original Google-backed data again.

8. **AC-4.5.8 - Schema changes match the Epic 4 tech spec:** The Story 4.5 migration adds `pending_event.operation_type TEXT NOT NULL DEFAULT 'edit'`, creates the `deleted_event` table, and creates the `recurring_event_series` table. Story 4.5 must not add new columns to `gcal_event`, must not rely on `gcal_event.is_deleted` for local delete staging, and must not replace the `deleted_event` table with a boolean soft-delete flag.

9. **AC-4.5.9 - Story 4.5 does not perform outbound deletion or version writes:** No `Events.Delete` Google API call is made in this story, no `gcal_event` row is moved to `deleted_event` yet, and no `gcal_event_version` snapshot is written just because an event was staged for deletion. Those operations belong to Story 4.4's push flow.

---

## Tasks / Subtasks

- [ ] **Task 1: Verify prerequisites and source-of-truth alignment before writing code** (AC: 4.5.1, 4.5.2, 4.5.3, 4.5.8, 4.5.9)
  - [ ] Confirm Story 4.1's edit panel, pending-event persistence, Revert flow, and `EventUpdatedMessage` refresh path are present on the branch.
  - [ ] Confirm Story 4.2's local-draft prerequisites are actually present before implementing AC-4.5.2: nullable `PendingEvent.GcalEventId`, source-agnostic event selection/querying, and the ability for `CalendarQueryService` to surface pending-only events. If they are not present, land Story 4.2 first or cherry-pick its prerequisite schema/query changes before implementing 4.5.
  - [ ] Treat [Epic 4 tech spec](../tech-spec.md) as authoritative for Story 4.5. Do not follow the outdated Epic 4 note in `docs/epics.md` that still says event deletion is outside Epic 4.
  - [ ] Do not implement deletion by toggling `GcalEvent.IsDeleted`, directly removing `gcal_event` rows, or inventing fake Google IDs for local drafts.

- [ ] **Task 2: Extend the Tier 2 schema for delete staging and future push compatibility** (AC: 4.5.3, 4.5.8, 4.5.9)
  - [ ] Extend `Data/Entities/PendingEvent.cs` with `OperationType` defaulting to `"edit"`.
  - [ ] Update `Data/Configurations/PendingEventConfiguration.cs` to map `operation_type` and preserve the unique-per-source-event behavior from prior stories.
  - [ ] Add `Data/Entities/DeletedEvent.cs` and `Data/Configurations/DeletedEventConfiguration.cs` for the `deleted_event` table defined in the tech spec.
  - [ ] Add `Data/Entities/RecurringEventSeries.cs` and `Data/Configurations/RecurringEventSeriesConfiguration.cs` for the schema-only table required by the Epic 4 migration sequence. Do not implement recurring-edit behavior in Story 4.5.
  - [ ] Register new `DbSet`s in `Data/CalendarDbContext.cs`.
  - [ ] Add one migration that captures the Story 4.5 schema delta, e.g. `AddPendingEventOperationTypeAndDeletionTables`.
  - [ ] Preserve the existing `gcal_event` schema exactly; Story 4.5 must not add or repurpose an `is_deleted` workflow on that table.

- [ ] **Task 3: Extend repository contracts for local-draft deletion and delete staging reuse** (AC: 4.5.2, 4.5.3, 4.5.4, 4.5.7)
  - [ ] Extend `Services/IPendingEventRepository.cs` with `GetByIdAsync(Guid id, CancellationToken ct = default)` and `DeleteByIdAsync(Guid id, CancellationToken ct = default)`.
  - [ ] Update `Services/PendingEventRepository.cs` to support both delete paths:
    - [ ] delete by Google event ID for published-event revert/staged-delete cleanup
    - [ ] delete by pending ID for local draft removal
  - [ ] Keep repository access on `IDbContextFactory<CalendarDbContext>` and keep all timestamps in UTC.
  - [ ] Do not add an unnecessary Google-delete service abstraction in Story 4.5; outbound deletion remains deferred to Story 4.4.

- [ ] **Task 4: Add reusable confirmation-dialog support instead of ad-hoc XAML dialogs** (AC: 4.5.1, 4.5.2, 4.5.4, 4.5.6)
  - [ ] Extend `Services/IContentDialogService.cs` beyond `ShowErrorAsync(...)` with a reusable confirmation API that can express:
    - [ ] Delete / Cancel for local drafts and published events with no pending edit
    - [ ] Revert Changes / Delete Event / Cancel for published events that already have a pending edit row
  - [ ] Implement the new API in `Services/ContentDialogService.cs` using `ContentDialog` and the existing `IWindowService`/`XamlRoot` pattern.
  - [ ] Keep dialog copy explicit and safe. The destructive button text must clearly distinguish immediate local draft removal from staged deletion of a published event.

- [ ] **Task 5: Extend query/display models so pending delete is a first-class UI state** (AC: 4.5.3, 4.5.5, 4.5.7)
  - [ ] If Story 4.2's widened display model is present, reuse it; otherwise extend the current `Models/CalendarEventDisplayModel.cs` minimally with a delete-state signal such as `IsPendingDelete` or `PendingOperationType`.
  - [ ] Update `Services/CalendarQueryService.cs` so pending-delete rows still project into the visible calendar surfaces using the event's current title/time/color plus the delete-state signal.
  - [ ] Keep staged deletes visible at 60% opacity until Story 4.4 completes the actual Google delete.
  - [ ] Local draft deletions must disappear from the query results immediately after `DeleteByIdAsync(...)`.
  - [ ] Reverting a staged delete must restore the event to its original non-pending display model on the next refresh cycle.

- [ ] **Task 6: Implement the details-panel delete flows on top of the existing Story 4.1 VM and XAML** (AC: 4.5.1 through 4.5.7)
  - [ ] Extend `ViewModels/EventDetailsPanelViewModel.cs`; do not build a second delete-specific panel or second view model.
  - [ ] Add a `Delete` command and any supporting state needed to distinguish:
    - [ ] local draft (`PendingEvent` only)
    - [ ] published event with no pending row
    - [ ] published event with pending edit row
    - [ ] published event already staged for delete
  - [ ] For local drafts, confirm and then call `DeleteByIdAsync(...)`, clear selection, and publish an update so the event disappears from all active views immediately.
  - [ ] For published events with no pending row, create a pending row from the selected event's current effective data and set `OperationType = "delete"`.
  - [ ] For published events with a pending edit row, use the three-outcome confirmation flow and either:
    - [ ] route to the existing revert path unchanged, or
    - [ ] mutate the same pending row to `OperationType = "delete"`
  - [ ] For already-staged deletes, either keep `Delete` disabled with clear copy or confirm the same staged-delete intent without duplicating writes. Do not create duplicate pending rows.
  - [ ] Reuse `EventUpdatedMessage` and `MainViewModel` refresh behavior rather than adding a second refresh mechanism.

- [ ] **Task 7: Update the details-panel UI and any pending-state visual affordances** (AC: 4.5.1, 4.5.5, 4.5.7)
  - [ ] Update `Views/EventDetailsPanelControl.xaml` so `Delete` is available in both read-only and edit mode.
  - [ ] Keep the destructive action visually distinct from `Edit`, `Save`, and `Revert`.
  - [ ] Surface pending-delete state in the panel metadata or inline status area with explicit copy such as `Pending delete - will be removed from Google Calendar when pushed`.
  - [ ] If the current view templates already have a status-label or badge slot, reuse it for the delete indicator. If not, add the smallest shared affordance possible rather than forking per-view markup.

- [ ] **Task 8: Add automated coverage for schema, repository, VM branching, and query behavior** (AC: all)
  - [ ] Extend `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs` with:
    - [ ] `DeleteByIdAsync_RemovesLocalDraftPendingEvent`
    - [ ] `UpsertAsync_WhenOperationTypeDelete_PersistsDeleteState`
    - [ ] migration/schema assertions for `operation_type` default
  - [ ] Add integration coverage for the new `deleted_event` and `recurring_event_series` tables so the Story 4.5 migration shape is locked in.
  - [ ] Extend `GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs` with:
    - [ ] local draft delete confirmed -> pending row removed and selection cleared
    - [ ] published event delete confirmed -> pending delete staged, no Google call made
    - [ ] pending edit delete dialog choosing `Revert Changes` routes to existing revert behavior
    - [ ] cancel/dismiss produces no repository write
  - [ ] Add or extend `CalendarQueryService` tests so a pending delete remains visible with the delete-state flag while a deleted local draft disappears.

- [ ] **Task 9: Final validation** (AC: all)
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`
  - [ ] Manual validation:
    - [ ] create a local draft, delete it, confirm it disappears immediately from panel and calendar
    - [ ] select a published event with no pending row, delete it, confirm it stays visible as pending delete at 60% opacity
    - [ ] edit a published event, then delete it, confirm the dialog offers `Revert Changes`, `Delete Event`, and `Cancel`
    - [ ] click `Revert` on a staged delete and confirm the original event returns at 100% opacity
    - [ ] cancel each delete dialog path and confirm no state changes occur

## Dev Notes

### Architecture Patterns and Constraints

**Use the actual repo shape, not the older multi-project architecture layout.**

- The live project is still a flat WinUI 3 app: `Data/`, `Services/`, `Models/`, `Messages/`, `ViewModels/`, and `Views/` under the project root. New files for Story 4.5 must follow that structure.
- `EventDetailsPanelViewModel` and `EventDetailsPanelControl.xaml` already own single-event edit/revert interactions. Story 4.5 extends those same types; it must not create a second delete panel, modal workflow, or competing refresh path.
- `MainViewModel` already listens for `EventUpdatedMessage` and updates only the affected calendar item. Reuse that pattern for delete/revert refreshes.
- All persistence logic continues to use `IDbContextFactory<CalendarDbContext>` because the app registers repositories/services as singletons.
- All database timestamps are UTC. Any new delete-staging timestamps or soft-delete timestamps must follow the same UTC convention.

**Authoritative-source guardrails for Story 4.5:**

- Follow [Epic 4 tech spec](../tech-spec.md) as the source of truth for deletion staging, `deleted_event`, and `recurring_event_series`.
- The current branch and some older planning docs still contain now-stale assumptions:
  - `docs/epics.md` still claims event deletion is not in Epic 4.
  - `Data/Entities/GcalEvent.cs` still contains legacy `IsDeleted` and `AppLastModifiedAt` fields.
  - `Data/Entities/PendingEvent.cs` still requires `GcalEventId` and has no `OperationType`.
- For Story 4.5, do **not** implement user-driven delete staging by toggling `GcalEvent.IsDeleted`. The Epic 4 tech spec is explicit that user deletes stage through `pending_event.operation_type = 'delete'`, and successful pushed deletes move rows into `deleted_event`.

**Critical dependency on Story 4.2:**

- AC-4.5.2 depends on local-only drafts from Story 4.2. The current codebase does **not** support them yet:
  - `PendingEvent.GcalEventId` is still non-nullable in [PendingEvent.cs](../../../Data/Entities/PendingEvent.cs)
  - `PendingEventConfiguration` still enforces a unique required `gcal_event_id`
  - `CalendarQueryService` and `CalendarSelectionService` are still GCal-ID-centric
- Do not hack around this by storing fake Google IDs or by inserting local drafts into `gcal_event`.
- If Story 4.2 is not on the branch, implement or merge its schema/query-selection prerequisites first, then implement Story 4.5.

**Delete-flow boundaries:**

- Story 4.5 owns:
  - local draft confirmation + removal
  - published-event delete staging in `pending_event`
  - UI affordances for "pending delete"
  - the schema additions required by the Epic 4 migration sequence
- Story 4.5 does **not** own:
  - outbound `Events.Delete` Google API calls
  - moving rows from `gcal_event` to `deleted_event`
  - conflict handling after a pushed delete
  - recurring-series edit behavior beyond the schema-only table
- Those behaviors remain part of Story 4.4 or Story 4.9, even though Story 4.5 prepares the data model for them.

### Project Structure Notes

**Files likely to change or be added:**

```text
GoogleCalendarManagement/
|-- App.xaml.cs
|-- Data/
|   |-- CalendarDbContext.cs
|   |-- Configurations/
|   |   |-- PendingEventConfiguration.cs
|   |   |-- DeletedEventConfiguration.cs                # new
|   |   `-- RecurringEventSeriesConfiguration.cs       # new
|   |-- Entities/
|   |   |-- PendingEvent.cs
|   |   |-- DeletedEvent.cs                            # new
|   |   `-- RecurringEventSeries.cs                    # new
|   `-- Migrations/
|       `-- <Story 4.5 migration>.cs
|-- Messages/
|   `-- EventUpdatedMessage.cs
|-- Models/
|   `-- CalendarEventDisplayModel.cs
|-- Services/
|   |-- IContentDialogService.cs
|   |-- ContentDialogService.cs
|   |-- IPendingEventRepository.cs
|   |-- PendingEventRepository.cs
|   |-- ICalendarQueryService.cs / CalendarQueryService.cs
|   `-- ICalendarSelectionService.cs / CalendarSelectionService.cs   # only if 4.2 source-kind work is landing here
|-- ViewModels/
|   |-- EventDetailsPanelViewModel.cs
|   `-- MainViewModel.cs
`-- Views/
    `-- EventDetailsPanelControl.xaml

GoogleCalendarManagement.Tests/
|-- Integration/
|   `-- PendingEventRepositoryTests.cs
`-- Unit/
    `-- ViewModels/EventDetailsPanelViewModelTests.cs
```

**Current branch realities the story must accommodate:**

- `IContentDialogService` currently only exposes `ShowErrorAsync(...)`; Story 4.5 needs to extend that shared service for confirmation flows instead of constructing raw dialogs inside view models or code-behind.
- `PendingEventRepository` currently supports only `GetByGcalEventIdAsync`, `UpsertAsync`, and `DeleteByGcalEventIdAsync`; Story 4.5 must add delete-by-pending-ID support for local drafts.
- `CalendarEventDisplayModel` currently only knows `IsPending`, `Opacity`, and `PendingUpdatedAt`. Pending delete needs an explicit state signal so the UI can distinguish delete staging from ordinary local edits.

### Previous Story Intelligence

- Story 4.1 already established the correct local-edit pattern:
  - persist edits to `pending_event`, never straight to `gcal_event`
  - publish `EventUpdatedMessage`
  - let `MainViewModel` refresh only the affected event
  - use `Revert` to remove the pending row and restore the original event
- Story 4.3 corrected the color-save path to the same `pending_event` upsert workflow and explicitly removed version-history writes from that local-only step. Story 4.5 should follow the same principle: staging a delete is a local pending operation, not a Google mutation and not a version-history event.
- Existing integration tests in [PendingEventRepositoryTests.cs](../../../GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs) already use in-memory SQLite plus `EnsureCreated()`. Extend that style instead of inventing a second repository-test harness.

### Git Intelligence Summary

- Recent commits show the branch is still converging on the Story 4.1 edit-panel implementation rather than a finished Epic 4 platform:
  - `96de70f` - `Tier 1 code reviews complete, 4.1 edit panel first draft`
  - `905023f` - `code review up to 3.10`
- That means Story 4.5 should preserve the current extension style:
  - small, direct changes to the existing panel/view-model/query stack
  - no broad architectural refactor while adding deletion
  - keep automated coverage close to the touched services/view models

### Latest Technical Information

- The current Google Calendar v3 delete endpoint is `DELETE /calendars/{calendarId}/events/{eventId}` with no request body; optional guest-notification behavior is controlled by `sendUpdates`. Story 4.5 should **not** call it yet, but Story 4.4's push-delete path should use this endpoint rather than inventing a custom delete workflow.
- EF Core's Fluent API supports filtered/partial indexes via `.HasFilter(...)`. Once Story 4.2 makes `pending_event.gcal_event_id` nullable, keep the unique-per-published-event rule with a partial unique index instead of sentinel/fake IDs.

### References

- [Epic 4 tech spec](../tech-spec.md) - authoritative Story 4.5 goal, core flow, migration sequence, and `deleted_event`/`recurring_event_series` schema
- [Epic 4 prior stories](./4-1-implement-event-editing-panel.md) - existing edit/revert/pending-opacity behavior to extend
- [Epic 4 prior stories](./4-3-implement-color-picker-for-event-colors.md) - latest pending-event save semantics and "no version write on local change" pattern
- [Current pending entity](../../../Data/Entities/PendingEvent.cs) - current branch gap: no `OperationType`, `GcalEventId` still required
- [Current pending configuration](../../../Data/Configurations/PendingEventConfiguration.cs) - current unique required `gcal_event_id` mapping
- [Current pending repository contract](../../../Services/IPendingEventRepository.cs) - add delete-by-ID and lookup-by-ID support here
- [Current pending repository implementation](../../../Services/PendingEventRepository.cs) - current upsert/delete behavior to extend
- [Current details panel VM](../../../ViewModels/EventDetailsPanelViewModel.cs) - existing Revert flow, pending-event save path, and event-refresh messaging
- [Current details panel XAML](../../../Views/EventDetailsPanelControl.xaml) - current read-only/edit button hosts
- [Current display model](../../../Models/CalendarEventDisplayModel.cs) - current pending-state surface that needs a delete-state signal
- [Current query service](../../../Services/CalendarQueryService.cs) - existing gcal + pending overlay logic
- [Current dialog service contract](../../../Services/IContentDialogService.cs) - currently error-only, needs confirmation support
- [Current dialog service implementation](../../../Services/ContentDialogService.cs) - current `ContentDialog` + `XamlRoot` pattern to reuse
- [Current GCal entity](../../../Data/Entities/GcalEvent.cs) - legacy `IsDeleted` field exists, but is not the Story 4.5 delete-staging mechanism
- [Current app DI registration](../../../App.xaml.cs) - repository/dialog/query/view-model registrations
- [Database schemas](../../_database-schemas.md) - background context for Tier 2 pending-event semantics and MergeTimestamp conflict strategy
- Google Calendar API delete reference: https://developers.google.com/workspace/calendar/api/v3/reference/events/delete
- Google .NET client delete request reference: https://googleapis.dev/dotnet/Google.Apis.Calendar.v3/latest/api/Google.Apis.Calendar.v3.EventsResource.DeleteRequest.html
- EF Core filtered/partial index reference: https://learn.microsoft.com/en-us/ef/core/modeling/indexes

## Dev Agent Record

### Context Reference

- Story created from [Epic 4 tech spec](../tech-spec.md) on 2026-04-19

### Agent Model Used

gpt-5

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

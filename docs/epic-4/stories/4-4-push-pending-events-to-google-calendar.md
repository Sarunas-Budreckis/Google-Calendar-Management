# Story 4.4: Push Pending Events to Google Calendar

Status: ready-for-dev

## Story

As a **user**,
I want **to publish selected pending events to Google Calendar in one controlled batch**,
so that **my local drafts and edits become fully published events without losing conflict visibility or failure details**.

## Acceptance Criteria

1. **AC-4.4.1 - Top-bar push entry point shows pending count:** Given one or more `pending_event` rows exist, the main toolbar shows a `Push to GCal` control with a visible count badge. Given no pending rows exist, the control is disabled and the badge is hidden.

2. **AC-4.4.2 - Pending list supports subset selection:** Given the user opens the push dropdown, a list of pending items is shown with title, local date/time summary, source kind (`new draft` or `edited event`), and current failure state if any. The user can select any subset to publish and can use `Select All` to stage the full list.

3. **AC-4.4.3 - Confirmation is required before publish:** Given one or more pending items are selected, clicking the publish action opens a confirmation dialog summarizing the selected count and the exact events that will be sent to Google Calendar. No API call is made until the user confirms.

4. **AC-4.4.4 - New draft publish path inserts into Google and promotes locally:** Given a selected pending row has `GcalEventId = null`, when publish succeeds, the app calls Google Calendar `Events.Insert`, creates or upserts the corresponding `gcal_event` row with the returned Google event ID, ETag, Google-updated timestamp, `app_created = true`, `app_published = true`, `app_published_at = now`, and then deletes the `pending_event` row.

5. **AC-4.4.5 - Existing edit publish path updates Google conditionally:** Given a selected pending row has a non-null `GcalEventId`, when publish succeeds, the app writes a `gcal_event_version` snapshot of the current live row before overwrite, then sends a full Google Calendar `Events.Update` request guarded by the stored ETag, updates the local `gcal_event` row from the API response, sets `app_published = true`, refreshes `app_published_at`, and deletes the `pending_event` row.

6. **AC-4.4.6 - Publish payloads are mapped correctly for Google Calendar:** Given a pending row is being published, timed events are sent using `start.dateTime` / `end.dateTime` in RFC3339 form, all-day events are sent using `start.date` / `end.date`, and local canonical colour keys are converted back to Google Calendar `colorId` values before the request is sent. Raw hex values and canonical keys such as `azure` or `sage` are never sent directly to Google.

7. **AC-4.4.7 - Conflicts use MergeTimestamp rules without silent data loss:** Given Google returns `412 Precondition Failed` for an update, the app compares local `pending_event.updated_at` against local cache `gcal_event.gcal_updated_at`. If the local pending edit is newer, the app re-fetches the latest Google resource, reapplies the pending values, retries once with the fresh ETag, and records success if the retry succeeds. If the Google version is newer, the row remains pending, `publish_error` is populated with a conflict message, and the user sees that the event was not published.

8. **AC-4.4.8 - Recurring-instance scope is explicit and limited:** Given a selected pending item represents a recurring instance, the publish flow shows an informational popup that this publish applies only to the selected instance. The implementation updates only that instance ID; it does not attempt `this and following` or whole-series modification in Story 4.4.

9. **AC-4.4.9 - Failures are isolated and retryable:** Given a batch contains multiple selected items, a failure for one item does not cancel already-completed items and does not remove the failed row from `pending_event`. For every failed item, `publish_attempted_at` and `publish_error` are updated, the row remains visible in the pending list, and there is no automatic retry loop.

10. **AC-4.4.10 - Successful publish updates visible UI state immediately:** Given an item publishes successfully and is visible in the current calendar view, it transitions from pending to published without app restart: the event leaves the pending list, its calendar block changes from 60% opacity to 100% opacity using a `300 ms` fade, and the details panel reflects the published state if that event remains selected.

11. **AC-4.4.11 - Batch progress and summary are visible:** Given the user confirms a publish batch, the UI shows progress as `n / total` while work is in flight, disables duplicate publish actions until the batch completes, and shows a completion summary with success and failure counts when done.

12. **AC-4.4.12 - Automated coverage exists for publish-critical paths:** Unit and integration tests cover new-draft insert, existing-event update, version-history snapshot creation on publish, Google ETag conflict handling, per-item failure retention, and canonical-colour-to-Google-colour mapping.

## Scope Boundaries

**IN SCOPE**
- Publish pending create and edit rows to Google Calendar from a top-bar batch action
- Pending-event selection UI, confirmation dialog, batch progress UI, and completion feedback
- Google Calendar insert/update write APIs and local persistence updates after success
- Version-history snapshot creation immediately before a successful local overwrite of `gcal_event`
- Conflict handling using ETags plus the MergeTimestamp rule from the Epic 4 tech spec
- Per-item publish error retention in `pending_event`
- Recurring-instance informational handling for the `single instance only` path

**OUT OF SCOPE**
- Deletion publish flow and `deleted_event` table handling; Story 4.5 owns deletion staging and delete-specific persistence
- Multi-select on calendar surfaces themselves; Story 4.6 owns that interaction
- Recurring-series publish scopes (`this and following`, `all events`); Story 4.9 owns those flows
- Automatic background retry of failed publishes
- Arbitrary Google Calendar field support beyond the fields already represented in local entities (`summary`, `description`, `start/end`, `is_all_day`, `color_id`)

## Dev Notes

### Critical Repo Truth Before Starting

The current branch still reflects the post-4.1 state:
- `PendingEvent` is edit-only and still requires non-null `GcalEventId`
- `ICalendarSelectionService`, `EventSelectedMessage`, `ICalendarQueryService`, `CalendarEventDisplayModel`, and all calendar views are still Google-ID-centric
- `IGoogleCalendarService` and `IGoogleCalendarApiClient` only support fetch/list operations today

Story 4.4 assumes the post-4.2 / 4.3 contract from `docs/epic-4/tech-spec.md`, not the older branch shape. If the branch still lacks the Story 4.2 schema widening and source-agnostic event identity, land those prerequisites first rather than working around them inside publish code.

### Actual Project Structure

This is a flat WinUI 3 app. Keep new code in existing root folders:

```
GoogleCalendarManagement/
├── Views/
├── ViewModels/
├── Services/
├── Messages/
├── Models/
├── Data/
└── GoogleCalendarManagement.Tests/
```

Do **not** introduce `Core/`, `Managers/`, or a second assembly. New services stay flat under `Services/`.

### Recommended Implementation Surface

Expected files to extend or add:

- `Views/MainPage.xaml`
- `Views/MainPage.xaml.cs`
- `ViewModels/MainViewModel.cs`
- `Services/IGoogleCalendarService.cs`
- `Services/GoogleCalendarService.cs`
- `Services/GoogleCalendarApiClient.cs`
- `Services/IPendingEventRepository.cs`
- `Services/PendingEventRepository.cs`
- `Services/IColorMappingService.cs`
- `Services/ColorMappingService.cs`
- `Data/Entities/PendingEvent.cs`
- `Data/Entities/GcalEvent.cs`
- `Data/Entities/GcalEventVersion.cs`
- `Data/CalendarDbContext.cs`
- `App.xaml.cs`
- `GoogleCalendarManagement.Tests/Unit/...`
- `GoogleCalendarManagement.Tests/Integration/...`

Prefer introducing a dedicated orchestration service such as `IPendingEventPublishService` / `PendingEventPublishService` for the batch workflow. Keep `MainViewModel` focused on state and commands, not Google API orchestration or EF persistence.

### PendingEvent Prerequisite Shape

Story 4.4 expects the `PendingEvent` shape described in `docs/epic-4/tech-spec.md`, including:

```csharp
public class PendingEvent
{
    public Guid Id { get; set; }
    public string? GcalEventId { get; set; }
    public string CalendarId { get; set; } = "primary";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? StartDatetime { get; set; }
    public DateTime? EndDatetime { get; set; }
    public bool? IsAllDay { get; set; }
    public string? ColorId { get; set; }
    public bool AppCreated { get; set; } = true;
    public string SourceSystem { get; set; } = "manual";
    public bool ReadyToPublish { get; set; } = false;
    public DateTime? PublishAttemptedAt { get; set; }
    public string? PublishError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

Do not bolt publish fields onto the old 4.1-only entity ad hoc. Story 4.4 depends on the widened Story 4.2 schema contract.

### Google Calendar Write Guardrails

- Google Calendar `Events.Update` is a full-resource `PUT`, not patch semantics. The safest path is: get latest event when needed, apply local fields, then send the full event back with ETag protection.
- Conditional update/delete flows use the `If-Match` header with the last-known ETag; Google returns `412 Precondition Failed` on mismatch.
- Insert operations do not support conditional modification.
- Recurring-instance edits target the instance URL directly and should remain single-instance only in this story.

These rules were verified against Google’s official Calendar API docs on **2026-04-19**.

### Colour Mapping Guardrail

Story 4.3 stores local semantic keys such as `azure`, `purple`, and `sage` in `pending_event.color_id`. Google Calendar write payloads must use Google’s event colour IDs instead. Add or expose the reverse mapping in `IColorMappingService`; do not hardcode a second mapping table in the publish service.

Canonical-to-Google mapping from the Epic 4 tech spec:

| Canonical Key | Google `colorId` |
|---|---|
| `azure` | `"1"` |
| `navy` | `"2"` |
| `lavender` | `"3"` |
| `flamingo` | `"4"` |
| `yellow` | `"5"` |
| `orange` | `"6"` |
| `grey` | `"8"` |
| `purple` | `"9"` |
| `sage` | `"10"` |

### Local Persistence Rules After Successful Publish

For a successful **insert**:
- Use the Google response as the source of truth for the new `gcal_event` row
- Set `AppCreated = true`, `AppPublished = true`, `AppPublishedAt = now`, `SourceSystem = "manual"`
- Delete the source `pending_event` row after the local upsert succeeds
- Do **not** create a `gcal_event_version` row for a brand-new publish

For a successful **update**:
- Create a `gcal_event_version` snapshot from the current live row before overwriting it
- Use `ChangedBy = "manual_publish"` and keep `ChangeReason = "updated"` for consistency with existing history semantics
- Update the live `gcal_event` row from the Google response, not from the pending row
- Delete the source `pending_event` row only after snapshot + live-row update both succeed

Keep the external API call outside the EF Core transaction. Keep the local snapshot/update/delete sequence inside one short database transaction per item.

### UI Guardrails

- Reuse the existing top-bar patterns in `MainPage`; do not create a second shell or modal workflow page
- Reuse or extend `IContentDialogService` for confirmation and error dialogs instead of scattering raw `ContentDialog` construction throughout code-behind
- Disable repeat publish clicks while a batch is in progress
- Failed rows must stay visible in the pending list with their error text so the user can retry intentionally
- The `300 ms` success fade belongs in the view layer (`XAML` / animation), not in persistence code

### Previous Story Intelligence

- Story 4.1 already established: use `IDbContextFactory<CalendarDbContext>` in singleton services, keep all timestamps UTC, and refresh visible events through `WeakReferenceMessenger`
- Story 4.3 already established: all local edits, including colour changes, stage through `pending_event`; no version-history snapshot is written until publish time
- Story 4.2 defines the contract widening required so pending drafts can be selected, loaded, and refreshed through the same UI surface as Google-backed events

### Testing Requirements

Add or extend tests in the existing xUnit style. Minimum expected coverage:

- `GoogleCalendarManagement.Tests/Unit/Services/GoogleCalendarServiceTests.cs`
  - insert payload uses Google colour IDs and correct timed/all-day field shapes
  - update path retries correctly after a `412` only when local timestamp wins
- `GoogleCalendarManagement.Tests/Integration/...`
  - successful new-draft publish inserts a `gcal_event`, removes the `pending_event`, and sets publish metadata
  - successful edit publish appends exactly one `gcal_event_version` row before overwrite
  - failed publish preserves the pending row and stores `publish_error`
  - mixed-result batch succeeds item-by-item without rolling back successful items
- `GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs`
  - push command enable/disable state
  - pending-count badge behavior
  - completion summary / progress state

### Build / Verification

- `dotnet build -p:Platform=x64`
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`
- Manual:
  - create or edit event so it becomes pending
  - open push dropdown and publish a selected subset
  - confirm success transitions to 100% opacity
  - force a publish failure and verify the row remains pending with an inline error

## Tasks / Subtasks

- [ ] **Task 1: Verify and land prerequisite contracts from Stories 4.2 and 4.3** (AC: 4.4.1, 4.4.4, 4.4.6)
  - [ ] Confirm `PendingEvent` includes nullable `GcalEventId`, publish metadata, and post-4.2 creation fields
  - [ ] Confirm source-agnostic event identity is available end-to-end (`EventId`, `SourceKind`) before starting publish UI work
  - [ ] Confirm `IColorMappingService` can map canonical keys back to Google `colorId` values

- [ ] **Task 2: Add Google Calendar write operations and payload mapping** (AC: 4.4.4, 4.4.5, 4.4.6, 4.4.7, 4.4.8)
  - [ ] Extend `IGoogleCalendarApiClient` and `IGoogleCalendarService` with insert, get, and update support needed for publish
  - [ ] Use full-resource update semantics plus ETag conditional requests
  - [ ] Build timed/all-day request payloads correctly
  - [ ] Keep recurring-instance publish scoped to the instance ID only

- [ ] **Task 3: Implement batch publish orchestration service** (AC: 4.4.4, 4.4.5, 4.4.7, 4.4.9, 4.4.11)
  - [ ] Add `IPendingEventPublishService` / `PendingEventPublishService`
  - [ ] Query selected pending rows in deterministic order
  - [ ] For inserts: call Google, upsert live row, delete pending row
  - [ ] For edits: snapshot `gcal_event_version`, update Google, update live row, delete pending row
  - [ ] Persist per-item `publish_attempted_at` / `publish_error` on failure

- [ ] **Task 4: Add top-bar publish UI and selection workflow** (AC: 4.4.1, 4.4.2, 4.4.3, 4.4.11)
  - [ ] Add `Push to GCal` control with badge to `Views/MainPage.xaml`
  - [ ] Add pending-list selection state, `Select All`, and publish command handling in `MainViewModel`
  - [ ] Show confirmation dialog before the batch starts
  - [ ] Disable duplicate publish actions while the batch is running

- [ ] **Task 5: Update UI state after publish results** (AC: 4.4.8, 4.4.9, 4.4.10, 4.4.11)
  - [ ] Refresh visible events after each success without requiring full app restart
  - [ ] Remove successful rows from the pending list and keep failed rows visible
  - [ ] Add the `300 ms` pending-to-published opacity transition
  - [ ] Surface completion summary and conflict/failure messages clearly

- [ ] **Task 6: Add automated tests and local verification** (AC: 4.4.12)
  - [ ] Unit tests for payload mapping and conflict resolution
  - [ ] Integration tests for insert, update, failure retention, and version-history writes
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`

## References

- `docs/epic-4/tech-spec.md` - `Story 4.4 — Push Pending Events to Google Calendar`
- `docs/epic-4/tech-spec.md` - `Design note — unified save path`
- `docs/epic-4/tech-spec.md` - `Risks, Assumptions, Open Questions` (`R1`, `A3`)
- `docs/tier-2-requirements.md` - `Push to GCal`
- `docs/ux-design-specification.md` - `Confirmations Only for Commits`, `Push to GCal Confirmation Dialog`, `Tier 2: Edit & Sync Journey`
- Google Calendar API - versioned resources / conditional modification:
  - https://developers.google.com/workspace/calendar/api/guides/version-resources
- Google Calendar API - `Events.Insert`:
  - https://developers.google.com/workspace/calendar/api/v3/reference/events/insert
- Google Calendar API - `Events.Update`:
  - https://developers.google.com/workspace/calendar/api/v3/reference/events/update
- Google Calendar API - recurring instances:
  - https://developers.google.com/workspace/calendar/api/guides/recurringevents

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

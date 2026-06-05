# Story 4.11: Instant Sync Toggle

Status: ready-for-dev

## Story

As a **user**,
I want **a top-bar toggle to enable Instant Sync mode**,
so that **every event I create, edit, revert, or delete is immediately pushed to Google Calendar without any manual publish step — mirroring the native Google Calendar editing experience**.

## Acceptance Criteria

1. **AC-4.11.1 - Instant Sync toggle is visible and persisted in the top bar:**
   Given the application is open, a labeled toggle (e.g. `Instant Sync`) appears in the top bar alongside the existing `Push to GCal` button. The toggle state is persisted to `app_config` via `IConfigRepository` under a key such as `"instant_sync_enabled"` and is restored on next launch. The default state is OFF.

2. **AC-4.11.2 - Newly created events are pushed to GCal on focus-loss when Instant Sync is ON:**
   Given Instant Sync is ON and the user creates a new draft event (via drag-to-create or "+ Add Event"), when the user clicks away from that event (the details panel loses focus or a different event/date is selected), the app immediately calls `IGoogleCalendarService.InsertEventAsync` with no confirmation dialog. A small loading spinner appears in the top-right corner of the event tile on the calendar while the call is in flight. The event displays at **full opacity** (not 60%) while in-flight, as if it were already a real GCal event.

3. **AC-4.11.3 - Successful instant insert promotes the event and removes the pending row:**
   Given an instant insert call completes successfully, the spinner is removed from the tile, the `pending_event` row is deleted, and the `gcal_event` row is created (same promotion logic as Story 4.4 AC-4.4.4). The event remains at full opacity without any flash or re-render.

4. **AC-4.11.4 - Failed instant insert shows a banner and moves the event to the pending list:**
   Given an instant insert call fails (network error, API error, or any non-2xx response), the spinner is removed, the event tile drops to 60% opacity, a dismissible error `InfoBar` banner is shown at the top of the main content area with a short error description, and the `pending_event` row remains so the event appears in the existing `Push to GCal` list. The user can then manually publish from there.

5. **AC-4.11.5 - Edited events are pushed to GCal on save when Instant Sync is ON:**
   Given Instant Sync is ON and the user edits an event (title, time, description, color) and either presses the explicit Save button or clicks away from the event (triggering the 500 ms debounced auto-save commit), the app immediately calls `IGoogleCalendarService.UpdateEventAsync` with no confirmation dialog. A loading spinner appears on the event tile. Success and failure handling match AC-4.11.3 and AC-4.11.4 respectively (on failure, the edit row remains in `pending_event` at 60% opacity with an error banner).

6. **AC-4.11.6 - Edits are queued when an event is already in-flight:**
   Given Instant Sync is ON and an event has a sync call already in flight (spinner visible), when the user makes another edit and saves/clicks away, that second edit is queued. The second edit is applied immediately to the local UI (the tile and details panel reflect the new values), but the GCal call for that edit is not initiated until the prior in-flight call completes. If the prior call fails, the queued edit is merged into the `pending_event` row (not discarded) and shown in the push list.

7. **AC-4.11.7 - Create → immediate edit race: edits are queued until the insert GcalEventId is returned:**
   Given Instant Sync is ON and a new event was created and the insert call is in flight (no `GcalEventId` yet), when the user clicks back to that event and makes edits, those edits are applied to the local `pending_event` row immediately (UI stays current), but no update call is made until the insert succeeds and returns a `GcalEventId`. Once the insert confirms, the queued edit is sent as a single `UpdateEventAsync` call. If the insert fails, the merged draft stays in the pending list with the edits included.

8. **AC-4.11.8 - Deletions are executed instantly to GCal when Instant Sync is ON:**
   Given Instant Sync is ON and the user deletes a published event (from the details panel), the app calls `IGoogleCalendarService.DeleteEventAsync` immediately without staging a `pending_event` delete row, and without the existing staging confirmation dialog from Story 4.5. The event is immediately removed from the calendar view. An **undo toast** is shown for 5 seconds ("Event deleted — Undo") that, if clicked, re-creates the event in GCal via `InsertEventAsync` and restores it to the calendar. If the delete call itself fails, show an error banner; do not remove the event from the calendar. Instant Sync mode does NOT change the behavior for deletion of local-only drafts (those always delete locally with no GCal call needed regardless of mode).

9. **AC-4.11.9 - Reverts are pushed to GCal instantly when Instant Sync is ON:**
   Given Instant Sync is ON and the user clicks Revert on an event that has a pending edit row, the app clears the `pending_event` row locally and immediately calls `IGoogleCalendarService.UpdateEventAsync` with the original `gcal_event` values. A loading spinner appears on the tile while the call is in flight. On success, the event is at full opacity with original values. On failure, show an error banner and restore the pending row.

10. **AC-4.11.10 - Pre-existing pending items in the push list are NOT auto-synced when Instant Sync is toggled ON:**
    Given Instant Sync is toggled from OFF to ON while items already exist in the `Push to GCal` list, those items remain in the list at 60% opacity and are not automatically published. The user must still manually use `Push to GCal` for those items. No notification or banner is shown on toggle — the pending list badge count continues to reflect these items.

11. **AC-4.11.11 - Batch operations are instant-synced when Instant Sync is ON:**
    Given Instant Sync is ON and the user performs a multi-select batch operation (color change from Story 4.6), the batch changes are immediately pushed to GCal via individual `UpdateEventAsync` calls for each affected event. Each tile shows a spinner while its call is in flight. Per-event success/failure follows AC-4.11.3 and AC-4.11.4. Failures for individual items in a batch do not block successful items.

12. **AC-4.11.12 - Events instantly synced in this mode always display at full opacity:**
    Given Instant Sync is ON, any event that has been successfully pushed to GCal (or is currently in-flight) is rendered at **full opacity** (not 60%). Only events that are in the `pending_event` table due to prior failures or because they pre-dated the Instant Sync toggle remain at 60% opacity. This is the core visual distinction: in Instant Sync mode the calendar looks like native GCal, not a staging area.

13. **AC-4.11.13 - Instant Sync toggle state does not affect normal Push-to-GCal flow:**
    Given Instant Sync is OFF, the app behaves exactly as before this story: all creates and edits stage to `pending_event`, the `Push to GCal` button is the only outbound path, and the confirmation dialog is shown as before.

---

## Scope Boundaries

**IN SCOPE**
- `Instant Sync` toggle in the top bar (XAML + `MainViewModel`)
- Toggle state persistence via `IConfigRepository` (`"instant_sync_enabled"` key)
- Per-event in-flight loading spinner on calendar tiles (all views that render event blocks: Week, Day, Month, Year)
- Instant insert on focus-loss for new drafts
- Instant update on save/click-away for edits
- Edit + create/edit race queuing logic
- Instant delete with undo toast
- Instant revert
- Batch operation instant sync
- Opacity contract: in-flight + successfully synced = full opacity; pending/failed = 60%
- Error banner (InfoBar) for any instant sync failure

**OUT OF SCOPE**
- Any changes to the existing `Push to GCal` flyout or confirmation dialog flow
- Background auto-retry of failed instant sync items (they fall to the existing pending list)
- Recurring-series operations beyond what Story 4.9 already defines
- Animation or transition changes to the existing 300 ms opacity fade from Story 4.4

---

## Dev Notes

### Architecture: Where Instant Sync Logic Lives

Do **not** build a parallel publish pipeline. Instant Sync reuses `IPendingEventPublishService.PublishAsync` for the actual GCal API call (passing the single pending event ID). The orchestration logic (when to trigger, spinner management, queue, and failure routing) belongs in a new `IInstantSyncService` / `InstantSyncService` in `Services/`, injected into `MainViewModel`.

The queue for in-flight/pending edits can be a simple per-event `ConcurrentDictionary<string, Task>` chain: when a new sync is requested for an event that already has an in-flight task, `.ContinueWith(...)` the new request onto the existing task.

### Toggle Persistence

Use the existing `IConfigRepository.SetConfigValueAsync("instant_sync_enabled", "true"/"false")` pattern. Load on app init in `MainViewModel.InitializeAsync`. No new table or migration needed.

### Loading Spinner on Event Tiles

The `CalendarEventDisplayModel` carries visual state for all views. Add a new `bool IsSyncing` property (default `false`). Each calendar tile's data template should show a small `ProgressRing` (`IsActive="{x:Bind IsSyncing, Mode=OneWay}"`) pinned to the top-right corner of the tile, visible only when `IsSyncing` is true. Set `IsSyncing = true` before the GCal call; set back to `false` (and update `IsPending` / opacity) on completion. Use `MainViewModel.RefreshAffectedEventAsync` to push these state changes to live tiles — that path already exists.

### Opacity Contract in Instant Sync Mode

Currently: `IsPending = true` → 60% opacity.

In Instant Sync mode, newly instant-synced events should never be marked `IsPending = true`. Keep `IsPending = false` for any event that either successfully synced or is currently syncing. Only events that *failed* and fell to the `pending_event` table get `IsPending = true`.

Pre-existing pending items (from before Instant Sync was enabled) already have `IsPending = true` from the query service — leave them as-is.

### Focus-Loss Trigger (New Events)

The "click away" trigger for new draft events should be driven by `ICalendarSelectionService.SelectionChanged`: when the selected event changes away from a draft event that has never been synced (`pending_event.GcalEventId == null`, `PublishAttemptedAt == null`), fire the instant insert. Guard against re-triggering if a sync for that pending ID is already in the queue.

### Save/Click-Away Trigger (Edits)

The existing `EventDetailsPanelViewModel` debounced auto-save already fires when the user tabs away or navigates. In Instant Sync mode, after the `IPendingEventDraftService` write completes, publish the single pending event immediately. The trigger point is after the debounce commit, not during typing.

### Undo Toast for Instant Delete

Display a `TeachingTip` or an `InfoBar` at the bottom of the calendar area showing "Event deleted — Undo" for 5 seconds. Store the deleted event's full field values in memory (not the DB — the row is already gone) for the duration of the toast. If Undo is clicked, call `InsertEventAsync` with those stored values and re-create the `gcal_event` row on success.

### Project File Structure

No new files are strictly required beyond:
- `Services/IInstantSyncService.cs`
- `Services/InstantSyncService.cs`

All UI changes are additive to existing XAML controls. All new properties (`IsSyncing`) are added to `CalendarEventDisplayModel`. `MainViewModel` exposes `IsInstantSyncEnabled` (bound to the toggle), `InstantSyncService` is injected, and the existing message handlers call into it.

### Key Existing Patterns to Follow

- **Message bus:** `EventUpdatedMessage`, `EventPublishedMessage` — fire these after an instant sync so all views refresh correctly.
- **`RefreshAffectedEventAsync`:** Already used to push live model updates to calendar tiles. Use it to flip `IsSyncing` on/off.
- **`ShowNotification`:** Already in `MainViewModel`. Use it for the error InfoBar on failure.
- **`IGoogleCalendarService.DeleteEventAsync`:** Already implemented. Parameters: `calendarId`, `eventId`. The calendar ID is stored in `app_config` (same key used by the sync manager).
- **ETag conflict (412) on instant update:** Apply the same re-fetch-and-retry-once logic from `PendingEventPublishService` (Story 4.4 AC-4.4.7). If retry fails, fall to pending.

### Tests to Add

- `InstantSyncService` unit tests: insert-on-focus-loss, update-on-save, queue behavior, failure → pending routing
- Integration test: create event → click away → verify `gcal_event` row created and `pending_event` row deleted
- Integration test: edit in-flight → queue holds → second call fires after first completes
- Unit test: delete undo toast → re-insert restores event
- Unit test: pre-existing pending items not auto-published when toggle turned ON

# Epic 2 Follow-Ups - 2026-03-30

## Logged Items

### 1. Intermittent "Last successful sync: Never" on startup

- Type: bug
- Priority: resolved
- Symptom: On startup, the settings screen sometimes shows `Last successful sync: Never` even though `data_source_refresh` contains successful sync rows.
- Likely causes:
  - `SettingsViewModel` starts `InitializeAsync()` from the constructor with `_ = InitializeAsync();`, so initialization is fire-and-forget and can lose failures or race with page load.
  - `RefreshLastSyncTextAsync()` reads SQLite immediately on startup and does not handle transient lock/read failures, so a locked database can leave the UI stuck on `Never`.
- Resolution:
  - Moved startup initialization to the page load path so the last sync state is read deterministically from the database.
  - Added safe handling and logging for startup read failures so the UI does not incorrectly remain on `Never`.

### 2. App becomes white / "Not Responding" during sync when clicked repeatedly

- Type: bug
- Priority: resolved
- Symptom: While sync is running, repeated UI interaction can make the WinUI app appear frozen.
- Likely causes:
  - Sync is started from the UI command path and async continuations likely resume on the UI thread.
  - The fetch/persist loop reports progress frequently and performs per-event database writes, which can monopolize the UI dispatcher even without a hard deadlock.
- Resolution:
  - Moved sync execution off the UI thread while keeping progress updates marshalled back to UI.
  - Added sync exception handling so failures surface as user-facing errors instead of app-fatal UI faults.

### 3. Pull previous Google versions into `gcal_event_version`

- Type: future investigation
- Priority: defer
- Question: Can the app fetch historical Google revisions and backfill them into `gcal_event_version`?
- Current assessment:
  - This is not a current defect in Story 2.3.
  - Google Calendar sync already stores the current event snapshot locally before overwrite/delete.
  - Google Calendar API event fetches expose current event state metadata such as `etag` and `updated`, but not a built-in prior-version history feed for normal event revisions.
- Suggested direction:
  - Treat local history as the system of record for rollback/versioning unless a new Google API capability is identified and validated.
  - Do not prioritize this until the core sync UX and correctness bugs are resolved.

### 4. `gcal_event_version.color_id` is null for some edited events

- Type: bug
- Priority: resolved
- Symptom: Some history rows have `color_id = null` for events where color or summary changed.
- Current assessment:
  - The new snapshot logic copies `existingEvent.ColorId`, so if snapshots are null, the null likely already exists on the live row before snapshot or is coming from Google mapping.
  - Google event `colorId` is optional, so null may be valid for default-colored events, but null for explicitly colored events would be a real bug.
- Resolution:
  - Verified the live-to-snapshot color copy path and the Google event color mapping path.
  - Added test coverage for Google `colorId` mapping and the observed manual verification now passes.

## Recommendation

Items 1, 2, and 4 are resolved.

- Item 3 remains deferred unless you specifically want a separate research spike.

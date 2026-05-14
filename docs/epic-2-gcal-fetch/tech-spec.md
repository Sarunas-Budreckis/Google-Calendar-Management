# Epic Technical Specification: Google Calendar Integration & Sync (Read-Only)

Date: 2026-03-30
Author: Sarunas Budreckis
Epic ID: epic-2
Status: Draft

---

## Overview

Epic 2 delivers the first functional capability of Google Calendar Management: a fully operational, read-only local mirror of the user's Google Calendar. Building directly on the database schema, EF Core infrastructure, logging, and migration system established in Epic 1, this epic implements Google OAuth 2.0 authentication, batch event fetching, persistent local caching with version history, and per-date sync status indicators.

The deliverable at the close of Epic 2 is a Tier 1 application that authenticates silently with Google, syncs all calendar events into the local SQLite database, preserves full version snapshots on every re-sync, and displays green/grey status indicators per date — providing the data foundation that Epic 3's calendar UI will render and that all subsequent epics depend upon.

## Objectives and Scope

**In Scope:**

- Google OAuth 2.0 "installed application" flow (PKCE) via `Google.Apis.Auth`
- Encrypted token persistence (Windows DPAPI) in AppMetadata / Windows Credential Store
- Automatic access-token refresh; persistent refresh-token across restarts
- "Reconnect" flow for account switching or re-authorization
- `Events.List()` pagination with `nextPageToken` for full initial fetch
- Incremental sync via Google's `syncToken` for subsequent calls (delta-only)
- Mapping of Google API event model → `gcal_event` EF entity
- Recurring event expansion into individual `gcal_event` instances
- All-day and timed events; UTC-normalized `start_datetime` / `end_datetime`
- Version snapshot to `gcal_event_version` before every overwrite
- Delete detection: events removed from GCal marked `is_deleted = TRUE` with snapshot
- Sync status per date (green = has `gcal_event` rows for date; grey = no rows)
- `data_source_refresh` table updated after each sync with timestamp and sync token
- Background sync timer (configurable interval, default 30 min)
- Offline-aware: queues sync if network unavailable, runs when restored
- Polly retry with exponential backoff on all Google API calls
- Progress indicator and mid-flight cancellation support

**Out of Scope:**

- Calendar UI rendering (Epic 3)
- Event creation, editing, or publishing to Google Calendar (Epic 6)
- Approval workflow and pending events (Epic 6)
- Other data sources: Toggl, YouTube, call logs (Epic 4)
- Save/restore snapshots and rollback (Epic 8)
- Sync status aggregation into `date_state` multi-dimensional flags (Epic 7)

**Dependencies:**

- **Prerequisite:** Epic 1 complete — `gcal_event`, `gcal_event_version`, `audit_log`, `config`, `data_source_refresh`, `system_state` tables must exist
- **Enables:** Epic 3 (reads `gcal_event` to render calendar UI)
- **External:** Google Cloud project with Calendar API enabled and OAuth 2.0 credentials (client ID / secret)
- **Developer setup:** Download the Desktop App OAuth client JSON from Google Cloud Console and place it at `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\client_secret.json` on the local machine. Do not store this file in the repository.

## System Architecture Alignment

Epic 2 activates the Google Calendar integration slice of the architecture defined in [architecture.md](../architecture.md):

| Concern | Architecture Component |
|---|---|
| API client | `GoogleCalendarService` in `GoogleCalendarManagement.Core/Services/` |
| Sync orchestration | `SyncManager` in `GoogleCalendarManagement.Core/Managers/` |
| Data persistence | `GcalEventRepository`, `GcalEventVersionRepository` in `GoogleCalendarManagement.Data/` |
| Database tables | `gcal_event`, `gcal_event_version`, `audit_log`, `data_source_refresh` |
| Token storage | Windows DPAPI via `System.Security.Cryptography.ProtectedData` |
| HTTP resilience | Polly `WaitAndRetryAsync` registered on `HttpClient` for `GoogleCalendarService` |
| Logging | Serilog structured logging; all operations → `audit_log` table |

**Layering constraint:** `GoogleCalendarService` and `SyncManager` live in the Core layer and have no WinUI 3 dependency. The UI layer invokes sync via ViewModel → Service calls and receives progress via `IProgress<T>` / `WeakReferenceMessenger`.

**Naming alignment:** Tables use `singular_snake_case` (`gcal_event`, not `gcal_events`). C# classes use PascalCase (`GcalEvent`, `IGoogleCalendarService`). All I/O methods are `async` and suffixed `Async`. All timestamps stored as UTC (`DateTime.UtcNow`).

## Detailed Design

### Services and Modules

| Service / Module | Layer | Responsibility | Inputs | Outputs |
|---|---|---|---|---|
| `IGoogleCalendarService` | Core/Interfaces | Contract for all GCal API operations | Date ranges, cancellation tokens | Event lists, `OperationResult<T>` |
| `GoogleCalendarService` | Core/Services | Google Calendar API wrapper; OAuth flow; token refresh; Events.List; incremental sync | Credentials, date range, `syncToken` | `IList<GcalEventDto>`, updated sync token |
| `ISyncManager` | Core/Interfaces | Contract for sync orchestration | Sync request parameters | `SyncResult` |
| `SyncManager` | Core/Managers | Orchestrates full vs. incremental sync; version history writes; conflict resolution (GoogleWins in Tier 1); progress reporting | `IGoogleCalendarService`, `IGcalEventRepository`, `IGcalEventVersionRepository`, `IAuditService` | Updates `gcal_event`, `gcal_event_version`, `data_source_refresh` |
| `ITokenStorageService` | Core/Interfaces | Contract for encrypted credential persistence | OAuth token response | Stored/retrieved `TokenResponse` |
| `DpapiTokenStorageService` | Core/Services | DPAPI-encrypt/decrypt `TokenResponse`; stored in `AppMetadata` table | Raw `TokenResponse` JSON | Encrypted bytes (stored in DB) |
| `ISyncStatusService` | Core/Interfaces | Compute green/grey status per date | Date or date range | `Dictionary<DateOnly, SyncStatus>` |
| `SyncStatusService` | Core/Services | Queries `gcal_event` for presence of rows per date; computes indicator state | `IGcalEventRepository` | `SyncStatus` enum per date |
| `BackgroundSyncService` | Core/Services | Periodic timer; network availability check; queues sync when offline; fires sync on restore | `ISyncManager`, `INetworkMonitor` | Triggers `SyncManager` on schedule |
| `GcalEventRepository` | Data/Repositories | CRUD + bulk upsert for `gcal_event` | `CalendarDbContext` | `GcalEvent` entities |
| `GcalEventVersionRepository` | Data/Repositories | Insert-only version snapshots for `gcal_event_version` | `CalendarDbContext` | `GcalEventVersion` entities |

### Data Models and Contracts

**`GcalEventDto`** — transfer object from Google API to `SyncManager`:
```csharp
public record GcalEventDto(
    string GcalEventId,        // Google's Event.Id
    string CalendarId,
    string? Summary,
    string? Description,
    DateTime StartDateTimeUtc,
    DateTime EndDateTimeUtc,
    bool IsAllDay,
    string? ColorId,
    string? GcalEtag,
    DateTime GcalUpdatedAtUtc,
    bool IsDeleted,             // status == "cancelled"
    string? RecurringEventId,
    bool IsRecurringInstance
);
```

**`SyncResult`** — returned from `SyncManager.SyncAsync()`:
```csharp
public record SyncResult(
    bool Success,
    int EventsAdded,
    int EventsUpdated,
    int EventsDeleted,
    string? NewSyncToken,
    string? ErrorMessage
);
```

**`SyncStatus`** (enum, used for indicators):
```csharp
public enum SyncStatus { Synced, NotSynced }
```

**`TokenResponse`** (Google SDK type) — persisted as DPAPI-encrypted JSON in `AppMetadata` row with `Key = "GcalTokenResponse"`.

**EF entities impacted** (already defined in Epic 1 schema, no new tables):

- `GcalEvent` → `gcal_event` — all columns; `last_synced_at` updated on each sync
- `GcalEventVersion` → `gcal_event_version` — insert-only; `changed_by = "gcal_sync"`
- `DataSourceRefresh` → `data_source_refresh` — one row per source; `sync_token` column stores GCal incremental token

### APIs and Interfaces

**Google Calendar API v3 endpoints used:**

| Operation | Method / Endpoint | Notes |
|---|---|---|
| Full event list | `Events.List(calendarId)` with `MaxResults=250`, `SingleEvents=true` | Paginate via `nextPageToken`; `SingleEvents=true` expands recurrences |
| Incremental sync | `Events.List(calendarId, SyncToken=token)` | Returns only changed events since last sync |
| Auth (initial) | OAuth 2.0 loopback redirect (port 0) via `GoogleWebAuthorizationBroker` | Opens system browser; PKCE enforced by SDK |
| Token refresh | Automatic via `Google.Apis.Auth` credential object | No manual calls needed |

**Core service interface signatures:**

```csharp
// GoogleCalendarManagement.Core/Interfaces/IGoogleCalendarService.cs
public interface IGoogleCalendarService
{
    Task<OperationResult<OAuthStatus>> AuthenticateAsync(CancellationToken ct = default);
    Task<OperationResult<bool>> IsAuthenticatedAsync();
    Task RevokeAndClearTokensAsync();
    Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>>
        FetchAllEventsAsync(string calendarId, DateTime start, DateTime end,
                           IProgress<int>? progress = null, CancellationToken ct = default);
    Task<OperationResult<(IList<GcalEventDto> Events, string? SyncToken)>>
        FetchIncrementalEventsAsync(string calendarId, string syncToken,
                                   CancellationToken ct = default);
}

// GoogleCalendarManagement.Core/Interfaces/ISyncManager.cs
public interface ISyncManager
{
    Task<SyncResult> SyncAsync(string calendarId, DateTime? rangeStart = null,
                               DateTime? rangeEnd = null,
                               IProgress<SyncProgress>? progress = null,
                               CancellationToken ct = default);
}

// GoogleCalendarManagement.Core/Interfaces/ISyncStatusService.cs
public interface ISyncStatusService
{
    Task<Dictionary<DateOnly, SyncStatus>> GetSyncStatusAsync(DateOnly from, DateOnly to);
    Task<DateTime?> GetLastSyncTimeAsync();
}
```

**Error codes / results:**

| Scenario | `OperationResult` Outcome |
|---|---|
| Auth success | `Success = true`, `Data = OAuthStatus.Authenticated` |
| User cancels auth | `Success = false`, message = "Authentication cancelled by user." |
| Network unavailable | `Success = false`, message = "Unable to reach Google. Check internet connection." |
| Token expired + refresh fails | `Success = false`, message = "Session expired. Please reconnect Google Calendar." |
| API quota exceeded | `Success = false`, message = "Google Calendar quota exceeded. Try again tomorrow." |
| Sync token invalid (410 Gone) | `SyncManager` falls back to full re-sync automatically |

### Workflows and Sequencing

**Flow 1 — Initial Authentication (Story 2.1):**
```
User clicks "Connect Google Calendar"
  → SettingsViewModel.ConnectGoogleCalendarAsync()
  → GoogleCalendarService.AuthenticateAsync()
    → GoogleWebAuthorizationBroker.AuthorizeAsync() [opens browser]
    → Google redirects to loopback with auth code
    → SDK exchanges code for access + refresh tokens
  → DpapiTokenStorageService.StoreTokenAsync(tokenResponse)
    → ProtectedData.Protect(tokenJson, entropy, Scope=CurrentUser)
    → AppMetadata.Upsert("GcalTokenResponse", encryptedBase64)
  → WeakReferenceMessenger.Send(new AuthenticationSucceededMessage())
  → UI shows "Connected as user@gmail.com"
```

**Flow 2 — Full Sync (Story 2.2, first run):**
```
User clicks "Sync with Google Calendar"
  → MainViewModel.SyncAsync()
  → SyncManager.SyncAsync(calendarId, start=now-6months, end=now+1month)
    → GoogleCalendarService.FetchAllEventsAsync()
      → Events.List() → page 1 (up to 250 events)
      → ... paginate until nextPageToken == null
    → For each GcalEventDto:
        if new:    GcalEventRepository.InsertAsync(entity)
        if update: GcalEventVersionRepository.InsertSnapshotAsync(existing)
                   GcalEventRepository.UpdateAsync(entity)
        if deleted: GcalEventVersionRepository.InsertSnapshotAsync(existing)
                    GcalEventRepository.MarkDeletedAsync(id)
    → DataSourceRefresh.Upsert("google_calendar", syncToken, DateTime.UtcNow)
    → AuditService.LogAsync("gcal_sync", added, updated, deleted)
  → WeakReferenceMessenger.Send(new SyncCompletedMessage(result))
  → UI refreshes calendar view
```

**Flow 3 — Incremental Sync (Story 2.5, subsequent runs):**
```
BackgroundSyncService timer fires (every 30 min)
  → NetworkMonitor.IsConnected? → No: enqueue, return
  → SyncManager.SyncAsync() [no rangeStart/End]
    → DataSourceRefresh.GetSyncTokenAsync("google_calendar") → token
    → GoogleCalendarService.FetchIncrementalEventsAsync(token)
      → 410 Gone? → fall back to FetchAllEventsAsync (full re-sync)
    → Process delta events (same upsert logic)
    → Update sync token in data_source_refresh
```

**Flow 4 — Sync Status Calculation (Story 2.4):**
```
Calendar view requests status for visible date range
  → SyncStatusService.GetSyncStatusAsync(from, to)
    → SELECT DISTINCT DATE(start_datetime) FROM gcal_event
       WHERE start_datetime BETWEEN @from AND @to
       AND is_deleted = FALSE
    → For each date in range:
        if date in result set → SyncStatus.Synced (green)
        else                  → SyncStatus.NotSynced (grey)
  → Returns Dictionary<DateOnly, SyncStatus>
```

## Non-Functional Requirements

### Performance

| Target | Metric | Source |
|---|---|---|
| Initial full sync (≤500 events) | < 10 seconds end-to-end | PRD NFR-P2 |
| Incremental sync (delta, typical weekday) | < 3 seconds | PRD NFR-P2 |
| Sync status query (1-month date range) | < 100 ms | PRD NFR-P1 (UI responsiveness) |
| Token load + credential check on startup | < 200 ms | PRD NFR-P1 |
| Background sync (must not block UI thread) | 0 ms UI impact | PRD NFR-P3 |

All network I/O runs on background threads via `async`/`await`. `SyncManager` publishes progress via `IProgress<SyncProgress>` marshalled to the UI thread by the ViewModel. The `BackgroundSyncService` uses `PeriodicTimer` (non-blocking, .NET 6+) on a dedicated background thread; it never touches the UI dispatcher.

Pagination fetches up to 250 events per page, targeting ≤ 4 API round-trips for a typical personal calendar with 6 months of events.

### Security

- **OAuth scope:** `https://www.googleapis.com/auth/calendar` (read + write) requested at initial auth; Epic 2 only reads — write scope is pre-provisioned for Epic 6 to avoid a second consent prompt.
- **Token encryption:** `TokenResponse` JSON serialized and encrypted with `ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)` before storage. Decrypted in-process only; never written to disk unencrypted.
- **Token storage location:** `AppMetadata` table row (`Key = "GcalTokenResponse"`) in the SQLite database file at `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db` — user-private directory.
- **Client secret handling:** `client_secret.json` must **not** be committed to source control. Loaded from `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\client_secret.json` at runtime. Documented in README.
- **Credential setup steps:** In Google Cloud Console, enable Calendar API, create a Desktop App OAuth client, download the JSON, copy it to `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\`, and rename it to `client_secret.json`.
- **PKCE:** Enforced automatically by `GoogleWebAuthorizationBroker` — no additional implementation required.
- **TLS:** All API calls use HTTPS enforced by the Google SDK's `HttpClientFactory`. No HTTP fallback permitted.
- **Refresh token rotation:** `DpapiTokenStorageService` must update stored token on every `TokenResponse` received (including refreshes), because Google may issue a new refresh token.
- **Re-auth trigger:** If `TokenResponseException` is caught with `Error = "invalid_grant"`, surface "Session expired. Please reconnect Google Calendar." and clear stored token — do not retry silently.

### Reliability/Availability

- **Offline operation:** App launches and displays cached data without network. Sync attempt when offline logs a warning and schedules retry — no error dialog shown unless user manually triggered sync.
- **Retry policy (Polly):** All `GoogleCalendarService` HTTP calls wrapped in `WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)))` — attempts at 2s, 4s, 8s before reporting failure.
- **410 Gone (expired sync token):** `SyncManager` catches HTTP 410, clears stored sync token from `data_source_refresh`, and automatically falls back to a full re-sync. No user intervention required.
- **Partial sync failure:** If sync fails mid-flight, already-written events are committed (no transaction spanning the full sync). Next sync will catch up — no data loss.
- **Auth failure on startup:** If stored token cannot be decrypted, log error and prompt user to reconnect — do not crash.

### Observability

**Serilog structured log events (minimum required):**

| Event | Level | Message template |
|---|---|---|
| Auth initiated | Information | `"Google Calendar auth initiated"` |
| Auth succeeded | Information | `"Google Calendar auth succeeded for {AccountEmail}"` |
| Auth failed | Error | `"Google Calendar auth failed: {Error}"` |
| Token refreshed | Debug | `"Access token refreshed, expires {ExpiresAt}"` |
| Sync started | Information | `"GCal sync started: full={IsFull}, calendar={CalendarId}"` |
| Sync completed | Information | `"GCal sync completed: +{Added} ~{Updated} -{Deleted} in {ElapsedMs}ms"` |
| Sync failed | Error | `"GCal sync failed after {RetryCount} retries: {Error}"` |
| 410 fallback | Warning | `"Sync token expired (410), falling back to full sync"` |
| Background sync skipped | Debug | `"Background sync skipped: network unavailable"` |
| API page fetched | Debug | `"Fetched page {PageNum} with {EventCount} events"` |

**`audit_log` table entries** (all user-visible operations):

| Operation | `operation_type` | Notes |
|---|---|---|
| Manual sync | `"gcal_sync"` | Includes added/updated/deleted counts |
| Background sync | `"gcal_sync_background"` | Same fields |
| Connect account | `"gcal_auth"` | No sensitive data logged |
| Disconnect account | `"gcal_revoke"` | |

**Performance signals:** Sync elapsed time logged at `Information` level. Operations exceeding 10 seconds logged at `Warning`. All thresholds configurable via `config` table.

## Dependencies and Integrations

**NuGet Packages (new in Epic 2 — not already in Epic 1):**

| Package | Version | Purpose |
|---|---|---|
| `Google.Apis.Calendar.v3` | 1.73.0.3993 | Google Calendar API client |
| `Google.Apis.Auth` | 1.73.0.x | OAuth 2.0 flow, token management, PKCE |
| `Microsoft.Extensions.Http.Polly` | 9.0.x | Retry policies on `HttpClient` |

**Already present from Epic 1 (used but not added):**

| Package | Version | Usage in Epic 2 |
|---|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | 9.0.12 | `GcalEvent`, `GcalEventVersion`, `DataSourceRefresh` repositories |
| `Serilog` / `Serilog.Sinks.File` | 4.x / 6.x | All structured logging |
| `CommunityToolkit.Mvvm` | 8.x | `WeakReferenceMessenger` for sync events to UI |
| `Microsoft.Extensions.Http` | 9.0.x | `IHttpClientFactory` base |

**Built-in .NET features used:**

| Feature | Usage |
|---|---|
| `System.Security.Cryptography.ProtectedData` | DPAPI token encryption (`DataProtectionScope.CurrentUser`) |
| `System.Net.NetworkInformation.NetworkChange` | Network availability detection in `BackgroundSyncService` |
| `System.Threading.PeriodicTimer` | Non-blocking background sync timer |
| `System.Text.Json` | Serialize/deserialize `TokenResponse` and version history JSON |
| `CancellationToken` / `IProgress<T>` | Cancellable sync with progress reporting |

**External service dependencies:**

| Service | Auth | Quota | Constraint |
|---|---|---|---|
| Google Calendar API v3 | OAuth 2.0 (refresh token) | 1M requests/day; 10 req/s/user | Requires Google Cloud project + enabled Calendar API; client secret stored outside repo |
| Google OAuth 2.0 endpoint | N/A (browser redirect) | No quota concern | Requires internet on first auth only; subsequent calls use refresh token |

**Internal (cross-epic) dependencies:**

| Dependency | Direction | Contract |
|---|---|---|
| Epic 1: `gcal_event` table | Writes (Epic 2) / Reads (Epic 3) | Schema defined in [_database-schemas.md](../_database-schemas.md) §3 |
| Epic 1: `gcal_event_version` table | Writes (Epic 2) / Reads (Epic 8) | Schema defined in [_database-schemas.md](../_database-schemas.md) §4 |
| Epic 1: `data_source_refresh` table | Writes (Epic 2) / Reads (Epic 7) | Stores `sync_token` and `last_synced_at` for GCal source |
| Epic 1: `audit_log` table | Writes (Epic 2) | All sync operations logged |
| Epic 1: `CalendarDbContext` | Shared | All repositories inject this context |
| Epic 1: `ILoggingService` / Serilog | Shared | All services use structured logging |
| Epic 3: Calendar view | Reads `gcal_event` | Epic 2 must have populated `gcal_event` before Epic 3 can render events |

## Acceptance Criteria (Authoritative)

**Story 2.1 — OAuth 2.0 Authentication**

1. Given the app has no stored credentials, when the user clicks "Connect Google Calendar", the system browser opens to Google's OAuth consent screen.
2. Given the user completes OAuth consent, the app receives an access token and refresh token, encrypts both with Windows DPAPI, and stores the result in `AppMetadata` — no plaintext credentials persist on disk.
3. Given a stored refresh token, when the app starts, it silently obtains a fresh access token without user interaction.
4. Given a valid access token expires mid-session, the token is refreshed automatically before the next API call with no user-visible interruption.
5. Given the user clicks "Reconnect Google Calendar" in Settings, existing tokens are cleared and a new OAuth flow begins, supporting account switching.
6. Given authentication fails (network error, user cancels, invalid credentials), a user-friendly error message is shown and no partial token state is persisted.

**Story 2.2 — Fetch Events and Store Locally**

7. Given the user is authenticated, when they click "Sync with Google Calendar", all events within the configured date range (default: 6 months back, 1 month forward) are fetched from the Google Calendar API.
8. Given the API returns paginated results, all pages are fetched and every event is stored in the `gcal_event` table before sync is considered complete.
9. Given a sync is in progress, a progress indicator is visible and a "Cancel" action stops the sync mid-flight cleanly, leaving already-written events in the database.
10. Given fetched events, all-day events are stored with `is_all_day = TRUE` and timed events with UTC-normalized `start_datetime` / `end_datetime`.
11. Given recurring events in the calendar, each instance is expanded and stored as an individual `gcal_event` row with `is_recurring_instance = TRUE` and a populated `recurring_event_id`.
12. Given an empty calendar, sync completes successfully with zero rows written and no error presented to the user.

**Story 2.3 — Version History on Sync**

13. Given an event already in `gcal_event` is updated in Google Calendar, when the next sync runs, the existing row's previous state is saved to `gcal_event_version` (with `changed_by = "gcal_sync"`) before the row is overwritten.
14. Given a new event from Google Calendar that does not exist locally, it is inserted into `gcal_event` with no version history entry (first occurrence has no prior state to snapshot).
15. Given an event deleted from Google Calendar, when sync runs, the existing `gcal_event` row is updated to `is_deleted = TRUE` and a snapshot is written to `gcal_event_version` with `changed_by = "gcal_sync"`.
16. Given version history rows, they are never automatically deleted — history is preserved indefinitely.

**Story 2.4 — Sync Status Indicators**

17. Given the calendar view is displayed, each date cell shows a green indicator if at least one non-deleted `gcal_event` row exists for that date, and a grey indicator otherwise.
18. Given a sync completes, the sync status indicators update immediately without requiring a manual page refresh.
19. Given a date with events, a tooltip on the indicator shows the `last_synced_at` timestamp from `data_source_refresh` formatted as "Last synced: X hours ago".
20. Given the user clicks "Refresh Status", the status indicators are recalculated from the current database state.

**Story 2.5 — Background Sync and Cache Management**

21. Given the app is running and the device has network connectivity, the background sync timer fires every 30 minutes (configurable via `config` table key `gcal_background_sync_interval_minutes`) and triggers an incremental sync.
22. Given the device has no network connectivity when the timer fires, the sync is skipped silently and retried at the next timer interval — no error is shown to the user.
23. Given an incremental sync returns HTTP 410 (sync token expired), `SyncManager` automatically falls back to a full re-sync for the default date range.
24. Given a background sync completes, the UI sync status indicators update and `data_source_refresh` is updated with the new sync token and timestamp.

## Traceability Mapping

| AC # | Story | Spec Section | Component(s) | Test Idea |
|---|---|---|---|---|
| 1 | 2.1 | APIs & Interfaces — Auth flow | `GoogleCalendarService.AuthenticateAsync`, `GoogleWebAuthorizationBroker` | Mock broker; verify browser open called |
| 2 | 2.1 | Security — Token encryption | `DpapiTokenStorageService`, `ProtectedData` | Verify AppMetadata row is Base64 ciphertext, not plaintext JSON |
| 3 | 2.1 | Workflows — Flow 1 | `GoogleCalendarService.IsAuthenticatedAsync`, token load on startup | Integration: startup with pre-seeded AppMetadata token |
| 4 | 2.1 | Security — Refresh token rotation | Google SDK credential auto-refresh | Unit: mock expired token triggers refresh; no exception thrown |
| 5 | 2.1 | APIs & Interfaces — RevokeAndClearTokensAsync | `DpapiTokenStorageService`, `AppMetadata` | Verify AppMetadata row deleted after revoke |
| 6 | 2.1 | NFR — Reliability; Error codes table | `GoogleCalendarService` error handling | Unit: cancelled auth returns `OperationResult.Success = false` |
| 7 | 2.2 | Workflows — Flow 2 | `SyncManager.SyncAsync`, `GoogleCalendarService.FetchAllEventsAsync` | Integration: mock API returns 50 events; verify 50 rows in gcal_event |
| 8 | 2.2 | APIs & Interfaces — Full event list | `FetchAllEventsAsync` pagination loop | Unit: mock 3-page response; verify all pages consumed |
| 9 | 2.2 | Services — `ISyncManager` | `SyncManager` + `CancellationToken` | Unit: cancel after page 1; verify partial write + clean exit |
| 10 | 2.2 | Data Models — `GcalEventDto` | `GcalEventDto` → `GcalEvent` mapping | Unit: all-day event mapped with `is_all_day=true`, times UTC |
| 11 | 2.2 | Data Models — `GcalEventDto` | `GcalEventDto.IsRecurringInstance` | Unit: recurring event expansion; verify `recurring_event_id` set |
| 12 | 2.2 | NFR — Reliability | `SyncManager` empty calendar path | Integration: mock empty API response; sync returns `EventsAdded=0` |
| 13 | 2.3 | Workflows — Flow 2 (update path) | `GcalEventVersionRepository.InsertSnapshotAsync` | Integration: sync updated event; verify gcal_event_version row inserted before update |
| 14 | 2.3 | Workflows — Flow 2 (new path) | `GcalEventRepository.InsertAsync` | Integration: new event; verify no gcal_event_version row created |
| 15 | 2.3 | Workflows — Flow 2 (delete path) | `GcalEventRepository.MarkDeletedAsync` | Integration: deleted event (status=cancelled); verify `is_deleted=true` + version row |
| 16 | 2.3 | Data Models — `GcalEventVersion` | `GcalEventVersionRepository` | Verify no DELETE statements issued against gcal_event_version |
| 17 | 2.4 | Workflows — Flow 4 | `SyncStatusService.GetSyncStatusAsync` | Unit: dates with/without events return correct SyncStatus enum |
| 18 | 2.4 | Workflows — Flow 4 | `WeakReferenceMessenger` + ViewModel | Integration: sync completes → UI receives message → status refreshed |
| 19 | 2.4 | Services — `ISyncStatusService.GetLastSyncTimeAsync` | `DataSourceRefresh` read | Unit: returns formatted timestamp from data_source_refresh |
| 20 | 2.4 | Services — `ISyncStatusService` | `SyncStatusService` | Unit: manual refresh triggers re-query |
| 21 | 2.5 | Workflows — Flow 3 | `BackgroundSyncService`, `PeriodicTimer` | Unit: mock timer fires; verify SyncManager.SyncAsync called |
| 22 | 2.5 | NFR — Reliability (offline) | `BackgroundSyncService`, `INetworkMonitor` | Unit: no network → sync skipped; no exception thrown |
| 23 | 2.5 | APIs — Error codes (410 Gone) | `SyncManager` 410 fallback path | Unit: mock 410 response; verify sync token cleared + full sync triggered |
| 24 | 2.5 | Workflows — Flow 3 | `DataSourceRefresh` update | Integration: background sync completes; verify data_source_refresh row updated |

## Risks, Assumptions, Open Questions

| # | Type | Item | Mitigation / Next Step |
|---|---|---|---|
| R1 | Risk | Google OAuth "installed application" flow requires the app to be registered in Google Cloud Console. If flagged for verification review, OAuth may be limited to 100 test users. | Register app in Google Cloud Console before Story 2.1. Use "Testing" mode initially; publish when ready. Document setup steps in README. |
| R2 | Risk | `client_secret.json` must exist at runtime but must not be committed. If a developer clones the repo without it, the app crashes on first auth attempt. | Add a startup check: if credentials file missing, show a setup dialog pointing to README, not an unhandled exception. `client_secret.json` is in `.gitignore`. |
| R3 | Risk | Google's incremental sync token (`syncToken`) expires after ~7 days of inactivity. Users who don't open the app for a week will hit a 410 and trigger a full re-sync. | 410 fallback is already designed (AC #23). Fallback path covered by tests. Acceptable behaviour. |
| R4 | Risk | DPAPI encryption is machine- and user-scoped. If the user reinstalls Windows or migrates to a new machine, stored tokens cannot be decrypted. | On startup decryption failure, log clearly and prompt reconnect. No data loss — calendar data remains in Google. |
| R5 | Risk | The `data_source_refresh` table's `sync_token` column may not have been included in the Epic 1 migration. | **Action before Story 2.2:** Verify `data_source_refresh` schema in `_database-schemas.md`. If `sync_token TEXT` column is missing, add a migration in Story 2.2. |
| A1 | Assumption | The user has a Google account and has provisioned a Google Cloud project with Calendar API enabled and OAuth 2.0 credentials available. | Documented in README onboarding. |
| A2 | Assumption | A single Google Calendar (primary) is synced in Epic 2. Multi-calendar support is deferred to a future epic. | `calendarId` defaults to `"primary"` in all API calls. |
| A3 | Assumption | Default sync range (6 months back, 1 month forward) is sufficient for Tier 1. Range is configurable via `config` table but the UI control is deferred to Epic 3+. | Defaults stored as `gcal_sync_range_months_back = 6` and `gcal_sync_range_months_forward = 1` in `config` table. |
| A4 | Assumption | Conflict resolution in Tier 1 is always GoogleWins — remote state overwrites local on every sync. | Explicit in architecture Key Decisions §5 and `gcal_event` schema conflict resolution notes. |
| Q1 | Open Question | Should the sync status indicator (green/grey) be rendered in the WinUI 3 CalendarView cell template or as an overlay? | Defer to Epic 3 UX design. This spec defines the data contract (`SyncStatus` per `DateOnly`); rendering is Epic 3's concern. |
| Q2 | Open Question | Does `data_source_refresh` currently have a `sync_token` column in the Epic 1 schema? | **Action:** Verify against `_database-schemas.md` before Story 2.2 begins. Add migration if missing. |

## Test Strategy Summary

**Test levels and coverage targets:**

| Level | Framework | Scope | Coverage Target |
|---|---|---|---|
| Unit | xUnit + Moq + FluentAssertions | `GoogleCalendarService`, `SyncManager`, `DpapiTokenStorageService`, `SyncStatusService`, `BackgroundSyncService` | All public methods; all error and edge paths |
| Integration | xUnit + in-memory SQLite | `GcalEventRepository`, `GcalEventVersionRepository`, `DataSourceRefresh` persistence; full sync pipeline against mock HTTP | All AC items tagged "Integration" in traceability table |
| Manual | Developer testing against live account | OAuth browser flow, real GCal sync, background timer, offline simulation | AC #1, #7, #17 verified against live Google account |

**Key test scenarios by story:**

- **2.1 Auth:** Mock `GoogleWebAuthorizationBroker`; verify token stored as ciphertext not plaintext; verify startup silent auth; verify revoke clears AppMetadata row; verify error paths return `OperationResult.Success = false`.
- **2.2 Sync:** Mock `HttpMessageHandler` returning paginated GCal event JSON; assert correct row counts in in-memory SQLite after sync; test cancellation token mid-flight; test all-day and recurring event mapping.
- **2.3 Version history:** Seed `gcal_event` with existing rows; run sync with updated/deleted API response; assert `gcal_event_version` rows created with correct `changed_by`; assert `is_deleted` flag set on deleted events.
- **2.4 Status:** Seed `gcal_event` with known dates; assert `SyncStatusService` returns `Synced` for seeded dates and `NotSynced` for others.
- **2.5 Background sync:** Unit-test `BackgroundSyncService` with mock `ISyncManager` and mock `INetworkMonitor`; assert sync called on timer fire when online; assert sync skipped silently when offline; assert 410 triggers full re-sync path in `SyncManager`.

**Test data location:** `GoogleCalendarManagement.Tests/TestData/` — add `sample_gcal_events_page1.json`, `sample_gcal_events_page2.json`, `sample_gcal_events_deleted.json`.

**Not tested (deferred):** UI rendering of green/grey indicators (Epic 3), multi-calendar scenarios, actual DPAPI round-trip in CI — use `ITokenStorageService` abstraction with an in-memory mock implementation in all automated tests.

# Story 5.8: Toggl Reports API for Historical Import

Status: review

## Story

As a **user**,
I want **to import Toggl sleep entries from any date range, including dates more than 3 months ago**,
so that **I can back-fill historical sleep data without hitting the regular API's 3-month limitation**.

## Acceptance Criteria

1. **AC-5.8.1 — `TogglApiClient.GetTimeEntriesAsync` uses the Reports API v3:**
   The implementation calls `POST https://api.track.toggl.com/reports/api/v3/workspace/{workspaceId}/search/time_entries` instead of the regular `GET /me/time_entries` endpoint. The `ITogglApiClient` interface signature is unchanged — no callers need updating.

2. **AC-5.8.2 — Workspace ID is fetched and cached:**
   Before the first Reports API call (or when the API token changes), the client fetches `default_workspace_id` from `GET https://api.track.toggl.com/api/v9/me` and caches it in memory on the singleton. If the workspace ID cannot be determined, import fails with a clear error message.

3. **AC-5.8.3 — Full pagination is supported:**
   When the Reports API response includes an `X-Next-Row-Number` header, the client issues additional paged requests (passing `first_row_number` in the POST body) until that header is absent. All pages are merged into the single `IReadOnlyList<TogglTimeEntryDto>` return value.

4. **AC-5.8.4 — Reports API response is mapped to `TogglTimeEntryDto`:**
   Each Reports API time entry is mapped as follows:
   - `id` (long) → `Id`
   - `description` (string?) → `Description`
   - `start` (string ISO 8601) → `Start`
   - `stop` (string?) → `Stop`
   - `dur` (long, **milliseconds**) → `Duration` as `(int)(dur / 1000)` (converts to seconds)
   - `pid` (long?) → `ProjectId`
   - `project` (string?) → `ProjectName`
   - `tags` (string[]?) → `Tags`

5. **AC-5.8.5 — Import of entries older than 3 months succeeds:**
   A Toggl sleep import for a date range entirely older than 90 days completes without errors and returns the correct filtered entries.

6. **AC-5.8.6 — `TogglSleepImportService` requires no changes:**
   The service code is untouched. All existing `TogglSleepImportServiceTests` pass without modification.

7. **AC-5.8.7 — Rate limit retry is preserved for Reports API calls:**
   Each POST request to the Reports API goes through `SendWithRateLimitRetryAsync`, retrying once on HTTP 429 using the `Retry-After` delay, consistent with existing behavior.

---

## Tasks / Subtasks

- [x] **Task 1: Widen HttpClient base URL and update existing relative paths**
  - [x] In `App.xaml.cs`, change the `TogglApiClient` HttpClient `BaseAddress` from `https://api.track.toggl.com/api/v9/` to `https://api.track.toggl.com/`
  - [x] In `TogglApiClient.cs`, update all existing relative URL strings to include the `api/v9/` segment:
    - `"me"` → `"api/v9/me"`
    - the `me/time_entries?...` template string → `"api/v9/me/time_entries?..."`
  - [x] Verify `TestConnectionAsync` and the old `GetTimeEntriesAsync` still compile and all current tests pass after this change

- [x] **Task 2: Add workspace ID caching**
  - [x] Add private fields to `TogglApiClient`: `_cachedWorkspaceId` (nullable int) and `_cachedWorkspaceToken` (nullable string)
  - [x] Add private `GetOrFetchWorkspaceIdAsync(string apiToken, CancellationToken ct) → Task<int>`: return cached ID if `_cachedWorkspaceToken == apiToken`, else call `GET api/v9/me`, parse `default_workspace_id` from the JSON response via `JsonDocument`, cache both values, return the ID
  - [x] If `/me` returns non-success or `default_workspace_id` is absent/zero, throw `TogglApiException` with a descriptive message

- [x] **Task 3: Add Reports API DTOs**
  - [x] Add `Services/TogglReportsTimeEntryDto.cs` — deserialization record with `JsonPropertyName` attributes:
    - `Id` (long, `"id"`), `Description` (string?, `"description"`), `Start` (string, `"start"`), `Stop` (string?, `"stop"`), `Dur` (long, `"dur"`), `Pid` (long?, `"pid"`), `Project` (string?, `"project"`), `Tags` (string[]?, `"tags"`)
  - [x] Add `Services/TogglReportsSearchRequestDto.cs` — serialization record (use `JsonPropertyName` attributes, mark nullable fields with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`):
    - `StartDate` (string, `"start_date"`), `EndDate` (string, `"end_date"`), `OrderBy` (string, `"order_by"`), `OrderDir` (string, `"order_dir"`), `FirstRowNumber` (int?, `"first_row_number"`)

- [x] **Task 4: Replace `GetTimeEntriesAsync` implementation with Reports API**
  - [x] Replace the body of `GetTimeEntriesAsync` in `TogglApiClient`:
    1. Call `GetOrFetchWorkspaceIdAsync(apiToken, ct)` to get `workspaceId`
    2. Set `inclusiveEnd = end.AddDays(1)` (same exclusive-end convention as before)
    3. Loop:
       - Build `TogglReportsSearchRequestDto` with `start_date`, `end_date`, `order_by = "date"`, `order_dir = "ASC"`, and `first_row_number` (null on first iteration)
       - Create `HttpRequestMessage` with `HttpMethod.Post` and absolute URL `https://api.track.toggl.com/reports/api/v3/workspace/{workspaceId}/search/time_entries`
       - Set `Authorization` header (same Basic auth pattern via `CreateRequest` helper, or inline)
       - Set `Content` to `JsonContent.Create(requestBody, options: JsonOptions)`
       - Send via `SendWithRateLimitRetryAsync`
       - Deserialize response body as `List<TogglReportsTimeEntryDto>`
       - Append to accumulator list after mapping each entry to `TogglTimeEntryDto` (see AC-5.8.4)
       - Read `X-Next-Row-Number` response header; if present and non-empty, set `first_row_number` and continue; otherwise break
    4. Return accumulated list
  - [x] Map helper (inline or private static): `TogglReportsTimeEntryDto → TogglTimeEntryDto`, converting `dur / 1000` for `Duration`
  - [x] Note: the Reports API does not return running entries (negative duration), so the `duration >= 0` filter in the service remains correct and harmless

- [x] **Task 5: Unit tests for `TogglApiClient`**
  - [x] Add `GoogleCalendarManagement.Tests/Unit/Services/TogglApiClientTests.cs`
  - [x] Use a custom `FakeHttpMessageHandler` inner class (no new NuGet packages) that captures requests and returns configured `HttpResponseMessage` objects
  - [x] `GetTimeEntriesAsync_MapsReportsApiEntryToDto` — verify duration milliseconds→seconds, `stop` field, and all other mapped fields
  - [x] `GetTimeEntriesAsync_FetchesWorkspaceIdFromMe` — verify that `/me` is called on first import and workspace ID appears in the Reports API URL
  - [x] `GetTimeEntriesAsync_CachesWorkspaceId` — verify `/me` is called only once across two sequential `GetTimeEntriesAsync` calls with the same token
  - [x] `GetTimeEntriesAsync_PaginatesUntilHeaderAbsent` — configure handler to return `X-Next-Row-Number` on first response and absent on second; verify two POST requests were made and results are merged
  - [x] `GetTimeEntriesAsync_RetriesOnRateLimit` — configure handler to return HTTP 429 with `Retry-After: 0` then HTTP 200; verify two POST attempts and successful result

- [x] **Task 6: Run full test suite**
  - [x] `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64 --no-restore` — 352/354 passing; 2 pre-existing failures in `EventDetailsPanelViewModelTests` unrelated to this story

---

## Dev Notes

### Reports API v3 Endpoint

```
POST https://api.track.toggl.com/reports/api/v3/workspace/{workspace_id}/search/time_entries
Authorization: Basic <base64(api_token:api_token)>
Content-Type: application/json

{
  "start_date": "YYYY-MM-DD",
  "end_date": "YYYY-MM-DD",
  "order_by": "date",
  "order_dir": "ASC",
  "first_row_number": null       // omit (or null) on first page; set to X-Next-Row-Number for subsequent pages
}
```

Response: JSON **array** of time entry objects (not wrapped in `{data: [...]}`):
```json
[
  {
    "id": 123456789,
    "description": "Deep sleep",
    "start": "2025-01-01T22:00:00+00:00",
    "stop": "2025-01-02T06:00:00+00:00",
    "dur": 28800000,
    "pid": null,
    "project": null,
    "tags": ["sleep"]
  }
]
```

Pagination: repeat requests with `"first_row_number": <header-value>` while `X-Next-Row-Number` response header is present.

### Workspace ID from `/me`

```json
{
  "id": 12345,
  "default_workspace_id": 67890,
  ...
}
```

Parse with `JsonDocument.Parse(stream)` and read `.RootElement.GetProperty("default_workspace_id").GetInt32()`.

### HttpClient Base URL Change

The registered `HttpClient` base URL changes from `https://api.track.toggl.com/api/v9/` to `https://api.track.toggl.com/`. All existing relative URLs in `TogglApiClient` must be prefixed with `api/v9/`. Absolute URLs (used for the Reports API) work regardless of the base URL setting.

### Duration Conversion

Regular API: `duration` field is in **seconds**.
Reports API: `dur` field is in **milliseconds** → convert with `(int)(dto.Dur / 1000)`.

The existing `IsCompletedSleepEntry` guard `entry.Duration >= 0` is safe: the Reports API does not surface running (in-progress) entries, so all returned `dur` values are positive.

### `end_date` Convention

The regular API's `end_date` is exclusive (pass `end.AddDays(1)` to include the selected last day). Apply the same convention to the Reports API `end_date`.

### Creating `HttpRequestMessage` with Absolute URL

Even though the `HttpClient` has a base address, an `HttpRequestMessage` constructed with an absolute URI overrides it:
```csharp
var request = new HttpRequestMessage(
    HttpMethod.Post,
    $"https://api.track.toggl.com/reports/api/v3/workspace/{workspaceId}/search/time_entries");
```

### `JsonContent.Create` for POST Body

Use `System.Net.Http.Json.JsonContent.Create(dto, options: JsonOptions)` (available in `System.Net.Http.Json`, which is included in the .NET runtime) to set the request body as JSON.

### Testing `HttpClient` Without a New Package

Use a `FakeHttpMessageHandler` that extends `HttpMessageHandler`:
```csharp
private sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];
    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        return Task.FromResult(_responses.Dequeue());
    }
}
```
Construct the `TogglApiClient` with `new HttpClient(fakeHandler)`.

### Prerequisites

- Story 5.6 complete (✓ in review): `TogglApiClient`, `ITogglApiClient`, `TogglTimeEntryDto`, `TogglSleepImportService` all exist.

### References

- [TogglApiClient.cs](../../../Services/TogglApiClient.cs) — current implementation
- [ITogglApiClient.cs](../../../Services/ITogglApiClient.cs) — interface (no changes)
- [TogglTimeEntryDto.cs](../../../Services/TogglTimeEntryDto.cs) — existing DTO, target of mapping
- [TogglSleepImportService.cs](../../../Services/TogglSleepImportService.cs) — no changes required
- [App.xaml.cs](../../../App.xaml.cs) — HttpClient registration (base URL update)
- [Toggl Reports API v3 docs](https://engineering.toggl.com/docs/reports/detailed)

---

## Dev Agent Record

### Implementation Plan

- Replace `GetTimeEntriesAsync` implementation entirely with Reports API v3 — keeping the interface signature unchanged so `TogglSleepImportService` and all its tests require zero modification.
- Widen the registered `HttpClient` base URL to just the domain so the same client can reach both the regular API (`api/v9/`) and the Reports API (`reports/api/v3/`) via absolute URLs.
- Cache the workspace ID in memory keyed by API token to avoid a redundant `/me` call on every import.
- Use a factory-function overload of `SendWithRateLimitRetryAsync` so POST request bodies can be recreated on retry (the original approach of cloning headers only would not work for POST).
- Test `TogglApiClient` with a zero-dependency `FakeHttpMessageHandler` rather than adding a new NuGet package.

### Debug Log

- Build failed with file-lock errors (MSB3021/MSB3027) on every attempt because the WinUI app was running and holding `GoogleCalendarManagement.exe`. Compilation succeeded with no C# errors.
- Used `dotnet msbuild -p:BuildProjectReferences=false` to compile just the test project DLL, then manually copied the freshly compiled main project DLL from `obj/x64/…/win-x64/` to the test project `bin/` directory before running with `--no-build`.
- Initial test run showed 5 new `TogglApiClientTests` failing with `JsonException: cannot convert to List<TogglTimeEntryDto>` — caused by the test bin still containing the old `GoogleCalendarManagement.dll`. Resolved by replacing that DLL with the newly compiled one.
- Full suite: 352 passed, 2 pre-existing failures (`EventDetailsPanelViewModelTests`) from in-progress work on other stories; those test files are not modified by this story.

### Completion Notes

- Changed `TogglApiClient.GetTimeEntriesAsync` to call `POST reports/api/v3/workspace/{id}/search/time_entries` instead of `GET api/v9/me/time_entries`, enabling imports of sleep entries from any date range.
- Added lazy workspace ID fetch + in-memory cache keyed by API token (`GetOrFetchWorkspaceIdAsync`).
- Added `TogglReportsTimeEntryDto` and `TogglReportsSearchRequestDto` for the Reports API contract; mapping converts `dur` (milliseconds) → `Duration` (seconds).
- Upgraded `SendWithRateLimitRetryAsync` to accept a `Func<HttpRequestMessage>` factory so POST body content is recreated on the retry attempt.
- All 7 existing `TogglSleepImportServiceTests` pass without modification — the service interface was unchanged.
- 5 new `TogglApiClientTests` cover field mapping, workspace ID caching, pagination, and 429 retry.

## File List

- `App.xaml.cs`
- `Services/TogglApiClient.cs`
- `Services/TogglReportsTimeEntryDto.cs`
- `Services/TogglReportsSearchRequestDto.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/TogglApiClientTests.cs`
- `docs/epic-5-day-select-left-data-panel/stories/5-8-toggl-reports-api-historical-import.md`
- `docs/sprint-status.yaml`

## Change Log

- 2026-05-14: Replaced `GetTimeEntriesAsync` implementation with Toggl Reports API v3 to support historical imports beyond the 3-month regular API limit. Added workspace ID caching, pagination, and 5 new unit tests for `TogglApiClient`.

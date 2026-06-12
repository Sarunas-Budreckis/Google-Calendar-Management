# Story 9.5: Linking Panel — Gaps Lens (W4)

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** high
**Dependencies:** 9.1 (blocking — Linking panel shell + icon strip must exist); 8.11 (blocking — `IClumpBlockProvider` contract + `IClumpBlockProviderRegistry`); 8.12 (blocking — `ILinkService` link/ignore/unlink + undo); 8.13 (blocking — `IEventPickerService` + `EventPickerDialog`)

---

## Story

As a user working through raw-data accountability,
I want a Gaps lens in the Linking panel that shows all raw datapoints in scope **not covered by any approved event**, clumped cross-source by contiguous blank period,
so that I can quickly find unaccounted-for time windows, create events to fill them, or intentionally dismiss the data — eliminating "dark" periods in my timeline.

---

## Acceptance Criteria

1. **Gaps lens tab/button** is present in the Linking panel (alongside By-Source and By-Event lenses from 9.3/9.4). Selecting it activates the Gaps lens; switching to another lens or panel deactivates it.

2. **Gap detection query:** For the active scope (view-following by default; see AC #6), fetch all `data_point` rows whose `[start_utc, end_utc]` time range does **not** overlap any `lifecycle = 'approved'` event. An approved event fully covers a datapoint if `event.start_datetime <= dp.start_utc AND event.end_datetime >= dp.end_utc`. Partial overlap (event covers part of datapoint window) still counts as covered — the datapoint is excluded from the gap list.

3. **Cross-source gap clumping:** Group the uncovered datapoints into **gap clusters** by contiguous blank period: two datapoints (from any source) belong to the same gap if there is no approved event separating them and the gap between them is ≤ 24 hours. A single gap cluster is always ≤ 24 hours in total extent (split at 24-hour boundaries if raw data spans more). Each gap cluster has a computed `GapStart` = min(`start_utc`) of its datapoints and `GapEnd` = max(`end_utc`) of its datapoints.

4. **Gap list UI:** The lens shows a scrollable list of gap cards. Each card displays:
   - Time range (local time): `"Mon Jun 9, 14:00 – 16:30"` or `"14:00 – 16:30 (2h 30m)"`
   - Source count + datapoint count: `"3 sources, 14 datapoints"`
   - Source pill chips (source display names, colored with source color, max 4 shown then `+N more`)
   - Action buttons: **Create event** and **Ignore all**

5. **"Create event" action:** Opens the standard event-creation flow pre-filled with the gap's time extent (`GapStart`–`GapEnd`), creating a `lifecycle = 'candidate'` (translucent) event. After creation, all datapoints in the gap are **auto-linked** to the new event via `ILinkService.LinkClumpAsync(gapDataPointIds, newEventId)` with `origin = 'manual'` (the user explicitly chose this action). The gap card disappears from the list (its datapoints are now covered). The calendar refreshes.

6. **"Ignore all" action:** Calls `ILinkService.IgnoreClumpAsync(gapDataPointIds)` — all datapoints in the gap get `state = 'ignored'`. Returns an `action_group_id` for undo. The gap card disappears. A toast notification appears: `"X datapoints ignored"` with an **Undo** button that calls `ILinkService.UndoActionGroupAsync(actionGroupId)` and re-triggers a refresh.

7. **Scope — view-following (default):** The gap query covers the date range currently visible in the calendar view. A scope indicator shows the active date range (e.g., `"Jun 2026"` for month view, `"Jun 9–15"` for week view).

8. **Empty state:** When no gaps are found (all datapoints covered), show a centered `"✓ No gaps — all data accounted for"` message with the current scope shown.

9. **Loading state:** While the gap query is running, show a `ProgressRing` in place of the list. The lens must not block the UI thread (all queries are `async`).

10. **Gap list ordering:** Gaps are sorted by `GapStart` ascending (chronological).

11. **No regression:** Switching to the Gaps lens and back does not affect the calendar rendering, event selection, or the By-Source/By-Event lens state. The By-Source and By-Event lenses preserve their selection when switching away and back within the same session.

12. **Unit tests** cover:
    - `GapDetectionService.GetGapsAsync` — datapoint covered by approved event is excluded; datapoint with no event is included; two cross-source datapoints with gap ≤ 24h merge into one cluster; datapoints > 24h apart form two clusters.
    - `GapClusterViewModel` — `IgnoreAllCommand.CanExecute` true when gap has datapoints; on execute calls `ILinkService.IgnoreClumpAsync` with correct ids; on success fires `GapResolved` event.
    - `GapsLensViewModel` — empty state when service returns no gaps; loading state toggles correctly; items sorted by `GapStart`.

---

## Tasks / Subtasks

- [ ] Task 1: `GapDetectionService` — query + clumping logic (AC: #2, #3)
  - [ ] 1.1 Create `Services/DataLinking/GapDetectionService.cs` implementing `IGapDetectionService`
  - [ ] 1.2 Create `Services/DataLinking/IGapDetectionService.cs`:
    ```csharp
    public interface IGapDetectionService
    {
        Task<IReadOnlyList<GapCluster>> GetGapsAsync(
            DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    }
    ```
  - [ ] 1.3 Create `Services/DataLinking/GapCluster.cs`:
    ```csharp
    public sealed record GapCluster(
        DateTime GapStartUtc,
        DateTime GapEndUtc,
        IReadOnlyList<ClumpDataPoint> DataPoints);  // ClumpDataPoint from 8.11
    ```
  - [ ] 1.4 Implement `GetGapsAsync`: query `data_point` rows in the time range; for each datapoint LEFT JOIN approved events where `event.start_datetime <= dp.start_utc AND event.end_datetime >= dp.end_utc AND event.lifecycle = 'approved'`; keep rows where no matching event exists. Group remaining datapoints into clusters: sort by `start_utc`; greedily merge adjacent datapoints where gap ≤ 24h and total cluster span ≤ 24h.
  - [ ] 1.5 Register `IGapDetectionService` → `GapDetectionService` as singleton in `App.xaml.cs`

- [ ] Task 2: `GapCluster` domain types + `GapClusterViewModel` (AC: #4, #5, #6)
  - [ ] 2.1 Create `ViewModels/GapClusterViewModel.cs` — constructor: `(GapCluster gap, ILinkService linkService, IEventCreationService eventCreationService, Action<string> onGapResolved)`
  - [ ] 2.2 Expose:
    - `string TimeRangeText` — `GapStartUtc.ToLocalTime()` formatted as `"ddd MMM d, HH:mm – HH:mm (Xh Ym)"` or just `"HH:mm – HH:mm"` if same day
    - `string SummaryText` — `"N sources, M datapoints"`
    - `IReadOnlyList<SourcePillViewModel> SourcePills` — max 4 sources shown; each `SourcePillViewModel` has `DisplayName` and `Color` (from `IDataSourceService`)
    - `string OverflowText` — `"+N more"` when more than 4 sources; empty otherwise
    - `IAsyncRelayCommand CreateEventCommand`
    - `IAsyncRelayCommand IgnoreAllCommand`
  - [ ] 2.3 `IgnoreAllCommand.Execute`: call `ILinkService.IgnoreClumpAsync(DataPointIds)`, capture `action_group_id`, fire `onGapResolved(actionGroupId)`
  - [ ] 2.4 `CreateEventCommand.Execute`: call `IEventCreationService.CreateCandidateFromGapAsync(GapStartUtc, GapEndUtc)` → returns new `eventId`; call `ILinkService.LinkClumpAsync(DataPointIds, eventId)`; fire `onGapResolved(null)` (no undo for create+link in this story — create event undo is standard event deletion)

- [ ] Task 3: `GapsLensViewModel` (AC: #1, #7, #8, #9, #10, #11)
  - [ ] 3.1 Create `ViewModels/GapsLensViewModel.cs` — inherits `ObservableObject`
  - [ ] 3.2 Constructor: `(IGapDetectionService gapDetectionService, ILinkService linkService, IEventCreationService eventCreationService, IDataSourceService dataSourceService, ICalendarViewStateService calendarViewState)`
  - [ ] 3.3 Properties:
    - `ObservableCollection<GapClusterViewModel> Gaps`
    - `bool IsLoading` (shows `ProgressRing`)
    - `bool IsEmpty` (true when loaded + 0 gaps — shows "✓ No gaps" message)
    - `string ScopeText` (e.g. `"Jun 2026"` from calendar view state)
  - [ ] 3.4 `RefreshAsync(CancellationToken ct)`: set `IsLoading = true`; compute `fromUtc`/`toUtc` from calendar view state; call `IGapDetectionService.GetGapsAsync`; project to `GapClusterViewModel[]` sorted by `GapStartUtc`; update `Gaps` collection on UI thread via `DispatcherQueue.TryEnqueue`; set `IsLoading = false`; update `IsEmpty`
  - [ ] 3.5 Subscribe to `ICalendarViewStateService.ViewRangeChanged` — call `RefreshAsync` on scope change
  - [ ] 3.6 When a gap is resolved (via `onGapResolved` callback): remove the resolved `GapClusterViewModel` from `Gaps`; update `IsEmpty`; if `actionGroupId` is non-null, trigger undo toast notification

- [ ] Task 4: `GapsLensControl` XAML (AC: #4, #8, #9)
  - [ ] 4.1 Create `Views/GapsLensControl.xaml` — UserControl
  - [ ] 4.2 Scope bar at top: `TextBlock` showing `ScopeText` (e.g. `"Jun 2026"`)
  - [ ] 4.3 `ProgressRing` — Visibility bound to `IsLoading` via `BoolToVisibilityConverter`
  - [ ] 4.4 `TextBlock` "✓ No gaps — all data accounted for" — Visibility bound to `IsEmpty` (and `!IsLoading`)
  - [ ] 4.5 `ListView`/`ItemsRepeater` bound to `Gaps`; item template is `GapCardTemplate` (inline or as a sub-UserControl)
  - [ ] 4.6 `GapCardTemplate`: card border; time range TextBlock (bold); summary TextBlock; source pills `ItemsRepeater` (horizontal, each pill is colored rounded TextBlock); overflow TextBlock; `Button` "Create event" → `CreateEventCommand`; `Button` "Ignore all" → `IgnoreAllCommand`

- [ ] Task 5: Wire Gaps lens into the Linking panel shell (AC: #1, #11)
  - [ ] 5.1 In `LinkingPanelViewModel` (from 9.1): add `GapsLensViewModel GapsLens` property; initialize it; expose `ActiveLens` enum/discriminated union that switches between By-Source / By-Event / Gaps views
  - [ ] 5.2 In `LinkingPanelControl.xaml` (from 9.1): add the Gaps lens tab button in the lens selector (alongside By Source / By Event); show `GapsLensControl` when `ActiveLens == Gaps`

- [ ] Task 6: `IEventCreationService` — `CreateCandidateFromGapAsync` (AC: #5)
  - [ ] 6.1 Add `Task<string> CreateCandidateFromGapAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct)` to `IEventCreationService` (or create the interface if it doesn't exist yet — check if 9.1/9.3 already define it)
  - [ ] 6.2 Implementation: create an `Event` row with `lifecycle = 'candidate'`, `publish = 'local_only'`, `summary = "New event"` (editable), `start_datetime = startUtc`, `end_datetime = endUtc`; mint `event_id` via `EventIdentityService.MintEventId()`; return `event_id`
  - [ ] 6.3 After candidate creation, send `WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(eventId))` so the calendar renders the translucent candidate

- [ ] Task 7: Undo toast for "Ignore all" (AC: #6)
  - [ ] 7.1 Reuse the existing undo toast pattern (see `spec-delete-ux-auto-stage-candidate-undo-toast.md` for the toast infrastructure already in place)
  - [ ] 7.2 On gap resolved with a non-null `actionGroupId`: show toast `"X datapoints ignored"` with `Undo` button that calls `ILinkService.UndoActionGroupAsync(actionGroupId)`, then refreshes the lens

- [ ] Task 8: Unit tests (AC: #12)
  - [ ] 8.1 Create `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/GapDetectionServiceTests.cs`
    - `GetGaps_DatapointCoveredByApprovedEvent_IsExcluded`
    - `GetGaps_DatapointWithNoEvent_IsIncluded`
    - `GetGaps_TwoCrossSourceDatapoints_WithGapUnder24h_MergeIntoOneCluster`
    - `GetGaps_TwoDatapoints_WithGapOver24h_FormSeparateClusters`
    - `GetGaps_ClusterExceeding24h_SplitsAtBoundary`
  - [ ] 8.2 Create `GoogleCalendarManagement.Tests/Unit/ViewModels/GapClusterViewModelTests.cs`
    - `IgnoreAllCommand_CanExecute_WhenGapHasDatapoints`
    - `IgnoreAllCommand_Execute_CallsIgnoreClumpAsync_WithCorrectIds`
    - `IgnoreAllCommand_Execute_OnSuccess_FiresGapResolvedCallback`
  - [ ] 8.3 Create `GoogleCalendarManagement.Tests/Unit/ViewModels/GapsLensViewModelTests.cs`
    - `IsEmpty_True_WhenServiceReturnsNoGaps`
    - `IsLoading_TogglesCorrectly_DuringRefresh`
    - `Gaps_SortedByGapStartAscending`

---

## Dev Notes

### Gap detection — SQL pattern

Use raw ADO.NET (same pattern as `CoverageService` from 8.10) to query uncovered datapoints:

```sql
SELECT
    dp.data_point_id,
    dp.source_key,
    dp.source_ref,
    dp.start_utc,
    dp.end_utc
FROM data_point dp
WHERE dp.start_utc >= @fromUtc
  AND dp.end_utc   <= @toUtc
  AND NOT EXISTS (
    SELECT 1 FROM event e
    WHERE e.lifecycle = 'approved'
      AND e.start_datetime <= dp.start_utc
      AND e.end_datetime   >= dp.end_utc
  )
ORDER BY dp.start_utc
```

**Important:** The `event` table uses the unified schema from 8.2 — query `event` (not `gcal_event` or `pending_event`). The `lifecycle` column distinguishes `'approved'` from `'candidate'`. Only `'approved'` events exclude data from the gap list.

**Date format:** `start_utc` and `end_utc` in `data_point` are stored as ISO-8601 strings (per 8.7). Pass parameters as `startUtc.ToString("O")`. `event.start_datetime` / `event.end_datetime` follow the same format (per 8.2).

### Gap clumping algorithm

```csharp
public static IReadOnlyList<GapCluster> BuildClusters(
    IReadOnlyList<ClumpDataPoint> sortedPoints)  // pre-sorted by start_utc
{
    var clusters = new List<GapCluster>();
    if (sortedPoints.Count == 0) return clusters;

    var current = new List<ClumpDataPoint> { sortedPoints[0] };
    var clusterStart = sortedPoints[0].StartUtc;
    var clusterEnd = sortedPoints[0].EndUtc;

    for (int i = 1; i < sortedPoints.Count; i++)
    {
        var dp = sortedPoints[i];
        var gapBetween = dp.StartUtc - clusterEnd;
        var newEnd = dp.EndUtc > clusterEnd ? dp.EndUtc : clusterEnd;
        var newSpan = newEnd - clusterStart;

        if (gapBetween <= TimeSpan.FromHours(24) && newSpan <= TimeSpan.FromHours(24))
        {
            current.Add(dp);
            if (dp.EndUtc > clusterEnd) clusterEnd = dp.EndUtc;
        }
        else
        {
            clusters.Add(new GapCluster(clusterStart, clusterEnd, current.ToList()));
            current = [dp];
            clusterStart = dp.StartUtc;
            clusterEnd = dp.EndUtc;
        }
    }
    clusters.Add(new GapCluster(clusterStart, clusterEnd, current.ToList()));
    return clusters;
}
```

The 24h constraint: both `gapBetween ≤ 24h` **and** the resulting cluster span `≤ 24h` must be satisfied. If adding a datapoint would make the cluster exceed 24h even with gap ≤ 24h, start a new cluster.

### IEventCreationService — check 9.1/9.3 first

Before creating `IEventCreationService`, check whether story 9.1 or 9.3 already defines it for the "create candidate from clump" flow in By-Source. If it exists, **add** the `CreateCandidateFromGapAsync` method to the existing interface — do not create a duplicate service.

If no such service exists yet, create `Services/IEventCreationService.cs` and `Services/EventCreationService.cs`. Candidate creation follows the same pattern as `pending_event`-based creation in pre-8.2 code, but now writes directly to the unified `event` table using `IEventRepository` from 8.3:

```csharp
public async Task<string> CreateCandidateFromGapAsync(
    DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
{
    var ev = new Event
    {
        EventId = _identityService.MintEventId(),
        Summary = "New event",
        StartDatetime = startUtc,
        EndDatetime = endUtc,
        Lifecycle = "candidate",
        Publish = "local_only",
        HasUnpublishedChanges = false,
        SourceSystem = "manual",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        AppLastModifiedAt = DateTime.UtcNow
    };
    await _eventRepository.AddAsync(ev, ct);
    WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(ev.EventId));
    return ev.EventId;
}
```

### ICalendarViewStateService — scope binding

The Gaps lens needs the active calendar view range. Check if `ICalendarViewStateService` (or equivalent) exists from Epic 3/5 stories — it should expose the currently visible date range (week, month, day, year bounds). If no single service exists, query the `CalendarViewModel` or main state directly. **Do not duplicate** view-state tracking — find the existing source of truth.

Possible existing types to check:
- `ViewModels/CalendarViewModel.cs` — look for `VisibleDateRange`, `CurrentViewMode`, `DisplayedDate`
- `Services/ICalendarViewStateService.cs` — may or may not exist
- `Messages/` — may have a `CalendarViewChangedMessage` broadcast on navigation

### Auto-link origin after "Create event from gap"

When the user clicks "Create event" from a gap card, the resulting link calls `ILinkService.LinkClumpAsync(...)`. This is a **manual** operation — the user explicitly chose to link these datapoints. `ILinkService`'s public `LinkClumpAsync` always uses `origin = 'manual'` (enforced inside `LinkService`, not by the caller — per 8.12 design). No special origin flag needed here.

### Undo toast infrastructure

From `docs/spec-delete-ux-auto-stage-candidate-undo-toast.md`: the project already has a toast/notification system. Reuse it rather than creating a new notification mechanism. The toast for "Ignore all" follows the same pattern as the delete-undo toast: transient notification at bottom of screen with action button.

Look for:
- `Services/IToastService.cs` or `Views/ToastControl.xaml`
- `WeakReferenceMessenger` message for showing toasts
- Existing `UndoToastMessage` or equivalent

### Source pills — color lookup

Each `SourcePillViewModel` needs the source's display color. Use `IDataSourceService.GetDataSources()` (or the `DataSource` list from `DataSourcePanelViewModel`) to get `DisplayName` + `Color` for each `source_key`. Source colors use the same system established in Epic 5 (Story 5.9 data-source custom color picker).

Do NOT hardcode source colors — look them up from the existing data source list. Each `DataSource` entity or display model should already carry a `Color` property.

### DI registration summary

In `App.xaml.cs`, add:
```csharp
services.AddSingleton<IGapDetectionService, GapDetectionService>();
// IEventCreationService — add if not already registered by 9.1/9.3
services.AddSingleton<IEventCreationService, EventCreationService>();
```

`GapsLensViewModel` — created per-activation (not singleton); instantiate at the call site in `LinkingPanelViewModel`.

### File placement

```
Services/DataLinking/IGapDetectionService.cs     (new)
Services/DataLinking/GapDetectionService.cs      (new)
Services/DataLinking/GapCluster.cs               (new)
Services/IEventCreationService.cs                (new or extend existing)
Services/EventCreationService.cs                 (new or extend existing)
ViewModels/GapsLensViewModel.cs                  (new)
ViewModels/GapClusterViewModel.cs                (new)
ViewModels/SourcePillViewModel.cs                (new — or reuse if 9.2/9.3 already added it)
Views/GapsLensControl.xaml                       (new)
Views/GapsLensControl.xaml.cs                    (new)
App.xaml.cs                                      (modified — DI registrations)
ViewModels/LinkingPanelViewModel.cs              (modified — add GapsLens, wire lens switching)
Views/LinkingPanelControl.xaml                   (modified — add Gaps lens tab)
GoogleCalendarManagement.Tests/Unit/Services/DataLinking/GapDetectionServiceTests.cs  (new)
GoogleCalendarManagement.Tests/Unit/ViewModels/GapClusterViewModelTests.cs            (new)
GoogleCalendarManagement.Tests/Unit/ViewModels/GapsLensViewModelTests.cs              (new)
```

### What this story does NOT do

- Does **NOT** render gap outlines on the calendar (gray outlines + `+` icon) — that is Story 9.6.
- Does **NOT** build the gap detail panel (top-4 sources, vertical dots) — that is Story 9.7.
- Does **NOT** implement the By-Source lens (9.3) or By-Event lens (9.4) — those are separate stories.
- Does **NOT** add the left icon strip or panel shell — that is Story 9.1.
- Does **NOT** implement new data source providers — all providers come from 8.11's `IClumpBlockProviderRegistry`.
- Does **NOT** create rules or auto-linking automation — gap resolution is always manual.

### Testing framework

xUnit + FluentAssertions + Moq. Unit tests mock `ILinkService` and `IGapDetectionService`. `GapDetectionServiceTests` use in-memory SQLite (same pattern as `CoverageServiceTests` from 8.10):

```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>()
    .UseSqlite(_connection).Options;
using var ctx = new CalendarDbContext(options);
ctx.Database.EnsureCreated();
```

Seed `data_point` rows and `event` rows with raw SQL since `DbSet` may not be exposed for these tables.

### References

- Concepts §2 (vocabulary — gap, clump, covered): [concepts.md](../../epic-8-data-linking/concepts.md)
- Concepts §5 (link table + ignore semantics): [concepts.md §5](../../epic-8-data-linking/concepts.md)
- Concepts §6 (coverage — approved events exclude datapoints): [concepts.md §6](../../epic-8-data-linking/concepts.md)
- Concepts §8 (Gaps W4 workflow spec): [concepts.md §8](../../epic-8-data-linking/concepts.md)
- Story 8.11 (`IClumpBlockProvider` + `ClumpDataPoint`): [8-11-block-clump-provider-contract.md](../../epic-8-data-linking/stories/8-11-block-clump-provider-contract.md)
- Story 8.12 (`ILinkService` — `IgnoreClumpAsync`, `LinkClumpAsync`, `UndoActionGroupAsync`): [8-12-link-table-and-link-ignore-unlink-operations.md](../../epic-8-data-linking/stories/8-12-link-table-and-link-ignore-unlink-operations.md)
- Story 8.13 (`EventPickerDialog` — not directly used but `IEventPickerService` pattern reused): [8-13-link-to-any-event-picker.md](../../epic-8-data-linking/stories/8-13-link-to-any-event-picker.md)
- Story 8.10 (`CoverageService` — SQL pattern for raw ADO.NET + data_point schema): [8-10-coverage-service-and-delete-date-source-integration.md](../../epic-8-data-linking/stories/8-10-coverage-service-and-delete-date-source-integration.md)
- Story 8.2 (unified event table + lifecycle field): [8-2-unified-event-table-and-migration.md](../../epic-8-data-linking/stories/8-2-unified-event-table-and-migration.md)
- Epic 9 overview (story spec + gap clumping rules): [epic-overview.md](../epic-overview.md)
- Undo toast spec: `docs/spec-delete-ux-auto-stage-candidate-undo-toast.md`
- DI registrations: `App.xaml.cs`

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

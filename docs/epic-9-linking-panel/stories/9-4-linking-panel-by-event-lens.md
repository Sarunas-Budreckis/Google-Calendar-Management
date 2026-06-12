# Story 9.4: Linking Panel — By Event Lens (W3)

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 8.12 (blocking — `ILinkService` must exist); 8.10 (blocking — `ICoverageService.GetEventCoverageAsync` must be implemented); 9.1 (blocking — Linking panel container, icon strip, and `LensType` enum must exist)

---

## Story

As a user reviewing a calendar event,
I want to open the By Event lens in the Linking panel to see all raw datapoints that overlap the event's time window, grouped by source with their current link state,
so that I can quickly account for all raw data concurrent with an event through bulk link/ignore actions and see the event's coverage at a glance.

---

## Acceptance Criteria

1. **Right-click context menu:** Right-clicking a calendar event in any calendar view shows a "Show concurrent raw data" `MenuFlyoutItem`. Clicking it: (a) activates the Linking panel (the icon strip switches to the Linking panel if it isn't already active), (b) selects the By Event lens within the Linking panel, and (c) loads data for the clicked event.

2. **Lens header:** The By Event lens header shows the event's `Summary`, formatted local time range (e.g., `"Jun 11, Thu 14:00 – 15:30"`), and a live coverage indicator from `ICoverageService.GetEventCoverageAsync(startUtc, endUtc)` — shown as `CoverageLevelSymbol` + `"N/M linked"`.

3. **Datapoint listing:** All `data_point` rows where `dp.start_utc < event.end_datetime AND dp.end_utc > event.start_datetime` are fetched (via raw SQL — see Dev Notes), joined with `link`, and displayed grouped by `source_key` — each group labeled with `DataSource.DisplayName` for that key (fallback to `source_key` when no matching row exists).

4. **Per-datapoint display:** Each item shows local time range (`StartUtc.ToLocalTime()` → `EndUtc.ToLocalTime()`), a state badge (`"linked"` / `"linked to other"` / `"ignored"` / `"unlinked"`), and an `"auto"` tag when `link.origin = 'auto_rule'`.

5. **"Link all unlinked" action:** A "Link all" button calls `ILinkService.LinkClumpAsync(unlinkedIds, eventId)` where `unlinkedIds` = all datapoints in scope with no link row. Disabled when there are no unlinked datapoints.

6. **"Ignore all unlinked" action:** An "Ignore all" button calls `ILinkService.IgnoreClumpAsync(unlinkedIds)`. Disabled when no unlinked datapoints remain.

7. **Live refresh after bulk action:** After any bulk action, the datapoint list and coverage indicator both refresh automatically (no user interaction required).

8. **Undo:** After each bulk action, an undo control appears showing `"N datapoints linked · Undo"` (or `"N datapoints ignored · Undo"`). Clicking Undo calls `ILinkService.UndoActionGroupAsync(actionGroupId)` then refreshes. Undo disappears after being used or after the next bulk action.

9. **Passive auto-populate from calendar selection:** When the By Event lens is already the active lens and the user clicks a calendar event (normal selection — not right-click), the lens reloads for the newly selected event. The lens does NOT force-switch panels on a plain calendar click; it only force-switches when triggered via the right-click "Show concurrent raw data" entry.

10. **Empty state:** When no datapoints overlap the event's time range, show `"No raw data found for this time window."` with the event's time range below it.

11. **No regression:** Existing calendar event display, event selection, event editing, drilldown panel behavior (Epic 5), and all link/ignore/unlink operations from existing stories are unaffected.

---

## Tasks / Subtasks

- [ ] Task 1: Data service — `ByEventDatapointEntry` + `IByEventLensService` + `ByEventLensService` (AC: #3)
  - [ ] 1.1 Create `Models/ByEventDatapointEntry.cs`:
    ```csharp
    public sealed record ByEventDatapointEntry(
        int DataPointId,
        string SourceKey,
        string SourceRef,
        DateTime StartUtc,
        DateTime EndUtc,
        bool HasLink,           // true when a link row exists
        string? LinkState,      // 'linked' | 'ignored' | null (unlinked)
        string? LinkedEventId,  // non-null when state='linked'
        string? Origin,         // 'manual' | 'auto_rule' | null (unlinked)
        string? ActionGroupId); // non-null when HasLink
    ```
  - [ ] 1.2 Create `Services/IByEventLensService.cs`:
    ```csharp
    public interface IByEventLensService
    {
        Task<IReadOnlyList<ByEventDatapointEntry>> GetOverlappingDatapointsAsync(
            DateTime eventStartUtc, DateTime eventEndUtc, CancellationToken ct = default);
    }
    ```
  - [ ] 1.3 Create `Services/ByEventLensService.cs` — raw SQL via `IDbContextFactory<CalendarDbContext>` (see Dev Notes)
  - [ ] 1.4 Register `IByEventLensService` → `ByEventLensService` as Singleton in `App.xaml.cs`

- [ ] Task 2: View models (AC: #2–#10)
  - [ ] 2.1 Create `Models/DatapointLinkState.cs`:
    ```csharp
    public enum DatapointLinkState { Unlinked, Linked, LinkedToOther, Ignored }
    ```
  - [ ] 2.2 Create `ViewModels/LinkingPanel/ByEventDatapointItemViewModel.cs`:
    - Properties: `int DataPointId`, `string SourceKey`, `DateTime StartUtc`, `DateTime EndUtc`
    - `string TimeRangeText` (computed: `"HH:mm – HH:mm"` in local time)
    - `DatapointLinkState State` (set at construction from `ByEventDatapointEntry` + current `eventId`)
    - `bool IsAutoLinked` (true when `entry.Origin == "auto_rule"`)
    - `string? LinkedEventId`
    - `string StateBadgeText` (computed from `State`: `"linked"` / `"linked to other"` / `"ignored"` / `"unlinked"`)
    - `bool IsUnlinked` → `State == DatapointLinkState.Unlinked`
  - [ ] 2.3 Create `ViewModels/LinkingPanel/ByEventSourceGroupViewModel.cs`:
    - Properties: `string SourceKey`, `string DisplayName`
    - `ObservableCollection<ByEventDatapointItemViewModel> Items`
    - `string GroupSummaryText` (computed: e.g., `"3/5 linked"` — see Dev Notes)
  - [ ] 2.4 Create `ViewModels/LinkingPanel/ByEventLensViewModel.cs` (inherits `ObservableObject`):
    - Constructor: `(IByEventLensService lensService, ILinkService linkService, ICoverageService coverageService, IDataSourceRepository dataSourceRepo, DispatcherQueue dispatcherQueue)`
    - State: `string? EventId`, `string EventSummary`, `string EventTimeRangeText`, `CoverageResult EventCoverage`
    - Display helpers: `CoverageLevelSymbol`, `CoverageCountText` (see Dev Notes — match `DataSourceDayCardViewModel` pattern)
    - `ObservableCollection<ByEventSourceGroupViewModel> Groups`
    - `bool IsLoading`, `bool IsEmpty`, `bool HasPendingUndo`
    - `string? LastUndoLabel`
    - `IAsyncRelayCommand LinkAllCommand` — CanExecute: `!IsLoading && HasUnlinkedDatapoints`
    - `IAsyncRelayCommand IgnoreAllCommand` — CanExecute: `!IsLoading && HasUnlinkedDatapoints`
    - `IAsyncRelayCommand UndoCommand` — calls `UndoActionGroupAsync(_lastActionGroupId)` then `RefreshAsync()`
    - `Task LoadAsync(string eventId, string summary, DateTime startUtc, DateTime endUtc)` — stores event fields, calls `RefreshAsync()`
    - `Task RefreshAsync()` — queries service, rebuilds Groups, refreshes coverage (see Dev Notes)
    - Private: `_lastActionGroupId`, `_startUtc`, `_endUtc` fields

- [ ] Task 3: `ByEventLensControl.xaml` (AC: #2–#6, #8, #10)
  - [ ] 3.1 Create `Views/LinkingPanel/ByEventLensControl.xaml` (`UserControl`)
  - [ ] 3.2 Header: `TextBlock` bound to `EventSummary`; `TextBlock` bound to `EventTimeRangeText`; `TextBlock` bound to `CoverageLevelSymbol` + `CoverageCountText`
  - [ ] 3.3 Action row: "Link all" `Button` (Command=`LinkAllCommand`) + "Ignore all" `Button` (Command=`IgnoreAllCommand`)
  - [ ] 3.4 Undo bar: a horizontal `StackPanel` (Visibility bound to `HasPendingUndo`) containing a `TextBlock` bound to `LastUndoLabel` and a `HyperlinkButton` "Undo" (Command=`UndoCommand`)
  - [ ] 3.5 `ListView` with `CollectionViewSource` `IsSourceGrouped=True` bound to `Groups`; `GroupStyle` header shows `DisplayName` + `GroupSummaryText`; item template shows `TimeRangeText`, `StateBadge` (see Dev Notes for badge styling), `"auto"` tag (`Visibility` bound to `IsAutoLinked`)
  - [ ] 3.6 `ProgressRing` overlay when `IsLoading = true`
  - [ ] 3.7 Empty state `TextBlock` "No raw data found for this time window." + time range sub-text, Visibility bound to `IsEmpty && !IsLoading`
  - [ ] 3.8 Create `Views/LinkingPanel/ByEventLensControl.xaml.cs` — code-behind sets `ViewModel` from property set by `LinkingPanelControl`

- [ ] Task 4: `OpenByEventLensMessage` + integration with 9.1's Linking panel (AC: #1, #9)
  - [ ] 4.1 Create `Messages/OpenByEventLensMessage.cs`:
    ```csharp
    public sealed record OpenByEventLensMessage(
        string EventId,
        string Summary,
        DateTime StartUtc,
        DateTime EndUtc);
    ```
  - [ ] 4.2 In `LinkingPanelViewModel` (from 9.1): add `ByEventLensViewModel ByEventLens` property; instantiate in the constructor with injected services
  - [ ] 4.3 Subscribe to `OpenByEventLensMessage` in `LinkingPanelViewModel` — on receipt: switch to Linking panel (via whatever mechanism 9.1 exposes) + `ActiveLens = LensType.ByEvent` + `await ByEventLens.LoadAsync(msg.EventId, msg.Summary, msg.StartUtc, msg.EndUtc)`
  - [ ] 4.4 In `LinkingPanelControl.xaml` (from 9.1): show `ByEventLensControl` when `ActiveLens == LensType.ByEvent`
  - [ ] 4.5 For AC #9 (passive auto-populate): subscribe to `CalendarEventSelectedMessage` (or equivalent from Epic 3/5) in `LinkingPanelViewModel`; when received and `ActiveLens == LensType.ByEvent`, call `ByEventLens.LoadAsync(...)` — but do NOT switch to the Linking panel or change `ActiveLens`

- [ ] Task 5: Right-click context menu entry (AC: #1)
  - [ ] 5.1 Find the calendar event item template/view in `Views/` (search for `MenuFlyout`, `RightTapped`, or `EventItem` patterns — it may already have a context menu from event-editing stories)
  - [ ] 5.2 Add a `MenuFlyoutItem Text="Show concurrent raw data"` to the existing `MenuFlyout` (or attach a new `MenuFlyout` if none exists via `FlyoutBase.AttachedFlyout`)
  - [ ] 5.3 In the click handler, resolve the event display model from `DataContext` (likely `CalendarEventDisplayModel` from 8.5) and send:
    ```csharp
    WeakReferenceMessenger.Default.Send(new OpenByEventLensMessage(
        ev.EventId, ev.Summary, ev.StartDatetimeUtc, ev.EndDatetimeUtc));
    ```
  - [ ] 5.4 Ensure this is wired in every view that renders events (week, day, month — whichever use the same template; avoid duplicating if a shared `DataTemplate` is used)

- [ ] Task 6: Tests (AC: #11)
  - [ ] 6.1 Create `GoogleCalendarManagement.Tests/Unit/Services/ByEventLensServiceTests.cs` (in-memory SQLite + `EnsureCreated()`):
    - `GetOverlapping_ReturnsOnlyDatapointsInRange` — 3 datapoints (1 fully within event, 1 partially overlapping, 1 entirely outside); assert 2 returned
    - `GetOverlapping_ExcludesInstantOutsideWindow` — instant datapoint (`start == end`) at exact `eventEndUtc`; assert NOT returned (half-open interval)
    - `GetOverlapping_JoinsLinkStateWhenLinked` — linked datapoint; assert `HasLink=true, LinkState="linked", LinkedEventId` set
    - `GetOverlapping_ReturnsNullLinkStateWhenUnlinked` — no link row; assert `HasLink=false, LinkState=null`
    - `GetOverlapping_DetectsAutoLink` — `origin='auto_rule'`; assert `Origin="auto_rule"`
    - `GetOverlapping_LinkedToOtherEvent` — datapoint linked to event-A while querying for event-B; assert `LinkedEventId != event-B`
  - [ ] 6.2 Create `GoogleCalendarManagement.Tests/Unit/ViewModels/ByEventLensViewModelTests.cs` (mocked dependencies):
    - `LinkAllCommand_CannotExecute_WhenNoUnlinkedDatapoints`
    - `LinkAllCommand_CanExecute_WhenUnlinkedDatapointsExist`
    - `LinkAllCommand_CallsLinkClumpAsync_WithUnlinkedIdsOnly`
    - `IgnoreAllCommand_CallsIgnoreClumpAsync_WithUnlinkedIdsOnly`
    - `UndoCommand_CallsUndoActionGroupAsync_ThenRefreshes`
    - `AfterLinkAll_HasPendingUndoIsTrue`
    - `IsEmpty_TrueWhenServiceReturnsNoDatapoints`
    - `CoverageLevelSymbol_MapsCorrectly` (Full → `"●"`, Partial → `"◐"`, None → `"○"`, Full+Total0 → `"—"`)

---

## Dev Notes

### `ByEventLensService` — raw SQL query

Follows the exact same raw-SQL-via-ADO.NET pattern as `CoverageService` (8.10). Use `IDbContextFactory<CalendarDbContext>`:

```csharp
public async Task<IReadOnlyList<ByEventDatapointEntry>> GetOverlappingDatapointsAsync(
    DateTime eventStartUtc, DateTime eventEndUtc, CancellationToken ct = default)
{
    await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync(ct);

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        SELECT dp.data_point_id, dp.source_key, dp.source_ref, dp.start_utc, dp.end_utc,
               l.link_id,        l.event_id,    l.state,       l.origin,    l.action_group_id
        FROM data_point dp
        LEFT JOIN link l ON l.data_point_id = dp.data_point_id
        WHERE dp.start_utc < @evEnd AND dp.end_utc > @evStart
        ORDER BY dp.source_key, dp.start_utc";
    cmd.Parameters.Add(new SqliteParameter("@evStart", eventStartUtc.ToString("O")));
    cmd.Parameters.Add(new SqliteParameter("@evEnd",   eventEndUtc.ToString("O")));

    var results = new List<ByEventDatapointEntry>();
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    while (await reader.ReadAsync(ct))
    {
        var hasLink = !reader.IsDBNull(5); // link_id
        results.Add(new ByEventDatapointEntry(
            DataPointId:    reader.GetInt32(0),
            SourceKey:      reader.GetString(1),
            SourceRef:      reader.GetString(2),
            StartUtc:       DateTime.Parse(reader.GetString(3), null, DateTimeStyles.RoundtripKind),
            EndUtc:         DateTime.Parse(reader.GetString(4), null, DateTimeStyles.RoundtripKind),
            HasLink:        hasLink,
            LinkState:      hasLink ? reader.GetString(7) : null,
            LinkedEventId:  hasLink && !reader.IsDBNull(6) ? reader.GetString(6) : null,
            Origin:         hasLink ? reader.GetString(8) : null,
            ActionGroupId:  hasLink && !reader.IsDBNull(9) ? reader.GetString(9) : null));
    }
    return results;
}
```

**Overlap condition:** `dp.start_utc < eventEndUtc AND dp.end_utc > eventStartUtc` — standard half-open interval test. An instant datapoint (`start == end`) is included only if `start < eventEndUtc AND start > eventStartUtc` (both conditions collapse to the same field).

**DateTime parsing:** `data_point.start_utc`/`end_utc` are ISO-8601 strings (`"O"` format). Use `DateTimeStyles.RoundtripKind` to preserve UTC `Kind`. Do NOT use `DateTime.Parse(str)` without the styles parameter.

**`using` directive needed:** `using System.Globalization;` for `DateTimeStyles`.

### `ByEventLensViewModel` — key implementation patterns

**`DatapointLinkState` derivation** (call in `ByEventDatapointItemViewModel` constructor):
```csharp
private static DatapointLinkState DeriveState(ByEventDatapointEntry entry, string currentEventId)
{
    if (!entry.HasLink) return DatapointLinkState.Unlinked;
    if (entry.LinkState == "ignored") return DatapointLinkState.Ignored;
    return entry.LinkedEventId == currentEventId
        ? DatapointLinkState.Linked
        : DatapointLinkState.LinkedToOther;
}
```

**`HasUnlinkedDatapoints` computed property:**
```csharp
private bool HasUnlinkedDatapoints =>
    Groups.SelectMany(g => g.Items).Any(i => i.State == DatapointLinkState.Unlinked);
```

**`LinkAllCommand` execute body:**
```csharp
var unlinkedIds = Groups
    .SelectMany(g => g.Items)
    .Where(i => i.State == DatapointLinkState.Unlinked)
    .Select(i => i.DataPointId)
    .ToList();
if (unlinkedIds.Count == 0) return;

var groupId = await _linkService.LinkClumpAsync(unlinkedIds, EventId!, ct);
_lastActionGroupId = groupId;
LastUndoLabel = $"{unlinkedIds.Count} datapoints linked · Undo";
HasPendingUndo = true;
await RefreshAsync(ct);
```

**`RefreshAsync` — group building and coverage refresh:**
```csharp
IsLoading = true;
try
{
    var entries = await _lensService.GetOverlappingDatapointsAsync(_startUtc, _endUtc, ct);

    // Build source → display name mapping (query once per load)
    var allSources = await _dataSourceRepo.GetAllSourcesAsync(ct);
    var nameByKey = allSources.ToDictionary(s => s.SourceKey, s => s.DisplayName);

    var newGroups = entries
        .GroupBy(e => e.SourceKey)
        .OrderBy(g => g.Key)
        .Select(g => new ByEventSourceGroupViewModel(
            g.Key,
            nameByKey.TryGetValue(g.Key, out var name) ? name : g.Key,
            g.Select(e => new ByEventDatapointItemViewModel(e, EventId!)).ToList()))
        .ToList();

    var coverage = await _coverageService.GetEventCoverageAsync(_startUtc, _endUtc, ct);

    _dispatcherQueue.TryEnqueue(() =>
    {
        Groups.Clear();
        foreach (var grp in newGroups) Groups.Add(grp);
        IsEmpty = !Groups.Any();
        EventCoverage = coverage;
        OnPropertyChanged(nameof(CoverageLevelSymbol));
        OnPropertyChanged(nameof(CoverageCountText));
        OnPropertyChanged(nameof(HasUnlinkedDatapoints));
        LinkAllCommand.NotifyCanExecuteChanged();
        IgnoreAllCommand.NotifyCanExecuteChanged();
        IsLoading = false;
    });
}
catch
{
    _dispatcherQueue.TryEnqueue(() => IsLoading = false);
    throw;
}
```

**Coverage display helpers** — use the identical pattern established in `DataSourceDayCardViewModel` (Story 8.10):
```csharp
public string CoverageLevelSymbol => EventCoverage.Level switch
{
    CoverageLevel.Full when EventCoverage.Total == 0 => "—",
    CoverageLevel.Full    => "●",
    CoverageLevel.Partial => "◐",
    CoverageLevel.None    => "○",
    _ => "○"
};
public string CoverageCountText => EventCoverage.Total > 0
    ? $"{EventCoverage.Covered}/{EventCoverage.Total} linked"
    : string.Empty;
```

**`DispatcherQueue` injection:** Same pattern as `EventPickerViewModel` (8.13) — caller captures `DispatcherQueue.GetForCurrentThread()` at the UI thread and passes it to the constructor. In `LinkingPanelViewModel`, do this when constructing `ByEventLensViewModel`.

**`GroupSummaryText` on `ByEventSourceGroupViewModel`:**
```csharp
public string GroupSummaryText
{
    get
    {
        var linked = Items.Count(i => i.State == DatapointLinkState.Linked);
        var ignored = Items.Count(i => i.State == DatapointLinkState.Ignored);
        var covered = linked + ignored;
        return $"{covered}/{Items.Count} resolved";
    }
}
```

**`TimeRangeText` on `ByEventDatapointItemViewModel`:**
```csharp
public string TimeRangeText
{
    get
    {
        var start = StartUtc.ToLocalTime();
        var end   = EndUtc.ToLocalTime();
        return start == end
            ? start.ToString("MMM d HH:mm")                         // instant
            : $"{start:MMM d HH:mm} – {end:HH:mm}";                // range
    }
}
```

**`EventTimeRangeText` on `ByEventLensViewModel`:**
```csharp
private string FormatEventTimeRange(DateTime startUtc, DateTime endUtc)
{
    var s = startUtc.ToLocalTime();
    var e = endUtc.ToLocalTime();
    return $"{s:MMM d, ddd HH:mm} – {e:HH:mm}";
    // Example: "Jun 11, Thu 14:00 – 15:30"
}
```

### Integration with 9.1's Linking panel

What 9.1 must provide (match its exact names — do not guess):
- `LinkingPanelViewModel` class with an `ActiveLens` property (type: whatever `LensType` enum 9.1 defines)
- `LensType` enum with at least `BySource`, `ByEvent`, `Gaps` values
- A mechanism to programmatically switch the icon strip to the Linking panel (e.g., a message or a method on the parent VM)
- `LinkingPanelControl.xaml` with a content region for each lens

What this story adds to those:
- `ByEventLensViewModel ByEventLens { get; }` property on `LinkingPanelViewModel`
- `WeakReferenceMessenger` subscription to `OpenByEventLensMessage` in `LinkingPanelViewModel`
- `ByEventLensControl` content shown when `ActiveLens == LensType.ByEvent`

**If 9.3 (By Source lens) is already implemented:** follow its ViewModel/View naming conventions exactly.

### Right-click context menu in WinUI 3

Find the calendar event item template. Search for existing right-click / context menu patterns:
```
grep -rn "MenuFlyout\|RightTapped\|ContextRequested" src/GoogleCalendarManagement/Views/
```

**If a `MenuFlyout` already exists** on event items (e.g., from event-delete or edit UX): add the new `MenuFlyoutItem` to that existing flyout.

**If no `MenuFlyout` exists yet:**
```xml
<!-- On the event item root element in the DataTemplate -->
<Grid RightTapped="EventItem_RightTapped">
    <FlyoutBase.AttachedFlyout>
        <MenuFlyout>
            <MenuFlyoutItem Text="Show concurrent raw data"
                            Click="EventItem_ShowConcurrentRawData_Click"/>
        </MenuFlyout>
    </FlyoutBase.AttachedFlyout>
    <!-- existing event item content -->
</Grid>
```

```csharp
private void EventItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
    => FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);

private void EventItem_ShowConcurrentRawData_Click(object sender, RoutedEventArgs e)
{
    if ((sender as MenuFlyoutItem)?.DataContext is CalendarEventDisplayModel ev)
    {
        WeakReferenceMessenger.Default.Send(new OpenByEventLensMessage(
            ev.EventId, ev.Summary,
            ev.StartDatetimeUtc, ev.EndDatetimeUtc));
    }
}
```

**IMPORTANT:** Verify the actual property names on the event display model — check `CalendarEventDisplayModel` (from Story 8.5) for `EventId`, `StartDatetimeUtc`, `EndDatetimeUtc`. These names come from Story 8.2's unified `event` table; do not guess them.

### Undo infrastructure

Check for an existing undo toast service before implementing a new one:
```
grep -rn "UndoToast\|ShowUndo\|ToastService" src/GoogleCalendarManagement/
```

**If `UndoToastService` or equivalent exists:** Use it. Call it from `ByEventLensViewModel` after each bulk action.

**If not:** Use the in-lens undo bar described in Task 3.4 (a simple `StackPanel` that appears when `HasPendingUndo = true`). This is self-contained and does not require a shared service.

### `IDataSourceRepository.GetAllSourcesAsync` — source name lookup

`IDataSourceRepository` (from Story 5.1) provides `GetAllSourcesAsync()` which returns all `DataSource` rows. Use this to build the `source_key → display_name` mapping in `RefreshAsync`. Cache it in a local variable per `RefreshAsync` call — do not call it per-group in a loop.

If `GetAllSourcesAsync` does not exist under that name, search the interface for the equivalent method that returns all data sources.

### `ILinkService` — method signatures used in this story

From Story 8.12:
```csharp
// Returns action_group_id for undo
Task<string> LinkClumpAsync(IEnumerable<int> dataPointIds, string eventId, CancellationToken ct = default);
Task<string> IgnoreClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);
Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);
```

Match the exact signatures from 8.12 — do not adapt.

### `ICoverageService` — method used in this story

From Story 8.10:
```csharp
Task<CoverageResult> GetEventCoverageAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
```
Returns `CoverageResult(int Total, int Covered, CoverageLevel Level)` where `CoverageLevel ∈ {Full, Partial, None}`.

### State badge styling

For the state badge, use a colored `Border` with a `TextBlock` inside:
- `Linked` → green border/text (e.g., `#22C55E`)
- `LinkedToOther` → amber (e.g., `#F59E0B`)
- `Ignored` → gray (e.g., `#9CA3AF`)
- `Unlinked` → transparent border, muted text

Or reuse whatever badge style is established in Story 9.3 (By Source lens) if it exists.

### Project structure — files to create/modify

| Action | File |
|--------|------|
| Add | `Models/ByEventDatapointEntry.cs` |
| Add | `Models/DatapointLinkState.cs` |
| Add | `Services/IByEventLensService.cs` |
| Add | `Services/ByEventLensService.cs` |
| Add | `Messages/OpenByEventLensMessage.cs` |
| Add | `ViewModels/LinkingPanel/ByEventDatapointItemViewModel.cs` |
| Add | `ViewModels/LinkingPanel/ByEventSourceGroupViewModel.cs` |
| Add | `ViewModels/LinkingPanel/ByEventLensViewModel.cs` |
| Add | `Views/LinkingPanel/ByEventLensControl.xaml` |
| Add | `Views/LinkingPanel/ByEventLensControl.xaml.cs` |
| Modify | `ViewModels/LinkingPanelViewModel.cs` (from 9.1) — add `ByEventLens` property + `OpenByEventLensMessage` subscription |
| Modify | `Views/LinkingPanelControl.xaml` (from 9.1) — add By Event lens content region |
| Modify | Calendar event item view(s) — add `MenuFlyoutItem` for right-click |
| Modify | `App.xaml.cs` — register `IByEventLensService` |
| Add | `GoogleCalendarManagement.Tests/Unit/Services/ByEventLensServiceTests.cs` |
| Add | `GoogleCalendarManagement.Tests/Unit/ViewModels/ByEventLensViewModelTests.cs` |

All namespaces follow the project convention: `GoogleCalendarManagement.*`.

### Testing framework and patterns

xUnit + FluentAssertions + Moq — same as all other tests.

**Service tests** — in-memory SQLite (`Data Source=:memory:`), `EnsureCreated()`. Seed `data_point` and `link` rows with raw SQL (`INSERT INTO data_point ...`) since `DbSet` may not be available directly. Use `DateTime.UtcNow.ToString("O")` for UTC strings.

```csharp
// Minimal link row seed for FK satisfaction:
// event row must exist before linking (FK to event.event_id)
// INSERT INTO event (event_id, lifecycle, publish, ...) VALUES ('evt-1', 'approved', 'local_only', ...)
// INSERT INTO data_point (...) VALUES (...)
// INSERT INTO link (data_point_id, event_id, state, origin, action_group_id, created_at, updated_at) VALUES (...)
```

**ViewModel tests** — mock all four dependencies. Use `new DispatcherQueue` or null (mock `DispatcherQueue` is complex — alternatively, make `_dispatcherQueue` nullable and skip dispatch in tests by executing inline when null).

A clean approach: make `ByEventLensViewModel` testable by accepting an optional `Action<Action> dispatcher` parameter that defaults to `DispatcherQueue.TryEnqueue` in production and `action => action()` in tests.

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

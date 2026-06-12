# Story 9.3: Linking Panel — By Source Lens (W1 + W2 Merged)

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** high
**Dependencies:** 8.11 (block/clump provider — blocking), 8.12 (link table + link/ignore/unlink — blocking), 8.13 (event picker dialog — blocking), 9.1 (left icon strip + panel shell — blocking)

---

## Story

As a user reviewing raw data accountability in the Linking panel,
I want to pick a source, see its unlinked clumps in a date scope I control, and link / ignore / unlink / create-event for each clump with Next/Prev navigation,
so that I can systematically resolve every datapoint for a source without leaving the panel, at my own pace, independent of the calendar view.

---

## Acceptance Criteria

1. The Linking panel (introduced by 9.1) shows a **source list** as its default state; sources are ordered by the hardcoded link order (every source named in a rule appears even with no data in the date scope); rule-less sources with data appear at the end; rule-less sources with no data are hidden.

2. Selecting a source loads its clumps via `IClumpBlockProviderRegistry.GetProvider(sourceKey).GetClumpsAndBlocksAsync(from, to)` for the current scope. If no provider is registered for a source (rule-only sources), the clump list is empty with a "No clump data — source uses rules only" message.

3. **Scope toggle** — two modes selectable per-source (persisted per source key in `SystemState`):
   - **View-following**: `from`/`to` are read from `ICalendarViewRangeProvider.GetCurrentViewDisplayRange()` and update automatically when `CalendarViewRangeChangedMessage` fires.
   - **Custom date range**: two `DatePicker` controls (`RangeStart`, `RangeEnd`) independent of the calendar; defaults to the past 7 days from today.

4. Each clump is rendered as a **ClumpRow** with:
   - Source color swatch (16×16 `Rectangle`, fill from `IDataSourceRepository.GetByKeyAsync(sourceKey).Color`)
   - Time range: `ClumpStartUtc.ToLocalTime()` formatted as `"MMM d HH:mm"` – `ClumpEndUtc.ToLocalTime()` `"HH:mm"`; spans midnight rendered as `"MMM d HH:mm"` – `"MMM d+1 HH:mm"`
   - Datapoint count: `"N datapoints"`
   - Link state badge: none (unlinked), `"linked → <EventSummary>"` (linked), `"ignored"` (ignored) — always visible regardless of state
   - Concurrent-event hints: up to 3 `approved` event summaries whose time overlaps the clump, shown as small chips below the time range; if none, the chip row is hidden
   - Action buttons: **Link**, **Ignore**, **Unlink**, **+ Event** — visible for all clumps; **Unlink** is disabled when the clump has no link row; **Link** and **Ignore** are disabled for fully auto-linked clumps (all datapoints `origin='auto_rule'`) — show a tooltip "Auto-linked by rule; unlink first"

5. **Next/Prev navigation** steps through clumps where at least one datapoint is `state=unlinked` (no link row). The counter reads `"N / M unlinked"` (current unlinked index / total unlinked clumps). Both keyboard (← → or J / K keys when the panel has focus) and buttons work. Next/Prev skip fully-resolved (all-linked or all-ignored) clumps.

6. **Link action**: opens `EventPickerDialog` (8.13) scoped to the clump's time range; on confirmation calls `ILinkService.LinkClumpAsync(dataPointIds, eventId)`. The clump row updates its link state immediately; the clump stays visible with a `"linked → <EventSummary>"` badge.

7. **Ignore action**: calls `ILinkService.IgnoreClumpAsync(dataPointIds)`. The clump row updates to `"ignored"` badge immediately; clump stays visible.

8. **Unlink action**: calls `ILinkService.UnlinkClumpAsync(dataPointIds)`. Clump row reverts to unlinked; the `action_group_id` is returned and stored for the undo toast.

9. **+ Event action**: creates a `lifecycle=candidate` event spanning the clump's time extent (rounded to minute precision using `ClumpStartUtc.ToLocalTime()` and `ClumpEndUtc.ToLocalTime()`). Uses the event-creation path established by Story 8.5 (create via `IEventRepository.CreateCandidateAsync` or equivalent — match whatever 8.5 exposes). After creation, immediately calls `ILinkService.LinkClumpAsync` to link the clump to the new event. Sends `EventUpdatedMessage` so the calendar refreshes and renders the new candidate translucent. The panel does NOT navigate away.

10. All four actions (Link, Ignore, Unlink, + Event) are **undoable**: after each action the app sends `RequestUndoToastMessage` with the returned `action_group_id` and label ("Linked N datapoints", "Ignored N datapoints", "Unlinked N datapoints", "Created candidate + linked N datapoints"). Undo calls `ILinkService.UndoActionGroupAsync(actionGroupId)` and for `+Event` also deletes the candidate event.

11. **Link order soft prompt**: when the selected source has a rule-order predecessor that is not yet fully covered, a non-blocking banner appears above the clump list: `"Consider linking [PredecessorSource] first (not required)"`. The banner has a `Dismiss` button that suppresses it for the session.

12. All clump list operations are **async and cancellation-safe**: switching source or changing scope cancels any in-flight load via `CancellationTokenSource.CancelAsync()` + new CTS. While loading, a `ProgressRing` replaces the list.

13. No regression: existing `DataSourcePanelControl` (Sources and Day Detail panels from 9.1), all event editing flows, and all Epic 8 service contracts are unaffected.

14. Unit tests:
    - `LinkingBySourceViewModelTests`: source list ordered by link order; rule-less sources hidden when no data; scope toggle persists; Next/Prev skips resolved clumps; concurrent-event hints populated from `IEventRepository`; `IgnoreClumpCommand` calls `ILinkService.IgnoreClumpAsync` with correct ids.
    - `LinkOrderServiceTests`: sources present in link order appear first; tie-breaking (same position) sorts alphabetically by display name; rule-less sources with data sort to end; rule-less empty sources hidden.

---

## Tasks / Subtasks

- [ ] Task 1: Define `LinkOrderService` (AC: #1, #11)
  - [ ] 1.1 Create `Services/DataLinking/LinkOrderService.cs` implementing `ILinkOrderService`
  - [ ] 1.2 Hardcode the link order as a static `IReadOnlyList<string>` of `source_key` values — ordered list: `["toggl_entry", "toggl_phone", "call_log", "civ5_session", "comfyui_scan", "spotify_stream", "google_maps_segment", "outlook_calendar", "voice_memo", "chrome_search"]` — verify this matches the order implied by 8.14/8.15 rules; adjust if those stories define a canonical order
  - [ ] 1.3 `GetOrderedSources(IEnumerable<DataSource> allSources, string? scopeFilter)` returns sources in link-order position; rule-less sources with data appended; rule-less empty sources excluded
  - [ ] 1.4 `GetPredecessorWithLowCoverage(string sourceKey, ICoverageService coverage, DateOnly from, DateOnly to)` → returns the nearest predecessor source key whose day-range coverage is `< 1.0`, or null if all predecessors are covered (or no predecessors)
  - [ ] 1.5 Register `ILinkOrderService` as singleton in `App.xaml.cs`

- [ ] Task 2: `ClumpRowViewModel` model (AC: #4, #5)
  - [ ] 2.1 Create `ViewModels/LinkingPanel/ClumpRowViewModel.cs` — properties: `Clump` (ClumpBlockResult), `SourceKey` (string), `LinkState` (enum: Unlinked/Linked/Ignored), `LinkedEventSummary` (string?), `IsAllAutoLinked` (bool), `ConcurrentEventHints` (IReadOnlyList<string>, max 3), `DataPointCount` (int), `TimeRangeText` (string), `IsSelected` (bool)
  - [ ] 2.2 `LinkState` and `LinkedEventSummary` are populated from `ILinkService.GetLinksByDataPointIds(clump.DataPoints.Select(p => p.DataPointId))` — add a batch query overload to `ILinkService` if not present (check 8.12 story for the exact query API, use what exists)
  - [ ] 2.3 `ConcurrentEventHints`: query `IEventRepository.GetEventsForRangeAsync` for the clump's UTC date range; filter `lifecycle=approved`; take first 3 summaries

- [ ] Task 3: `LinkingBySourceViewModel` (AC: #1–#12)
  - [ ] 3.1 Create `ViewModels/LinkingPanel/LinkingBySourceViewModel.cs` — inherits `ObservableObject`
  - [ ] 3.2 Constructor injects: `ILinkOrderService`, `IClumpBlockProviderRegistry`, `ILinkService`, `IEventPickerService` (for concurrent hints), `IDataSourceRepository`, `ICalendarViewRangeProvider`, `ISystemStateRepository`, `TimeProvider`, `DispatcherQueue`, `IEventRepository` (for candidate creation + event hints)
  - [ ] 3.3 `Sources` (ObservableCollection<DataSourceSummaryViewModel>) populated by `ILinkOrderService.GetOrderedSources`; reloads when `DataSourceImportCompletedMessage` fires
  - [ ] 3.4 `SelectedSource` — on set: cancel in-flight load CTS; save scope mode to SystemState; reload `Clumps`; compute predecessor hint
  - [ ] 3.5 `ScopeMode` enum (`ViewFollowing`/`CustomDateRange`); persisted per source key in `SystemState` with key `"linking_scope_{sourceKey}"`. Default = `ViewFollowing`
  - [ ] 3.6 `RangeStart`/`RangeEnd` (DateOnly): default to `(today − 7, today)` for CustomDateRange; used only when `ScopeMode == CustomDateRange`
  - [ ] 3.7 `LoadClumpsAsync(CancellationToken ct)` — resolve from/to based on scope mode; call `GetClumpsAndBlocksAsync`; for each `ClumpBlockResult` build a `ClumpRowViewModel` (populate link state + concurrent hints in parallel); sort clumps by `ClumpStartUtc` ascending; update `Clumps` on UI thread; update `CurrentUnlinkedIndex`/`TotalUnlinkedCount`
  - [ ] 3.8 Subscribe to `CalendarViewRangeChangedMessage` — when `ScopeMode == ViewFollowing`, cancel + reload clumps
  - [ ] 3.9 `SelectedClumpIndex` (int) — tracks position in `Clumps`; `NextClumpCommand` / `PrevClumpCommand` advance to next/prev clump with `LinkState == Unlinked`
  - [ ] 3.10 `LinkClumpCommand` (IAsyncRelayCommand<ClumpRowViewModel>) — open `EventPickerDialog`; on success call `ILinkService.LinkClumpAsync`; refresh the row's `LinkState`; send `RequestUndoToastMessage`
  - [ ] 3.11 `IgnoreClumpCommand` (IAsyncRelayCommand<ClumpRowViewModel>) — call `ILinkService.IgnoreClumpAsync`; refresh row; send `RequestUndoToastMessage`
  - [ ] 3.12 `UnlinkClumpCommand` (IAsyncRelayCommand<ClumpRowViewModel>) — call `ILinkService.UnlinkClumpAsync`; refresh row; send `RequestUndoToastMessage`
  - [ ] 3.13 `CreateEventFromClumpCommand` (IAsyncRelayCommand<ClumpRowViewModel>) — create `lifecycle=candidate` event; link clump to it; send `EventUpdatedMessage` + `RequestUndoToastMessage`
  - [ ] 3.14 `PredecessorHintText` (string?) — populated by `ILinkOrderService.GetPredecessorWithLowCoverage`; null = banner hidden; `DismissPredecessorHintCommand` sets `_predecessorHintDismissed = true` for the session
  - [ ] 3.15 `IsLoading` (bool) — true during `LoadClumpsAsync`; `ProgressRing` binding

- [ ] Task 4: `LinkingPanelControl.xaml` By-Source view (AC: #4, #5, #6)
  - [ ] 4.1 Create `Views/LinkingPanel/LinkingPanelControl.xaml` — UserControl; `DataContext` bound to `LinkingBySourceViewModel`; the panel shell (`UserControl` boundary and panel-switch wiring) is provided by 9.1 — this file is the **content** of the Linking panel tab
  - [ ] 4.2 Top section: source picker `ListView` (vertical, compact rows with source color swatch + name + `"x/y linked"` text from `DataSourceSummaryViewModel.LinkedCountText`). Source list is shown when no source is selected or via a "← Back" breadcrumb
  - [ ] 4.3 Scope toggle (shown when a source is selected): `ToggleButton` pair "View" / "Date range"; when Date range selected, show two `CalendarDatePicker` controls (`RangeStart`, `RangeEnd`)
  - [ ] 4.4 Predecessor hint banner: `Border` with yellow background, `TextBlock` for `PredecessorHintText`, and `Button` "Dismiss"; `Visibility` bound to `PredecessorHintText != null` via `BoolToVisibilityConverter` (or null-to-collapsed converter)
  - [ ] 4.5 Clump list `ListView` bound to `Clumps`; `IsLoading=true` shows a `ProgressRing` in place; item template per `ClumpRowViewModel` (see AC #4)
  - [ ] 4.6 Per-clump `DataTemplate`: color swatch rectangle + time range `TextBlock` + datapoint count `TextBlock` + link state `TextBlock` (conditional style: red = unlinked, green = linked, grey = ignored) + concurrent hints `ItemsControl` (horizontal chips) + action button bar (`StackPanel` Horizontal: Link, Ignore, Unlink, +Event buttons)
  - [ ] 4.7 Nav bar (bottom of clump list section): `← Prev`, `"N / M unlinked"` `TextBlock`, `Next →` + clump count `TextBlock`; `← Prev` and `Next →` bound to `PrevClumpCommand` / `NextClumpCommand`
  - [ ] 4.8 Keyboard handling in `Views/LinkingPanel/LinkingPanelControl.xaml.cs`: `KeyDown` handler on the `ListView` — `Key.Right` or `Key.K` → `NextClumpCommand`; `Key.Left` or `Key.J` → `PrevClumpCommand`

- [ ] Task 5: Wire panel into 9.1 shell (AC: #1)
  - [ ] 5.1 In the panel-switching control introduced by 9.1, add `LinkingPanelControl` as the content for the Linking icon tab — check how 9.1 defines the `ILinkingPanelContent` slot or equivalent; follow whatever wiring pattern 9.1 established
  - [ ] 5.2 Register `LinkingBySourceViewModel` in `App.xaml.cs` DI — as transient (it holds per-session state); the panel control creates it via DI at instantiation
  - [ ] 5.3 Verify `ILinkOrderService`, `IClumpBlockProviderRegistry` (from 8.11), `ILinkService` (from 8.12), `IEventPickerService` (from 8.13) are all registered; add `// TODO` comment placeholders for any not yet present from prior stories

- [ ] Task 6: Unit tests (AC: #14)
  - [ ] 6.1 Create `GoogleCalendarManagement.Tests/Unit/ViewModels/LinkingPanel/LinkingBySourceViewModelTests.cs` — mock all injected services; use in-memory `ObservableCollection` assertions
  - [ ] 6.2 Test: `Sources_OrderedByLinkOrder` — feed 3 sources (1 in link order pos 2, 1 in pos 5, 1 rule-less with data); assert list order matches link order positions, rule-less at end
  - [ ] 6.3 Test: `Sources_HidesRuleLessSourcesWithNoData` — feed rule-less source with 0 datapoints in scope; assert not present in `Sources`
  - [ ] 6.4 Test: `ScopeMode_Persists` — select source, set `ScopeMode = CustomDateRange`; simulate re-select same source; assert mode restored from SystemState mock
  - [ ] 6.5 Test: `NextClump_SkipsResolvedClumps` — set up clumps [unlinked, linked, ignored, unlinked]; call `NextClumpCommand` from index 0; assert `SelectedClumpIndex` jumps to index 3
  - [ ] 6.6 Test: `ConcurrentEventHints_PopulatedFromEventRepository` — clump spans 14:00–15:00; seed two `approved` events overlapping that range; assert `ClumpRowViewModel.ConcurrentEventHints.Count == 2`
  - [ ] 6.7 Test: `IgnoreClumpCommand_CallsService_WithCorrectDataPointIds` — verify `ILinkService.IgnoreClumpAsync` called with all datapoint ids from the clump
  - [ ] 6.8 Create `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/LinkOrderServiceTests.cs`
  - [ ] 6.9 Test: sources in link order appear before rule-less sources; rule-less sources with data at end; rule-less empty sources absent

---

## Dev Notes

### What 9.1 provides

Story 9.1 creates the left icon strip and the three-panel container (Sources, Day Detail, Linking). It establishes:
- The `LinkingPanelControl` host slot in the panel container
- The panel-switching mechanism (icon strip selects which panel is visible)
- Any shared panel shell infrastructure (breadcrumb navigation model, panel width)

**Match the exact wiring pattern 9.1 defines.** If 9.1 uses a `ContentControl` with `DataTemplate` selection or a `Frame` with navigation, follow it. Do not invent a new panel-hosting mechanism.

### What 8.11 provides

```csharp
// Services/DataLinking/IClumpBlockProviderRegistry.cs
public interface IClumpBlockProviderRegistry
{
    IClumpBlockProvider? GetProvider(string sourceKey);
    IReadOnlyList<IClumpBlockProvider> AllProviders { get; }
}

// Services/DataLinking/IClumpBlockProvider.cs
public interface IClumpBlockProvider
{
    string SourceKey { get; }
    Task<IReadOnlyList<ClumpBlockResult>> GetClumpsAndBlocksAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
```

Convert `DateOnly from/to` to `DateTime` UTC via `from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime()` and `to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime()` (exclusive end for midnight boundary).

Sources that have rules but no clump provider (e.g. Spotify at rule-engine level) will have `GetProvider(sourceKey)` return `null`. Show "Source uses rules only — no manual clumps" in the clump area.

### What 8.12 provides

```csharp
// Services/ILinkService.cs — key methods for this story
Task<string> LinkClumpAsync(IEnumerable<int> dataPointIds, string eventId, CancellationToken ct = default);
Task<string> IgnoreClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);
Task<string> UnlinkClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);
Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);
Task<Link?> GetLinkAsync(int dataPointId, CancellationToken ct = default);
```

To get link state for all datapoints in a clump, call `GetLinkAsync` for each in parallel:
```csharp
var links = await Task.WhenAll(clump.DataPoints
    .Select(dp => _linkService.GetLinkAsync(dp.DataPointId, ct)));
```
A clump is "unlinked" if any datapoint has `links[i] == null`. It is "linked" if all datapoints have `links[i].State == "linked"` (they may point to different events — show the most recent event summary or "multiple events"). It is "ignored" if all are `state='ignored'`. It is "mixed" if some are linked and some are ignored — display as "partially resolved" with the action buttons still active.

### What 8.13 provides

```csharp
// Views/EventPickerDialog — created per invocation
var dialog = new EventPickerDialog(new EventPickerViewModel(
    _eventPickerService, _linkService,
    new DateTimeOffset(clump.ClumpStartUtc, TimeSpan.Zero),
    new DateTimeOffset(clump.ClumpEndUtc, TimeSpan.Zero),
    dataPointIds));
dialog.XamlRoot = this.XamlRoot; // set from code-behind
var result = await dialog.ShowAsync();
```

The dialog handles the link write internally (calls `ILinkService.LinkClumpAsync` on confirm). After `ShowAsync()` returns `ContentDialogResult.Primary`, refresh the clump row's link state — the service has already been called.

### Creating a candidate event from a clump

Story 8.5 updates the event-creation path so that `lifecycle=candidate` events can be created programmatically. By the time 9.3 is implemented, use whatever `IEventRepository.CreateAsync(...)` or `IEventRepository.CreateCandidateAsync(...)` overload 8.5 exposes. The key fields:

```csharp
// DO NOT use IPendingEventDraftService.CreateDraftAsync — that method creates
// PendingEvent (old model). By Epic 9, use the new IEventRepository create path
// from Story 8.5. Match the exact method signature 8.5 defines.
// Minimal candidate event:
var candidate = new Event {
    EventId = _eventIdentityService.MintEventId(),
    Lifecycle = "candidate",
    Publish = "local_only",
    HasUnpublishedChanges = false,
    StartDatetime = clump.ClumpStartUtc,
    EndDatetime = clump.ClumpEndUtc,
    IsAllDay = false,
    // Summary: leave empty (user can edit in event panel after creation)
};
```

After creating the event, call `ILinkService.LinkClumpAsync(dataPointIds, candidate.EventId)` to link the clump. Both operations should be wrapped in a logical group: the `action_group_id` for the link is also the undo key for the composite operation — store it, and on undo call `UndoActionGroupAsync` THEN delete the candidate event (soft delete or hard delete — follow whatever delete path 8.5/8.6 uses for `lifecycle=candidate` events).

### Undo toast integration

`RequestUndoToastMessage` is defined in `Messages/RequestUndoToastMessage.cs`. Send it after any action:

```csharp
WeakReferenceMessenger.Default.Send(new RequestUndoToastMessage(
    actionGroupId,
    $"Linked {dataPoints.Count} datapoints",
    async () => await _linkService.UndoActionGroupAsync(actionGroupId)));
```

For `+ Event` undo, wrap the lambda to also delete the candidate:
```csharp
async () => {
    await _linkService.UndoActionGroupAsync(actionGroupId);
    await _eventRepository.DeleteAsync(candidateEventId);
    WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(candidateEventId));
}
```

### Link order definition

The hardcoded link order is a ranked list of `source_key` strings. Define it as a static array in `LinkOrderService`:

```csharp
// Ordered from "do first" to "do last" — adjust when 8.14/8.15 finalize rule catalog
private static readonly string[] LinkOrderKeys = [
    "toggl_entry",     // 1 — Toggl time entries
    "toggl_phone",     // 2 — Phone usage
    "call_log",        // 3 — iOS call log
    "civ5_session",    // 4 — Civilization 5
    "comfyui_scan",    // 5 — ComfyUI
    "spotify_stream",  // 6 — Spotify (auto-links via rule)
    "google_maps_segment", // 7 — Maps
    "outlook_calendar",    // 8 — Outlook (auto-generates candidates)
    "voice_memo",          // 9
    "chrome_search",       // 10
];
```

Sources not in this list are "rule-less" — append them (with data) to the end, sorted by display name.

### DataSourceSummaryViewModel linked count

`DataSourceSummaryViewModel` (existing, used by the Sources panel) may need a new `LinkedCountText` property (`"x/y linked"`) for display in the source picker list. If it already exposes linked/total counts from the coverage service (8.10), use them. If not, compute them from `ILinkService.GetLinksByDataPointIds` (batch call) for the in-scope datapoints. Do NOT add this property until checking what 9.2 (Sources panel coverage rollup) already adds — if 9.2 adds coverage counts, reuse those observables rather than duplicating the computation.

### Time range formatting

```csharp
private static string FormatClumpRange(DateTime startUtc, DateTime endUtc)
{
    var startLocal = startUtc.ToLocalTime();
    var endLocal = endUtc.ToLocalTime();
    if (startLocal.Date == endLocal.Date)
        return startLocal.ToString("MMM d HH:mm") + " – " + endLocal.ToString("HH:mm");
    return startLocal.ToString("MMM d HH:mm") + " – " + endLocal.ToString("MMM d HH:mm");
}
```

Place in a static `LinkingFormatters` utility class at `ViewModels/LinkingPanel/LinkingFormatters.cs` — shared with future 9.4 and 9.5 stories.

### Concurrent-event hints per clump

Query for concurrent events when building `ClumpRowViewModel`:

```csharp
var rangeFrom = DateOnly.FromDateTime(clump.ClumpStartUtc.ToLocalTime());
var rangeTo = DateOnly.FromDateTime(clump.ClumpEndUtc.ToLocalTime());
var events = await _eventRepository.GetEventsForRangeAsync(rangeFrom, rangeTo.AddDays(1), ct);
var concurrent = events
    .Where(e => e.Lifecycle == "approved"
             && e.StartDatetime < clump.ClumpEndUtc
             && e.EndDatetime > clump.ClumpStartUtc)
    .OrderBy(e => e.StartDatetime)
    .Take(3)
    .Select(e => e.Summary ?? "(no title)")
    .ToList();
```

### CancellationToken management pattern

Cancellation on source switch / scope change:

```csharp
private CancellationTokenSource _loadCts = new();

private async Task ReloadAsync()
{
    await _loadCts.CancelAsync();
    _loadCts = new CancellationTokenSource();
    try { await LoadClumpsAsync(_loadCts.Token); }
    catch (OperationCanceledException) { /* expected */ }
}
```

### WinUI 3 keyboard handling

Add `KeyDown` event handler in the `LinkingPanelControl.xaml.cs` code-behind. WinUI 3 key handling requires setting `e.Handled = true` to prevent bubbling:

```csharp
private void ClumpListView_KeyDown(object sender, KeyRoutedEventArgs e)
{
    if (e.Key == Windows.System.VirtualKey.Right || (int)e.Key == 0x4B) // K
    {
        ViewModel.NextClumpCommand.Execute(null);
        e.Handled = true;
    }
    else if (e.Key == Windows.System.VirtualKey.Left || (int)e.Key == 0x4A) // J
    {
        ViewModel.PrevClumpCommand.Execute(null);
        e.Handled = true;
    }
}
```

### DispatcherQueue — UI thread dispatch

All `ObservableCollection` mutations must occur on the UI thread. Follow the same pattern as `DataSourcePanelViewModel`:

```csharp
_dispatcherQueue.TryEnqueue(() =>
{
    Clumps.Clear();
    foreach (var row in newRows) Clumps.Add(row);
    IsLoading = false;
    UpdateUnlinkedCounters();
});
```

### File placement

New files go in:
- `Services/DataLinking/ILinkOrderService.cs`
- `Services/DataLinking/LinkOrderService.cs`
- `ViewModels/LinkingPanel/LinkingBySourceViewModel.cs`
- `ViewModels/LinkingPanel/ClumpRowViewModel.cs`
- `ViewModels/LinkingPanel/LinkingFormatters.cs`
- `Views/LinkingPanel/LinkingPanelControl.xaml`
- `Views/LinkingPanel/LinkingPanelControl.xaml.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/LinkingPanel/LinkingBySourceViewModelTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/DataLinking/LinkOrderServiceTests.cs`

Modified:
- `App.xaml.cs` — DI registrations
- The panel-switching control from 9.1 — wire `LinkingPanelControl` into the Linking tab slot

### What this story does NOT do

- Does NOT implement the By Event lens (W3) — that is 9.4.
- Does NOT implement the Gaps lens (W4) — that is 9.5.
- Does NOT implement the Sources panel coverage rollup — that is 9.2.
- Does NOT implement gap calendar rendering — that is 9.6.
- Does NOT modify the block/clump providers or link service — those are 8.11 and 8.12.
- Does NOT add the panel icon strip — that is 9.1.
- Does NOT add coverage indicators to the Sources panel — that is 9.8.
- Does NOT persist clumps or blocks to the database — they are always computed (from 8.11).

### REVISIT note (from epic overview)

> **REVISIT (Sarunas):** core linking workflow — exercise with real multi-source data.

After implementation, test with at least 3 real sources loaded to validate the source ordering, clump display, and Next/Prev flow before marking done.

### Testing framework

xUnit + FluentAssertions + Moq. Mock all services in unit tests. Do not use in-memory SQLite for ViewModel tests — mock `ILinkService`, `IClumpBlockProviderRegistry`, `IEventRepository`, `IDataSourceRepository` directly.

### Project Structure Notes

- New subfolder `ViewModels/LinkingPanel/` — groups all 9.x ViewModel files; keeps them separated from the large existing `ViewModels/` flat list
- New subfolder `Views/LinkingPanel/` — same rationale
- New subfolder `Services/DataLinking/` already created by 8.11 — add `ILinkOrderService.cs` and `LinkOrderService.cs` there
- Namespace: `GoogleCalendarManagement.ViewModels.LinkingPanel`, `GoogleCalendarManagement.Views.LinkingPanel`, `GoogleCalendarManagement.Services.DataLinking`
- All `ObservableObject` subclasses use `CommunityToolkit.Mvvm`; commands use `AsyncRelayCommand`/`RelayCommand` from the same toolkit
- Color lookup: use `IDataSourceRepository.GetByKeyAsync(sourceKey).Color` (SolidColorBrush stored as hex string or `Color` struct — check the DataSource entity from Epic 5 for the actual property type and the existing color-to-brush converter in `Views/Converters/`)

### References

- Epic 9 overview: [docs/epic-9-linking-panel/epic-overview.md](../../epic-9-linking-panel/epic-overview.md) — §Story 9.3
- Canonical concepts: [docs/epic-8-data-linking/concepts.md](../../epic-8-data-linking/concepts.md) — §2 vocabulary, §5 links, §7 rules/link-order, §8 workflows
- Block/clump contract: [docs/epic-8-data-linking/stories/8-11-block-clump-provider-contract.md](../../epic-8-data-linking/stories/8-11-block-clump-provider-contract.md) — §Contract design
- Link service contract: [docs/epic-8-data-linking/stories/8-12-link-table-and-link-ignore-unlink-operations.md](../../epic-8-data-linking/stories/8-12-link-table-and-link-ignore-unlink-operations.md) — §ILinkService interface
- Event picker dialog: [docs/epic-8-data-linking/stories/8-13-link-to-any-event-picker.md](../../epic-8-data-linking/stories/8-13-link-to-any-event-picker.md) — §ContentDialog + ConfirmLinkCommand wiring
- Existing panel pattern: `Views/DataSourcePanelControl.xaml` + `ViewModels/DataSourcePanelViewModel.cs`
- Messaging: `Messages/RequestUndoToastMessage.cs`, `Messages/CalendarViewRangeChangedMessage.cs`, `Messages/EventUpdatedMessage.cs`
- View range provider: `Services/ICalendarViewRangeProvider.cs`
- DI registration reference: `App.xaml.cs` (~lines 268–310)

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

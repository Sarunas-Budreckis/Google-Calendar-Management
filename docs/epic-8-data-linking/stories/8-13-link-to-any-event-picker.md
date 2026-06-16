# Story 8.13: Link-to-Any-Event Picker (Tolerates Non-Concurrency)

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Sonnet · **Effort:** medium
**Prerequisites:** Story 8.12 (link table + link/ignore/unlink operations) and Story 8.3 (Event repository + identity service) must be complete

---

## Story

As a user working with raw data in the Linking panel,
I want to manually link a datapoint (or clump) to any calendar event — not just time-concurrent ones —
so that rounding errors and approximate tracking times don't block me from recording what the data actually belongs to.

---

## Acceptance Criteria

1. `IEventPickerService` exists with `GetCandidatesAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, string? searchText, CancellationToken ct)` that returns `EventPickerResult` containing two ordered lists: `ConcurrentEvents` (all `lifecycle=approved` events whose time range overlaps `[rangeStart, rangeEnd]`, sorted by `start_datetime` ascending) and `OtherEvents` (all remaining `lifecycle=approved` events, sorted by absolute distance of their midpoint from the input range midpoint, nearest first). `lifecycle=candidate` events are excluded from both lists.

2. When `searchText` is non-null and non-empty, both lists are filtered to events whose `Summary` contains `searchText` (case-insensitive, `StringComparison.OrdinalIgnoreCase`). The concurrent/other grouping is preserved after filtering. An empty concurrent list after filtering renders no "Concurrent events" section header.

3. `EventPickerViewModel` exposes:
   - `ConcurrentEvents` (ObservableCollection<EventPickerItem>) — concurrent results
   - `OtherEvents` (ObservableCollection<EventPickerItem>) — non-concurrent results sorted by proximity
   - `SearchText` (string property; changing it cancels any in-flight search and re-calls `IEventPickerService` with 300ms debounce via a `CancellationTokenSource` swap)
   - `SelectedItem` (EventPickerItem?; changing it calls `ConfirmLinkCommand.NotifyCanExecuteChanged()`)
   - `ConfirmLinkCommand` (IAsyncRelayCommand; `CanExecute` = `SelectedItem != null`)
   - `IsEmpty` (bool; true when both collections are empty after the current search)
   - `ErrorMessage` (string?; set on link-write failure; bound to an error TextBlock in the dialog)

4. `EventPickerItem` is a record/class with: `EventId` (string), `Summary` (string), `StartLocal` (DateTime, converted from UTC to local at query time), `EndLocal` (DateTime), `ColorId` (string?), `IsConcurrent` (bool).

5. `EventPickerDialog` is a WinUI 3 `ContentDialog` that:
   - Hosts `EventPickerViewModel` as its `DataContext`
   - Shows a `TextBox` (PlaceholderText = "Search events…") bound TwoWay to `SearchText` at the top
   - Shows a `ListView` with two optional section groups ("Concurrent events" / "Other events") via `CollectionViewSource` grouped binding; each item shows a 16×16 color-swatch `Rectangle` (fill from `ColorId` via existing color-lookup converter), a `TextBlock` for `Summary`, and a `TextBlock` for the formatted time range (`StartLocal "MMM d HH:mm"` – `EndLocal "HH:mm"`)
   - Primary button labeled "Link" — bound to `ConfirmLinkCommand`; disabled when `SelectedItem` is null (`IsPrimaryButtonEnabled` bound to `SelectedItem != null`)
   - Secondary button labeled "Cancel" — closes without writing any link
   - Shows a centered "No events found" `TextBlock` (Visibility bound to `IsEmpty`) in place of the list when both collections are empty
   - Shows `ErrorMessage` in a red `TextBlock` below the list (Visibility bound to `ErrorMessage != null`)

6. When `ConfirmLinkCommand` executes successfully:
   - For a **single datapoint**: calls `ILinkService.LinkAsync(dataPointId, selectedItem.EventId, actionGroupId)` where `actionGroupId` is `Guid.NewGuid().ToString("N")` minted per dialog invocation
   - For a **clump** (multiple dataPointIds): calls `ILinkService.LinkClumpAsync(dataPointIds, selectedItem.EventId)` (the service mints its own shared `action_group_id`)
   - In both cases `origin = "manual"` is enforced inside `ILinkService` (not passed by the picker — all public calls default to manual)
   - After the link is written, sends `WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(selectedItem.EventId))` so the calendar refreshes
   - On failure, sets `ErrorMessage` and does NOT close the dialog

7. `IEventPickerService` is registered as singleton in `App.xaml.cs`. `EventPickerViewModel` is created with `new` at the call site (not a singleton — it holds runtime per-invocation state). `EventPickerDialog` is also created per-invocation.

8. No regression: existing calendar rendering, event selection, event editing, drilldown candidate creation, and all existing link/ignore/unlink operations via 8.12 are unaffected.

9. Unit tests:
   - `EventPickerServiceTests`: concurrent event appears in `ConcurrentEvents` and not in `OtherEvents`; non-concurrent events are ordered nearest-midpoint-first; `lifecycle=candidate` events are excluded; `searchText` filters both lists case-insensitively; empty DB returns empty result
   - `EventPickerViewModelTests`: `ConfirmLinkCommand.CanExecute` is false when `SelectedItem` is null, true after it is set; `IsEmpty` is true when service returns empty lists, false when either has items; mock `ILinkService.LinkAsync` is called with correct args on confirm

---

## Tasks / Subtasks

- [x] Task 1: Data models + `IEventPickerService` + `EventPickerService` (AC: #1, #2, #4)
  - [x] 1.1 Create `Models/EventPickerItem.cs` — record: `EventId` (string), `Summary` (string), `StartLocal` (DateTime), `EndLocal` (DateTime), `ColorId` (string?), `IsConcurrent` (bool)
  - [x] 1.2 Create `Models/EventPickerResult.cs` — record: `IReadOnlyList<EventPickerItem> ConcurrentEvents`, `IReadOnlyList<EventPickerItem> OtherEvents`
  - [x] 1.3 Create `Services/IEventPickerService.cs` — one method: `Task<EventPickerResult> GetCandidatesAsync(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, string? searchText, CancellationToken ct = default)`
  - [x] 1.4 Create `Services/EventPickerService.cs`:
    - Inject `IEventRepository` (from 8.3)
    - Fetch `lifecycle=approved` events via `IEventRepository.GetByDateRangeAsync` with a ±90-day window around the datapoint date by default; if `searchText` is present and the caller needs a wider window, widen to ±365 days (see Dev Notes)
    - Split: concurrent = `event.start_datetime < rangeEnd && event.end_datetime > rangeStart`; non-concurrent = all others
    - Sort concurrent by `start_datetime` ascending
    - Sort non-concurrent by `|eventMid - rangeMid|` ascending (see Dev Notes for midpoint calculation)
    - Apply `searchText` filter (case-insensitive Contains on `Summary`) after splitting
    - Convert `start_datetime` / `end_datetime` (UTC) to local time for `EventPickerItem.StartLocal` / `EndLocal`
    - Return `EventPickerResult`

- [x] Task 2: `EventPickerViewModel` (AC: #3, #6)
  - [x] 2.1 Create `ViewModels/EventPickerViewModel.cs` — inherits `ObservableObject` (CommunityToolkit.Mvvm)
  - [x] 2.2 Constructor: `(IEventPickerService pickerService, ILinkService linkService, DateTimeOffset rangeStart, DateTimeOffset rangeEnd, IReadOnlyList<int> dataPointIds)` — store all; fire `_ = LoadAsync(null)` immediately for initial population
  - [x] 2.3 Add `ConcurrentEvents` and `OtherEvents` as `ObservableCollection<EventPickerItem>`; populate from `IEventPickerService` result; update `IsEmpty` after each load
  - [x] 2.4 Add `SearchText` — on set, cancel in-flight CTS, schedule a 300ms debounce (use `Task.Delay(300, cts.Token)` pattern), then call `LoadAsync(SearchText)`
  - [x] 2.5 `LoadAsync(string? searchText)` — calls `_pickerService.GetCandidatesAsync`, updates both collections via `DispatcherQueue.TryEnqueue` (WinUI 3 UI-thread dispatch requirement), updates `IsEmpty`
  - [x] 2.6 `SelectedItem` property — setter calls `ConfirmLinkCommand.NotifyCanExecuteChanged()`
  - [x] 2.7 `ConfirmLinkCommand` implementation:
    - `CanExecute`: `SelectedItem != null`
    - `Execute`: call `ILinkService.LinkAsync` (single) or `ILinkService.LinkClumpAsync` (clump, when `dataPointIds.Count > 1`) with the selected `EventId`; on success send `EventUpdatedMessage`; on exception set `ErrorMessage` and return without closing dialog
  - [x] 2.8 `IsEmpty` computed property — returns `ConcurrentEvents.Count == 0 && OtherEvents.Count == 0`
  - [x] 2.9 `ErrorMessage` property (string?, notify on change)

- [x] Task 3: `EventPickerDialog` XAML + code-behind (AC: #5)
  - [x] 3.1 Create `Views/EventPickerDialog.xaml` — ContentDialog; Title = "Link to event"; PrimaryButtonText = "Link"; SecondaryButtonText = "Cancel"
  - [x] 3.2 XAML Content structure:
    - `StackPanel` (Width=400, MaxHeight=500)
    - `TextBox` PlaceholderText="Search events…", Text bound TwoWay to `SearchText`
    - `CollectionViewSource` in page resources keyed `GroupedEventSource` — `Source` bound to a merged `ObservableCollection<EventPickerGroup>` that the VM maintains (see Dev Notes for grouping strategy)
    - `ListView` bound to `GroupedEventSource`; `IsGrouping=True`; `GroupStyle` with `HeaderTemplate` showing group key string
    - Item DataTemplate: `StackPanel` (Horizontal) → 16×16 `Rectangle` Fill from `ColorHex` via `HexToBrushConverter` → `TextBlock` Summary → `TextBlock` time range (see Dev Notes for formatting)
    - `TextBlock` "No events found" Visibility bound to `IsEmpty` via `BoolToVisibilityConverter`
    - `TextBlock` for `ErrorMessage` (Foreground=Red, Visibility bound to `HasError`)
  - [x] 3.3 Create `Views/EventPickerDialog.xaml.cs`:
    - Constructor takes `EventPickerViewModel vm`; sets `DataContext = vm`; binds `IsPrimaryButtonEnabled` to `vm.SelectedItem != null` (observe via PropertyChanged); wires `PrimaryButtonClick` to `args.Cancel = true` (let the command handle the link write — dialog stays open on failure)
    - Sets `CloseButtonText = string.Empty` (secondary button is Cancel via `SecondaryButtonClick`)

- [x] Task 4: DI registration (AC: #7)
  - [x] 4.1 `App.xaml.cs`: add `services.AddSingleton<IEventPickerService, EventPickerService>()`
  - [x] 4.2 `ILinkService` is already registered from 8.12 — verified present in `App.xaml.cs`

- [x] Task 5: Tests (AC: #9)
  - [x] 5.1 Create `GoogleCalendarManagement.Tests/Unit/Services/EventPickerServiceTests.cs`:
    - Setup: in-memory SQLite seeded with `Event` rows (mix of `lifecycle=approved` and `lifecycle=candidate`)
    - Test `ConcurrentEvents`: event overlapping the query range lands in `ConcurrentEvents`, not `OtherEvents`
    - Test `OtherEvents` ordering: two non-concurrent events, nearer midpoint first
    - Test candidate exclusion: `lifecycle=candidate` event not present in either list
    - Test search: search "foo" excludes events without "foo" in summary; case-insensitive match works
    - Test empty: no events in DB → both lists empty
  - [x] 5.2 Create `GoogleCalendarManagement.Tests/Unit/ViewModels/EventPickerViewModelTests.cs`:
    - Mock `IEventPickerService` (returns controlled `EventPickerResult`)
    - Mock `ILinkService`
    - Test: `ConfirmLinkCommand.CanExecute` false initially; true after `SelectedItem` set
    - Test: `IsEmpty` true when service returns empty lists; false when items present
    - Test: `ConfirmLinkCommand` calls `ILinkService.LinkAsync` with correct `dataPointId` and `eventId`
    - Test: on `ILinkService` throw, `ErrorMessage` is set and command does not rethrow

### Review Findings

- [x] [Review][Patch] Cancel or version all picker loads so the fire-and-forget initial load cannot overwrite newer search results [ViewModels/EventPickerViewModel.cs:50]
- [x] [Review][Patch] Surface non-cancellation picker load failures instead of leaving stale results with no `ErrorMessage` [ViewModels/EventPickerViewModel.cs:104]
- [x] [Review][Patch] Use `rangeEnd` when calculating the positive fetch window for long picker ranges [Services/EventPickerService.cs:24]
- [x] [Review][Patch] Treat null `EndDatetime` as `StartDatetime` instead of omitting otherwise valid approved events [Services/EventPickerService.cs:38]
- [x] [Review][Patch] Validate `rangeEnd > rangeStart` before concurrency and midpoint calculations [Services/EventPickerService.cs:31]
- [x] [Review][Patch] Normalize event timestamps as UTC before local display and proximity calculations, matching `CalendarQueryService` [Services/EventPickerService.cs:41]
- [x] [Review][Patch] Complete `ContentDialog` deferrals in `finally` so unexpected command exceptions cannot strand the dialog [Views/EventPickerDialog.xaml.cs:27]

---

## Dev Notes

### Hard prerequisites: Story 8.12 + 8.3 deliverables

Story 8.13 cannot start until both 8.12 and 8.3 are merged. Expected interfaces:

**From 8.3 — `IEventRepository` (subset relevant to 8.13):**
```csharp
// Services/IEventRepository.cs
public interface IEventRepository {
    Task<IList<Event>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Event?> GetByEventIdAsync(string eventId, CancellationToken ct = default);
}
```
For the picker's query, pass a wide date range: `DateOnly.FromDateTime(rangeStart.AddDays(-90).Date)` to `DateOnly.FromDateTime(rangeStart.AddDays(90).Date)` to keep the result set bounded without missing close events. If a search string is present, widen to ±365 days — a user typing "gym" may want events from months ago. Match 8.3's actual method signatures — do not invent methods not defined there.

**From 8.12 — `ILinkService` (expected shape):**
```csharp
// Services/ILinkService.cs
public interface ILinkService {
    // Single datapoint — origin defaults to "manual" for all public calls
    Task LinkAsync(string dataPointId, string eventId, string? actionGroupId = null, CancellationToken ct = default);
    Task IgnoreAsync(string dataPointId, string? actionGroupId = null, CancellationToken ct = default);
    Task UnlinkAsync(string dataPointId, CancellationToken ct = default);
    // Clump — writes N rows under one shared action_group_id minted inside this method
    Task LinkClumpAsync(IReadOnlyList<string> dataPointIds, string eventId, CancellationToken ct = default);
    Task IgnoreClumpAsync(IReadOnlyList<string> dataPointIds, CancellationToken ct = default);
    // Undo all rows sharing an action_group_id
    Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);
}
```
If 8.12 exposes a different signature, match it exactly — do not adapt the picker to a guessed interface.

### Event picker sorting algorithm

**Concurrent determination** (use UTC throughout — all `DateTime` values in the `event` table are UTC):
```csharp
bool IsConcurrent(Event ev, DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    => ev.StartDatetime < rangeEnd.UtcDateTime && ev.EndDatetime > rangeStart.UtcDateTime;
```

**Non-concurrent proximity sort** — sort by absolute midpoint distance:
```csharp
var rangeMid = rangeStart + (rangeEnd - rangeStart) / 2;
double Distance(Event ev) {
    var evMid = ev.StartDatetime + (ev.EndDatetime - ev.StartDatetime) / 2;
    return Math.Abs((evMid - rangeMid.UtcDateTime).TotalSeconds);
}
otherEvents = otherEvents.OrderBy(Distance).ToList();
```

**Local time conversion** — for display only, convert after sorting:
```csharp
StartLocal = ev.StartDatetime.ToLocalTime(),
EndLocal   = ev.EndDatetime.ToLocalTime(),
```

### WinUI 3 `CollectionViewSource` grouping pattern

Maintain an `ObservableCollection<EventPickerGroup>` on the VM (updated in `LoadAsync`):
```csharp
public class EventPickerGroup : ObservableCollection<EventPickerItem>
{
    public string Key { get; }
    public EventPickerGroup(string key, IEnumerable<EventPickerItem> items) : base(items) => Key = key;
}
```
In XAML resources:
```xml
<CollectionViewSource x:Key="GroupedEventSource"
                      Source="{x:Bind ViewModel.Groups}"
                      IsSourceGrouped="True"
                      ItemsPath="." />
```
Only add a group to `Groups` when it has at least one item — this prevents empty section headers from appearing. After a search that empties the concurrent list, remove the "Concurrent events" group entirely.

### `ContentDialog` + `ConfirmLinkCommand` wiring

WinUI 3 `ContentDialog.PrimaryButtonCommand` fires before the dialog closes. To keep the dialog open on failure (so `ErrorMessage` is visible), handle `PrimaryButtonClick` and set `args.Cancel = true`, then manually call `Hide()` only after the command reports success:

```csharp
private async void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args) {
    var deferral = args.GetDeferral();
    args.Cancel = true;  // Prevent auto-close
    await ViewModel.ConfirmLinkCommand.ExecuteAsync(null);
    if (ViewModel.ErrorMessage == null)
        sender.Hide();
    deferral.Complete();
}
```

### `DispatcherQueue` requirement for WinUI 3

`ObservableCollection` modifications must occur on the UI thread. In `LoadAsync`, use:
```csharp
_dispatcherQueue.TryEnqueue(() => {
    ConcurrentEvents.Clear();
    foreach (var item in result.ConcurrentEvents) ConcurrentEvents.Add(item);
    // ... same for OtherEvents, Groups, IsEmpty
});
```
Inject `DispatcherQueue` via constructor: `DispatcherQueue.GetForCurrentThread()` called from the UI thread at construction time.

### `origin = "manual"` is enforced in `ILinkService`, not in the picker

From concepts.md §7: manual links are sacred — rules never override them. The picker always produces manual links. `ILinkService.LinkAsync` and `LinkClumpAsync` public methods default `origin = "manual"` internally. The picker VM does not pass an origin parameter. The rule engine will use an internal or separate method that sets `origin = "auto_rule"`.

### What NOT to build in this story

- **No UI entry point in the existing app** — no right-click menu item, no button in any existing view. Epic 9's Linking Panel stories (9.1–9.7) will call `EventPickerDialog` from the panel. Building a wiring point now would create dead code.
- **No ignore or unlink actions in the picker** — the picker is link-only. Epic 9 will build the ignore/unlink controls inside the Linking Panel.
- **No Linking Panel shell** — that is Epic 9 story 9.1.
- **No rule-engine invocation** — the picker is a manual operation only. Rule engine is story 8.14.

### Time format string for the item template

```csharp
// In a value converter or in the VM item construction:
string FormatRange(DateTime startLocal, DateTime endLocal)
    => startLocal.ToString("MMM d, ddd HH:mm") + " – " + endLocal.ToString("HH:mm");
// Example: "Jun 5, Thu 14:00 – 15:30"
```
Add a `TimeRangeText` computed property to `EventPickerItem` or use a converter in XAML.

### Color lookup pattern

Use the same color-lookup pattern established in `CalendarQueryService.MapEventToDisplayModel` (story 8.5). The `EventPickerItem.ColorId` is the raw GCal color id string (e.g., `"3"`, `"grape"`, null). Pass it through the existing `IColorMappingService` in `EventPickerService` to produce a `ColorHex` string stored on the item. A `HexToBrushConverter` in `Views/Converters/` converts this to a `SolidColorBrush` in the item template.

### Project Structure Notes

New files only — no existing files need modification except `App.xaml.cs` (DI registration):

- `Models/EventPickerItem.cs`
- `Models/EventPickerResult.cs`
- `Services/IEventPickerService.cs`
- `Services/EventPickerService.cs`
- `ViewModels/EventPickerViewModel.cs` (includes `EventPickerGroup` nested or alongside)
- `Views/Converters/BoolToVisibilityConverter.cs`
- `Views/Converters/HexToBrushConverter.cs`
- `Views/EventPickerDialog.xaml`
- `Views/EventPickerDialog.xaml.cs`
- `App.xaml.cs` — add `IEventPickerService` singleton registration
- `GoogleCalendarManagement.Tests/Unit/Services/EventPickerServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/EventPickerViewModelTests.cs`

### References

- [Epic 8 overview](../epic-overview.md) — §Phase 2, Story 8.13
- [Concepts §2 vocabulary](../concepts.md) — datapoint, clump, link, manual vs auto_rule origin
- [Concepts §5 links](../concepts.md) — link table schema; `origin` field; link/ignore/unlink semantics; "link to any event" design decision
- [Concepts §7 rules](../concepts.md) — why `origin="manual"` links must never be overwritten by rules
- [Story 8.3](8-3-event-repository-and-identity-service.md) — `IEventRepository` interface (prerequisite)
- [Story 8.5](8-5-rendering-and-drilldowns-mint-candidates.md) — `CalendarQueryService.MapEventToDisplayModel` for color-lookup pattern; `CalendarEventSourceKind` derivation (do not use Outlook kind — it is removed in 8.5)
- [Epic 9 overview](../../epic-9-linking-panel/epic-overview.md) — context for where `EventPickerDialog` will be called from in Epic 9 stories 9.3–9.5

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

**ILinkService signature mismatch:** The story Dev Notes anticipated `LinkAsync(string dataPointId, ...)` with a string ID, but the actual 8.12 implementation uses `Task<string> LinkAsync(int dataPointId, string eventId, CancellationToken ct = default)` with an `int` dataPointId and no actionGroupId param (it returns the action_group_id instead). Adapted the VM constructor and `ExecuteConfirmLinkAsync` to use `IReadOnlyList<int>` and match the real interface.

**IEventRepository method name:** The story Dev Notes referenced `GetEventsForRangeAsync` but the actual 8.3 interface exposes `GetByDateRangeAsync(DateOnly from, DateOnly to, ...)`. Used the real method name.

**BoolToVisibilityConverter:** WinUI 3 has no built-in BoolToVisibilityConverter resource. Created `Views/Converters/BoolToVisibilityConverter.cs`. Added `HasError` computed bool property to `EventPickerViewModel` for the ErrorMessage visibility binding.

**ColorId → color brush:** No existing XAML value converter for color id → brush. `EventPickerService` pre-computes `ColorHex` (hex string) using `IColorMappingService`, stored on `EventPickerItem` alongside the raw `ColorId`. Created `Views/Converters/HexToBrushConverter.cs` to convert hex string to `SolidColorBrush` in the item DataTemplate.

### Completion Notes List

- All 5 tasks (15 subtasks) completed.
- 15 new tests added: 8 in `EventPickerServiceTests` + 7 in `EventPickerViewModelTests`. All 15 pass.
- Full regression suite: 539 passed, 0 failed (19 pre-existing skips unrelated to this story).
- `IEventPickerService` registered as singleton in `App.xaml.cs` alongside existing `ILinkService`.
- `EventPickerDialog` created per-invocation at the call site (Epic 9 stories will wire it).
- `EventPickerViewModel.Groups` ObservableCollection drives the grouped `CollectionViewSource`; empty groups are never added so no phantom section headers appear.
- `DispatcherQueue` captured at VM construction time; null-safe dispatch path used so unit tests run inline without UI thread.

### File List

- `Models/EventPickerItem.cs` — new
- `Models/EventPickerResult.cs` — new
- `Services/IEventPickerService.cs` — new
- `Services/EventPickerService.cs` — new
- `ViewModels/EventPickerViewModel.cs` — new (includes `EventPickerGroup`)
- `Views/Converters/BoolToVisibilityConverter.cs` — new
- `Views/Converters/HexToBrushConverter.cs` — new
- `Views/EventPickerDialog.xaml` — new
- `Views/EventPickerDialog.xaml.cs` — new
- `App.xaml.cs` — modified (added `IEventPickerService` singleton registration)
- `GoogleCalendarManagement.Tests/Unit/Services/EventPickerServiceTests.cs` — new
- `GoogleCalendarManagement.Tests/Unit/ViewModels/EventPickerViewModelTests.cs` — new
- `docs/sprint-status.yaml` — modified (8-13 status: ready-for-dev → review)

### Change Log

- 2026-06-16: Story 8.13 implemented — EventPickerService, EventPickerViewModel, EventPickerDialog, converters, DI registration, and tests (15 new passing)
- 2026-06-16: Code review fixes applied — stale-load protection, picker load error surfacing, timestamp/range hardening, null-end handling, and dialog deferral cleanup

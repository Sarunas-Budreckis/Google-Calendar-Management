# Story 9.7: Gap Detail Panel (Top-4 Sources, Vertical Dots)

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 9.6 (blocking — gap selection + `GapInfo` model must exist), 8.11 (blocking — `IClumpBlockProviderRegistry` and `data_point` table must exist)

---

## Story

As a user reviewing a gap in the Linking panel Gaps lens,
I want to click a gap outline on the calendar (or in the Gaps lens list) and see a gap-detail panel showing which data sources have activity during that gap,
so that I can understand what raw data exists in the gap and create an event from it with one click.

---

## Acceptance Criteria

1. Clicking a gap outline on the calendar (Story 9.6) or a gap entry in the Gaps lens (Story 9.5) opens/activates the gap-detail panel, which displays data for the selected gap.
2. The panel header shows the gap's time extent: formatted `start → end` (local time).
3. The panel shows up to **4 source columns** — the 4 sources with the highest datapoint count in the gap's time range. Sources with zero datapoints in the range are excluded.
4. Each source column is labeled with the source display name, colored with the source's registered color, and contains one **dot per datapoint** in the gap time range, placed proportionally by timestamp (top = gap start, bottom = gap end).
5. Hovering a dot shows an **instant tooltip** (no delay) with: source key, local-time timestamp, and duration (if `end_utc != start_utc`). Exact content is a REVISIT item — see Dev Notes.
6. If a source has **≥ 1000 datapoints** in the gap, that source column shows a collapsed summary ("N datapoints — expand to view") instead of individual dots. Tapping/clicking it expands to render all dots in a virtualized scroll list. Exact expansion UX is a REVISIT item — see Dev Notes.
7. A **"Create event for gap"** button creates a `lifecycle = candidate` event with `start_datetime = gapStart`, `end_datetime = gapEnd` using the candidate creation path from Story 8.5. After creation the event editing panel opens on the new candidate.
8. When no gap is selected, the panel shows a neutral empty state ("Select a gap to see source detail").
9. Selecting a different gap replaces the panel content in place (no full navigation).

---

## Tasks / Subtasks

- [ ] Task 1: Data types + service contract (AC: #2–#4)
  - [ ] 1.1 Create `Services/DataLinking/IGapDetailService.cs` (see Dev Notes §Service contract)
  - [ ] 1.2 Create `Services/DataLinking/GapDetailService.cs` implementing `IGapDetailService`
  - [ ] 1.3 `GetTopSourcesAsync(DateTime gapStart, DateTime gapEnd, int maxSources = 4, CancellationToken ct)` → queries `data_point` table for `start_utc >= gapStart && start_utc < gapEnd`, groups by `source_key`, orders by descending count, takes top `maxSources`, then loads all datapoints for those sources in the range

- [ ] Task 2: ViewModel (AC: #1–#9)
  - [ ] 2.1 Create `ViewModels/Linking/GapDetailPanelViewModel.cs`
  - [ ] 2.2 Properties: `GapStart`, `GapEnd`, `GapLabel` (formatted header string), `SourceColumns` (`ObservableCollection<GapSourceColumnViewModel>`), `IsEmpty` (no gap selected), `IsLoading`
  - [ ] 2.3 `LoadGapAsync(GapInfo gap)` — calls `IGapDetailService.GetTopSourcesAsync`, populates `SourceColumns`, clears `IsEmpty`
  - [ ] 2.4 `CreateEventFromGapCommand` — creates a candidate event for the gap (see Dev Notes §Event creation); after creation, navigates to event editing panel on the new candidate
  - [ ] 2.5 Create `ViewModels/Linking/GapSourceColumnViewModel.cs` with: `SourceKey`, `DisplayName`, `SourceColor` (Windows.UI.Color from source color registry), `DataPoints` (`IReadOnlyList<GapDotViewModel>`), `IsHighVolume` (count ≥ 1000), `IsExpanded` (observable bool toggle for high-volume expansion), `DataPointCount`
  - [ ] 2.6 Create `ViewModels/Linking/GapDotViewModel.cs` with: `DataPointId`, `StartUtc`, `EndUtc`, `RelativePosition` (0.0–1.0 within gap duration), `TooltipText` (formatted string)

- [ ] Task 3: XAML view (AC: #2–#8)
  - [ ] 3.1 Create `Views/Linking/GapDetailPanel.xaml` + `GapDetailPanel.xaml.cs`
  - [ ] 3.2 Header: gap time-extent `TextBlock` + "Create event for gap" `Button` (bound to `CreateEventFromGapCommand`, disabled when `IsEmpty`)
  - [ ] 3.3 Source columns: horizontal `ItemsControl` (or `StackPanel`), each column shows source name + colored header `Border`, then a vertical dot canvas (see Dev Notes §Dot positioning)
  - [ ] 3.4 Normal dots: small `Ellipse` (6–8dp, source color fill) inside a `Canvas`; `ToolTipService.SetToolTip` with the dot's `TooltipText`; `ToolTipService.SetInitialShowDelay="0"` for instant display
  - [ ] 3.5 High-volume column: show a `TextBlock` with summary count + expand `Button`; when `IsExpanded = true`, render dots inside a `ScrollViewer` > `VirtualizingStackPanel` (see Dev Notes §High-volume rendering)
  - [ ] 3.6 Empty state: `TextBlock` "Select a gap to see source detail" visible when `IsEmpty = true`
  - [ ] 3.7 Hook panel into the panel host from Story 9.1's layout — gap-detail is shown inside the left panel area when a gap is selected

- [ ] Task 4: Wire gap selection from 9.6 (AC: #1, #9)
  - [ ] 4.1 Identify how 9.5/9.6 exposes gap selection (event/message/observable property) and subscribe in the parent ViewModel or via messaging
  - [ ] 4.2 On gap selected: call `GapDetailPanelViewModel.LoadGapAsync(gap)`; ensure this panel is visible in the panel host

- [ ] Task 5: DI registration (AC: all)
  - [ ] 5.1 Register `IGapDetailService` → `GapDetailService` in `App.xaml.cs` (scoped or singleton — match DB context factory lifetime)
  - [ ] 5.2 Register `GapDetailPanelViewModel` (transient)

- [ ] Task 6: Unit tests
  - [ ] 6.1 `GapDetailServiceTests` — in-memory SQLite DB with known `data_point` rows; verify top-4 ordering by count, correct exclusion of zero-count sources, correct datapoint loading per source

---

## Dev Notes

### Service contract

```csharp
// Services/DataLinking/IGapDetailService.cs
namespace GoogleCalendarManagement.Services.DataLinking;

public interface IGapDetailService
{
    Task<IReadOnlyList<GapSourceDetail>> GetTopSourcesAsync(
        DateTime gapStart, DateTime gapEnd,
        int maxSources = 4, CancellationToken ct = default);
}

public sealed record GapSourceDetail(
    string SourceKey,
    string DisplayName,
    int DataPointCount,
    IReadOnlyList<GapDataPointInfo> DataPoints);

public sealed record GapDataPointInfo(
    long DataPointId,
    DateTime StartUtc,
    DateTime EndUtc,
    string SourceRef);
```

Implementation queries the `data_point` EF entity set:
```csharp
var groups = await _dbContext.DataPoints
    .Where(dp => dp.StartUtc >= gapStart && dp.StartUtc < gapEnd)
    .GroupBy(dp => dp.SourceKey)
    .Select(g => new { SourceKey = g.Key, Count = g.Count() })
    .OrderByDescending(g => g.Count)
    .Take(maxSources)
    .ToListAsync(ct);
// Then for each top source, load its DataPoints in range
```

### Source colors

Data source colors are managed by the registry established in **Story 5.1** (data source infrastructure). Search the codebase for `IDataSourceColorService`, `DataSourceRegistry`, or `DataSourceColor` — one of these exists and maps `source_key` → `Color`. Do **NOT** hardcode source colors. Inject the registry into `GapDetailService` or `GapDetailPanelViewModel` to resolve colors.

### Dot positioning

Use `RelativePosition` (0.0 = top, 1.0 = bottom) computed as:
```csharp
double relativePosition = gapDuration.TotalSeconds > 0
    ? (dp.StartUtc - gapStart).TotalSeconds / (gapEnd - gapStart).TotalSeconds
    : 0.0;
```
In XAML, render dots on a `Canvas` with a fixed height (e.g. 240dp) and compute `Canvas.Top = relativePosition * canvasHeight - dotRadius`. Each source column gets its own `Canvas`. If dots overlap, they can stack in z-order (no collision avoidance needed in this story).

### High-volume rendering (REVISIT)

The 1000-dot threshold is specified by the epic. The exact expansion UX is **deferred** (concepts §10 item 3). Implement with a WinUI 3 `Expander` control or a `Button`-toggled `Visibility`. When expanded, render dots inside:
```xml
<ScrollViewer MaxHeight="400">
    <ItemsControl ItemsSource="{x:Bind DataPoints}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <VirtualizingStackPanel />
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
    </ItemsControl>
</ScrollViewer>
```
Use `VirtualizingStackPanel` to avoid rendering 1000+ `Ellipse` elements simultaneously.

**REVISIT (Sarunas):** Review the expansion behavior, collapse/expand animation, and whether the collapsed state shows a mini summary chart instead of just a count. Design this when you reach the story in the running app.

### Tooltip content (REVISIT)

**REVISIT (Sarunas):** Exact tooltip fields noted as deferred in the epic (concepts §10 item 3). Default implementation:
- Source display name
- Local time: `dp.StartUtc.ToLocalTime().ToString("HH:mm:ss")`
- Duration if `end_utc != start_utc`: `(dp.EndUtc - dp.StartUtc).TotalMinutes:F0` min

Implement with `ToolTipService` (not `FlyoutBase`) for instant display:
```xml
<Ellipse ...>
    <ToolTipService.ToolTip>
        <ToolTip Content="{x:Bind TooltipText}" />
    </ToolTipService.ToolTip>
</Ellipse>
```

### Event creation from gap

Use the **same candidate creation path** established in **Story 8.5** (which wired drilldown "Create Candidate Event" buttons). Search for an existing `IEventRepository.CreateCandidateAsync(...)` or equivalent used in `Civ5DrilldownViewModel`, `TogglDrilldownViewModel`, or `ComfyUIDrilldownViewModel`. Do NOT invent a new creation path.

Pre-fill the new candidate event:
- `start_datetime = gapStart` (local or UTC — match how 8.5 creates events)
- `end_datetime = gapEnd`
- `lifecycle = "candidate"`
- `source_system = "manual"` (user-initiated, not a rule)

After creation, navigate to the event editing panel on the new candidate using the same post-creation navigation used by existing drilldown create buttons.

### GapInfo model

Story 9.5 defines gap detection and 9.6 adds calendar rendering. By 9.7, there should be a `GapInfo` (or `Gap`) record/class from 9.5 with at minimum `StartUtc` and `EndUtc`. If the type name differs, adapt. If for any reason the gap model does not exist, `GapDetailPanelViewModel.LoadGapAsync` can accept `(DateTime gapStart, DateTime gapEnd)` directly — the service queries the DB for datapoints itself.

### File placement

```
Views/Linking/GapDetailPanel.xaml
Views/Linking/GapDetailPanel.xaml.cs
ViewModels/Linking/GapDetailPanelViewModel.cs
ViewModels/Linking/GapSourceColumnViewModel.cs
ViewModels/Linking/GapDotViewModel.cs
Services/DataLinking/IGapDetailService.cs
Services/DataLinking/GapDetailService.cs
Tests: GoogleCalendarManagement.Tests/Unit/Services/DataLinking/GapDetailServiceTests.cs
```

- `Views/Linking/` — new subfolder; consistent with keeping Epic 9 Linking panel views together
- `ViewModels/Linking/` — new subfolder; consistent with keeping Linking panel ViewModels together
- `Services/DataLinking/` — already created by 8.11; add `IGapDetailService` + `GapDetailService` here

### DI registration

Follow the pattern of existing DI registrations in `App.xaml.cs` (~lines 268–310). Register `IGapDetailService` → `GapDetailService` with the same lifetime as `IDbContextFactory<CalendarDbContext>` usages in `Services/DataLinking/`.

### What this story does NOT do

- Does NOT implement gap detection or gap calendar outlines — those are 9.5 and 9.6 (prereqs).
- Does NOT add link/ignore actions on dots — linking is 9.3/W1; dots here are read-only display.
- Does NOT show link state on dots — coverage visualization is 9.8.
- Does NOT persist gap data — gaps are computed by 9.5.
- Does NOT implement per-source dot rendering specifics beyond the basic color + timestamp dot.

### Testing framework

xUnit + FluentAssertions + Moq. Unit tests use in-memory SQLite:
```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
```

### Project Structure Notes

- New subfolders `Views/Linking/` and `ViewModels/Linking/` group all Epic 9 Linking panel UI files.
- `Services/DataLinking/` (created by 8.11) is extended with the gap detail service.
- Architecture: WinUI 3 XAML + MVVM. XAML files in `Views/`, ViewModels in `ViewModels/`, services in `Services/`. All C# files use namespace `GoogleCalendarManagement.<subfolder>`.

### References

- Gap concept + Gaps workflow (W4): [concepts.md §8](../../epic-8-data-linking/concepts.md)
- High-volume / dot expansion deferred detail: [concepts.md §10 item 3](../../epic-8-data-linking/concepts.md)
- `IClumpBlockProvider` contract: [8-11 story](../../epic-8-data-linking/stories/8-11-block-clump-provider-contract.md) and `Services/DataLinking/IClumpBlockProvider.cs`
- `data_point` schema: [concepts.md §4](../../epic-8-data-linking/concepts.md)
- Candidate event creation path: Story 8.5 + search `Civ5DrilldownViewModel.cs` for "Create Candidate" usage
- Data source colors: Story 5.1 — search `DataSourceColor` / `IDataSourceColorService`
- Gap calendar outlines + GapInfo model: Story 9.6 (prereq)
- Gap detection + gap list: Story 9.5 (prereq)
- Panel host layout: Story 9.1 (prereq)
- DI registration: `App.xaml.cs`

---

## Dev Agent Record

### Agent Model Used

Sonnet

### Debug Log References

### Completion Notes List

### File List

# Story 9.2: Sources Panel Coverage Rollup + Rule-Driven Ordering

**Epic:** 9 — Linking Panel & Workflows
**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 8.10 (blocking — `ICoverageService` must exist); 8.14/8.15 (blocking — `ILinkRule` registry + `SpotifyAutoLinkRule`/`OutlookGenerateCandidateRule` must be registered, so rule-named source keys are known)

---

## Story

As a user reviewing my data accountability,
I want the Sources panel to show per-source coverage glyphs and `x/y linked` counts, ordered by link order with rule-named sources always visible,
so that I can see at a glance which sources are fully accounted for, which need attention, and which to tackle next.

---

## Acceptance Criteria

1. `ICoverageService` gains a new method `Task<CoverageResult> GetSourceTotalCoverageAsync(string sourceKey, CancellationToken ct = default)` that aggregates **all** `data_point` rows for that source key across all dates and returns `CoverageResult(Total, Covered, Level)` — same `BuildResult` semantics as existing per-date methods (zero datapoints ⇒ `Level=Full, Total=0`).

2. `ILinkOrderService` (new) exposes `IReadOnlyList<string> GetOrderedRuleSourceKeys()` — the hardcoded, ranked list of source keys that have at least one rule. Initial implementation: `["spotify", "outlook"]` (in the order Spotify → Outlook, reflecting the two rules from Story 8.15). This list is the source of truth for "rule-named" membership AND ordering.

3. In the global Sources panel list, sources are ordered as follows:
   - **First:** sources whose `source_key` is in `ILinkOrderService.GetOrderedRuleSourceKeys()`, in that rank order.
   - **Second:** rule-less sources that have at least one datapoint (`CoverageResult.Total > 0`), sorted alphabetically by `display_name`.
   - **Hidden:** rule-less sources with zero datapoints are not shown.

4. Rule-named sources (in `ILinkOrderService`) appear in the list **even if `CoverageResult.Total == 0`** (showing `—` symbol, no count text).

5. Each source row in the global-mode list shows:
   - Coverage level symbol: `●` (Full, Total > 0), `◐` (Partial), `○` (None), `—` (Full, Total == 0) — from `DataSourceSummaryViewModel.CoverageLevelSymbol`.
   - Count text `"N/M linked"` visible only when `Total > 0` — from `DataSourceSummaryViewModel.CoverageCountText`.
   - All existing fields: `DisplayName`, `LastDataDateLabel`, Import button (unchanged behavior).

6. Coverage is loaded inside `DataSourcePanelViewModel.LoadSourcesAsync()` — one `GetSourceTotalCoverageAsync` call per source — and refreshes alongside the existing source list on `DataSourceImportCompletedMessage`.

7. Integration test `GetSourceTotalCoverage_AllLinked_ReturnsFullCoverage` — seed 3 `data_point` rows + 3 matching `link` rows (`state='linked'`) for `source_key='toggl_entry'`; assert `Total=3, Covered=3, Level=Full`.

8. Integration test `GetSourceTotalCoverage_PartialCoverage_ReturnsPartial` — seed 4 datapoints, 2 links; assert `Level=Partial, Covered=2, Total=4`.

9. Integration test `GetSourceTotalCoverage_ZeroDatapoints_ReturnsFull` — no rows for source; assert `Total=0, Covered=0, Level=Full`.

10. Unit test `LoadSources_RuleNamedSource_AppearsEvenWithZeroDatapoints` — mock `ICoverageService.GetSourceTotalCoverageAsync` returning `CoverageResult(0, 0, Full)` for a source whose `source_key` is in `ILinkOrderService`; assert it is included in `Sources`.

11. Unit test `LoadSources_RulelessSourceZeroDatapoints_IsHidden` — mock coverage returning `CoverageResult(0, 0, Full)` for a source NOT in the link order; assert it is excluded from `Sources`.

12. Unit test `LoadSources_OrderedByLinkOrder` — two rule-named sources (Spotify before Outlook) + one rule-less source with data; assert display order: Spotify, Outlook, then the rule-less source.

---

## Tasks / Subtasks

- [ ] Task 1: Extend `ICoverageService` + `CoverageService` with total-rollup method (AC: #1, #7, #8, #9)
  - [ ] 1.1 Add to `Services/ICoverageService.cs`:
    ```csharp
    Task<CoverageResult> GetSourceTotalCoverageAsync(string sourceKey, CancellationToken ct = default);
    ```
  - [ ] 1.2 Implement in `Services/CoverageService.cs` using the same ADO.NET pattern as existing methods, but **no date filter**:
    ```sql
    SELECT COUNT(dp.data_point_id),
           SUM(CASE WHEN l.link_id IS NOT NULL THEN 1 ELSE 0 END)
    FROM data_point dp
    LEFT JOIN link l ON l.data_point_id = dp.data_point_id
    WHERE dp.source_key = @sk
    ```
    Use the same `try/catch SqliteException` fallback for missing `link` table (identical to existing methods).
  - [ ] 1.3 Add tests to `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` (three cases: full, partial, zero-datapoints — ACs #7, #8, #9). Follow the same in-memory SQLite pattern already in that file.

- [ ] Task 2: Add `ILinkOrderService` and `LinkOrderService` (AC: #2, #3, #4)
  - [ ] 2.1 Create `Services/ILinkOrderService.cs`:
    ```csharp
    public interface ILinkOrderService
    {
        // Returns source keys of rule-named sources, in link order (rank 0 = highest priority).
        IReadOnlyList<string> GetOrderedRuleSourceKeys();
    }
    ```
  - [ ] 2.2 Create `Services/LinkOrderService.cs`:
    ```csharp
    public sealed class LinkOrderService : ILinkOrderService
    {
        private static readonly IReadOnlyList<string> _order =
            ["spotify", "outlook"];

        public IReadOnlyList<string> GetOrderedRuleSourceKeys() => _order;
    }
    ```
    > **Note to implementer:** this list must be manually extended whenever a new `ILinkRule` is added for a new source. Add new sources at the logical priority position (Toggl before Spotify per concepts.md §7 sequencing guidance — update when Toggl rule lands).
  - [ ] 2.3 Register in `App.xaml.cs`:
    ```csharp
    services.AddSingleton<ILinkOrderService, LinkOrderService>();
    ```

- [ ] Task 3: Update `DataSourceSummaryViewModel` — add coverage fields (AC: #5)
  - [ ] 3.1 Add constructor parameter `CoverageResult coverage` (after existing params).
  - [ ] 3.2 Add `public CoverageResult Coverage { get; }` backed by the constructor arg.
  - [ ] 3.3 Add computed display properties:
    ```csharp
    public string CoverageLevelSymbol => Coverage.Level switch
    {
        CoverageLevel.Full when Coverage.Total == 0 => "—",
        CoverageLevel.Full  => "●",
        CoverageLevel.Partial => "◐",
        CoverageLevel.None  => "○",
        _ => "○"
    };
    public string CoverageCountText =>
        Coverage.Total > 0 ? $"{Coverage.Covered}/{Coverage.Total} linked" : string.Empty;
    public Visibility CoverageCountVisibility =>
        Coverage.Total > 0 ? Visibility.Visible : Visibility.Collapsed;
    ```

- [ ] Task 4: Update `DataSourcePanelViewModel.LoadSourcesAsync` — inject new services, apply ordering and filtering (AC: #3, #4, #6)
  - [ ] 4.1 Inject `ILinkOrderService _linkOrderService` and `ICoverageService _coverageService` into the constructor (add fields).
  - [ ] 4.2 In `LoadSourcesAsync`, after fetching sources from the repository, build coverage per source:
    ```csharp
    var coverageByKey = new Dictionary<string, CoverageResult>();
    foreach (var source in allSources)
    {
        coverageByKey[source.SourceKey] =
            await _coverageService.GetSourceTotalCoverageAsync(source.SourceKey, ct);
    }
    ```
  - [ ] 4.3 Get rule-named source keys: `var ruleKeys = _linkOrderService.GetOrderedRuleSourceKeys();`
  - [ ] 4.4 Filter and partition:
    ```csharp
    var ruleNamedSources = ruleKeys
        .Select(key => allSources.FirstOrDefault(s => s.SourceKey == key))
        .Where(s => s != null)
        .Select(s => s!);

    var rulelessSources = allSources
        .Where(s => !ruleKeys.Contains(s.SourceKey))
        .Where(s => coverageByKey.TryGetValue(s.SourceKey, out var cov) && cov.Total > 0)
        .OrderBy(s => s.DisplayName);
    ```
  - [ ] 4.5 Map to `DataSourceSummaryViewModel`, passing `coverageByKey[source.SourceKey]` for rule-named sources (use `new CoverageResult(0, 0, CoverageLevel.Full)` as fallback if a rule-named source is not yet registered in the DB):
    ```csharp
    var ordered = ruleNamedSources.Concat(rulelessSources);
    Sources = new ObservableCollection<DataSourceSummaryViewModel>(
        ordered.Select(s => new DataSourceSummaryViewModel(
            s.DataSourceId, s.SourceKey, s.DisplayName,
            /* lastDataDateLabel */ ...,
            /* lastImportedRelativeLabel */ ...,
            coverageByKey.GetValueOrDefault(s.SourceKey, new CoverageResult(0, 0, CoverageLevel.Full)),
            /* hasImportHandler */ ...,
            /* importCommand */ ...)));
    ```
  - [ ] 4.6 Register `ICoverageService` in the constructor; it is already registered as singleton in `App.xaml.cs` (done in Story 8.10). Just add the constructor parameter and field.

- [ ] Task 5: Update `DataSourcePanelControl.xaml` — add coverage display in global source list (AC: #5)
  - [ ] 5.1 Locate the global-mode `ListView`/`ItemsControl` item template (added in Story 5.4).
  - [ ] 5.2 In each item, add a coverage `StackPanel` (right-aligned or below `DisplayName`):
    ```xml
    <StackPanel Orientation="Horizontal" Spacing="4">
        <TextBlock Text="{Binding CoverageLevelSymbol}" FontSize="14"/>
        <TextBlock
            Text="{Binding CoverageCountText}"
            FontSize="11"
            Opacity="0.7"
            Visibility="{Binding CoverageCountVisibility}"/>
    </StackPanel>
    ```
  - [ ] 5.3 Ensure the coverage row is visually distinguishable from the import date row. A `Grid`-based layout with coverage on the right column and name/dates on the left is idiomatic for this panel.

- [ ] Task 6: Tests (AC: #10, #11, #12)
  - [ ] 6.1 In `DataSourcePanelViewModelTests.cs`, mock `ILinkOrderService` returning `["spotify"]` and `ICoverageService.GetSourceTotalCoverageAsync` per source.
  - [ ] 6.2 Add `LoadSources_RuleNamedSource_AppearsEvenWithZeroDatapoints` — mock coverage returns `CoverageResult(0,0,Full)` for `"spotify"` source; assert `Sources` contains that source.
  - [ ] 6.3 Add `LoadSources_RulelessSourceZeroDatapoints_IsHidden` — mock a `"toggl_entry"` source (not in link order) with `CoverageResult(0,0,Full)`; assert NOT in `Sources`.
  - [ ] 6.4 Add `LoadSources_OrderedByLinkOrder` — link order = `["spotify", "outlook"]`; three sources: `outlook`, `spotify`, `toggl_entry` (toggl with Total=5); assert order: spotify → outlook → toggl_entry.

---

## Dev Notes

### `DataSourceSummaryViewModel` constructor call site

`DataSourcePanelViewModel.LoadSourcesAsync` currently constructs `DataSourceSummaryViewModel` without a `coverage` parameter (Story 5.4 pattern). After this story the updated signature adds `CoverageResult coverage` as the 6th parameter. Grep for `new DataSourceSummaryViewModel(` to find all call sites.

### Coverage display position in XAML

The global-mode item template (from Story 5.4) has a vertical stack: `DisplayName` (prominent) → `LastDataDateLabel` (secondary) → `LastImportedRelativeLabel` (muted). Insert the coverage `StackPanel` after these, or use a `Grid` with a right column for the coverage glyph + count (aligned with the Import button row). Match the existing `DataSourceDayCardViewModel` day-mode coverage display pattern from Story 8.10 (`DataSourcePanelControl.xaml` Task 6 in story 8.10) for visual consistency.

### `ICoverageService.GetSourceTotalCoverageAsync` — SQL

Identical to `GetDateSourceCoverageAsync` minus the date WHERE clause:

```csharp
public async Task<CoverageResult> GetSourceTotalCoverageAsync(string sourceKey, CancellationToken ct = default)
{
    await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync(ct);

    try
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(dp.data_point_id),
                   SUM(CASE WHEN l.link_id IS NOT NULL THEN 1 ELSE 0 END)
            FROM data_point dp
            LEFT JOIN link l ON l.data_point_id = dp.data_point_id
            WHERE dp.source_key = @sk";
        cmd.Parameters.Add(new SqliteParameter("@sk", sourceKey));

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var total = reader.GetInt32(0);
            var covered = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            return BuildResult(total, covered);  // existing private helper
        }
        return new CoverageResult(0, 0, CoverageLevel.Full);
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
    {
        // link table doesn't exist yet (pre-8.12)
        await using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM data_point WHERE source_key=@sk";
        cmd2.Parameters.Add(new SqliteParameter("@sk", sourceKey));
        var total = Convert.ToInt32(await cmd2.ExecuteScalarAsync(ct));
        return new CoverageResult(total, 0, total > 0 ? CoverageLevel.None : CoverageLevel.Full);
    }
}
```

If `data_point` table also doesn't exist (pre-8.7), add a second `catch (SqliteException)` returning `new CoverageResult(0, 0, CoverageLevel.Full)`.

### Link order list expansion

The initial `LinkOrderService` list contains only the two sources from Stories 8.15 rules. When future rules land (e.g., a Toggl rule), update `LinkOrderService._order` to include the new source key at the appropriate position. Per concepts.md §7 sequencing guidance ("finish Toggl before Spotify"), the intended long-term order is approximately:

```
toggl_entry → call_log → civ5_session → comfyui_job → spotify → outlook → youtube → maps → voice_memo → chrome_search
```

Only `spotify` and `outlook` have rules today. The rest are rule-less and will appear only when they have data — no link-order registration needed yet.

### Rule-named source not in DB

A source can be in the link order but not yet registered in the `data_source` table (e.g., Outlook hasn't been connected). In this case `allSources.FirstOrDefault(s => s.SourceKey == key)` returns `null` and the `Where(s => s != null)` guard silently drops it. This is correct — the source can't appear without being registered; the rule simply has no effect yet.

### `DataSourceSummaryViewModel.ImportCommand` and `HasImportHandler`

These are unchanged. Pass them through from the existing `DataSourceImportHandlerRegistry` lookup, as in Story 5.4.

### DI registration checklist

| Service | Registration | Story added |
|---------|-------------|-------------|
| `ICoverageService` → `CoverageService` | `AddSingleton` in `App.xaml.cs` | 8.10 ✓ |
| `ILinkOrderService` → `LinkOrderService` | `AddSingleton` in `App.xaml.cs` | **9.2 (this story)** |

### Project structure — files changed

| Action | File |
|--------|------|
| Modify | `Services/ICoverageService.cs` — add `GetSourceTotalCoverageAsync` |
| Modify | `Services/CoverageService.cs` — implement new method |
| Add | `Services/ILinkOrderService.cs` |
| Add | `Services/LinkOrderService.cs` |
| Modify | `ViewModels/DataSourceSummaryViewModel.cs` — add `Coverage` + display helpers |
| Modify | `ViewModels/DataSourcePanelViewModel.cs` — inject services, reorder/filter logic |
| Modify | `Views/DataSourcePanelControl.xaml` — coverage glyph + count in global list |
| Modify | `App.xaml.cs` — register `ILinkOrderService` |
| Modify | `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` — 3 new tests |
| Modify | `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` — 3 new tests, mock new services |

### References

- Canonical vocabulary + coverage model: [concepts.md §2, §6, §7](../../epic-8-data-linking/concepts.md)
- Epic 9 story spec: [epic-overview.md §Story 9.2](../epic-overview.md)
- `ICoverageService` + `CoverageService` (to extend): `Services/ICoverageService.cs`, `Services/CoverageService.cs`
- `CoverageResult` / `CoverageLevel` (defined in 8.10): `Models/CoverageResult.cs`
- `DataSourceSummaryViewModel` (to extend): `ViewModels/DataSourceSummaryViewModel.cs`
- `DataSourcePanelViewModel.LoadSourcesAsync` call site: `ViewModels/DataSourcePanelViewModel.cs`
- XAML global source list template (from Story 5.4): `Views/DataSourcePanelControl.xaml`
- DI registrations: `App.xaml.cs`
- `DataSourcePanelViewModelTests` (to extend): `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs`
- `CoverageServiceTests` (to extend): `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs`
- Existing day-mode coverage XAML pattern: Story 8.10 Task 6 in [8-10-coverage-service-and-delete-date-source-integration.md](../../epic-8-data-linking/stories/8-10-coverage-service-and-delete-date-source-integration.md)

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

# Story 9.8: Coverage Indicators Everywhere + Certified-Day Unlinked Dot

Status: ready-for-dev
**Agent:** Sonnet · **Effort:** low
**Dependencies:** 8.10 (blocking — `ICoverageService`, `CoverageResult`, `CoverageLevel` must exist); 9.1 (blocking — 3-panel + icon-strip structure must exist). By the time this story is dev'd, 9.2–9.7 will also be complete (coverage glyphs will exist inline in each panel from their own stories; this story extracts them into a shared component and adds the certified-day dot).

---

## Story

As a user,
I want coverage glyphs (`● / ◐ / ○`) rendered by a single shared component and a subtle dot on certified days that still have unlinked data,
so that coverage status looks consistent everywhere and I can tell at a glance which certified days have raw data still waiting for review.

---

## Acceptance Criteria

1. A `CoverageGlyphControl` (WinUI 3 `UserControl`) exists at `Views/CoverageGlyphControl.xaml`. It accepts a bindable `CoverageResult Coverage` dependency property and renders:
   - Symbol: `●` (Full, Total > 0), `◐` (Partial), `○` (None), `—` (Full + Total == 0 / N/A)
   - Optional count label `"N/M linked"` — visible only when `Total > 0`
   - Compact layout: symbol (14px) + count text (11px, 70% opacity) side-by-side with 4px spacing
2. All existing inline glyph + count TextBlock pairs from previous stories (8.10 day-card, 9.2 sources panel, 9.3 By-Source lens, 9.4 By-Event lens) are replaced with `<CoverageGlyphControl Coverage="{Binding Coverage}"/>`. No inline glyph rendering remains outside this control.
3. The Sources panel global-mode list (`DataSourceSummaryViewModel`, Story 5.4) exposes a `CoverageResult Coverage` property populated by `ICoverageService.GetDateSourceCoverageAsync` for the current day aggregate (or `CoverageResult(0, 0, Full)` when no day is selected). The Sources panel XAML renders it via `CoverageGlyphControl`.
4. The Day Detail panel card (`DataSourceDayCardViewModel`, Story 8.10) uses `CoverageGlyphControl` in `DataSourcePanelControl.xaml` instead of the inline TextBlocks added in 8.10.
5. The Linking panel By-Source lens source header shows the per-source coverage glyph via `CoverageGlyphControl` (bound to `ICoverageService.GetDateSourceCoverageAsync` for the selected date range).
6. The Linking panel By-Event lens shows event-level coverage via `CoverageGlyphControl` (bound to `ICoverageService.GetEventCoverageAsync`).
7. **Certified-day dot:** When a calendar day cell represents a date where `date_state.approved = true` AND `ICoverageService.GetDayCoverageAsync(date)` returns `Level != Full` and `Total > 0` — a small filled dot (4–6px, source-neutral gray or accent color, distinct from event color dots) appears in the corner of the day cell.
8. The certified-day dot is visible in all calendar views that show individual day cells (month view, week view, day view). Year view is excluded (too small).
9. Day certification (setting `date_state.approved`) is **not gated** on coverage level — the user can certify a day that has unlinked data; the dot is purely informational.
10. Coverage queries for the calendar day cells are batched: `MainViewModel` (or the calendar view model) fetches certified-unlinked status for all visible days in a single async pass, not one query per cell.
11. `dotnet test` passes — no new test failures. New unit test: `CoverageGlyphControl_ViewModel` (or a simple VM helper) verifies the symbol/count output for all four states (Full+data, Full+no-data, Partial, None).

---

## Tasks / Subtasks

- [ ] **Task 1: Create `CoverageGlyphControl`** (AC: #1)
  - [ ] 1.1 Add `Views/CoverageGlyphControl.xaml` + `Views/CoverageGlyphControl.xaml.cs`
  - [ ] 1.2 Define `public CoverageResult Coverage` as a `DependencyProperty` (or use MVVM binding via a thin VM)
  - [ ] 1.3 XAML layout:
    ```xml
    <UserControl ...>
        <StackPanel Orientation="Horizontal" Spacing="4">
            <TextBlock x:Name="SymbolText" FontSize="14" VerticalAlignment="Center"/>
            <TextBlock x:Name="CountText"  FontSize="11" Opacity="0.7" VerticalAlignment="Center"/>
        </StackPanel>
    </UserControl>
    ```
  - [ ] 1.4 In code-behind, compute symbol + count from `Coverage` when the property changes:
    ```csharp
    SymbolText.Text = Coverage.Level switch {
        CoverageLevel.Full when Coverage.Total == 0 => "—",
        CoverageLevel.Full    => "●",
        CoverageLevel.Partial => "◐",
        CoverageLevel.None    => "○",
        _                     => "○"
    };
    CountText.Text       = Coverage.Total > 0 ? $"{Coverage.Covered}/{Coverage.Total} linked" : string.Empty;
    CountText.Visibility = Coverage.Total > 0 ? Visibility.Visible : Visibility.Collapsed;
    ```
  - [ ] 1.5 Register nothing in DI (it is a control, not a service)

- [ ] **Task 2: Replace inline glyphs in Day Detail panel** (AC: #2, #4)
  - [ ] 2.1 In `Views/DataSourcePanelControl.xaml`, locate the `StackPanel` added by story 8.10 (approx lines where `CoverageLevelSymbol` TextBlock and `CoverageCountText` TextBlock were added in the day-mode card)
  - [ ] 2.2 Replace that entire `StackPanel` with `<local:CoverageGlyphControl Coverage="{Binding Coverage}"/>`
  - [ ] 2.3 Verify `DataSourceDayCardViewModel.Coverage` property already exists (added in 8.10 Task 4.3); no VM changes needed

- [ ] **Task 3: Replace inline glyphs in Sources panel** (AC: #2, #3)
  - [ ] 3.1 In `DataSourceSummaryViewModel` (Story 5.4), add:
    ```csharp
    public CoverageResult Coverage { get; private set; } = new(0, 0, CoverageLevel.Full);
    ```
  - [ ] 3.2 In `DataSourcePanelViewModel.LoadSourcesAsync()`, after building each `DataSourceSummaryViewModel`, call `await _coverageService.GetDateSourceCoverageAsync(today, source.SourceKey)` (or the selected day if one is active) and set `Coverage` on the VM. Use `DateOnly.FromDateTime(DateTime.Today)` as the default date when no day is selected.
  - [ ] 3.3 Inject `ICoverageService` into `DataSourcePanelViewModel` constructor (may already be injected from 9.2 — verify before adding)
  - [ ] 3.4 In `DataSourcePanelControl.xaml` global-mode item template, replace any inline glyph elements (added by Story 9.2) with `<local:CoverageGlyphControl Coverage="{Binding Coverage}"/>`

- [ ] **Task 4: Replace inline glyphs in Linking panel lenses** (AC: #2, #5, #6)
  - [ ] 4.1 Locate By-Source lens XAML (added by Story 9.3) — find where per-source coverage symbols are rendered inline; replace with `<local:CoverageGlyphControl Coverage="{Binding Coverage}"/>`
  - [ ] 4.2 Locate By-Event lens XAML (added by Story 9.4) — find event-coverage glyph inline rendering; replace with `<local:CoverageGlyphControl Coverage="{Binding Coverage}"/>` bound to the event-level `CoverageResult`
  - [ ] 4.3 Ensure the VM properties in those lenses are named `Coverage` (type `CoverageResult`) — if differently named, add a `Coverage` alias or rename uniformly

- [ ] **Task 5: Certified-day dot on calendar** (AC: #7, #8, #9, #10)
  - [ ] 5.1 Add method to coverage-aware calendar VM (likely `MainViewModel` or `CalendarDayViewModel`):
    ```csharp
    public async Task<HashSet<DateOnly>> GetCertifiedUnlinkedDaysAsync(
        DateOnly rangeStart, DateOnly rangeEnd, CancellationToken ct = default)
    ```
    — queries `date_state` for `approved = true` in the range, then filters to those where `ICoverageService.GetDayCoverageAsync` returns `Level != Full && Total > 0`. Returns the set of matching dates.
  - [ ] 5.2 In the calendar view model, when the visible range changes (month/week/day navigation), call `GetCertifiedUnlinkedDaysAsync` for the newly visible range and populate an `ObservableCollection<DateOnly> CertifiedUnlinkedDays` (or a `HashSet` wrapped in an observable property).
  - [ ] 5.3 In day cell XAML templates (month, week, day views), bind a small `Ellipse` (or `Border` with `CornerRadius="3"` 4–6px) to visibility — visible when the date is in `CertifiedUnlinkedDays`. Place it in the top-right corner of the cell, distinct from event indicators.
    ```xml
    <!-- Inside day cell template -->
    <Ellipse Width="5" Height="5"
             Fill="{ThemeResource SystemAccentColor}"
             HorizontalAlignment="Right" VerticalAlignment="Top"
             Margin="0,3,3,0"
             Visibility="{x:Bind IsCertifiedUnlinked, Mode=OneWay,
                          Converter={StaticResource BoolToVisibilityConverter}}"/>
    ```
  - [ ] 5.4 Each day cell VM (or the converter) checks the `CertifiedUnlinkedDays` set: `IsCertifiedUnlinked = CertifiedUnlinkedDays.Contains(date)`
  - [ ] 5.5 Ensure certification (setting `date_state.approved = true`) does NOT gate on coverage — no validation added to the approval/certification write path. The dot is read-only; it does not interact with the certification toggle.
  - [ ] 5.6 Year view: skip dot rendering (cells too small — no change to year view template)

- [ ] **Task 6: Tests** (AC: #11)
  - [ ] 6.1 Add `Unit/ViewModels/CoverageGlyphViewModelTests.cs` (or test the symbol/count helper directly):
    - `Symbol_Full_WithData_ReturnsBullet` — `new CoverageResult(5, 5, Full)` → symbol `●`
    - `Symbol_Full_NoData_ReturnsDash` — `new CoverageResult(0, 0, Full)` → symbol `—`
    - `Symbol_Partial_ReturnsHalfCircle` — `new CoverageResult(5, 2, Partial)` → symbol `◐`
    - `Symbol_None_ReturnsEmptyCircle` — `new CoverageResult(5, 0, None)` → symbol `○`
    - `CountText_HiddenWhenTotalZero` — `Total = 0` → count text empty / hidden
  - [ ] 6.2 Ensure existing 8.10 tests (`CoverageServiceTests`) still pass unchanged

---

## Dev Notes

### CoverageResult / CoverageLevel — already exists

From Story 8.10, the following are already in the codebase:
```csharp
// GoogleCalendarManagement.Core/Models/CoverageResult.cs
public sealed record CoverageResult(int Total, int Covered, CoverageLevel Level);

// GoogleCalendarManagement.Core/Models/CoverageResult.cs (or same file)
public enum CoverageLevel { Full, Partial, None }

// GoogleCalendarManagement.Core/Services/ICoverageService.cs
public interface ICoverageService
{
    Task<CoverageResult> GetDateSourceCoverageAsync(DateOnly date, string sourceKey, CancellationToken ct = default);
    Task<CoverageResult> GetDayCoverageAsync(DateOnly date, CancellationToken ct = default);
    Task<CoverageResult> GetEventCoverageAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
}
```

`ICoverageService` is registered as singleton in `App.xaml.cs`.

### DataSourceDayCardViewModel — from 8.10

`DataSourceDayCardViewModel` already has:
```csharp
public CoverageResult Coverage { get; }
public string CoverageLevelSymbol => ...  // computed
public string CoverageCountText => ...    // computed
public Visibility CoverageCountVisibility => ...
```

In Task 2, you replace the XAML TextBlocks that bound to these computed properties with `CoverageGlyphControl`. The computed properties on the VM (`CoverageLevelSymbol`, etc.) can be kept (they cost nothing) or removed — either is fine. Remove only if you are confident no other binding references them.

### Sources panel — DataSourceSummaryViewModel (Story 5.4)

Current file: `ViewModels/DataSourceSummaryViewModel.cs`

Current properties: `DataSourceId`, `SourceKey`, `DisplayName`, `LastDataDateLabel`, `LastImportedRelativeLabel`, `HasImportHandler`, `ImportCommand`.

Story 9.2 may have already added `Coverage`; if so, Task 3.1 is a no-op — just verify the binding path and replace the inline XAML glyph with `CoverageGlyphControl`.

### Certified day = `date_state.approved`

The canonical concept is "certify a day" (concepts.md §2). In the current database schema, this maps to the `approved` column on the `date_state` table (`DateState.Approved` entity property). Story 8.1 (terminology sweep) may rename code references to `Certified`/`IsCertified`; check whether 8.1 has been run and adjust the property name accordingly.

### Batch coverage query for calendar dot (Task 5.2)

Querying coverage for every day cell individually on render would be slow. Batch strategy:
1. On navigation, collect the full set of visible dates.
2. Call `ICoverageService.GetDayCoverageAsync` for each in parallel:
   ```csharp
   var tasks = visibleDates.Select(d => _coverageService.GetDayCoverageAsync(d, ct));
   var results = await Task.WhenAll(tasks);
   ```
3. Post-filter: certified (`date_state.approved = true`) AND (`Level != Full` AND `Total > 0`).
4. The `date_state` query for approved days can be batched in one SQL: `SELECT date FROM date_state WHERE date >= @start AND date <= @end AND approved = 1`.

If the visible range is large (e.g., month = 31 days, week = 7), this is acceptable. Avoid re-querying on every frame; re-query only on navigation or on `DataSourceImportCompletedMessage`.

### WinUI 3 DependencyProperty pattern

For `CoverageGlyphControl`, if using a `DependencyProperty` (recommended for binding from outside):
```csharp
public static readonly DependencyProperty CoverageProperty =
    DependencyProperty.Register(nameof(Coverage), typeof(CoverageResult),
        typeof(CoverageGlyphControl),
        new PropertyMetadata(new CoverageResult(0, 0, CoverageLevel.Full), OnCoverageChanged));

private static void OnCoverageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    => ((CoverageGlyphControl)d).UpdateDisplay((CoverageResult)e.NewValue);

public CoverageResult Coverage
{
    get => (CoverageResult)GetValue(CoverageProperty);
    set => SetValue(CoverageProperty, value);
}
```

Alternatively, wrap in a thin `CoverageGlyphViewModel : ObservableObject` with a `Coverage` property and computed `Symbol` / `CountText` — same approach as `DataSourceDayCardViewModel`. Use whichever pattern matches existing similar controls in the codebase.

### Avoid MVVMTK0045

If using `[ObservableProperty]` in a WinRT-targeted ViewModel, the existing codebase uses **manual `SetProperty` pattern** to avoid MVVMTK0045 (see Story 5.2 dev notes). Follow the same pattern for any new VM code.

### Project structure — files touched

| Action | File |
|--------|------|
| Add | `Views/CoverageGlyphControl.xaml` |
| Add | `Views/CoverageGlyphControl.xaml.cs` |
| Modify | `Views/DataSourcePanelControl.xaml` — day-card: replace inline glyph StackPanel → `CoverageGlyphControl` |
| Modify | `Views/DataSourcePanelControl.xaml` — global-mode row: replace inline glyph → `CoverageGlyphControl` |
| Modify | `ViewModels/DataSourceSummaryViewModel.cs` — add `Coverage` property if not already added by 9.2 |
| Modify | `ViewModels/DataSourcePanelViewModel.cs` — populate coverage in `LoadSourcesAsync` if not already done by 9.2 |
| Modify | Linking panel XAML (By-Source lens, By-Event lens) — replace inline glyphs → `CoverageGlyphControl` |
| Modify | Calendar day cell templates (month, week, day views) — add certified-unlinked dot `Ellipse` |
| Modify | `ViewModels/MainViewModel.cs` (or equivalent) — add `GetCertifiedUnlinkedDaysAsync` + `CertifiedUnlinkedDays` property |
| Add | `GoogleCalendarManagement.Tests/Unit/ViewModels/CoverageGlyphViewModelTests.cs` (or `CoverageGlyphControlTests.cs`) |

### Scope boundary

This story does NOT:
- Change the certification workflow (setting `date_state.approved`) — 9.8 is purely additive/read-only w.r.t. certification
- Add new `ICoverageService` methods (all three already exist from 8.10)
- Change how coverage is computed (8.10 owns that)
- Add coverage to the Gaps lens (9.5) — that lens doesn't have per-source coverage, only gap resolution state
- Change the year view day cells

### References

- Coverage model + `ICoverageService`: [concepts.md §6](../../epic-8-data-linking/concepts.md#6-coverage), [Story 8.10](../../epic-8-data-linking/stories/8-10-coverage-service-and-delete-date-source-integration.md)
- Glyph symbols already used in 8.10: `Views/DataSourcePanelControl.xaml` (~lines added in Task 6 of 8.10); `ViewModels/DataSourceDayCardViewModel.cs` (computed `CoverageLevelSymbol` etc.)
- Day-mode panel: `Views/DataSourcePanelControl.xaml`, `ViewModels/DataSourceDayCardViewModel.cs`, `ViewModels/DataSourcePanelViewModel.cs`
- Sources panel (global mode): `ViewModels/DataSourceSummaryViewModel.cs`, `ViewModels/DataSourcePanelViewModel.cs`
- 3-panel layout structure: `Views/MainPage.xaml` (Story 5.2)
- `BoolToVisibilityConverter`: `Converters/BoolToVisibilityConverter.cs`
- `date_state` entity (certification field): `Data/Entities/DateState.cs` — `Approved` / `IsCertified` property
- Epic 9 overview: [epic-overview.md](../epic-overview.md) §Story 9.8

---

## Dev Agent Record

### Agent Model Used

{{agent_model_name_version}}

### Debug Log References

### Completion Notes List

### File List

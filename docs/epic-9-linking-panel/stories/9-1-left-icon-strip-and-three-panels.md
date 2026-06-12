# Story 9.1: Left Icon Strip + 3 Selectable Panels

**Status:** ready-for-dev
**Agent:** Sonnet · **Effort:** medium
**Epic:** 9 — Linking Panel & Workflows
**Prereqs:** Epic 5 panels (all done), Epic 9 overview

---

## Story

As a **user**,
I want **a VS Code-style icon strip on the left edge to explicitly choose which panel is shown**,
so that **selecting a day on the calendar no longer forces the panel to switch, and I can freely switch between Sources, Day Detail, and Linking panels**.

---

## Acceptance Criteria

### AC-9.1.1 — Icon strip renders alongside the left panel
A vertical icon strip (~48px wide) is added to the left side of `DataSourcePanelControl`.
It contains three icon buttons, stacked vertically near the top:
- **Sources** icon (e.g. Segoe UI Symbol `&#xE8F1;` / "Library" or similar)
- **Day Detail** icon (e.g. `&#xE787;` / "Calendar")
- **Linking** icon (e.g. `&#xE71B;` / "Link")

The active panel's icon button is visually highlighted (accent color background or selected state).

### AC-9.1.2 — Clicking an icon switches the panel body
Clicking a strip icon sets the active panel to that panel's content area. Only one panel body is shown at a time:
- Sources → shows the existing global source-list content (current `GlobalModeVisibility` content)
- Day Detail → shows the existing per-day content (current `DayModeVisibility` content)
- Linking → shows a placeholder `TextBlock` "Linking panel — coming in later stories"

The icon strip is always visible regardless of which panel is active.

### AC-9.1.3 — Selecting a day no longer force-switches panels
When the user clicks a day number in the calendar (triggering `DaySelectedMessage`), the active panel does **not** change. The `DataSourcePanelViewModel` updates its internal `CurrentDay` and loads day data in the background, but `ActivePanel` remains unchanged. The previous auto-switch behavior in `ApplySelectedDay` must be removed.

### AC-9.1.4 — Day Detail panel shows "Select a day" prompt when no day is selected
When the Day Detail panel is active (`ActivePanel == PanelKind.DayDetail`) and `CurrentDay` is null, the panel body shows a centered "Select a day to see its data" placeholder instead of the source card list.

### AC-9.1.5 — Sources and Day Detail panels preserve their existing content
Switching away from Sources and back must not reset the scroll position or trigger a full reload. Switching away from Day Detail and back preserves `CurrentDay` and `DrilldownCard` state. The existing content/data loading logic is unchanged — only the visibility conditions are rewired to `ActivePanel`.

### AC-9.1.6 — Active panel persists across app restarts
`DataSourcePanelViewModel.ActivePanel` is read from and written to the `system_state` table using key `"DataSourcePanelActivePanel"` (values: `"Sources"`, `"DayDetail"`, `"Linking"`). Default on first launch: `"Sources"`. On restore, the correct panel body is shown.

### AC-9.1.7 — Minimize/restore behavior is unchanged
The existing minimize button (top-right chevron) and restore tab behavior from Story 5.2 are fully preserved. When minimized, the icon strip is also hidden (the restore tab replaces the entire left area as before).

### AC-9.1.8 — No regressions in existing functionality
`dotnet test` passes. Day-mode data loading, drilldown navigation, global-mode source list, import buttons, day-name header tap — all work correctly regardless of which panel is active.

---

## Technical Design

### Key design decision: icon strip inside `DataSourcePanelControl`

The icon strip is **internal to `DataSourcePanelControl`** — no changes to `MainPage.xaml` column structure needed.

The control's root `Grid` gets a two-column layout:
- Column 0: `Width="48"` — icon strip (always visible)
- Column 1: `Width="*"` — panel body content area (varies by `ActivePanel`)

The existing minimize/restore tab logic is unchanged; when `IsMinimized = true`, both columns collapse as before.

### New enum + VM property

```csharp
// In DataSourcePanelViewModel.cs (or a new file, same namespace)
public enum PanelKind { Sources, DayDetail, Linking }
```

```csharp
// In DataSourcePanelViewModel
private PanelKind _activePanel = PanelKind.Sources;

public PanelKind ActivePanel
{
    get => _activePanel;
    set
    {
        if (SetProperty(ref _activePanel, value))
        {
            OnPropertyChanged(nameof(SourcesPanelVisibility));
            OnPropertyChanged(nameof(DayDetailPanelVisibility));
            OnPropertyChanged(nameof(LinkingPanelVisibility));
            OnPropertyChanged(nameof(DayDetailPlaceholderVisibility));
            // ... other derived visibilities
            _ = _systemStateRepository.SetAsync(ActivePanelStateKey,
                value.ToString());
        }
    }
}

public Visibility SourcesPanelVisibility =>
    ActivePanel == PanelKind.Sources ? Visibility.Visible : Visibility.Collapsed;

public Visibility DayDetailPanelVisibility =>
    ActivePanel == PanelKind.DayDetail ? Visibility.Visible : Visibility.Collapsed;

public Visibility LinkingPanelVisibility =>
    ActivePanel == PanelKind.Linking ? Visibility.Visible : Visibility.Collapsed;

public Visibility DayDetailPlaceholderVisibility =>
    ActivePanel == PanelKind.DayDetail && CurrentDay is null
        ? Visibility.Visible : Visibility.Collapsed;
```

### Existing visibility properties to rewire

The current `IsGlobalMode` flag drives which panel body content is shown. After this story, `IsGlobalMode` is **retired** (or kept as a derived convenience property, no longer the primary switch). All visibility bindings currently using `IsGlobalMode` / `DayModeVisibility` / `GlobalModeVisibility` are replaced by `SourcesPanelVisibility` / `DayDetailPanelVisibility`.

**Before (current):**
```
GlobalModeVisibility → IsGlobalMode
DayModeVisibility    → !IsGlobalMode
```

**After:**
```
SourcesPanelVisibility   → ActivePanel == Sources
DayDetailPanelVisibility → ActivePanel == DayDetail
LinkingPanelVisibility   → ActivePanel == Linking
```

`IsGlobalMode` can be deleted entirely, or kept as `public bool IsGlobalMode => ActivePanel == PanelKind.Sources;` if other code depends on it — check all usages before deleting.

### `ApplySelectedDay` — remove the auto-switch

Current `ApplySelectedDay` sets `IsGlobalMode = true/false` based on whether `selectedDay` is null. **Remove that behavior.** The method should only:
- Update `CurrentDay`
- Update `DayLabel` / `DayName`
- Enqueue `LoadDayModeAsync` if `selectedDay` is not null
- Clear day state if `selectedDay` is null

It must NOT touch `ActivePanel`. Day Detail content still loads in the background; it's just not automatically shown until the user clicks the Day Detail icon.

### `InitializeAsync` changes

Add active panel restore:
```csharp
var storedPanel = await _systemStateRepository.GetAsync(ActivePanelStateKey);
ActivePanel = storedPanel switch
{
    "DayDetail" => PanelKind.DayDetail,
    "Linking"   => PanelKind.Linking,
    _           => PanelKind.Sources
};
```

Keep existing: load minimized state, load panel width, apply selected day, then conditionally load sources if `ActivePanel == Sources`.

Also pre-load day data if `ActivePanel == DayDetail && _daySelectionService.SelectedDay is not null`.

### Commands for icon strip buttons

Add three `IRelayCommand` properties:
```csharp
public IRelayCommand SelectSourcesPanelCommand { get; }
public IRelayCommand SelectDayDetailPanelCommand { get; }
public IRelayCommand SelectLinkingPanelCommand { get; }
```

Each sets `ActivePanel` to the corresponding `PanelKind`.

### Panel activation side-effects

When `ActivePanel` changes **to** a specific panel, trigger any needed load:
- → `Sources`: if `Sources.Count == 0 && !IsLoadingGlobal`, call `LoadSourcesAsync()`
- → `DayDetail`: if `CurrentDay is not null && DayCards.Count == 0`, call `LoadDayModeAsync(CurrentDay.Value)`
- → `Linking`: no-op (placeholder only in this story)

### Icon strip XAML structure

```xml
<!-- Inside the existing root Grid of DataSourcePanelControl, 
     add a two-column split to the full-panel Border's inner Grid -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="48" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <!-- Icon strip (Column 0) -->
    <Border Grid.Column="0"
            BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
            BorderThickness="0,0,1,0">
        <StackPanel Margin="0,8,0,0" Spacing="4">
            <Button Width="40" Height="40" Padding="0"
                    Command="{x:Bind ViewModel.SelectSourcesPanelCommand}"
                    ToolTipService.ToolTip="Sources"
                    Style="{x:Bind ViewModel.SourcesIconStyle, Mode=OneWay}">
                <FontIcon Glyph="&#xE8F1;" FontSize="16"
                          FontFamily="{ThemeResource SymbolThemeFontFamily}" />
            </Button>
            <Button Width="40" Height="40" Padding="0"
                    Command="{x:Bind ViewModel.SelectDayDetailPanelCommand}"
                    ToolTipService.ToolTip="Day Detail"
                    Style="{x:Bind ViewModel.DayDetailIconStyle, Mode=OneWay}">
                <FontIcon Glyph="&#xE787;" FontSize="16"
                          FontFamily="{ThemeResource SymbolThemeFontFamily}" />
            </Button>
            <Button Width="40" Height="40" Padding="0"
                    Command="{x:Bind ViewModel.SelectLinkingPanelCommand}"
                    ToolTipService.ToolTip="Linking"
                    Style="{x:Bind ViewModel.LinkingIconStyle, Mode=OneWay}">
                <FontIcon Glyph="&#xE71B;" FontSize="16"
                          FontFamily="{ThemeResource SymbolThemeFontFamily}" />
            </Button>
        </StackPanel>
    </Border>

    <!-- Panel body (Column 1) -->
    <Grid Grid.Column="1">
        <!-- existing header + body grid here, unchanged structure -->
    </Grid>
</Grid>
```

**Active icon styling:** use `x:Bind` to a computed `Style` property or a boolean `IsSourcesActive`, `IsDayDetailActive`, `IsLinkingActive`, and drive button background with a converter. Alternatively, use `RadioButton` with custom style (simpler for mutually exclusive selection). WinUI 3 `RadioButton` in a `StackPanel` with custom template may be the cleanest approach for the icon strip.

Simplest working approach: three `ToggleButton`s, with `IsChecked` bound to `IsSourcesActive` / `IsDayDetailActive` / `IsLinkingActive` (bool properties derived from `ActivePanel`), and a `Checked` event or command that sets `ActivePanel`. Make sure only one can be checked at a time by unchecking others in the setter.

### File changes summary

| File | Change |
|------|--------|
| `ViewModels/DataSourcePanelViewModel.cs` | Add `PanelKind` enum, `ActivePanel` property, commands, rewire visibility props, update `ApplySelectedDay`, update `InitializeAsync` |
| `Views/DataSourcePanelControl.xaml` | Add icon strip column; rewire `Visibility` bindings from `IsGlobalMode`/`DayModeVisibility` to new properties; add Linking placeholder |
| `Views/DataSourcePanelControl.xaml.cs` | No significant changes expected |

No changes to `MainPage.xaml`, `MainPage.xaml.cs`, `App.xaml.cs`, or any other files.

---

## REVISIT note (from Epic 9 overview)

> **REVISIT (Sarunas):** first appearance of the panel strip — review the switching model in the running app.

After implementing, verify in the running app:
1. Do the three icons feel natural and discoverable?
2. Does it feel odd that selecting a day no longer jumps to Day Detail?
3. Is the 48px icon strip width appropriate, or should it be narrower (e.g. 36px)?
4. Should the strip collapse when the panel is minimized (current design) or remain always-visible?

These are UX decisions for Sarunas to evaluate before 9.2 proceeds.

---

## Dev Guardrails

### DO NOT reinvent — use existing infrastructure
- **`ISystemStateRepository`** — already used in `DataSourcePanelViewModel` for `IsMinimized` and `PanelWidth`. Follow the exact same pattern for `ActivePanel`.
- **`WeakReferenceMessenger`** — already wired for `DaySelectedMessage`, `DataSourceImportCompletedMessage`, `CalendarViewRangeChangedMessage`, `DataSourceDayOpenRequestedMessage`. No new messages needed for this story.
- **`ICalendarDaySelectionService`** — already injected. `InitializeAsync` already calls `ApplySelectedDay(_daySelectionService.SelectedDay)`. Keep this call.
- **`SetProperty` pattern** — all existing VM properties use manual `SetProperty` (not `[ObservableProperty]`). Match this: the `MVVMTK0045 WinRT AOT` issue from Story 5.2 still applies.

### DO NOT break
- The existing drag-to-reorder for `DayCards` (`DayCardDragHandle_*` pointer events in XAML code-behind)
- The resize handle at the right edge of the panel body (`ResizeHandle_*` events)
- The minimize/restore tab (`MinimizeButton_Click`, `ExpandButton_Click`)
- `DayHeader_Tapped` → `OpenOrCreateDayNameEventAsync`
- Color swatch button click in `GlobalSourceCardTemplate`
- `DataSourcePanelControl_Loaded` → calls `ViewModel.InitializeAsync()`

### WinUI 3 / XAML patterns to follow
- `x:Bind` for all new bindings (not `{Binding}`)
- `Visibility` via computed properties on the VM (not converters) — this is the established pattern in this codebase
- `FontIcon` with `FontFamily="{ThemeResource SymbolThemeFontFamily}"` for Segoe icons
- Button styles: use default WinUI button style; for selected state, a `ToggleButton` with `IsChecked` is cleanest
- `ObservableObject` base class (not `ObservableRecipient`) — DataSourcePanelViewModel uses `ObservableObject`

### Testing
- `dotnet test` must pass (run from the solution root)
- No new tests are required for this story (UI switching model; same as Story 5.2 approach)
- Manual smoke test checklist:
  - [ ] Icon strip visible; three icons render correctly
  - [ ] Clicking each icon switches the panel body
  - [ ] Sources panel shows source list (same as before)
  - [ ] Day Detail panel shows "Select a day" prompt when no day is selected
  - [ ] Select a day → Day Detail panel populates but active panel stays on Sources
  - [ ] Click Day Detail icon → day data shows immediately (already loaded)
  - [ ] Click Linking icon → placeholder text shown
  - [ ] Close and reopen app → last active panel restored
  - [ ] Minimize/restore works as before
  - [ ] Drilldown navigation within Day Detail panel works
  - [ ] Import buttons in Sources panel work

---

## Previous story context

Epic 5 (all in review/done) established the three-panel layout and `DataSourcePanelControl`. Key learnings:
- `DataSourcePanelViewModel` is registered as **singleton** in `App.xaml.cs` — state survives control re-creation.
- `DataSourcePanelControl` is registered as **transient** in `App.xaml.cs`.
- Use `SetProperty` (not `[ObservableProperty]`) to avoid WinUI AOT issues.
- Minimization state uses `Visibility.Collapsed` + `Width="Auto"` column (not opacity) so layout contribution is removed.
- The panel body `Border` has `Width="{x:Bind ViewModel.PanelWidth, Mode=OneWay}"` with a `MinWidth="160"` clamp.
- `InitializeAsync()` is called from `DataSourcePanelControl_Loaded`.

The `IsGlobalMode` field currently drives global vs day content visibility. This story replaces that flag with the `ActivePanel` enum. The data-loading side-effects that `IsGlobalMode` triggered in `ApplySelectedDay` must be preserved but decoupled from the panel-switching decision.

---

## Out of scope

- Linking panel content (Stories 9.3–9.5)
- Sources panel coverage rollup enhancement (Story 9.2)
- Any Epic 8 engine integration
- Badge/notification dots on icon strip icons (deferred)
- Keyboard shortcuts for switching panels (deferred)

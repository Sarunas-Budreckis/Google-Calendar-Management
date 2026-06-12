# Story 5.9: Data Source Custom Color Picker

Status: review

## Story

As a **user**,
I want **to assign a custom color to each data source by choosing from a color picker flyout on its panel card**,
so that **the week data markers in the left panel reflect each source's color instead of always showing the same green**.

## Acceptance Criteria

1. **AC-5.9.1 — `data_source` table has a `color_hex` column:**
   A new nullable TEXT column `color_hex` is added to the `data_source` table via an EF Core migration (e.g. `AddDataSourceColorHex`). The column stores hex strings in `#RRGGBB` format. Existing rows get `NULL` (no default value in migration). The `DataSource` entity and `DataSourceConfiguration` are updated to map this column.

2. **AC-5.9.2 — `IDataSourceRepository` exposes a color update method:**
   A new method `UpdateSourceColorAsync(int dataSourceId, string? colorHex, CancellationToken ct = default)` is added to `IDataSourceRepository` and implemented in `DataSourceRepository`. It patches only the `color_hex` field on the matching row. The `GetAllSourcesAsync` and `GetSourceByKeyAsync` methods return the populated `ColorHex` field from the DB.

3. **AC-5.9.3 — Global-mode source cards show a color swatch button:**
   Both the `GlobalSourceCardTemplate` in `DataSourcePanelControl.xaml` gain a circular color swatch button (16×16 px filled circle, similar size/style to event swatches in `EventColorPickerFlyoutController`). The swatch fills with the source's current `ColorHex`; sources with no color stored show a neutral grey (`#888888`). The button is positioned in the card header row beside the source name.

4. **AC-5.9.4 — Clicking the swatch opens a WinUI 3 `ColorPicker` flyout:**
   Clicking the swatch button opens a `Flyout` containing a `Microsoft.UI.Xaml.Controls.ColorPicker` (`IsAlphaEnabled = false`, `IsHexInputVisible = true`). The picker is pre-seeded with the source's current color. Selecting a color immediately calls `UpdateSourceColorAsync` and dismisses the flyout.

5. **AC-5.9.5 — Color change persists and refreshes in-session:**
   After a color is selected the source card's swatch updates immediately. The day-data marker cells (the Mon–Sun mini cells in the global card) also update to use the new color for the "has data" state without requiring an app restart or full reload.

6. **AC-5.9.6 — Day-data markers use the source's color:**
   `DataSourceDayDataMarkerViewModel` accepts an optional `sourceColorHex` constructor parameter. When provided and non-empty, `BackgroundBrush` on the "has data" state uses that color instead of the hardcoded `#22874A` green. Sources with no color set fall back to `#22874A` (preserving current behavior). The no-data grey (`#585858`) is unchanged.

7. **AC-5.9.7 — `DataSourceSummaryViewModel` carries and updates the color:**
   `DataSourceSummaryViewModel` is constructed with the source's `ColorHex` (nullable string). It exposes:
   - `ColorHex` property (string?, observable)
   - `ColorBrush` computed property (returns a `SolidColorBrush` from `ColorHex` or the grey fallback)
   - `UpdateColorAsync(string? colorHex)` method that calls the repository and then fires property-changed on `ColorHex` and `ColorBrush`, and refreshes the `DayDataMarkers` collection brushes.

8. **AC-5.9.8 — Schema test covers the new column:**
   An integration test in `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs` asserts that the `data_source` table has a `color_hex` column after migration.

---

## Tasks / Subtasks

- [x] **Task 1: DB schema — entity, configuration, migration**
  - [x] Add `string? ColorHex { get; set; }` to `Data/Entities/DataSource.cs`
  - [x] In `Data/Configurations/DataSourceConfiguration.cs`, add `builder.Property(e => e.ColorHex).HasColumnName("color_hex");` (nullable, no default)
  - [x] Run `dotnet ef migrations add AddDataSourceColorHex` — verify the migration adds only `color_hex` to `data_source` and touches no other tables
  - [x] Run `dotnet ef database update` against the local DB; verify existing rows get `color_hex = NULL`

- [x] **Task 2: Repository — `UpdateSourceColorAsync`**
  - [x] Add `Task UpdateSourceColorAsync(int dataSourceId, string? colorHex, CancellationToken ct = default);` to `Services/IDataSourceRepository.cs`
  - [x] Implement in `Services/DataSourceRepository.cs`: load the `DataSource` row by PK, set `ColorHex`, save. Use `IDbContextFactory<CalendarDbContext>` as per singleton pattern.

- [x] **Task 3: `DataSourceDayDataMarkerViewModel` — accept source color**
  - [x] Add optional `string? sourceColorHex = null` parameter to the constructor in `ViewModels/DataSourceDayDataMarkerViewModel.cs`
  - [x] Store in a private field `_sourceColorHex`
  - [x] Change `HasDataBrush` from a static field to an instance-computed brush: parse `_sourceColorHex` if non-null/non-empty, else fall back to `#22874A`. Use the same hex-to-brush logic as `EventColorPickerFlyoutController.CreateBrush`.
  - [x] `BackgroundBrush` continues to return the computed brush

- [x] **Task 4: `DataSourceSummaryViewModel` — carry and update color**
  - [x] Add `string? colorHex` parameter to `DataSourceSummaryViewModel` constructor
  - [x] Store as `_colorHex` backing field with observable `ColorHex` property
  - [x] Add `ColorBrush` computed property: parse `_colorHex` if set, else `#888888` grey; return `SolidColorBrush`
  - [x] Add `async Task UpdateColorAsync(string? colorHex)` method:
    1. Calls `IDataSourceRepository.UpdateSourceColorAsync(DataSourceId, colorHex, ct)` — inject the repository via constructor
    2. Sets `_colorHex = colorHex`; fires `OnPropertyChanged(nameof(ColorHex))` and `OnPropertyChanged(nameof(ColorBrush))`
    3. Rebuilds each `DataSourceDayDataMarkerViewModel` in `DayDataMarkers` with the new color (clear and re-add with same data but new color, or replace with new instances)
  - [x] Thread note: `UpdateColorAsync` is only called from the UI thread; no cross-thread dispatch needed

- [x] **Task 5: `DataSourcePanelViewModel` — pass color to summaries and markers**
  - [x] In `CreateSummaryAsync`, pass `source.ColorHex` to `DataSourceSummaryViewModel` constructor
  - [x] In `CreateSummaryAsync`, pass `source.ColorHex` as `sourceColorHex` when creating each `DataSourceDayDataMarkerViewModel`
  - [x] Inject `IDataSourceRepository` into `DataSourceSummaryViewModel` (it is already available in `DataSourcePanelViewModel`; thread it through the `CreateSummaryAsync` call)

- [x] **Task 6: XAML — add color swatch button to source card template**
  - [x] In `Views/DataSourcePanelControl.xaml`, in `GlobalSourceCardTemplate`, add a `Button` in the header row next to the `DisplayName` `TextBlock`
  - [x] Position it so it appears to the left of the DisplayName in the header row (wrapped with horizontal StackPanel)

- [x] **Task 7: Code-behind — color picker flyout**
  - [x] In `Views/DataSourcePanelControl.xaml.cs`, add `ColorSwatchButton_Click` handler and `ShowColorPickerFlyout` with `ParseHexToColor`/`FormatColorToHex` helpers
  - [x] Add `using Microsoft.UI.Xaml.Controls;` if not already present

- [x] **Task 8: Schema test**
  - [x] In `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs`, add `DataSource_HasColorHexColumn_AfterMigration` test asserting `data_source` has a `color_hex` column after migration

---

## Dev Notes

### DataSource Entity After Change

```csharp
// Data/Entities/DataSource.cs
public class DataSource
{
    public int DataSourceId { get; set; }
    public string SourceKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool SupportsNoDataHint { get; set; }
    public string? ColorHex { get; set; }   // new — nullable, e.g. "#4CAF50"
    public DateTime CreatedAt { get; set; }
}
```

### Color Fallback Chain

| Situation | Color Used |
|-----------|------------|
| Source has `ColorHex = "#FF6B35"` | `#FF6B35` (persisted pick) |
| Source has `ColorHex = null` (swatch button) | `#888888` grey (displayed on card) |
| Source has `ColorHex = null` (day marker "has data") | `#22874A` green (preserves current behavior) |
| Source has `ColorHex = null` (day marker "no data") | `#585858` grey (unchanged) |

### WinUI 3 `ColorPicker` Notes

- Namespace: `Microsoft.UI.Xaml.Controls.ColorPicker` — already in the WinUI 3 SDK, no extra package needed
- Set `IsAlphaEnabled = false` to hide the alpha slider
- `ColorChanged` fires on every pointer move, not just on confirmation — hide the flyout on first change rather than trying to debounce. This matches the swatch-click pattern in `EventColorPickerFlyoutController`.
- The `Color` property type is `Windows.UI.Color` (not `Microsoft.UI.Color`)
- The `ColorPicker` is ~280×400 px by default; the flyout sizes to it

### `DataSourceSummaryViewModel` Constructor — Repository Injection

`DataSourceSummaryViewModel` currently takes `DataSourceImportHandlerRegistry handlerRegistry` but NOT the repository. You need to add `IDataSourceRepository dataSourceRepository` as a constructor parameter so `UpdateColorAsync` can call it. Update the two call sites in `DataSourcePanelViewModel`:
1. `CreateSummaryAsync` (for sources that exist in DB)
2. The handler-only path (pass `_dataSourceRepository` as well — `UpdateColorAsync` will be a no-op on id=0, or add a guard `if (DataSourceId == 0) return;`)

### Hex-to-Brush Helper

The `EventColorPickerFlyoutController.CreateBrush(string hex)` static method already does this (it strips `#`, parses 3 bytes, returns `SolidColorBrush`). Either copy the logic inline into `DataSourceDayDataMarkerViewModel` and `DataSourceSummaryViewModel`, or extract it to a shared `ColorHelpers` static class in the `Views` or `Services` namespace. Do NOT reference `EventColorPickerFlyoutController` from ViewModels.

### No Rebuild of Entire Panel on Color Change

`UpdateColorAsync` should NOT call `LoadSourcesAsync()` — that would reload everything from DB and discard the current view state. Instead, mutate only the affected `DataSourceSummaryViewModel` in place (update `ColorHex`, fire property-changed). The `DayDataMarkers` collection entries need their `HasDataBrush` updated; since `DataSourceDayDataMarkerViewModel` is immutable-ish, the cleanest approach is to recreate the marker VMs with the new color and replace them in the `DayDataMarkers` `ObservableCollection`.

### File List Expected

```text
Data/
├── Entities/DataSource.cs                         # add ColorHex property
├── Configurations/DataSourceConfiguration.cs      # map color_hex column
└── Migrations/<timestamp>_AddDataSourceColorHex.cs  # new migration

Services/
├── IDataSourceRepository.cs                       # add UpdateSourceColorAsync
└── DataSourceRepository.cs                        # implement UpdateSourceColorAsync

ViewModels/
├── DataSourceDayDataMarkerViewModel.cs            # accept sourceColorHex param
├── DataSourceSummaryViewModel.cs                  # add ColorHex, ColorBrush, UpdateColorAsync
└── DataSourcePanelViewModel.cs                    # pass color to summaries & markers

Views/
├── DataSourcePanelControl.xaml                    # add swatch button to card template
└── DataSourcePanelControl.xaml.cs                 # ColorSwatchButton_Click handler + flyout helpers

GoogleCalendarManagement.Tests/Integration/
└── SchemaTests.cs                                 # add color_hex column assertion
```

### References

- [DataSource entity](../../../Data/Entities/DataSource.cs) — add `ColorHex`
- [DataSourceConfiguration](../../../Data/Configurations/DataSourceConfiguration.cs) — map the column
- [IDataSourceRepository](../../../Services/IDataSourceRepository.cs) — add the method
- [DataSourceDayDataMarkerViewModel](../../../ViewModels/DataSourceDayDataMarkerViewModel.cs) — accept color param
- [DataSourceSummaryViewModel](../../../ViewModels/DataSourceSummaryViewModel.cs) — add color props + update method
- [DataSourcePanelViewModel](../../../ViewModels/DataSourcePanelViewModel.cs) — thread color through `CreateSummaryAsync`
- [DataSourcePanelControl.xaml](../../../Views/DataSourcePanelControl.xaml) — add swatch button
- [EventColorPickerFlyoutController](../../../Views/EventColorPickerFlyoutController.cs) — hex-to-brush pattern and flyout style reference
- [SchemaTests](../../../GoogleCalendarManagement.Tests/Integration/SchemaTests.cs) — test pattern

---

## Dev Agent Record

### Agent Notes

Implemented all 8 tasks. Key decisions:
- `HasDataBrush` moved from static to instance field in `DataSourceDayDataMarkerViewModel` so each marker can carry its own source color.
- `DataSourceSummaryViewModel` constructor gains two new params (`IDataSourceRepository`, `string? colorHex`) with defaults to avoid breaking existing test stubs; the test stub in `DataSourcePanelViewModelTests` was updated to implement the new `UpdateSourceColorAsync` interface method.
- `RefreshDayMarkerColors` in `DataSourceSummaryViewModel` recreates the marker VMs with the new color (clear + re-add pattern) rather than calling `LoadSourcesAsync`.
- Migration `20260608180000_AddDataSourceColorHex` uses `migrationBuilder.AddColumn` (EF Core built-in) rather than raw SQL, which matches the tier-3 tables pattern.
- The flyout `ColorChanged` event hides the flyout on first change (matches `EventColorPickerFlyoutController` pattern from dev notes).
- Pre-existing test failures (11) were present before this story; none introduced by these changes.

### File List

```
Data/Entities/DataSource.cs
Data/Configurations/DataSourceConfiguration.cs
Data/Migrations/20260608180000_AddDataSourceColorHex.cs
Data/Migrations/20260608180000_AddDataSourceColorHex.Designer.cs
Data/Migrations/CalendarDbContextModelSnapshot.cs
Services/IDataSourceRepository.cs
Services/DataSourceRepository.cs
ViewModels/DataSourceDayDataMarkerViewModel.cs
ViewModels/DataSourceSummaryViewModel.cs
ViewModels/DataSourcePanelViewModel.cs
Views/DataSourcePanelControl.xaml
Views/DataSourcePanelControl.xaml.cs
GoogleCalendarManagement.Tests/Integration/SchemaTests.cs
GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs
```

### Change Log

- 2026-06-08: Added `color_hex` column to `data_source` table via EF Core migration `AddDataSourceColorHex`; updated entity, configuration, and model snapshot.
- 2026-06-08: Added `UpdateSourceColorAsync` to `IDataSourceRepository` and `DataSourceRepository`.
- 2026-06-08: `DataSourceDayDataMarkerViewModel` now accepts optional `sourceColorHex`; day markers use source color for "has data" state.
- 2026-06-08: `DataSourceSummaryViewModel` carries `ColorHex`/`ColorBrush` and exposes `UpdateColorAsync` to persist and propagate color changes.
- 2026-06-08: `DataSourcePanelViewModel.CreateSummaryAsync` threads `source.ColorHex` through to both summary and marker VMs.
- 2026-06-08: Added color swatch button to `GlobalSourceCardTemplate` XAML; added flyout handler in code-behind.
- 2026-06-08: Added `DataSource_HasColorHexColumn_AfterMigration` schema integration test.

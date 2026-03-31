# Story 3.2: Display Events with Colour-Coded Visual System

Status: ready-for-dev

## Story

As a **calendar user**,
I want **each event to render in its personal category colour (Azure, Purple, Grey, Yellow, Navy, Sage, Flamingo, Orange, or Lavender) with readable white or black text automatically chosen for contrast**,
so that **I can instantly identify event categories by colour in all four calendar views, exactly as I track them in Google Calendar**.

## Acceptance Criteria

1. **AC-3.2.1 — Per-category colour rendering:** Given events in the database with various `color_id` values (`"1"`–`"10"` and the alias strings like `"azure"`, `"grey"`, etc.), each event renders in its assigned category colour in all four views (Year, Month, Week, Day).

2. **AC-3.2.2 — Fallback colour for null/unrecognised:** Given an event with a `null` or unrecognised `color_id`, it renders in Azure `#0088CC` without throwing an exception.

3. **AC-3.2.3 — WCAG AA text contrast:** Given any coloured event block or chip, the title text is automatically white (`#FFFFFF`) or black (`#000000`) based on background luminance so the WCAG AA 4.5:1 contrast ratio is met.

4. **AC-3.2.4 — Cross-view colour consistency:** Given the same event viewed in Year, Month, Week, and Day views, the background colour (and text colour) is identical in all four views.

## Scope Boundaries

**IN SCOPE — this story:**
- Replace the `ColorMappingService` Azure-only stub (from Story 3.1) with the full 9-colour dictionary
- Add WCAG AA text contrast selection logic (white vs. black based on background luminance)
- Bind event background colour (`ColorHex`) to `EventChip` and `EventBlock` controls (from Story 3.1)
- Bind text contrast colour to the title label inside `EventChip` and `EventBlock`
- Apply colour to Year view event dot / indicator elements
- Unit tests: all 9 colour mappings, null fallback, unknown-string fallback, contrast logic for all 9 colours

**OUT OF SCOPE — do NOT implement:**
- Event selection red outline (Story 3.3)
- Event details panel (Story 3.4)
- Colour picker in editing panel (Story 3.7)
- User-configurable colour overrides (future epic)
- Story 3.1 view layout work (must be complete before this story)

**PREREQUISITE:** Story 3.1 must be done. The following must already exist:
- `Models/CalendarEventDisplayModel.cs` (with `ColorHex` field)
- `Services/IColorMappingService.cs` and `Services/ColorMappingService.cs` (stub returning `#0088CC`)
- `Services/CalendarQueryService.cs` (already calls `_colorMappingService.GetHexColor(event.ColorId)`)
- `Views/Controls/EventChip.xaml` and `Views/Controls/EventBlock.xaml` with `ColorHex` binding

---

## Tasks / Subtasks

- [ ] **Task 1: Verify actual `color_id` values stored in the database** (AC: 3.2.1)
  - [ ] Run the app, trigger a Google Calendar sync, then open the SQLite database at `%LocalAppData%\GoogleCalendarManagement\calendar.db`
  - [ ] Run: `SELECT DISTINCT color_id, COUNT(*) AS cnt FROM gcal_event WHERE is_deleted = 0 GROUP BY color_id ORDER BY cnt DESC;`
  - [ ] Confirm which numeric IDs (e.g., `"8"`, `"9"`) actually appear in your data — not all 1–11 may be in use
  - [ ] Note any string aliases (e.g., `"azure"`) if present; these must be supported too
  - [ ] Cross-reference with the Google Calendar API Colors reference in Dev Notes below to confirm hex values
  - [ ] Update the colour dictionary in Task 2 with the confirmed hex values

- [ ] **Task 2: Replace stub `ColorMappingService` with full 9-colour dictionary** (AC: 3.2.1, 3.2.2)
  - [ ] Open `Services/ColorMappingService.cs` (created in Story 3.1 as a stub)
  - [ ] Replace the stub body with the dictionary-based implementation shown in Dev Notes
  - [ ] The dictionary must handle both numeric string keys (`"8"`) AND alias string keys (`"grey"`) for the same colour
  - [ ] `GetHexColor(null)` and `GetHexColor(unknown_string)` must return `"#0088CC"` without throwing
  - [ ] Update `IColorMappingService.cs` to add the `AllColors` read-only dictionary property (if not already there)

- [ ] **Task 3: Create `IColorContrastService` and `ColorContrastService`** (AC: 3.2.3)
  - [ ] Create `Services/IColorContrastService.cs`:
    ```csharp
    public interface IColorContrastService
    {
        /// Returns "#FFFFFF" or "#000000" — whichever passes WCAG AA (4.5:1) against the given hex background.
        string GetContrastTextColor(string backgroundHex);
    }
    ```
  - [ ] Create `Services/ColorContrastService.cs` implementing `IColorContrastService`
  - [ ] Use the sRGB luminance formula in Dev Notes; return `"#000000"` if luminance > 0.179, else `"#FFFFFF"`
  - [ ] Must not throw on any valid 6-digit hex string (e.g., `"#0088CC"` with or without `#` prefix)

- [ ] **Task 4: Register new services in DI** (all ACs)
  - [ ] Open `App.xaml.cs` → `ConfigureServices()`
  - [ ] `ColorMappingService` is already registered from Story 3.1 — no change needed there
  - [ ] Add: `services.AddSingleton<IColorContrastService, ColorContrastService>();`
  - [ ] Position after `IColorMappingService` registration, before ViewModels

- [ ] **Task 5: Wire `ColorHex` to `EventChip` background and text** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [ ] **Prerequisite:** Story 3.1 `EventChip` user control must exist in `Views/Controls/EventChip.xaml`
  - [ ] Ensure the chip's root `Border` (or `Grid`) `Background` is bound to the `ColorHex` dependency property (already done in Story 3.1 using Azure placeholder)
  - [ ] Add a `ContrastTextColor` dependency property to `EventChip` (type `string`, default `"#000000"`)
  - [ ] Bind the title `TextBlock.Foreground` to `ContrastTextColor`
  - [ ] In `EventChip`'s view code, when `ColorHex` changes, call `IColorContrastService.GetContrastTextColor(ColorHex)` and update `ContrastTextColor`
  - [ ] Preferred injection approach: receive `IColorContrastService` via a static `App.Services.GetService<IColorContrastService>()` call, or pass through `EventChipViewModel` if one exists

- [ ] **Task 6: Wire `ColorHex` to `EventBlock` background and text (week/day views)** (AC: 3.2.1, 3.2.3, 3.2.4)
  - [ ] Same pattern as Task 5, applied to `Views/Controls/EventBlock.xaml` and its code-behind
  - [ ] **Prerequisite:** Story 3.1 `EventBlock` must exist

- [ ] **Task 7: Wire colour to Year view event indicators** (AC: 3.2.1, 3.2.4)
  - [ ] In `YearViewControl`, month mini-grids show coloured event dots or small bars per day
  - [ ] If Story 3.1 used Azure as a placeholder dot colour, update each dot's `Fill` or `Background` to use the first event's `ColorHex` for that day, or a blended indicator
  - [ ] If the year view shows only sync status dots (not event colour dots), this task is skipped — confirm with the Story 3.1 implementation before acting

- [ ] **Task 8: Unit tests for `ColorMappingService`** (AC: 3.2.1, 3.2.2)
  - [ ] Create `GoogleCalendarManagement.Tests/Unit/ColorMappingServiceTests.cs`
  - [ ] Test each of the 9 known colour IDs returns the correct hex (both numeric string and alias string key)
  - [ ] Test `null` input → `"#0088CC"`
  - [ ] Test unknown string (e.g., `"banana"`, `"xyz"`) → `"#0088CC"`
  - [ ] Test result strings always start with `#` and are 7 characters long
  - [ ] Use `FluentAssertions` (already installed); no Moq needed — `ColorMappingService` has no dependencies

- [ ] **Task 9: Unit tests for `ColorContrastService`** (AC: 3.2.3)
  - [ ] Create `GoogleCalendarManagement.Tests/Unit/ColorContrastServiceTests.cs`
  - [ ] For each of the 9 category colours: assert that `GetContrastTextColor(hex)` returns either `"#FFFFFF"` or `"#000000"` and that the contrast ratio against that return value is ≥ 4.5:1 (calculate in test or assert based on known luminance)
  - [ ] Specific assertions (based on pre-calculated luminance — see Dev Notes):
    - Azure `#0088CC` → `"#000000"` (L ≈ 0.240, passes black contrast)
    - Graphite `#616161` → `"#FFFFFF"` (L ≈ 0.134, passes white contrast)
  - [ ] Test with/without leading `#` to ensure robustness

- [ ] **Task 10: Final validation**
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test`
  - [ ] Manual: launch app with synced events, switch between all four views — confirm each event category shows its distinct colour (not all Azure)
  - [ ] Manual: for each colour, confirm title text is legible (not same colour as background)
  - [ ] Manual: yellow/banana events should show dark text; dark purple/blueberry events should show white text

---

## Dev Notes

### Critical Context: This Story Replaces a Stub, NOT Creating from Scratch

Story 3.1 created the full colour infrastructure as stubs. Story 3.2 fills in the real values. **Do NOT create new interfaces or new `CalendarQueryService` logic** — they already exist. The only new production code is:
- Replacing `ColorMappingService.GetHexColor()` body
- Adding `IColorContrastService` + `ColorContrastService`
- Wiring `ContrastTextColor` into EventChip/EventBlock XAML

If you find that `Services/ColorMappingService.cs`, `Models/CalendarEventDisplayModel.cs`, `Views/Controls/EventChip.xaml`, or `Views/Controls/EventBlock.xaml` do **not** exist, **STOP** — Story 3.1 is not complete and this story cannot proceed.

### Project Structure (Flat Layout)

The project does NOT have a `Core/` or `src/` separation despite the architecture doc's aspirational structure. All files go into existing folders at root:

```
GoogleCalendarManagement/
├── Services/              ← IColorMappingService, ColorMappingService, IColorContrastService, ColorContrastService
├── Models/                ← CalendarEventDisplayModel (from Story 3.1)
├── Views/Controls/        ← EventChip.xaml, EventBlock.xaml (from Story 3.1)
├── GoogleCalendarManagement.Tests/Unit/   ← ColorMappingServiceTests, ColorContrastServiceTests
```

### `ColorMappingService` Full Implementation

Replace the Story 3.1 stub body with this dictionary. Populate hex values after completing Task 1 (querying the DB):

```csharp
// Services/ColorMappingService.cs
namespace GoogleCalendarManagement.Services;

public class ColorMappingService : IColorMappingService
{
    private static readonly IReadOnlyDictionary<string, string> ColorMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Null / default / azure → confirmed #0088CC
            { "azure", "#0088CC" },
            { "1",     "#0088CC" },   // Google ID "1" aliased to Azure in this user's taxonomy

            // Purple — Professional Work (Google "9" = Blueberry)
            { "purple", "#3F51B5" },  // ← VERIFY from Task 1
            { "9",      "#3F51B5" },

            // Grey — Sleep & Recovery (Google "8" = Graphite)
            { "grey",   "#616161" },  // ← VERIFY from Task 1
            { "8",      "#616161" },

            // Yellow — Passive Consumption (Google "5" = Banana)
            { "yellow", "#F6BF26" },  // ← VERIFY from Task 1
            { "5",      "#F6BF26" },

            // Navy — Personal Engineering (Google "2" = Sage)
            { "navy",   "#33B679" },  // ← VERIFY from Task 1
            { "2",      "#33B679" },

            // Sage — Wisdom & Meta-Reflection (Google "10" = Basil)
            { "sage",   "#0B8043" },  // ← VERIFY from Task 1
            { "10",     "#0B8043" },

            // Flamingo — Nerdsniped Deep Reading (Google "4" = Flamingo)
            { "flamingo", "#E67C73" }, // ← VERIFY from Task 1
            { "4",        "#E67C73" },

            // Orange — Physical Training (Google "6" = Tangerine)
            { "orange", "#F4511E" },  // ← VERIFY from Task 1
            { "6",      "#F4511E" },

            // Lavender — In-Between States (Google "3" = Grape)
            { "lavender", "#8E24AA" }, // ← VERIFY from Task 1
            { "3",        "#8E24AA" },
        };

    public string GetHexColor(string? colorId)
    {
        if (colorId is not null && ColorMap.TryGetValue(colorId, out var hex))
            return hex;
        return "#0088CC";  // Azure fallback for null or unrecognised
    }

    public IReadOnlyDictionary<string, string> AllColors => ColorMap;
}
```

> **⚠️ IMPORTANT — Verify hex values before shipping:** The placeholder hex values above are Google Calendar's standard event colour hexes. They may NOT match the user's actual custom colours. Complete Task 1 first, then adjust the hex values. The only confirmed value is `"#0088CC"` for Azure/null.

### Google Calendar API Colour Reference (for Task 1 verification)

Standard event color IDs returned by `googleEvent.ColorId`:

| ID | Google Name | Google Hex | User's Taxonomy Name |
|---|---|---|---|
| null / "1" | (calendar default / Lavender) | **#0088CC** (confirmed custom) | Azure — Eudaimonia |
| "2" | Sage | #33B679 | Navy — Personal Engineering |
| "3" | Grape | #8E24AA | Lavender — In-Between States |
| "4" | Flamingo | #E67C73 | Flamingo — Deep Reading |
| "5" | Banana | #F6BF26 | Yellow — Passive Consumption |
| "6" | Tangerine | #F4511E | Orange — Physical Training |
| "7" | Peacock | #039BE5 | (not used in taxonomy) |
| "8" | Graphite | #616161 | Grey — Sleep & Recovery |
| "9" | Blueberry | #3F51B5 | Purple — Professional Work |
| "10" | Basil | #0B8043 | Sage — Wisdom & Meta-Reflection |
| "11" | Tomato | #D50000 | (not used in taxonomy) |

To fetch the actual colours from Google Calendar API programmatically, the endpoint is `GET https://www.googleapis.com/calendar/v3/colors` (requires auth). This is not required for this story — manual verification is sufficient.

### WCAG AA Text Contrast — `ColorContrastService` Implementation

Use the W3C relative luminance formula:

```csharp
// Services/ColorContrastService.cs
namespace GoogleCalendarManagement.Services;

public class ColorContrastService : IColorContrastService
{
    // L > 0.179 → background is light → use black text
    // L <= 0.179 → background is dark → use white text
    private const double LuminanceThreshold = 0.179;

    public string GetContrastTextColor(string backgroundHex)
    {
        var l = GetRelativeLuminance(backgroundHex);
        return l > LuminanceThreshold ? "#000000" : "#FFFFFF";
    }

    private static double GetRelativeLuminance(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return 0;

        var r = Convert.ToInt32(hex[..2], 16) / 255.0;
        var g = Convert.ToInt32(hex[2..4], 16) / 255.0;
        var b = Convert.ToInt32(hex[4..6], 16) / 255.0;

        return 0.2126 * Linearise(r)
             + 0.7152 * Linearise(g)
             + 0.0722 * Linearise(b);
    }

    private static double Linearise(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
```

**Pre-calculated results** for the 9 colours (for test assertions):

| Colour | Hex | Luminance (approx) | Text Colour |
|---|---|---|---|
| Azure | #0088CC | 0.240 | `#000000` (black) |
| Purple | #3F51B5 | 0.086 | `#FFFFFF` (white) |
| Grey | #616161 | 0.134 | `#FFFFFF` (white) |
| Yellow | #F6BF26 | 0.497 | `#000000` (black) |
| Navy | #33B679 | 0.208 | `#000000` (black) |
| Sage | #0B8043 | 0.095 | `#FFFFFF` (white) |
| Flamingo | #E67C73 | 0.250 | `#000000` (black) |
| Orange | #F4511E | 0.205 | `#000000` (black) |
| Lavender | #8E24AA | 0.062 | `#FFFFFF` (white) |

> Note: These are based on Google's standard hexes. If hex values change after Task 1 verification, recalculate luminance and update test assertions accordingly.

### `EventChip` and `EventBlock` — Colour Binding Pattern

Story 3.1 should have already bound `ColorHex` to `Background`. The new binding needed in Story 3.2 is for the text foreground. Typical XAML pattern:

```xml
<!-- In EventChip.xaml — Title TextBlock -->
<TextBlock Text="{x:Bind Title, Mode=OneWay}"
           Foreground="{x:Bind ContrastTextColor, Mode=OneWay}"
           TextTrimming="CharacterEllipsis" />
```

The `ContrastTextColor` dependency property is computed from `ColorHex`. Options:
1. **Preferred:** Add a `ContrastTextColor` computed property on the `EventChip` code-behind or its ViewModel, updated whenever `ColorHex` changes, by calling `IColorContrastService.GetContrastTextColor(ColorHex)`.
2. **Alternative:** Create a WinUI 3 `IValueConverter` (`HexToContrastTextColorConverter`) that calls `ColorContrastService` inline.

If `EventChip` has no ViewModel (pure code-behind UserControl), inject `IColorContrastService` via `App.Current.Services.GetService<IColorContrastService>()` in the code-behind.

### `IColorMappingService` Interface Update

The Story 3.1 stub likely only has `GetHexColor(string? colorId)`. Add the `AllColors` property if not already present (needed for tests and potential future use):

```csharp
// Services/IColorMappingService.cs
public interface IColorMappingService
{
    string GetHexColor(string? colorId);
    IReadOnlyDictionary<string, string> AllColors { get; }
}
```

### Testing Patterns (from existing test infrastructure)

No in-memory DB needed for colour tests — these are pure unit tests with no dependencies. Follow the existing pattern in the test project:

```csharp
// GoogleCalendarManagement.Tests/Unit/ColorMappingServiceTests.cs
namespace GoogleCalendarManagement.Tests.Unit;

public sealed class ColorMappingServiceTests
{
    private readonly ColorMappingService _sut = new();

    [Theory]
    [InlineData(null, "#0088CC")]
    [InlineData("azure", "#0088CC")]
    [InlineData("1", "#0088CC")]
    [InlineData("unknown_value", "#0088CC")]
    [InlineData("8", "#616161")]   // adjust after Task 1
    [InlineData("grey", "#616161")]
    // ... etc
    public void GetHexColor_ReturnsExpectedHex(string? colorId, string expectedHex)
    {
        _sut.GetHexColor(colorId).Should().Be(expectedHex);
    }
}
```

Packages already installed: `xunit`, `Moq`, `FluentAssertions` — no new packages needed.

Build command: `dotnet build -p:Platform=x64`
Test command: `dotnet test`

### References

- Colour taxonomy and hex values: [docs/_color-definitions.md](../../_color-definitions.md)
- Epic 3 tech spec (ACs 9–12, colour mapping table, colour workflow): [docs/epic-3/tech-spec.md](../tech-spec.md#story-32--colour-coded-visual-system)
- Story 3.1 (creates service stubs and view controls that this story completes): [docs/epic-3/stories/3-1-build-year-month-week-day-calendar-views.md](./3-1-build-year-month-week-day-calendar-views.md)
- W3C relative luminance: WCAG 2.1 §1.4.3, formula at https://www.w3.org/TR/WCAG21/#dfn-relative-luminance
- Google Calendar API Colors reference: https://developers.google.com/calendar/api/v3/reference/colors

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

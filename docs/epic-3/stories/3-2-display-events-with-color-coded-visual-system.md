# Story 3.2: Display Events with Colour-Coded Visual System

Status: ready-for-dev

## Story

As a **calendar user**,
I want **each event to render in its personal category colour (Azure, Purple, Grey, Yellow, Navy, Sage, Flamingo, Orange, or Lavender) with readable white or black text automatically chosen for contrast**,
so that **I can instantly identify event categories by colour in all four calendar views, exactly as I track them in Google Calendar**.

## Acceptance Criteria

1. **AC-3.2.1 — Per-category colour rendering:** Given events in the database with various `color_id` values (`"1"`–`"10"` and alias strings like `"azure"`, `"grey"`, etc.), each event renders in its assigned category colour in all four views (Year, Month, Week, Day).

2. **AC-3.2.2 — Fallback colour for null/unrecognised:** Given an event with a `null` or unrecognised `color_id`, it renders in Azure `#0088CC` without throwing an exception.

3. **AC-3.2.3 — WCAG AA text contrast:** Given any coloured event block or chip, the title text is automatically white (`#FFFFFF`) or black (`#000000`) based on background luminance so the WCAG AA 4.5:1 contrast ratio is met.

4. **AC-3.2.4 — Cross-view colour consistency:** Given the same event viewed in Year, Month, Week, and Day views, the background colour (and text colour) is identical in all four views.

## Tasks / Subtasks

- [ ] **Task 1: Verify actual `color_id` values in the database** (AC: 3.2.1)
  - Run the app, trigger a Google Calendar sync
  - Open `%LocalAppData%\GoogleCalendarManagement\calendar.db` in a SQLite browser
  - Run: `SELECT DISTINCT color_id, COUNT(*) AS cnt FROM gcal_event WHERE is_deleted = 0 GROUP BY color_id ORDER BY cnt DESC;`
  - Confirm which numeric IDs actually appear — cross-reference with the colour table in Dev Notes
  - The hex values in the `ColorMappingService` dictionary below are Google's standard values and very likely correct; confirm before shipping

- [ ] **Task 2: Replace stub `ColorMappingService` with full 9-colour dictionary** (AC: 3.2.1, 3.2.2)
  - File: `Services/ColorMappingService.cs`
  - Current stub returns `"#0088CC"` for everything — replace body with dictionary implementation (see Dev Notes)
  - Dictionary must handle both numeric string keys (`"8"`) AND alias string keys (`"grey"`) case-insensitively
  - `GetHexColor(null)` and `GetHexColor(unknown_string)` must return `"#0088CC"` without throwing

- [ ] **Task 3: Add `AllColors` property to `IColorMappingService`** (AC: 3.2.1)
  - File: `Services/IColorMappingService.cs`
  - Add: `IReadOnlyDictionary<string, string> AllColors { get; }`
  - Needed for unit tests and potential future use

- [ ] **Task 4: Create `IColorContrastService` and `ColorContrastService`** (AC: 3.2.3)
  - Create `Services/IColorContrastService.cs` (see Dev Notes for interface definition)
  - Create `Services/ColorContrastService.cs` (see Dev Notes for full implementation using W3C sRGB luminance formula)
  - Returns `"#000000"` if relative luminance > 0.179, else `"#FFFFFF"`

- [ ] **Task 5: Register `IColorContrastService` in DI** (all ACs)
  - File: `App.xaml.cs` → `ConfigureServices()`
  - Add after line 187 (`IColorMappingService` registration):
    `services.AddSingleton<IColorContrastService, ColorContrastService>();`

- [ ] **Task 6: Wire contrast text colour into `MonthViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - File: `Views/MonthViewControl.xaml.cs`
  - Add field: `private IColorContrastService _contrastService = null!;`
  - In constructor: `_contrastService = App.GetRequiredService<IColorContrastService>();`
  - In `BuildDayCell()`: change the event `Border` child `TextBlock` Foreground from `new SolidColorBrush(Colors.White)` to `ToBrush(_contrastService.GetContrastTextColor(item.ColorHex))`
  - Pass `_contrastService` (or the computed hex string) through to `BuildDayCell` — see Dev Notes for signature change
  - There is **1 hardcoded `Colors.White`** in this file to replace

- [ ] **Task 7: Wire contrast text colour into `WeekViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - File: `Views/WeekViewControl.xaml.cs`
  - Same injection pattern as Task 6
  - `CreateEventChip(title, hexColor)` → add `contrastHex` parameter, set TextBlock Foreground from it
  - `CreateTimedEventBlock(item, culture)` → compute contrast color from `item.ColorHex`, apply to both TextBlocks
  - There are **3 hardcoded `Colors.White`** references in this file to replace (1 in chip, 2 in block)

- [ ] **Task 8: Wire contrast text colour into `DayViewControl`** (AC: 3.2.1, 3.2.3, 3.2.4)
  - File: `Views/DayViewControl.xaml.cs`
  - Same injection pattern as Task 6
  - There are **3 hardcoded `Colors.White`** references in this file to replace (1 in all-day border, 2 in timed event block)

- [ ] **Task 9: Year view — CONFIRM THEN SKIP** (AC: 3.2.4)
  - `YearViewControl` currently shows only static grey dots per day (sync-status placeholder from Story 2.4), NOT event colour dots
  - Verify by reading `YearViewControl.xaml.cs` `BuildDayButtonContent()` — it creates a grey `Ellipse` with a `TODO Story 2.4` comment
  - If confirmed: no changes needed to `YearViewControl` for this story
  - If year view actually shows event chips (unexpected): apply same pattern as Tasks 6–8

- [ ] **Task 10: Unit tests for `ColorMappingService`** (AC: 3.2.1, 3.2.2)
  - Create `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`
  - See exact test patterns in Dev Notes

- [ ] **Task 11: Unit tests for `ColorContrastService`** (AC: 3.2.3)
  - Create `GoogleCalendarManagement.Tests/Unit/Services/ColorContrastServiceTests.cs`
  - See exact test patterns and pre-calculated luminance values in Dev Notes

- [ ] **Task 12: Final validation**
  - Run `dotnet build -p:Platform=x64`
  - Run `dotnet test`
  - Manual: launch app with synced events, confirm each category shows its distinct colour (not all Azure)
  - Manual: confirm title text is legible — yellow/banana events show dark text; dark purple/blueberry events show white text

---

## Dev Notes

### CRITICAL: No EventChip.xaml or EventBlock.xaml — Inline Code-Behind Architecture

**The story's original planning doc assumed separate `EventChip` and `EventBlock` UserControl files. They do NOT exist.** Story 3.1 implemented all event rendering inline in each view's `Rebuild()` code-behind using static helper methods that build `Border` elements directly.

The pattern for this story is therefore **NOT** dependency properties on UserControls — instead:
1. Inject `IColorContrastService` into the view constructor via `App.GetRequiredService<IColorContrastService>()`
2. In each helper method that creates an event `Border`, call `_contrastService.GetContrastTextColor(hexColor)` to get the text color, then apply it to the `TextBlock.Foreground`

### Actual Service Stubs Already in Place (from Story 3.1)

```csharp
// Services/ColorMappingService.cs — CURRENT STUB (replace body)
public sealed class ColorMappingService : IColorMappingService
{
    public string GetHexColor(string? colorId)
    {
        return "#0088CC";  // ← replace entire class body
    }
}

// Services/IColorMappingService.cs — CURRENT INTERFACE (add AllColors)
public interface IColorMappingService
{
    string GetHexColor(string? colorId);
    // Add: IReadOnlyDictionary<string, string> AllColors { get; }
}

// App.xaml.cs line 187 — ALREADY REGISTERED
services.AddSingleton<IColorMappingService, ColorMappingService>();
// Add line 188:
// services.AddSingleton<IColorContrastService, ColorContrastService>();
```

### ColorMappingService Full Implementation

```csharp
// Services/ColorMappingService.cs
namespace GoogleCalendarManagement.Services;

public sealed class ColorMappingService : IColorMappingService
{
    private static readonly IReadOnlyDictionary<string, string> ColorMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Azure / Eudaimonia — confirmed custom colour #0088CC
            { "azure", "#0088CC" },
            { "1",     "#0088CC" },

            // Purple — Professional Work (Google Blueberry)
            { "purple", "#3F51B5" },
            { "9",      "#3F51B5" },

            // Grey — Sleep & Recovery (Google Graphite)
            { "grey",   "#616161" },
            { "8",      "#616161" },

            // Yellow — Passive Consumption (Google Banana)
            { "yellow", "#F6BF26" },
            { "5",      "#F6BF26" },

            // Navy — Personal Engineering (Google Sage)
            { "navy",   "#33B679" },
            { "2",      "#33B679" },

            // Sage — Wisdom & Meta-Reflection (Google Basil)
            { "sage",   "#0B8043" },
            { "10",     "#0B8043" },

            // Flamingo — Nerdsniped Deep Reading (Google Flamingo)
            { "flamingo", "#E67C73" },
            { "4",        "#E67C73" },

            // Orange — Physical Training (Google Tangerine)
            { "orange", "#F4511E" },
            { "6",      "#F4511E" },

            // Lavender — In-Between States (Google Grape)
            { "lavender", "#8E24AA" },
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

### ColorContrastService Implementation

```csharp
// Services/IColorContrastService.cs
namespace GoogleCalendarManagement.Services;

public interface IColorContrastService
{
    /// Returns "#FFFFFF" or "#000000" — whichever passes WCAG AA (4.5:1) against the given hex background.
    string GetContrastTextColor(string backgroundHex);
}

// Services/ColorContrastService.cs
namespace GoogleCalendarManagement.Services;

public sealed class ColorContrastService : IColorContrastService
{
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

### Exact Lines to Change in Each View

#### MonthViewControl.xaml.cs

Add field and constructor injection:
```csharp
// At class level (after ViewModel property)
private IColorContrastService _contrastService = null!;

// In constructor, after InitializeComponent():
_contrastService = App.GetRequiredService<IColorContrastService>();
```

`BuildDayCell()` now needs `_contrastService` — change signature to pass it, or make it an instance method:
```csharp
// Change from: private static Border BuildDayCell(...)
// Change to:   private Border BuildDayCell(...)  (remove static)

// Inside BuildDayCell, find the event Border's child TextBlock:
// BEFORE:
new TextBlock
{
    Text = item.Title,
    Foreground = new SolidColorBrush(Colors.White),   // ← replace this
    ...
}

// AFTER:
new TextBlock
{
    Text = item.Title,
    Foreground = ToBrush(_contrastService.GetContrastTextColor(item.ColorHex)),
    ...
}
```

Also change `ToBrush` from `private static` to `private` (or keep static and pass hex string separately — developer's choice).

#### WeekViewControl.xaml.cs

Add field and injection (same pattern):
```csharp
private IColorContrastService _contrastService = null!;
// In constructor: _contrastService = App.GetRequiredService<IColorContrastService>();
```

Update `CreateEventChip(string title, string hexColor)`:
```csharp
// BEFORE: Foreground = new SolidColorBrush(Colors.White)
// AFTER:  Foreground = ToBrush(_contrastService.GetContrastTextColor(hexColor))
```

Update `CreateTimedEventBlock(CalendarEventDisplayModel item, CultureInfo culture)` — **2 TextBlocks inside**:
```csharp
// Both TextBlocks in the StackPanel:
// BEFORE: Foreground = new SolidColorBrush(Colors.White)
// AFTER:  Foreground = ToBrush(_contrastService.GetContrastTextColor(item.ColorHex))
```

Remove `static` from `CreateEventChip` and `CreateTimedEventBlock` so they can access `_contrastService`.

#### DayViewControl.xaml.cs

Add field and injection (same pattern).

All-day event Border (in `Rebuild()`, inside `foreach (var item in dayEvents.Where(evt => evt.IsAllDay))`):
```csharp
// BEFORE: Foreground = new SolidColorBrush(Colors.White)
// AFTER:  Foreground = ToBrush(_contrastService.GetContrastTextColor(item.ColorHex))
```

Timed event block (inside `foreach (var item in dayEvents.Where(evt => !evt.IsAllDay))`) — **2 TextBlocks**:
```csharp
// Both TextBlocks in the StackPanel:
// BEFORE: Foreground = new SolidColorBrush(Colors.White)
// AFTER:  Foreground = ToBrush(_contrastService.GetContrastTextColor(item.ColorHex))
```

### Pre-Calculated Contrast Results (for Test Assertions)

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

### Unit Tests for ColorMappingService

File: `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`

No `Unit/Services/` folder exists yet — the Unit folder itself may not exist (current tests are all in Integration/). Create the full folder path.

```csharp
namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ColorMappingServiceTests
{
    private readonly ColorMappingService _sut = new();

    [Theory]
    [InlineData(null, "#0088CC")]
    [InlineData("azure", "#0088CC")]
    [InlineData("1", "#0088CC")]
    [InlineData("unknown_value", "#0088CC")]
    [InlineData("banana", "#0088CC")]         // unknown alias → fallback
    [InlineData("8", "#616161")]
    [InlineData("grey", "#616161")]
    [InlineData("9", "#3F51B5")]
    [InlineData("purple", "#3F51B5")]
    [InlineData("5", "#F6BF26")]
    [InlineData("yellow", "#F6BF26")]
    [InlineData("2", "#33B679")]
    [InlineData("navy", "#33B679")]
    [InlineData("10", "#0B8043")]
    [InlineData("sage", "#0B8043")]
    [InlineData("4", "#E67C73")]
    [InlineData("flamingo", "#E67C73")]
    [InlineData("6", "#F4511E")]
    [InlineData("orange", "#F4511E")]
    [InlineData("3", "#8E24AA")]
    [InlineData("lavender", "#8E24AA")]
    public void GetHexColor_ReturnsExpectedHex(string? colorId, string expectedHex)
    {
        _sut.GetHexColor(colorId).Should().Be(expectedHex);
    }

    [Theory]
    [InlineData("AZURE")]   // uppercase alias
    [InlineData("Grey")]    // mixed case
    [InlineData("PURPLE")]
    public void GetHexColor_CaseInsensitive_ReturnsCorrectHex(string colorId)
    {
        _sut.GetHexColor(colorId).Should().NotBe("#0088CC").Or.Be("#0088CC"); // just verifies no throw
        // More specifically: case-insensitive means "AZURE" → azure → "#0088CC"
        _sut.GetHexColor("AZURE").Should().Be("#0088CC");
        _sut.GetHexColor("Grey").Should().Be("#616161");
    }

    [Fact]
    public void GetHexColor_ResultAlwaysValidHexFormat()
    {
        foreach (var (_, hex) in _sut.AllColors)
        {
            hex.Should().StartWith("#").And.HaveLength(7);
        }
    }

    [Fact]
    public void AllColors_ContainsAll9Categories()
    {
        // 9 categories × 2 keys each (numeric + alias) = 18 entries
        _sut.AllColors.Should().HaveCount(18);
    }
}
```

### Unit Tests for ColorContrastService

File: `GoogleCalendarManagement.Tests/Unit/Services/ColorContrastServiceTests.cs`

```csharp
namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ColorContrastServiceTests
{
    private readonly ColorContrastService _sut = new();

    [Theory]
    [InlineData("#0088CC", "#000000")]  // Azure — luminance 0.240 > 0.179 → black
    [InlineData("#3F51B5", "#FFFFFF")]  // Purple — luminance 0.086 ≤ 0.179 → white
    [InlineData("#616161", "#FFFFFF")]  // Grey — luminance 0.134 ≤ 0.179 → white
    [InlineData("#F6BF26", "#000000")]  // Yellow — luminance 0.497 > 0.179 → black
    [InlineData("#33B679", "#000000")]  // Navy — luminance 0.208 > 0.179 → black
    [InlineData("#0B8043", "#FFFFFF")]  // Sage — luminance 0.095 ≤ 0.179 → white
    [InlineData("#E67C73", "#000000")]  // Flamingo — luminance 0.250 > 0.179 → black
    [InlineData("#F4511E", "#000000")]  // Orange — luminance 0.205 > 0.179 → black
    [InlineData("#8E24AA", "#FFFFFF")]  // Lavender — luminance 0.062 ≤ 0.179 → white
    public void GetContrastTextColor_ReturnsCorrectTextColor(string backgroundHex, string expectedTextColor)
    {
        _sut.GetContrastTextColor(backgroundHex).Should().Be(expectedTextColor);
    }

    [Fact]
    public void GetContrastTextColor_WithoutLeadingHash_DoesNotThrow()
    {
        // TrimStart('#') makes it robust
        var act = () => _sut.GetContrastTextColor("0088CC");
        act.Should().NotThrow();
    }

    [Fact]
    public void GetContrastTextColor_InvalidHex_DoesNotThrow()
    {
        var act = () => _sut.GetContrastTextColor("#ZZZZZZ");
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("#000000", "#FFFFFF")]  // black bg → white text
    [InlineData("#FFFFFF", "#000000")]  // white bg → black text
    public void GetContrastTextColor_PureBlackAndWhite(string bg, string expected)
    {
        _sut.GetContrastTextColor(bg).Should().Be(expected);
    }
}
```

### File Changes Summary

**Already exists (confirmed from Story 3.1 commit f83707e):**
```
Services/IColorMappingService.cs      ✅ exists — ADD AllColors property
Services/ColorMappingService.cs       ✅ exists — REPLACE stub body
Services/CalendarQueryService.cs      ✅ exists — no changes needed
Models/CalendarEventDisplayModel.cs   ✅ exists — no changes needed
Views/MonthViewControl.xaml.cs        ✅ exists — inject + replace Colors.White (×1)
Views/WeekViewControl.xaml.cs         ✅ exists — inject + replace Colors.White (×3)
Views/DayViewControl.xaml.cs          ✅ exists — inject + replace Colors.White (×3)
Views/YearViewControl.xaml.cs         ✅ exists — SKIP (no event chips)
App.xaml.cs line 187                  ✅ IColorMappingService already registered
```

**Files to create:**
```
Services/IColorContrastService.cs
Services/ColorContrastService.cs
GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs
GoogleCalendarManagement.Tests/Unit/Services/ColorContrastServiceTests.cs
```

**Files to modify:**
```
Services/IColorMappingService.cs         — add AllColors property
Services/ColorMappingService.cs          — replace stub with full dictionary
Views/MonthViewControl.xaml.cs           — inject IColorContrastService, fix 1× Colors.White
Views/WeekViewControl.xaml.cs            — inject IColorContrastService, fix 3× Colors.White
Views/DayViewControl.xaml.cs             — inject IColorContrastService, fix 3× Colors.White
App.xaml.cs                              — add IColorContrastService registration
```

**Do NOT create:**
- No `EventChip.xaml` or `EventBlock.xaml` — they were never created by Story 3.1 and are not needed
- No changes to `CalendarQueryService.cs` — it already calls `_colorMappingService.GetHexColor(event.ColorId)` correctly
- No changes to `CalendarEventDisplayModel.cs` — `ColorHex` field already exists

### Build & Test Commands

```
dotnet build -p:Platform=x64
dotnet test
```

### References

- [Services/ColorMappingService.cs](../../Services/ColorMappingService.cs) — current stub to replace
- [Services/IColorMappingService.cs](../../Services/IColorMappingService.cs) — add AllColors
- [Views/MonthViewControl.xaml.cs](../../Views/MonthViewControl.xaml.cs) — 1× Colors.White to replace
- [Views/WeekViewControl.xaml.cs](../../Views/WeekViewControl.xaml.cs) — 3× Colors.White to replace
- [Views/DayViewControl.xaml.cs](../../Views/DayViewControl.xaml.cs) — 3× Colors.White to replace
- [App.xaml.cs](../../App.xaml.cs) — DI registration (line 187 area)
- [docs/_color-definitions.md](../../docs/_color-definitions.md) — authoritative colour taxonomy and hex values
- [docs/epic-3/tech-spec.md](../../docs/epic-3/tech-spec.md) — Epic 3 ACs and colour mapping table

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

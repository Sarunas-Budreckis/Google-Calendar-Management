# Story 8.16: Closing Code Terminology-Cleanup + Naming Guard

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
**Agent:** Codex · **Effort:** low
**Prerequisites:** Stories 8.6, 8.10, 8.15 must be complete

---

## Story

As a developer maintaining Epic 8/9 code,
I want all straggler identifiers renamed away from retired data terms and a naming guard test in CI,
so that no production code identifier misuses "integration" for a data concept, "artifact" for a datapoint, or "IsIntegrated" for coverage state — and those terms cannot silently regress.

---

## Acceptance Criteria

1. No production C# type, property, method, or field name (outside `Data/Migrations/`) uses any banned term from the guard list (§ Banned terms) for the wrong concept.
2. `DataSourceDayDataMarkerViewModel` is renamed to `DataSourceDayCoverageViewModel`; all references updated.
3. `DataSourceDayCardViewModel.IsIntegrated` is renamed to `IsCovered`; `SetIntegrationAsync` / `GetIntegrationAsync` are removed (8.10 deletes `DateSourceIntegration` — this task verifies no stale references remain).
4. A new unit test `NamingGuardTests.NoRetiredDataTermsInProductionIdentifiers` passes; it scans all types in the main assembly via reflection and fails if any banned term is found in a type/property/method/field name (see § Naming guard implementation).
5. The naming guard test explicitly excludes:
   - Types in `Data/Migrations/` namespaces (historical migration classes are frozen).
   - The string literal `"DateSourceIntegration"` appearing only inside migration files.
   - The test file itself (scanning only the production assembly, not the test assembly).
6. After the sweep, running `grep -rn "IsIntegrated\|GetIntegrationAsync\|SetIntegrationAsync\|DataSourceDayDataMarker\|DateSourceIntegration" -- "*.cs"` on the production project (not migrations, not tests) returns zero hits.
7. No behavior changes: this story is rename-only. No logic, migrations, or XAML layout changes.

---

## Tasks / Subtasks

- [ ] Task 1: Verify prerequisite cleanup is complete (AC: #3, #6)
  - [ ] 1.1 Run `grep -rn "IsIntegrated\|GetIntegrationAsync\|SetIntegrationAsync\|DateSourceIntegration" -- "*.cs"` (exclude `Data/Migrations/`). Compile list of any hits.
  - [ ] 1.2 Run `grep -rn "DataSourceDayDataMarker" -- "*.cs" "*.xaml"`. Compile list of hits.
  - [ ] 1.3 If 8.10 removed `IDataSourceRepository.GetIntegrationAsync` and `SetIntegrationAsync` — confirm no dangling `DataSourceDayCardViewModel.IsIntegrated` property or call sites remain. If any remain, remove them now.

- [ ] Task 2: Rename `DataSourceDayDataMarkerViewModel` → `DataSourceDayCoverageViewModel` (AC: #2)
  - [ ] 2.1 `ViewModels/DataSourceDayDataMarkerViewModel.cs` → rename file to `DataSourceDayCoverageViewModel.cs`; rename class and constructor.
  - [ ] 2.2 `ViewModels/DataSourcePanelViewModel.cs` — replace all `DataSourceDayDataMarkerViewModel` references (construction + type annotation at ~line 478, 483).
  - [ ] 2.3 `ViewModels/DataSourceSummaryViewModel.cs` — replace all references (~lines 28, 74, 161).
  - [ ] 2.4 `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` — update `DayDataMarkers` collection access and type references.
  - [ ] 2.5 Verify build passes (no remaining `DataSourceDayDataMarkerViewModel` references).

- [ ] Task 3: Rename `DayDataMarkers` property → `DayCoverageItems` (AC: #1)
  - [ ] 3.1 `ViewModels/DataSourceSummaryViewModel.cs` — rename `DayDataMarkers` observable collection property → `DayCoverageItems`.
  - [ ] 3.2 `ViewModels/DataSourcePanelViewModel.cs` — any `.DayDataMarkers` access.
  - [ ] 3.3 Any XAML bindings referencing `DayDataMarkers` — search with `grep -rn "DayDataMarkers" -- "*.xaml"` and update.
  - [ ] 3.4 Tests — update `.DayDataMarkers[` and `.DayDataMarkers.` to `.DayCoverageItems[` / `.DayCoverageItems.`.

- [ ] Task 4: Sweep for any remaining residual "integration" or "artifact" data-concept misuse in identifiers (AC: #1)
  - [ ] 4.1 Run `grep -rn "Integrated\|Integration\|artifact\|Artifact" -- "*.cs"` (exclude `Data/Migrations/`). Review each hit for data-concept misuse.
  - [ ] 4.2 Fix any that fall under the banned-term rules (see § Banned terms). Leave legitimate non-data uses untouched.

- [ ] Task 5: Add naming guard test (AC: #4, #5)
  - [ ] 5.1 Create `GoogleCalendarManagement.Tests/Unit/NamingGuardTests.cs` (see § Naming guard implementation for the full implementation).
  - [ ] 5.2 Run the test; if it fails, add the offending identifiers to Task 4's fix list and re-run until green.

- [ ] Task 6: Final verification (AC: #6)
  - [ ] 6.1 `grep -rn "IsIntegrated\|GetIntegrationAsync\|SetIntegrationAsync\|DataSourceDayDataMarker\|DateSourceIntegration" -- "*.cs"` on production project (not migrations, not tests) → zero hits.
  - [ ] 6.2 `grep -rn "DayDataMarkers" -- "*.cs" "*.xaml"` → zero hits.
  - [ ] 6.3 Build succeeds, all tests pass.

---

## Dev Notes

### Context: what earlier stories already cleaned up

By the time 8.16 runs, the following should already be gone (verify in Task 1):

| Retired identifier | Removed by |
|---|---|
| `Data/Entities/DateSourceIntegration.cs` + `DateSourceIntegrationConfiguration.cs` | 8.10 |
| `CalendarDbContext.DateSourceIntegrations` DbSet | 8.10 |
| `IDataSourceRepository.GetIntegrationAsync` / `SetIntegrationAsync` | 8.10 |
| `Services/DataSourceRepository.cs` integration methods | 8.10 |
| `Data/Entities/PendingEvent.cs` + configuration + repository | 8.6 |
| `CalendarEventSourceKind.Outlook` enum value | 8.5 |
| `ViewModels/DataSourceDayCardViewModel.IsIntegrated` property | 8.10 |

Story 8.16's job is to catch any that slipped through and add the guard so they cannot come back.

### Banned terms

The naming guard and manual sweep enforce these rules:

| Banned identifier pattern | Wrong concept it encodes | Canonical replacement |
|---|---|---|
| `*Integration*` in type/method/property names (data concept) | Manual per-day coverage checkbox (deleted) | `Coverage`, `Covered`, `CoverageItem` |
| `IsIntegrated` | Whether a day-source pair has the old checkbox ticked | `IsCovered` |
| `GetIntegrationAsync` / `SetIntegrationAsync` | Reading/writing `DateSourceIntegration` rows | Deleted — coverage is computed, not stored |
| `DateSourceIntegration` | Deprecated table name | Removed — no new code should reference this |
| `*Artifact*` in data-layer identifiers | Datapoint | `Datapoint`, `DataPoint` |
| `DataSourceDayDataMarkerViewModel` | Vague "marker" vs. coverage semantics | `DataSourceDayCoverageViewModel` |
| `DayDataMarkers` (collection property) | Same | `DayCoverageItems` |

**Not banned** (intentional uses that must NOT be renamed):
- `GoogleCalendarManagement.Tests.Integration` namespace — that is the test category name, not a data concept.
- `"integration"` appearing inside migration class bodies or snapshot — frozen historical code.
- `SchemaTests.DateSourceIntegration_*` test method names — document the old schema; leave as historical record.
- `"integrated"` in non-data prose (comments, display strings) — out of scope for code identifier guard.

### Naming guard implementation

Place in `GoogleCalendarManagement.Tests/Unit/NamingGuardTests.cs`:

```csharp
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit;

public class NamingGuardTests
{
    private static readonly string[] BannedPatterns =
    [
        "DateSourceIntegration",
        "IsIntegrated",
        "GetIntegrationAsync",
        "SetIntegrationAsync",
        "DataSourceDayDataMarker",
        "DayDataMarkers",
    ];

    [Fact]
    public void NoRetiredDataTermsInProductionIdentifiers()
    {
        var productionAssembly = typeof(App).Assembly;

        var violations = new List<string>();

        foreach (var type in productionAssembly.GetTypes())
        {
            // Skip migration types (frozen historical code)
            if (type.Namespace?.Contains("Migrations") == true)
                continue;

            CheckName(type.Name, $"type {type.FullName}", violations);

            foreach (var member in type.GetMembers(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly))
            {
                CheckName(member.Name, $"{type.FullName}.{member.Name}", violations);
            }
        }

        violations.Should().BeEmpty(
            because: "retired data terms must not appear in production identifiers — " +
                     "see docs/epic-8-data-linking/concepts.md §2 Retired terms");
    }

    private static void CheckName(string name, string location, List<string> violations)
    {
        foreach (var banned in BannedPatterns)
        {
            if (name.Contains(banned, StringComparison.OrdinalIgnoreCase))
                violations.Add($"{location} contains banned term '{banned}'");
        }
    }
}
```

**Notes on the guard:**
- `typeof(App).Assembly` resolves to `GoogleCalendarManagement` (the WinUI project). Adjust if `App` is in a different namespace.
- The `Migrations` namespace filter covers EF Core migration classes and the model snapshot — these are auto-generated and must not be edited.
- The guard does **not** scan test assemblies — `GoogleCalendarManagement.Tests.Integration` namespace is a test category, not a data concept.
- `BannedPatterns` should be extended as new terms are retired in future epics.

### Rename cascade — DataSourceDayCoverageViewModel

The rename is mechanical: class name + file name + two ViewModel files + test file. No logic changes.

```
Before: DataSourceDayDataMarkerViewModel(DateOnly date, bool hasData, int? count, ...)
After:  DataSourceDayCoverageViewModel(DateOnly date, bool hasData, int? count, ...)
```

Constructor signature and properties (`Date`, `HasData`, `CountLabel`, `OpenAction`) are unchanged — only the class name and its collection property `DayDataMarkers` → `DayCoverageItems` on `DataSourceSummaryViewModel`.

### XAML binding search

Before renaming `DayDataMarkers`, run:

```
grep -rn "DayDataMarkers" Views/ --include="*.xaml"
```

If any XAML `{x:Bind}` or `{Binding}` references it, update those bindings to `DayCoverageItems`. If zero hits, the property is only bound in code-behind.

### Project structure

Files touched:

| File | Change |
|---|---|
| `ViewModels/DataSourceDayDataMarkerViewModel.cs` | Rename file + class → `DataSourceDayCoverageViewModel` |
| `ViewModels/DataSourceSummaryViewModel.cs` | Update type ref + rename `DayDataMarkers` → `DayCoverageItems` |
| `ViewModels/DataSourcePanelViewModel.cs` | Update type refs |
| Any XAML binding `DayDataMarkers` | Update to `DayCoverageItems` |
| `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` | Update type refs + `DayDataMarkers` → `DayCoverageItems` |
| `GoogleCalendarManagement.Tests/Unit/NamingGuardTests.cs` | **New file** — naming guard |

All other sweeps (Task 4) are conditional on what survived 8.10; if 8.10 was thorough they may be no-ops.

### References

- [Epic 8 overview](../epic-overview.md) — §Phase 2, Story 8.16
- [Concepts §2 vocabulary & retired terms](../concepts.md)
- [Story 8.1](8-1-terminology-doc-sweep.md) — parallel doc-sweep; code cleanup deliberately deferred to this story
- [Story 8.10](../stories/8-10-coverage-service-and-delete-date-source-integration.md) — deletes `DateSourceIntegration` table + `IsIntegrated` UI (prereq)
- `ViewModels/DataSourceDayDataMarkerViewModel.cs` — rename target
- `ViewModels/DataSourceSummaryViewModel.cs` — owns `DayDataMarkers` collection
- `ViewModels/DataSourcePanelViewModel.cs` — constructs `DataSourceDayDataMarkerViewModel`

---

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

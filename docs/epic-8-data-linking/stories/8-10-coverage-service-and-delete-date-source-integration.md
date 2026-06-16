# Story 8.10: Coverage Service + Delete `DateSourceIntegration`

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Sonnet · **Effort:** medium
**Dependencies:** 8.9 (blocking — `data_point` rows must exist for all sources); 8.12 provides real link state (link-aware second pass arrives when that story lands)

---

## Story

As the data accountability engine,
I want a `CoverageService` that computes `● / ◐ / ○` coverage from indexed `data_point` and `link` rows and a removal of the manual `DateSourceIntegration` checkbox table,
so that the day-mode panel shows computed coverage instead of the deprecated manual checkbox, and the retired table and all its write paths are gone.

---

## Acceptance Criteria

1. `ICoverageService` exists with methods for per-(date·source) coverage, per-day coverage, and per-event coverage — all returning a `CoverageResult` record carrying `(int Total, int Covered, CoverageLevel Level)`.
2. `CoverageLevel` enum has three values: `Full` (all covered, or zero total), `Partial` (some covered), `None` (zero covered, total > 0).
3. Zero datapoints in scope ⇒ `Level = Full` with `Total = 0, Covered = 0` — rendered as "—" (not a scary "0/0").
4. Coverage queries join `data_point LEFT JOIN link` using `dp.start_utc`/`dp.end_utc` and source_key indexes — no full-table scans.
5. When the `link` table does not yet exist (pre-8.12), `CoverageService` gracefully returns `CoverageResult(Total, Covered: 0, Level: None)` for all scopes with data — the SQL query catches `SqliteException` on missing table and falls back.
6. `DateSourceIntegration` entity, configuration, and `CalendarDbContext.DateSourceIntegrations` DbSet are deleted.
7. `IDataSourceRepository.GetIntegrationAsync` and `SetIntegrationAsync` are deleted from the interface and implementation.
8. `DataSourceDayCardViewModel` no longer has `IsIntegrated`, `ToggleIntegrationCommand`, or `IsIntegrationEnabled`. Instead it exposes a `CoverageResult Coverage` property populated at construction time (no live toggle needed in this story).
9. `DataSourcePanelViewModel.LoadDayModeAsync` no longer calls `GetIntegrationAsync`; it calls `ICoverageService.GetDateSourceCoverageAsync(date, source.SourceKey)` per card and passes the result to the card VM constructor.
10. `DataSourcePanelControl.xaml` — the `ToggleIntegrationCommand` `ToggleSwitch` / checkbox is replaced with a compact coverage display: level symbol (`●`, `◐`, `○`, `—`) and count text `"N/M linked"` (hidden when Total = 0).
11. An EF migration drops the `date_source_integration` table.
12. `DataSourceRepositoryTests` — the `SetIntegrationAsync_PersistsToDatabase` test and any other integration-related tests are deleted.
13. New integration tests for `CoverageService`: zero-data ⇒ Full; all-covered ⇒ Full; partial ⇒ Partial; none covered ⇒ None. Tests use in-memory SQLite + `context.Database.EnsureCreated()` seeding `data_point` rows directly (link table absent ⇒ graceful fallback confirmed separately).

---

## Tasks / Subtasks

- [x] Task 1: Create `CoverageResult` + `CoverageLevel` + `ICoverageService` (AC: #1, #2, #3)
  - [x] 1.1 Add `Models/CoverageResult.cs`:
    ```csharp
    public sealed record CoverageResult(int Total, int Covered, CoverageLevel Level);
    public enum CoverageLevel { Full, Partial, None }
    ```
  - [x] 1.2 Add `Services/ICoverageService.cs`:
    ```csharp
    public interface ICoverageService
    {
        Task<CoverageResult> GetDateSourceCoverageAsync(DateOnly date, string sourceKey, CancellationToken ct = default);
        Task<CoverageResult> GetDayCoverageAsync(DateOnly date, CancellationToken ct = default);
        Task<CoverageResult> GetEventCoverageAsync(DateTime startUtc, DateTime endUtc, CancellationToken ct = default);
    }
    ```
  - [x] 1.3 Implement `Services/CoverageService.cs` (see Dev Notes for SQL pattern)

- [x] Task 2: EF migration — drop `date_source_integration` (AC: #6, #11)
  - [x] 2.1 Table already dropped in UnifyEventTable migration (Story 8.2) — no new migration needed
  - [x] 2.2 N/A — already done in 8.2
  - [x] 2.3 N/A — already done in 8.2
  - [x] 2.4 Delete `Data/Entities/DateSourceIntegration.cs`
  - [x] 2.5 `Data/Configurations/DateSourceIntegrationConfiguration.cs` did not exist (already removed in 8.2)
  - [x] 2.6 `DbSet<DateSourceIntegration>` was not in `CalendarDbContext.cs` (already removed in 8.2)

- [x] Task 3: Remove `DateSourceIntegration` from `IDataSourceRepository` / `DataSourceRepository` (AC: #7)
  - [x] 3.1 Delete `GetIntegrationAsync` and `SetIntegrationAsync` from `Services/IDataSourceRepository.cs`
  - [x] 3.2 Delete the corresponding implementations in `Services/DataSourceRepository.cs`
  - [x] 3.3 Search for remaining callers — fixed all hits (SchemaTests, DataSourcePanelViewModelTests, DataSourceRepositoryTests deleted)

- [x] Task 4: Update `DataSourceDayCardViewModel` (AC: #8)
  - [x] 4.1 Remove constructor parameter `bool isIntegrated`, field `_isIntegrated`, property `IsIntegrated`, property `IsIntegrationEnabled`, command `ToggleIntegrationCommand`, method `ToggleIntegrationAsync`
  - [x] 4.2 Remove `_dataSourceRepository` field and constructor parameter (only used by `ToggleIntegrationAsync`)
  - [x] 4.3 Add constructor parameter `CoverageResult coverage` and property `public CoverageResult Coverage { get; }`
  - [x] 4.4 Add computed display helpers used by XAML: `CoverageLevelSymbol`, `CoverageCountText`, `CoverageCountVisibility`

- [x] Task 5: Update `DataSourcePanelViewModel.LoadDayModeAsync` (AC: #9)
  - [x] 5.1 Inject `ICoverageService` into constructor (add field `_coverageService`)
  - [x] 5.2 Remove the `GetIntegrationAsync` call in the per-source loop
  - [x] 5.3 Add `var coverage = await _coverageService.GetDateSourceCoverageAsync(date, source.SourceKey, ct);`
  - [x] 5.4 Pass `coverage` to `DataSourceDayCardViewModel` constructor (replacing `integration?.Integrated == true`)
  - [x] 5.5 Register `ICoverageService` → `CoverageService` as singleton in `App.xaml.cs`

- [x] Task 6: Update `DataSourcePanelControl.xaml` day-mode card (AC: #10)
  - [x] 6.1 Deleted `CheckBox` bound to `ToggleIntegrationCommand` and `IsIntegrated`
  - [x] 6.2 Updated "Integrated" label to "Coverage" in column header
  - [x] 6.3 Added coverage display `StackPanel` with `CoverageLevelSymbol` and `CoverageCountText`

- [x] Task 7: Update tests (AC: #12, #13)
  - [x] 7.1 Deleted `DataSourceRepositoryTests.cs` (entire file — only contained `SetIntegrationAsync_PersistsToDatabase`)
  - [x] 7.2 File deleted
  - [x] 7.3 Added `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` with all 5 tests (all pass)
  - [x] 7.4 Updated `DataSourcePanelViewModelTests.cs`: removed 3 integration tests, updated `StubDataSourceRepository`, added `ICoverageService` mock to `CreateViewModel`

### Review Findings

- [x] [Review][Patch] Coverage SQL binds O-format strings against EF `DateTime` TEXT columns, so real EF-seeded rows can be excluded by lexical comparison [Services/CoverageService.cs:35] — fixed by binding `DateTime` parameters and adding an EF-seeded regression test
- [x] [Review][Patch] Coverage joins count link rows instead of distinct datapoints, so duplicate link rows can inflate totals and covered counts [Services/CoverageService.cs:30] — fixed with distinct datapoint counting and duplicate-link regression coverage
- [x] [Review][Patch] `GetDayCoverageAsync` missing-table fallback still queries `data_point`, unlike the date-source fallback that returns zero coverage when `data_point` is absent [Services/CoverageService.cs:83] — fixed with a guarded day fallback and missing-table regression test

---

## Dev Notes

### CoverageService — SQL pattern

Use raw SQL via `_dbContextFactory.CreateDbContextAsync` → `db.Database.SqlQueryRaw` or ADO.NET on the SQLite connection for the join. EF has no `DataPoint` or `Link` DbSet yet (those are added by 8.7 and 8.12 respectively). Use raw SQL:

```sql
-- Per (date, source) coverage
SELECT
    COUNT(dp.data_point_id) AS total,
    SUM(CASE WHEN l.link_id IS NOT NULL THEN 1 ELSE 0 END) AS covered
FROM data_point dp
LEFT JOIN link l ON l.data_point_id = dp.data_point_id
WHERE dp.source_key = @sourceKey
  AND dp.start_utc >= @dateStart
  AND dp.start_utc < @dateEnd
```

**Date boundary:** `dateStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)` and `dateEnd = dateStart.AddDays(1)`.

**Link table missing (pre-8.12):** Wrap the SQL execution in a `try/catch` for `Microsoft.Data.Sqlite.SqliteException` with `SqliteErrorCode.Error` (code 1 = no such table). On catch, fall back to a `COUNT(*)` query against `data_point` only and return `CoverageResult(total, 0, total > 0 ? CoverageLevel.None : CoverageLevel.Full)`.

```csharp
public sealed class CoverageService : ICoverageService
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;

    public CoverageService(IDbContextFactory<CalendarDbContext> dbContextFactory)
        => _dbContextFactory = dbContextFactory;

    public async Task<CoverageResult> GetDateSourceCoverageAsync(DateOnly date, string sourceKey, CancellationToken ct = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(ct);

        var start = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(dp.data_point_id), SUM(CASE WHEN l.link_id IS NOT NULL THEN 1 ELSE 0 END)
                FROM data_point dp
                LEFT JOIN link l ON l.data_point_id = dp.data_point_id
                WHERE dp.source_key = @sk AND dp.start_utc >= @s AND dp.start_utc < @e";
            cmd.Parameters.Add(new SqliteParameter("@sk", sourceKey));
            cmd.Parameters.Add(new SqliteParameter("@s", start.ToString("O")));
            cmd.Parameters.Add(new SqliteParameter("@e", end.ToString("O")));

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var total = reader.GetInt32(0);
                var covered = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return BuildResult(total, covered);
            }
            return new CoverageResult(0, 0, CoverageLevel.Full);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) // no such table: link
        {
            return await FallbackCountOnlyAsync(conn, sourceKey, start, end, ct);
        }
    }

    private static CoverageResult BuildResult(int total, int covered)
    {
        if (total == 0) return new CoverageResult(0, 0, CoverageLevel.Full);
        if (covered >= total) return new CoverageResult(total, covered, CoverageLevel.Full);
        if (covered > 0) return new CoverageResult(total, covered, CoverageLevel.Partial);
        return new CoverageResult(total, 0, CoverageLevel.None);
    }

    private static async Task<CoverageResult> FallbackCountOnlyAsync(DbConnection conn, string sourceKey, DateTime start, DateTime end, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM data_point WHERE source_key=@sk AND start_utc>=@s AND start_utc<@e";
        cmd.Parameters.Add(new SqliteParameter("@sk", sourceKey));
        cmd.Parameters.Add(new SqliteParameter("@s", start.ToString("O")));
        cmd.Parameters.Add(new SqliteParameter("@e", end.ToString("O")));
        var total = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        return new CoverageResult(total, 0, total > 0 ? CoverageLevel.None : CoverageLevel.Full);
    }
}
```

**Note:** `data_point` table is created in story 8.7. If that table also doesn't exist yet, the fallback catch needs to handle that too — return `CoverageResult(0, 0, Full)`.

### What `data_point` looks like (from 8.7 / concepts.md §4)

```sql
data_point
  data_point_id  PK
  source_key     TEXT   ('toggl_entry', 'spotify_stream', 'civ5_session', ...)
  source_ref     TEXT
  start_utc      TEXT   (ISO-8601, UTC)
  end_utc        TEXT
  created_at     TEXT
```

Indexes on `(start_utc)` and `(source_key, start_utc)` per story 8.7 AC.

### What `link` looks like (from 8.12 / concepts.md §5) — not yet created

```sql
link
  link_id         PK
  data_point_id   FK → data_point  (ON DELETE CASCADE)
  event_id        FK → event, nullable
  state           'linked' | 'ignored'
  origin          'manual' | 'auto_rule'
  rule_id         nullable
  action_group_id TEXT
  created_at, updated_at
```

### `DataSourceDayCardViewModel` constructor call site

The call in `DataSourcePanelViewModel.LoadDayModeAsync` (line ~360) currently passes `integration?.Integrated == true` as the 4th argument. After this story the updated signature should be:

```csharp
DayCards.Add(new DataSourceDayCardViewModel(
    source.DataSourceId,
    source.SourceKey,
    source.DisplayName,
    coverage,           // <-- was: integration?.Integrated == true
    isGreyedOut,
    date,
    card => DrilldownCard = card,
    compactSummaryView,
    drilldownViewFactory,
    addAction,
    addButtonContent,
    allowAddWhenGreyedOut));
```

Remove the `_dataSourceRepository` argument from the constructor if no other method in the card VM uses it (currently only `ToggleIntegrationAsync` calls `_dataSourceRepository.SetIntegrationAsync`; after removal, check if any other method remains). If yes, remove it. If not, verify and remove.

### XAML location

`Views/DataSourcePanelControl.xaml`:
- Line 326: `TextBlock Text="Integrated"` — delete
- Lines 392–395: `ToggleSwitch` bound to `ToggleIntegrationCommand` / `IsIntegrated` — delete entire element
- Add coverage display in the same area (see Task 6.3)

### Existing test patterns

xUnit + FluentAssertions + Moq. New `CoverageServiceTests` follow the same in-memory SQLite pattern as `DataSourceRepositoryTests`:

```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
```

Seed `data_point` rows using raw SQL (`INSERT INTO data_point ...`) since there's no DbSet. The `link` table also does not exist yet in the schema — seed links with raw SQL too when testing the happy-path coverage.

For the "link table missing" fallback test: create a separate in-memory SQLite that has the `data_point` table but not `link` (i.e., manually `CREATE TABLE data_point ...` without calling `EnsureCreated`, or drop the link table after `EnsureCreated`).

### `DataSourcePanelViewModelTests` — mock changes

The test file mocks `IDataSourceRepository`. After removing `GetIntegrationAsync` and `SetIntegrationAsync` from the interface, any mock setups for those methods must be deleted. Search for `GetIntegrationAsync` and `SetIntegrationAsync` in the test file and remove them.

`ICoverageService` must be added to the mock stack — inject a mock `ICoverageService` that returns `new CoverageResult(0, 0, CoverageLevel.Full)` by default.

### App.xaml.cs DI registration

Add:
```csharp
services.AddSingleton<ICoverageService, CoverageService>();
```

near the other `IDataSourceRepository` registrations.

### Two-pass note (from epic overview)

This story is **Pass 1**. Coverage UI and service are live; all `DateSourceIntegration` code is gone. Until story 8.12 lands, `Covered` will always be 0 (link table absent → graceful fallback). The coverage symbol will show `○` or `—` for all sources. That is expected and correct — the UI is wired, it just needs links to light up. No `TODO` comments needed in code; this is by design.

### Scope boundary

This story does NOT:
- Create `data_point` DbSet on `CalendarDbContext` (that's 8.7)
- Create `link` DbSet on `CalendarDbContext` (that's 8.12)
- Change how clumps or blocks are computed (8.11)
- Change the linking panel or Epic 9 UI
- Modify `ICoverageService` for event-level coverage beyond defining the interface (implementation can be a stub `throw new NotImplementedException()` for `GetEventCoverageAsync` until 8.12 lands)

### Project structure — files changed

| Action | File |
|--------|------|
| Add | `Models/CoverageResult.cs` |
| Add | `Services/ICoverageService.cs` |
| Add | `Services/CoverageService.cs` |
| Delete | `Data/Entities/DateSourceIntegration.cs` |
| Delete | `Data/Configurations/DateSourceIntegrationConfiguration.cs` |
| Modify | `Data/CalendarDbContext.cs` — remove `DateSourceIntegrations` DbSet |
| Modify | `Services/IDataSourceRepository.cs` — remove 2 methods |
| Modify | `Services/DataSourceRepository.cs` — remove 2 methods |
| Modify | `ViewModels/DataSourceDayCardViewModel.cs` — remove toggle, add coverage |
| Modify | `ViewModels/DataSourcePanelViewModel.cs` — inject `ICoverageService`, remove integration call |
| Modify | `Views/DataSourcePanelControl.xaml` — replace toggle with coverage display |
| Modify | `App.xaml.cs` — register `ICoverageService` |
| Add | `Data/Migrations/<timestamp>_DropDateSourceIntegration.cs` |
| Modify | `GoogleCalendarManagement.Tests/Integration/DataSourceRepositoryTests.cs` — delete integration tests |
| Add | `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` |
| Modify | `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` — remove integration mocks, add coverage mock |

### References

- Canonical coverage model: [concepts.md §6](../concepts.md)
- Epic 8 story spec: [epic-overview.md §Phase 1 Story 8.10](../epic-overview.md)
- `DateSourceIntegration` entity (to delete): `Data/Entities/DateSourceIntegration.cs`
- `DateSourceIntegrationConfiguration` (to delete): `Data/Configurations/DateSourceIntegrationConfiguration.cs`
- `IDataSourceRepository` (to trim): `Services/IDataSourceRepository.cs`
- `DataSourceRepository` (to trim): `Services/DataSourceRepository.cs`
- `DataSourceDayCardViewModel` (to rewrite toggle → coverage): `ViewModels/DataSourceDayCardViewModel.cs`
- `DataSourcePanelViewModel.LoadDayModeAsync` call site: `ViewModels/DataSourcePanelViewModel.cs` (~line 325–380)
- XAML toggle location: `Views/DataSourcePanelControl.xaml` (~lines 326, 392–395)
- DI registrations: `App.xaml.cs`
- Existing tests to update: `GoogleCalendarManagement.Tests/Integration/DataSourceRepositoryTests.cs`, `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs`

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Created `Models/CoverageResult.cs` (`CoverageResult` record + `CoverageLevel` enum).
- Created `Services/ICoverageService.cs` with 3 methods; `GetEventCoverageAsync` stubs to `NotImplementedException` until 8.12.
- Created `Services/CoverageService.cs`: raw ADO.NET SQL with LEFT JOIN to `link`; catches `SqliteException` code 1 for missing `link` table (fallback count-only) and missing `data_point` table (returns Full/0).
- Deleted `Data/Entities/DateSourceIntegration.cs` (table was already dropped in 8.2's `UnifyEventTable` migration — no new migration needed).
- Removed `GetIntegrationAsync`/`SetIntegrationAsync` from `IDataSourceRepository` and `DataSourceRepository`; no remaining callers.
- Rewrote `DataSourceDayCardViewModel`: removed `_dataSourceRepository`, `IsIntegrated`, `IsIntegrationEnabled`, `ToggleIntegrationCommand`, `ToggleIntegrationAsync`; added `Coverage` property and `CoverageLevelSymbol`/`CoverageCountText`/`CoverageCountVisibility` helpers.
- Updated `DataSourcePanelViewModel`: added `ICoverageService` constructor param + field; `LoadDayModeAsync` now calls `GetDateSourceCoverageAsync` per source.
- Updated `DataSourcePanelControl.xaml`: column header "Integrated" → "Coverage"; replaced `CheckBox` with coverage `StackPanel`.
- Registered `ICoverageService → CoverageService` as singleton in `App.xaml.cs`.
- Deleted `DataSourceRepositoryTests.cs` (only test referenced dropped table).
- Added `CoverageServiceTests.cs` with 5 integration tests; all pass. Fixed shared-connection issue (guard `conn.State` before `OpenAsync`).
- Updated `DataSourcePanelViewModelTests.cs`: removed 3 integration tests, stripped `StubDataSourceRepository` of integration members, added `ICoverageService` mock parameter to `CreateViewModel`.
- Removed 2 stale `DateSourceIntegration` schema tests from `SchemaTests.cs`.
- Result: 488 passed, 0 failed, 19 skipped.
- Review fixes: bound coverage SQL date parameters as provider `DateTime` values, counted distinct datapoints across joins, and added missing `data_point` fallback for day coverage. Added 3 regression tests.

### File List

- `Models/CoverageResult.cs` — Added
- `Services/ICoverageService.cs` — Added
- `Services/CoverageService.cs` — Added
- `Data/Entities/DateSourceIntegration.cs` — Deleted
- `Services/IDataSourceRepository.cs` — Modified
- `Services/DataSourceRepository.cs` — Modified
- `ViewModels/DataSourceDayCardViewModel.cs` — Modified
- `ViewModels/DataSourcePanelViewModel.cs` — Modified
- `Views/DataSourcePanelControl.xaml` — Modified
- `App.xaml.cs` — Modified
- `GoogleCalendarManagement.Tests/Integration/DataSourceRepositoryTests.cs` — Deleted
- `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` — Added
- `GoogleCalendarManagement.Tests/Unit/ViewModels/DataSourcePanelViewModelTests.cs` — Modified
- `GoogleCalendarManagement.Tests/Integration/SchemaTests.cs` — Modified

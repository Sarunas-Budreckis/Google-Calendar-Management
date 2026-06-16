# Story 8.12: `link` Table + Link / Ignore / Unlink Operations (Undoable)

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Opus · **Effort:** high
**Dependencies:** 8.7 (blocking — `data_point` table + `DataPoint` entity must exist); 8.2 (blocking — unified `event` table with stable `event_id` string PK must exist)

---

## Story

As the data-linking engine,
I want a `link` table that records every resolved datapoint (linked to an event, or intentionally ignored), and a service that executes link / ignore / unlink operations atomically, grouped by `action_group_id` for clump-level undo,
so that every datapoint has exactly zero or one resolution row, all manual operations are undoable as a batch, and downstream consumers (rule engine 8.14, Linking panel 9.x) have a stable, well-guarded read/write contract.

---

## Acceptance Criteria

1. **Schema — `link` table created via EF migration:**
   - `link_id` INT PK, auto-increment.
   - `data_point_id` INT NOT NULL, FK → `data_point.data_point_id` **ON DELETE CASCADE**.
   - `event_id` TEXT nullable, FK → `event.event_id` **ON DELETE RESTRICT** (service handles cleanup before event deletion).
   - `state` TEXT NOT NULL — only valid values are `'linked'` and `'ignored'`.
   - `origin` TEXT NOT NULL — only valid values are `'manual'` and `'auto_rule'`.
   - `rule_id` TEXT nullable.
   - `action_group_id` TEXT NOT NULL.
   - `created_at` DATETIME NOT NULL, `updated_at` DATETIME NOT NULL.
   - **Unique index** on `data_point_id` (one resolution row per datapoint, max).
   - Index on `event_id` (link lookup by event).
   - Index on `action_group_id` (undo lookup).

2. **Invariant — state/event_id consistency enforced by `LinkService` (not DB):**
   - `state='linked'` ↔ `event_id IS NOT NULL`.
   - `state='ignored'` ↔ `event_id IS NULL`.
   - Any call that would violate this invariant throws `InvalidOperationException` before touching the DB.

3. **`LinkAsync(dataPointId, eventId)` — creates or replaces a link row:**
   - Upserts a `Link` row: `state='linked'`, `origin='manual'`, `rule_id=null`, `event_id` set, `action_group_id` = new group id.
   - Returns the `action_group_id`.
   - Previous state (or null if the datapoint was unlinked) is saved in the undo stack for this group.

4. **`LinkClumpAsync(dataPointIds, eventId)` — links N datapoints atomically:**
   - All rows share the **same** `action_group_id`.
   - Written in a single EF `SaveChangesAsync` call (one transaction).
   - Returns the shared `action_group_id`.
   - All previous states saved under the same group id in the undo stack.

5. **`IgnoreAsync(dataPointId)` — creates or replaces an ignore row:**
   - Upserts a `Link` row: `state='ignored'`, `origin='manual'`, `rule_id=null`, `event_id=null`.
   - Returns the `action_group_id`.

6. **`IgnoreClumpAsync(dataPointIds)` — ignores N datapoints atomically:**
   - Same group semantics as `LinkClumpAsync`.

7. **`UnlinkAsync(dataPointId)` — removes the link row:**
   - If no link row exists, this is a no-op (idempotent).
   - Previous state saved in undo stack.
   - Returns the `action_group_id` (so the caller can undo the unlink).

8. **`UnlinkClumpAsync(dataPointIds)` — removes N link rows atomically:**
   - Same group semantics as `LinkClumpAsync`.

9. **`UndoActionGroupAsync(actionGroupId)` — reverts one batch:**
   - For each datapoint in the group, restore its link row to the snapshot taken before the operation:
     - If the previous state was "no row" → delete the current link row.
     - If the previous state was a row → upsert it back (state, event_id, origin, rule_id preserved).
   - Written in a single `SaveChangesAsync` (one transaction).
   - After undo, the action group is removed from the undo stack.
   - Calling undo on an unknown `actionGroupId` is a no-op (the group may have been superseded).

10. **Cascade-delete verified:** Deleting a `DataPoint` row cascades and removes its `Link` row automatically (DB-level ON DELETE CASCADE). No `LinkService` call is needed.

11. **No DB cascade on `event_id`:** When an event is deleted, the service (or the caller — Story 8.14) must explicitly unlink/re-evaluate affected datapoints before deletion. The `ON DELETE RESTRICT` constraint prevents accidental orphaning.

12. **`WriteAutoLinkAsync` / `WriteAutoIgnoreAsync` — for rule engine use (8.14):**
    - Accept `origin='auto_rule'` and a `ruleId` string.
    - Same upsert semantics; NOT added to the undo stack (auto links are reversed by rule recomputation, not by undo).
    - Never overwrite a row where `origin='manual'` — throw `InvalidOperationException` if attempted.

13. **`GetLinkAsync(dataPointId)` → `Link?`:** Returns the current link row for a datapoint, or null if unlinked.

14. **`GetLinksByEventAsync(eventId)` → `IReadOnlyList<Link>`:** All link rows referencing a given event.

15. **`GetLinksByActionGroupAsync(actionGroupId)` → `IReadOnlyList<Link>`:** All link rows sharing a group (for UI undo-button label).

16. **Integration tests** cover all AC items; see Dev Notes for test matrix.

17. **DI registration:** `ILinkService` registered as Singleton in `App.xaml.cs`.

---

## Tasks / Subtasks

- [x] Task 1: Create `Link` entity + configuration + migration (AC: #1)
  - [x] 1.1 Create `Data/Entities/Link.cs` — public properties: `LinkId`, `DataPointId`, `EventId` (string?), `State`, `Origin`, `RuleId` (string?), `ActionGroupId`, `CreatedAt`, `UpdatedAt`; navigation properties: `DataPoint DataPoint`, `Event? Event`
  - [x] 1.2 Add `ICollection<Link> Links { get; set; }` navigation to the `DataPoint` entity (created in 8.7)
  - [x] 1.3 Create `Data/Configurations/LinkConfiguration.cs` implementing `IEntityTypeConfiguration<Link>` — see Dev Notes for full configuration
  - [x] 1.4 Add `public DbSet<Link> Links { get; set; }` to `CalendarDbContext`
  - [x] 1.5 Run `dotnet ef migrations add AddLinkTable` — verify generated SQL matches schema in AC #1; fix configuration if not
  - [x] 1.6 Confirm migration file uses timestamp naming convention matching existing migrations

- [x] Task 2: Create `ILinkService` interface (AC: #3–#15)
  - [x] 2.1 Create `Services/ILinkService.cs` — declare all methods listed in Dev Notes interface block; return types: link/ignore/unlink ops return `Task<string>` (the action_group_id); query ops return `Task<T>`
  - [x] 2.2 Confirm namespace `GoogleCalendarManagement.Services`

- [x] Task 3: Implement `LinkService` (AC: #2–#15)
  - [x] 3.1 Create `Services/LinkService.cs` — constructor takes `IDbContextFactory<CalendarDbContext>`, holds an in-memory undo stack (`Dictionary<string, List<LinkSnapshot>>`)
  - [x] 3.2 Implement `GenerateActionGroupId()` private helper: `Guid.NewGuid().ToString("N")` — same pattern as `EventIdentityService.MintEventId()`
  - [x] 3.3 Implement invariant guard `AssertManualOriginNotOverwritten(Link existing)` — throws if `existing.Origin == "manual"` (implemented as `AssertNotManual`)
  - [x] 3.4 Implement `LinkAsync` — snapshot previous → upsert link row → push to undo stack → return group id
  - [x] 3.5 Implement `LinkClumpAsync` — snapshot all previous states → upsert all link rows in one transaction → push one group entry → return group id
  - [x] 3.6 Implement `IgnoreAsync` — same pattern as `LinkAsync` with `state='ignored'`, `event_id=null`
  - [x] 3.7 Implement `IgnoreClumpAsync` — same pattern as `LinkClumpAsync`
  - [x] 3.8 Implement `UnlinkAsync` — snapshot previous (or null) → delete row if exists → push to undo stack → return group id
  - [x] 3.9 Implement `UnlinkClumpAsync` — snapshot all → delete in one transaction → push group → return group id
  - [x] 3.10 Implement `UndoActionGroupAsync` — retrieve snapshot from undo stack → restore rows in one transaction → remove group from stack
  - [x] 3.11 Implement `WriteAutoLinkAsync(dataPointId, eventId, ruleId)` — guard against overwriting `manual` rows; upsert with `origin='auto_rule'`; do NOT add to undo stack
  - [x] 3.12 Implement `WriteAutoIgnoreAsync(dataPointId, ruleId)` — same guard; upsert with `origin='auto_rule'`, `event_id=null`
  - [x] 3.13 Implement `GetLinkAsync`, `GetLinksByEventAsync`, `GetLinksByActionGroupAsync` — direct EF queries using `IDbContextFactory`
  - [x] 3.14 Verify all public methods are `async Task` / `async Task<T>` — no `.Result` or `.Wait()` calls

- [x] Task 4: DI registration (AC: #17)
  - [x] 4.1 In `App.xaml.cs`, add `services.AddSingleton<ILinkService, LinkService>()` near the other singleton data-service registrations

- [x] Task 5: Integration tests (AC: #16)
  - [x] 5.1 Create `GoogleCalendarManagement.Tests/Integration/LinkServiceTests.cs` — xUnit class; setup uses in-memory SQLite + `EnsureCreated()` (same pattern as `GoogleCalendarSyncTests.cs`)
  - [x] 5.2 Test: `LinkAsync_CreatesLinkRow_WithCorrectFields` — verify `state='linked'`, `origin='manual'`, `event_id` set, `action_group_id` not empty
  - [x] 5.3 Test: `IgnoreAsync_CreatesIgnoreRow_WithNullEventId` — verify `state='ignored'`, `event_id IS NULL`
  - [x] 5.4 Test: `UnlinkAsync_RemovesRow_WhenRowExists` — link a datapoint, then unlink, verify no row
  - [x] 5.5 Test: `UnlinkAsync_IsNoOp_WhenRowAbsent` — no row exists; call unlink; assert no exception, still no row
  - [x] 5.6 Test: `LinkClumpAsync_WritesNRows_UnderSameActionGroupId` — 3 datapoints; assert all rows share same group id
  - [x] 5.7 Test: `UndoActionGroupAsync_RestoresPreviousState_WhenWasUnlinked` — link then undo; assert row deleted
  - [x] 5.8 Test: `UndoActionGroupAsync_RestoresPreviousState_WhenWasLinkedToOtherEvent` — datapoint linked to eventA; re-link to eventB; undo; assert linked back to eventA
  - [x] 5.9 Test: `UndoActionGroupAsync_IsNoOp_WhenGroupIdUnknown` — call undo with a random guid; assert no exception
  - [x] 5.10 Test: `CascadeDelete_DeletesLinkRow_WhenDataPointDeleted` — create datapoint + link row; delete datapoint via EF; assert link row gone
  - [x] 5.11 Test: `WriteAutoLinkAsync_DoesNotOverwrite_ManualRow` — seed a manual link row; call `WriteAutoLinkAsync` on the same datapoint; assert throws `InvalidOperationException`
  - [x] 5.12 Test: `WriteAutoLinkAsync_Overwrites_ExistingAutoRow` — seed auto_rule link; call `WriteAutoLinkAsync` with different event; assert row updated, still `origin='auto_rule'`
  - [x] 5.13 Test: `UniqueConstraint_Enforced_AtDbLevel` — try to insert two link rows for the same `data_point_id` via raw EF; assert `DbUpdateException`
  - [x] 5.14 Test: `UndoClump_RestoresAllNDatapoints_InOneStep` — link 3-datapoint clump; undo; all 3 rows gone
  - [x] 5.15 (added) Test: `LinkAsync_Throws_WhenEventIdMissing` — verifies the state/event_id invariant guard (AC #2)

### Review Findings

- [x] [Review][Patch] Superseded action groups can still undo and overwrite newer link decisions [Services/LinkService.cs:57]
- [x] [Review][Patch] Clump operations do not normalize null, empty, or duplicate datapoint inputs before saving [Services/LinkService.cs:130]
- [x] [Review][Patch] `state` and `origin` allowed values are not enforced at the schema boundary [Data/Configurations/LinkConfiguration.cs:16]
- [x] [Review][Patch] Auto rule writes accept blank `ruleId`, leaving `auto_rule` rows without rule traceability [Services/LinkService.cs:51]
- [x] [Review][Patch] Integration tests do not cover all Story 8.12 acceptance criteria and public service methods [GoogleCalendarManagement.Tests/Integration/LinkServiceTests.cs:34]

---

## Dev Notes

### What 8.7 leaves for this story

Story 8.7 will have created:
- `Data/Entities/DataPoint.cs` — entity with `DataPointId` (int PK), `SourceKey`, `SourceRef`, `StartUtc`, `EndUtc`, `CreatedAt`
- `Data/Configurations/DataPointConfiguration.cs` — table `data_point`, indexes on `(start_utc)` and `(source_key, start_utc)`
- `DbSet<DataPoint> DataPoints` in `CalendarDbContext`
- `Services/IDataPointRepository.cs` + `DataPointRepository.cs` — CRUD over `data_point`

**Add the navigation property on `DataPoint`** (Task 1.2):
```csharp
// In Data/Entities/DataPoint.cs — add:
public ICollection<Link> Links { get; set; } = new List<Link>();
```
EF will configure the relationship from `LinkConfiguration`; the navigation here is only needed for EF graph traversal.

### What 8.2 leaves for this story

The unified `event` table exists with:
- `event_id` TEXT PK (stable local string minted by `EventIdentityService`)
- FK target for `link.event_id`

The `Event` entity is in `Data/Entities/Event.cs`, namespace `GoogleCalendarManagement.Data.Entities`.

### `Link` entity

```csharp
// Data/Entities/Link.cs
namespace GoogleCalendarManagement.Data.Entities;

public class Link
{
    public int LinkId { get; set; }
    public int DataPointId { get; set; }
    public string? EventId { get; set; }
    public string State { get; set; } = "";        // 'linked' | 'ignored'
    public string Origin { get; set; } = "manual"; // 'manual' | 'auto_rule'
    public string? RuleId { get; set; }
    public string ActionGroupId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DataPoint DataPoint { get; set; } = null!;
    public Event? Event { get; set; }
}
```

### `LinkConfiguration`

```csharp
// Data/Configurations/LinkConfiguration.cs
namespace GoogleCalendarManagement.Data.Configurations;

using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("link");
        builder.HasKey(x => x.LinkId);
        builder.Property(x => x.LinkId).HasColumnName("link_id");
        builder.Property(x => x.DataPointId).HasColumnName("data_point_id").IsRequired();
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.Origin).HasColumnName("origin").IsRequired();
        builder.Property(x => x.RuleId).HasColumnName("rule_id");
        builder.Property(x => x.ActionGroupId).HasColumnName("action_group_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // One link row per datapoint (max)
        builder.HasIndex(x => x.DataPointId).IsUnique();
        builder.HasIndex(x => x.EventId);
        builder.HasIndex(x => x.ActionGroupId);

        // Cascade from datapoint — if raw data is removed, its resolution is removed too
        builder.HasOne(x => x.DataPoint)
            .WithMany(dp => dp.Links)
            .HasForeignKey(x => x.DataPointId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on event — service must clean up links before deleting an event
        builder.HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### `ILinkService` interface

```csharp
// Services/ILinkService.cs
namespace GoogleCalendarManagement.Services;

using GoogleCalendarManagement.Data.Entities;

public interface ILinkService
{
    // --- Manual operations (all return action_group_id for undo) ---
    Task<string> LinkAsync(int dataPointId, string eventId, CancellationToken ct = default);
    Task<string> LinkClumpAsync(IEnumerable<int> dataPointIds, string eventId, CancellationToken ct = default);
    Task<string> IgnoreAsync(int dataPointId, CancellationToken ct = default);
    Task<string> IgnoreClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);
    Task<string> UnlinkAsync(int dataPointId, CancellationToken ct = default);
    Task<string> UnlinkClumpAsync(IEnumerable<int> dataPointIds, CancellationToken ct = default);

    // Undo a previously returned action_group_id (no-op if unknown)
    Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);

    // --- Rule engine operations (NOT added to undo stack) ---
    Task WriteAutoLinkAsync(int dataPointId, string eventId, string ruleId, CancellationToken ct = default);
    Task WriteAutoIgnoreAsync(int dataPointId, string ruleId, CancellationToken ct = default);

    // --- Queries ---
    Task<Link?> GetLinkAsync(int dataPointId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByEventAsync(string eventId, CancellationToken ct = default);
    Task<IReadOnlyList<Link>> GetLinksByActionGroupAsync(string actionGroupId, CancellationToken ct = default);
}
```

### `LinkService` — key implementation patterns

**Undo stack shape** (in-memory, session-scoped):

```csharp
// Private nested record — not public API
private record LinkSnapshot(int DataPointId, Link? PreviousRow);

private readonly Dictionary<string, List<LinkSnapshot>> _undoStack = new();
```

`Link? PreviousRow` is `null` when the datapoint was unlinked before the operation. A non-null `PreviousRow` is a **deep copy** (not the tracked EF instance) — clone all scalar fields before the upsert.

**`UpsertLinkRow` helper pattern** (used by all write operations):

```csharp
private async Task<Link> UpsertLinkRow(
    CalendarDbContext context,
    int dataPointId, string? eventId, string state, string origin, string? ruleId, string actionGroupId,
    DateTime now)
{
    var existing = await context.Links
        .SingleOrDefaultAsync(l => l.DataPointId == dataPointId);

    if (existing is null)
    {
        var row = new Link
        {
            DataPointId = dataPointId, EventId = eventId,
            State = state, Origin = origin, RuleId = ruleId,
            ActionGroupId = actionGroupId, CreatedAt = now, UpdatedAt = now
        };
        context.Links.Add(row);
        return row;
    }
    else
    {
        existing.EventId = eventId;
        existing.State = state;
        existing.Origin = origin;
        existing.RuleId = ruleId;
        existing.ActionGroupId = actionGroupId;
        existing.UpdatedAt = now;
        return existing;
    }
}
```

**Undo restore helper:**

```csharp
private async Task RestoreSnapshot(CalendarDbContext context, LinkSnapshot snapshot, DateTime now)
{
    var existing = await context.Links
        .SingleOrDefaultAsync(l => l.DataPointId == snapshot.DataPointId);

    if (snapshot.PreviousRow is null)
    {
        // Was unlinked before — delete the current row if any
        if (existing is not null) context.Links.Remove(existing);
    }
    else
    {
        // Was linked/ignored before — upsert back to previous state
        await UpsertLinkRow(context,
            snapshot.DataPointId,
            snapshot.PreviousRow.EventId,
            snapshot.PreviousRow.State,
            snapshot.PreviousRow.Origin,
            snapshot.PreviousRow.RuleId,
            snapshot.PreviousRow.ActionGroupId,
            now);
    }
}
```

**Invariant guard for auto operations** (Task 3.3):

```csharp
private static void AssertNotManual(Link? existing, string operation)
{
    if (existing?.Origin == "manual")
        throw new InvalidOperationException(
            $"Cannot {operation} datapoint {existing.DataPointId}: it has a manual link. " +
            "Rule engine must not overwrite manual decisions.");
}
```

### Why ON DELETE RESTRICT for event FK

Cascade-delete on `event_id` would silently drop link rows when an event is deleted, making datapoints appear unlinked without any audit trail. By using RESTRICT, we force the caller to explicitly clean up links first. Story 8.14 (rule engine triggers) handles this on event deletion: auto-origin links are removed via `UnlinkClumpAsync` (which snapshots them for undo), and manual-origin links are preserved by rule-engine convention.

**Important:** The `ON DELETE RESTRICT` constraint means any code that deletes an `Event` row must first call `UnlinkClumpAsync` (or `WriteAutoLinkAsync` with a null-event reroute) for all linked datapoints, otherwise EF throws `DbUpdateException`. Story 8.14 owns this trigger.

### Test setup pattern (matches existing integration tests)

```csharp
private SqliteConnection _connection;
private DbContextOptions<CalendarDbContext> _options;

public LinkServiceTests()
{
    _connection = new SqliteConnection("Data Source=:memory:");
    _connection.Open();
    _options = new DbContextOptionsBuilder<CalendarDbContext>()
        .UseSqlite(_connection)
        .Options;
    using var ctx = new CalendarDbContext(_options);
    ctx.Database.EnsureCreated();
}

public void Dispose() => _connection.Dispose();

private CalendarDbContext CreateContext() => new(_options);
private ILinkService CreateService() =>
    new LinkService(new TestDbContextFactory(_options));
```

`TestDbContextFactory` is the private nested class from `GoogleCalendarSyncTests.cs` — either copy it or extract it to a shared `TestHelpers` class in the test project.

When seeding datapoints for tests, also seed the matching `Event` row if `state='linked'` (because of the FK). Use minimal valid `Event` seeds:

```csharp
// Minimal Event seed for FK satisfaction:
new Event { EventId = "evt-1", Lifecycle = "approved", Publish = "local_only",
            HasUnpublishedChanges = false, CreatedAt = now, UpdatedAt = now }
```

When seeding DataPoints, follow whatever minimal shape 8.7 established for `DataPoint` rows.

### Migration naming

Follow the existing timestamp convention:
- Format: `yyyyMMddHHmmss_AddLinkTable.cs`
- Example: `20260612000000_AddLinkTable.cs`

Confirm the generated SQL creates the unique index on `data_point_id` — EF SQLite sometimes silently skips `HasIndex(...).IsUnique()` on nullable columns; verify by inspecting the migration file.

### What this story does NOT do

- Does NOT implement the Linking Panel UI (Epic 9).
- Does NOT implement the rule engine pipeline or any concrete rules (Stories 8.14, 8.15).
- Does NOT implement the link-to-any-event picker (Story 8.13 — depends on this story).
- Does NOT handle the event-deletion trigger (Story 8.14 owns that).
- Does NOT project data sources into datapoints (Story 8.9).
- Does NOT compute coverage (Story 8.10 — depends on this story for link state).

### Project structure — files created/modified

- **Created:** `Data/Entities/Link.cs`
- **Created:** `Data/Configurations/LinkConfiguration.cs`
- **Created:** `Services/ILinkService.cs`
- **Created:** `Services/LinkService.cs`
- **Created:** `Data/Migrations/20260612000000_AddLinkTable.cs` (+ Designer)
- **Modified:** `Data/Entities/DataPoint.cs` — add `Links` navigation property
- **Modified:** `Data/CalendarDbContext.cs` — add `DbSet<Link> Links`
- **Modified:** `App.xaml.cs` — DI registration
- **Created:** `GoogleCalendarManagement.Tests/Integration/LinkServiceTests.cs`

All C# files: `namespace GoogleCalendarManagement.*` (matching the project convention seen in `SpotifyStream.cs`, `CalendarDbContext.cs`, etc.)

### Testing framework

xUnit + FluentAssertions + Moq — same as all other tests. In-memory SQLite with `EnsureCreated()`.

### References

- Canonical link model: [concepts.md §5](../concepts.md)
- Coverage semantics (why links matter): [concepts.md §6](../concepts.md)
- Rule engine invariants (why manual decisions are sacred): [concepts.md §7](../concepts.md)
- Epic overview story 8.12 spec: [epic-overview.md §Phase 2 Story 8.12](../epic-overview.md)
- Story 8.7 (data_point table — prereq): `docs/epic-8-data-linking/stories/8-7-data-point-registry-table-and-source-pointer.md` (to be created)
- Story 8.2 (event table — prereq): [8-2-unified-event-table-and-migration.md](./8-2-unified-event-table-and-migration.md)
- DI registration: `App.xaml.cs` (~lines 268–310)
- Existing integration test pattern: `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs`
- Existing entity pattern: `Data/Entities/SpotifyStream.cs`
- Existing configuration pattern: `Data/Configurations/GcalEventVersionConfiguration.cs`

---

## Dev Agent Record

### Agent Model Used

Opus 4.8

### Debug Log References

- All `LinkServiceTests` pass; full suite 524 passed / 19 skipped after review fixes.
- **Schema landing broke 3 `CoverageServiceTests`** (8.10): those tests created a provisional `link` table with `action_group_id TEXT NULL` and inserted rows omitting it. Now that `EnsureCreated()` builds the canonical table (`action_group_id` NOT NULL + UNIQUE index on `data_point_id`), the inserts failed. Fixed by supplying `action_group_id` in the inserts and repurposing `GetDateSourceCoverage_DuplicateLinks_CountsDistinctDataPoints` → `..._SingleLinkPerDatapoint_...` (two link rows for one datapoint is now structurally impossible under the unique index; the constraint itself is covered by `LinkServiceTests.UniqueConstraint_Enforced_AtDbLevel`).
- **Pre-existing, unrelated failures (NOT introduced by this story):** 3 `DataPointReconciliationSweepServiceTests` (`RunPostImportAsync_InsertsOrphanedDataPoints`, `RunPostImportAsync_IsIdempotent`, `RunStartupDriftCheckAsync_CallsProjectorForEveryRegisteredSource`) fail with `ArgumentNullException` because their projector mock does not stub `GetAllRawSourceRefsAsync`. Verified failing on a clean checkout of commit 72a907b (3 failed / 2 passed) before any 8.12 change. Belongs to the projector-contract work (8.8/8.9), left untouched to avoid scope creep.

### Completion Notes List

- Added the canonical `link` table via migration `20260616183154_AddLinkTable`: `link_id` PK; `data_point_id` FK → `data_point` **ON DELETE CASCADE**; nullable `event_id` FK → `event` **ON DELETE RESTRICT**; UNIQUE index on `data_point_id`; indexes on `event_id` and `action_group_id`. Generated SQL inspected and confirmed to match AC #1 (unique index correctly emitted on the non-nullable `data_point_id`).
- `LinkService` enforces the state/event_id invariant in code (not the DB): `linked ⇔ event_id set`, `ignored ⇔ event_id null`; violations throw `InvalidOperationException` before touching the DB (AC #2).
- All manual write ops (Link/Ignore/Unlink, singular + clump) write in one `SaveChangesAsync`, share one `action_group_id`, snapshot prior state into an in-memory undo stack, and return the group id. Singular ops are implemented as clumps of one to avoid duplication.
- `UndoActionGroupAsync` restores the snapshot in one transaction and is a no-op for unknown groups. Undo snapshots are deep copies, so they survive in-place EF mutation of the tracked row.
- Auto (rule-engine) writes (`WriteAutoLinkAsync`/`WriteAutoIgnoreAsync`) never overwrite a `manual` row (throw) and are NOT added to the undo stack (AC #12).
- Undo stack guarded by a lock for thread safety (service is a DI Singleton).
- `ILinkService` registered as Singleton in `App.xaml.cs` (AC #17).

### File List

- **Created:** `Data/Entities/Link.cs`
- **Created:** `Data/Configurations/LinkConfiguration.cs`
- **Created:** `Services/ILinkService.cs`
- **Created:** `Services/LinkService.cs`
- **Created:** `Data/Migrations/20260616183154_AddLinkTable.cs`
- **Created:** `Data/Migrations/20260616183154_AddLinkTable.Designer.cs`
- **Created:** `GoogleCalendarManagement.Tests/Integration/LinkServiceTests.cs`
- **Modified:** `Data/Entities/DataPoint.cs` — added `Links` navigation property
- **Modified:** `Data/CalendarDbContext.cs` — added `DbSet<Link> Links`
- **Modified:** `Data/Migrations/CalendarDbContextModelSnapshot.cs` — regenerated by `dotnet ef`
- **Modified:** `App.xaml.cs` — DI registration of `ILinkService`
- **Modified:** `GoogleCalendarManagement.Tests/Integration/CoverageServiceTests.cs` — updated link inserts for the canonical schema (see Debug Log)

### Change Log

| Date       | Change                                                                 |
|------------|------------------------------------------------------------------------|
| 2026-06-16 | Implemented Story 8.12: `link` table + `LinkService` (link/ignore/unlink, clump, undo, auto-write guards) + 15 integration tests; DI registration; migration `AddLinkTable`. Updated `CoverageServiceTests` link inserts to the canonical schema. |
| 2026-06-16 | Addressed code review findings: superseded undo groups, clump input normalization, schema value checks, auto-rule `rule_id` validation, and expanded integration coverage. |

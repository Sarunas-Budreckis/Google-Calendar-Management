# Story 8.14: Rule Engine Pipeline (Propose-Ops, Invariants, Triggers, Reversal)

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Opus · **Effort:** high
**Dependencies:** 8.12 (blocking — `link` table + `ILinkOperationService` must exist); 8.13 (blocking — `ILinkPickerService` must exist)

---

## Story

As the data-linking engine,
I want an ordered, atomic rule pipeline that proposes `link`, `ignore`, and `generate-candidate` operations over unresolved datapoints,
so that automation is deterministic, idempotent, manually-sacred, fully auditable via `rule_id`, and correctly reverses only its own auto-set state when events are moved, deleted, or un-approved.

---

## Acceptance Criteria

1. `ILinkRule` interface exists in `Services/Rules/` with:
   - `string RuleId { get; }` — stable identifier (e.g. `"spotify_auto_link"`); used in `link.rule_id` column
   - `Task<IReadOnlyList<RuleProposedOp>> ProposeOpsAsync(RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, CancellationToken ct)` — receives only the eligible datapoints for this scope; must NOT write to the DB directly
2. `RuleProposedOp` value type exists in `Services/Rules/` with:
   - `ProposedOpKind Kind` — `Link | Ignore | GenerateCandidate`
   - `string DataPointId`
   - `string? EventId` — `null` for `Ignore` with no event; `null` for `GenerateCandidate` (engine creates the event)
   - `string RuleId` — which rule produced this op (set by the rule itself)
   - `string? GeneratedEventSummary` — only for `GenerateCandidate` ops; the proposed event title
   - `DateTime? GeneratedEventStart`, `DateTime? GeneratedEventEnd` — only for `GenerateCandidate`; UTC times
3. `RuleScope` record exists in `Services/Rules/` with `DateOnly FromDate`, `DateOnly ToDate`, and optional `string? SourceKeyFilter`.
4. `EligibleDataPoint` record exists in `Services/Rules/` with `string DataPointId`, `string SourceKey`, `DateTime StartUtc`, `DateTime EndUtc`. Eligible = unlinked (no `link` row) OR has a `link` row with `origin = 'auto_rule'`. Manually-linked/ignored datapoints (`origin = 'manual'`) are NEVER passed to any rule.
5. `IRuleEngineService` interface exists in `Services/` with:
   - `Task RunPipelineAsync(RuleScope scope, CancellationToken ct = default)` — runs all registered rules over eligible datapoints in scope; applies ops atomically
   - `Task ReverseAndRerunAsync(string eventId, CancellationToken ct = default)` — removes all `origin='auto_rule'` link rows for datapoints overlapping the event's time range, then re-runs `RunPipelineAsync` for that range
6. `RuleEngineService : IRuleEngineService` is implemented in `Services/RuleEngineService.cs`. The pipeline:
   a. Queries `data_point` rows whose `(start_utc, end_utc)` overlaps `scope` and whose `source_key` matches `SourceKeyFilter` if set.
   b. For each datapoint: loads its `link` row (if any). Eligible = no link row OR `link.origin = 'auto_rule'`. Skips `origin = 'manual'`.
   c. Runs each registered `ILinkRule` in deterministic order (registration order). Each rule receives the full eligible list for the scope.
   d. Aggregates all proposed ops. If the same `DataPointId` appears in multiple rules' proposals, **first rule wins** (later rules' ops for that datapoint are discarded).
   e. Applies all ops atomically in a single `SaveChangesAsync` transaction:
      - `Link` op: upsert `link` row with `state='linked'`, `origin='auto_rule'`, `rule_id=op.RuleId`, `action_group_id` (shared per pipeline run, see AC #9)
      - `Ignore` op: upsert `link` row with `state='ignored'`, `origin='auto_rule'`, `rule_id=op.RuleId`, `event_id=null`
      - `GenerateCandidate` op: create new `Event` with `lifecycle='candidate'`, `publish='local_only'`, `source_system='auto_rule'`, `summary=op.GeneratedEventSummary`, UTC times from op; then upsert `link` row with `state='linked'`, `origin='auto_rule'`, `rule_id=op.RuleId`, `event_id=<new event_id>`; send `EventUpdatedMessage` for the new candidate
   f. Existing `origin='auto_rule'` link rows for datapoints that no rule proposed an op for are **left untouched** (do not auto-unlink stale rows — reversal handles cleanup).
7. `ReverseAndRerunAsync` implementation:
   a. Load the `Event` row for `eventId` (if not found, skip re-run but still perform cleanup).
   b. Compute the UTC time range from the event's `start_datetime`/`end_datetime`.
   c. Find all `data_point` rows overlapping that time range. For each: if `link.origin = 'auto_rule'`, delete the `link` row (in a single transaction). `origin = 'manual'` rows are **never deleted**.
   d. After cleanup, call `RunPipelineAsync(new RuleScope(FromDate, ToDate))` for the date range covering the event's old time extent.
   e. If the event is being moved (time change): the caller supplies both old and new ranges; `ReverseAndRerunAsync` is called with the old event's id (before update), then `RunPipelineAsync` is called for the new range after the event update is saved.
8. Re-running `RunPipelineAsync` on the same scope with no data changes produces **no net changes** to the `link` table (idempotent). Specifically: if all eligible datapoints already have the correct auto_rule link, the upsert is a no-op (update with identical values).
9. All `link` rows written in a single `RunPipelineAsync` call share one `action_group_id` (`Guid.NewGuid().ToString("N")` per call). This enables atomic undo of an entire pipeline run via `ILinkOperationService.UndoActionGroupAsync`.
10. `IRuleTriggerService` interface and `RuleTriggerService` implementation exist in `Services/`. Wires four trigger points:
    - **Post-import**: subscribes to `DataImportCompletedMessage`; calls `RunPipelineAsync(scope covering the imported date range)`
    - **Event approve**: subscribes to `EventLifecycleChangedMessage` where `NewLifecycle = 'approved'`; calls `RunPipelineAsync(scope covering event's time range)`
    - **Event time-change edit**: subscribes to `EventTimeChangedMessage`; calls `ReverseAndRerunAsync(eventId)` for old range, then `RunPipelineAsync` for new range
    - **Event delete / un-approve**: subscribes to `EventDeletedMessage` and `EventLifecycleChangedMessage` where `NewLifecycle = 'candidate'`; calls `ReverseAndRerunAsync(eventId)`
11. Messages defined (if not already existing): `DataImportCompletedMessage(DateOnly FromDate, DateOnly ToDate, string SourceKey)`, `EventLifecycleChangedMessage(string EventId, string OldLifecycle, string NewLifecycle)`, `EventTimeChangedMessage(string EventId, DateTime OldStartUtc, DateTime OldEndUtc, DateTime NewStartUtc, DateTime NewEndUtc)`, `EventDeletedMessage(string EventId, DateTime StartUtc, DateTime EndUtc)`. All via `WeakReferenceMessenger`.
12. `IRuleEngineService` and `IRuleTriggerService` are registered in `App.xaml.cs` as `AddSingleton`. Rules are registered as `IEnumerable<ILinkRule>` (registered individually as `AddSingleton<ILinkRule, ConcreteRule>()`). `RuleEngineService` receives `IEnumerable<ILinkRule>` via constructor injection.
13. Integration tests in `GoogleCalendarManagement.Tests/Integration/RuleEnginePipelineTests.cs` cover the full integrity matrix (see Dev Notes §Test matrix). Tests use in-memory SQLite with `IDbContextFactory` pattern; seed `data_point` + `link` + `event` rows as needed.

---

## Tasks / Subtasks

> **Implementation note (deviations from original spec — see Completion Notes for full rationale):**
> The story was drafted before 8.12/8.13 merged. Two adaptations were made: (a) the real schema uses
> `int DataPointId` (not `string`), and the merged link service is `ILinkService` (not
> `ILinkOperationService`) — all engine types use `int` and route writes through `ILinkService`;
> (b) per user decision, triggers are **direct methods** on `IRuleEngineService` (`RunForImportAsync`,
> `RunForEventApproveAsync`, `RunForEventEditTimeAsync`, `RunForEventDeleteAsync`) — matching the
> downstream consumer Story 8.15 — instead of a message-subscription `IRuleTriggerService`. Tasks 5–7
> are checked against that substituted design.

- [x] Task 1: Define core types (AC: #1–#4) — `DataPointId` is `int` to match the merged schema
  - [x] 1.1 Create `Services/Rules/ProposedOpKind.cs` — `enum ProposedOpKind { Link, Ignore, GenerateCandidate }`
  - [x] 1.2 Create `Services/Rules/RuleProposedOp.cs` — record with `Kind`, `int DataPointId`, `EventId?`, `RuleId`, `GeneratedEventSummary?`, `GeneratedEventStart?`, `GeneratedEventEnd?` (+ optional `GeneratedEventSourceSystem?` so 8.15 rules can stamp their own source_system)
  - [x] 1.3 Create `Services/Rules/RuleScope.cs` — record with `DateOnly FromDate`, `DateOnly ToDate`, `string? SourceKeyFilter`
  - [x] 1.4 Create `Services/Rules/EligibleDataPoint.cs` — record with `int DataPointId`, `string SourceKey`, `DateTime StartUtc`, `DateTime EndUtc`
  - [x] 1.5 Create `Services/Rules/ILinkRule.cs` — interface with `RuleId` + `ProposeOpsAsync` signature from AC #1

- [x] Task 2: Define `IRuleEngineService` interface (AC: #5)
  - [x] 2.1 Create `Services/IRuleEngineService.cs` — `RunPipelineAsync` + `ReverseAndRerunAsync` (+ `ReverseRangeAndRerunAsync` overload + direct `RunFor*` triggers)

- [x] Task 3: Implement `RuleEngineService` — pipeline core (AC: #6, #8, #9)
  - [x] 3.1 Create `Services/RuleEngineService.cs` — constructor takes `IDbContextFactory<CalendarDbContext>`, `IEventRepository`, `IEventIdentityService`, `ILinkService`, `IEnumerable<ILinkRule>`, `ILogger<RuleEngineService>`
  - [x] 3.2 Implement `BuildEligibleListAsync(RuleScope scope, CancellationToken ct)` — queries `data_point` with a correlated `link` lookup to find unlinked or `origin='auto_rule'` datapoints in scope; returns `IReadOnlyList<EligibleDataPoint>`
  - [x] 3.3 Implement `AggregateProposals(...)` — first-rule-wins deduplication by `DataPointId`
  - [x] 3.4 Implement `ApplyOpsAsync(...)` — builds the batch of writes (minting events for `GenerateCandidate` ops first, sending `EventUpdatedMessage`), then applies all link/ignore writes atomically via `ILinkService.WriteAutoBatchAsync` (single transaction, shared `action_group_id`). Engine never writes the `link` table directly.
  - [x] 3.5 Implement `RunPipelineAsync`: `BuildEligibleListAsync` → run each `ILinkRule.ProposeOpsAsync` sequentially → aggregate → apply; logs op count at `Information`
  - [x] 3.6 Idempotency: `WriteAutoBatchAsync` skips a write whose existing row already has matching `state`/`event_id`/`origin='auto_rule'`/`rule_id` (no EF change, no undo snapshot) — re-runs produce no net change

- [x] Task 4: Implement `ReverseAndRerunAsync` (AC: #7)
  - [x] 4.1 Load the event's time range from `IEventRepository.GetByEventIdAsync`; if null, log warning and return
  - [x] 4.2 Convert `event.StartDatetime`/`EndDatetime` to `DateOnly` boundaries for the scope
  - [x] 4.3 Delete all `origin='auto_rule'` link rows for datapoints overlapping the event's UTC range, in one transaction (`ILinkService.DeleteAutoLinksForDataPointsAsync`)
  - [x] 4.4 Send `EventUpdatedMessage` for orphaned candidate events whose only link row was deleted (engine deletes the now-unreferenced candidate)
  - [x] 4.5 Call `RunPipelineAsync(scope)` for the time range (via `ReverseRangeAndRerunAsync`)

- [x] Task 5: Trigger entry points (AC: #11) — **adapted: direct methods, no new message types**
  - [x] 5.1 `RunForImportAsync(sourceKey)` replaces a `DataImportCompletedMessage` subscription (existing `DataSourceImportCompletedMessage` carries no date range; engine derives scope from the source's datapoints)
  - [x] 5.2 `RunForEventApproveAsync(eventId)` replaces an `EventLifecycleChangedMessage` subscription
  - [x] 5.3 `RunForEventEditTimeAsync(eventId, oldStartUtc, oldEndUtc)` replaces an `EventTimeChangedMessage` subscription (old range passed by caller)
  - [x] 5.4 `RunForEventDeleteAsync(eventId, startUtc, endUtc)` replaces an `EventDeletedMessage` subscription (range passed by caller)
  - [x] 5.5 No new message types created; existing `DataSourceImportCompletedMessage` senders left untouched. Call-site wiring (import services, event edit/delete/approve paths) is owned by the consuming stories (8.15 wires imports; Epic 9 wires lifecycle paths)

- [x] Task 6: Trigger surface (AC: #10) — **adapted: `RunFor*` methods on `IRuleEngineService`, no separate `IRuleTriggerService`**
  - [x] 6.1 Direct methods need no `Initialize()`/arming step
  - [x] 6.2 Engine owns the trigger logic directly (no separate service/ctor)
  - [x] 6.3 Four trigger entry points implemented (see Task 5.1–5.4)
  - [x] 6.4 Post-import: `RunForImportAsync(sourceKey)` → pipeline over that source's datapoint range
  - [x] 6.5 Event-approve: `RunForEventApproveAsync(eventId)` → pipeline over the event's range
  - [x] 6.6 Event-time-change: `RunForEventEditTimeAsync(...)` → reverse old range + reverse/re-run new range
  - [x] 6.7 Event-delete/un-approve: `RunForEventDeleteAsync(...)` → reverse over the supplied range

- [x] Task 7: DI registration (AC: #12)
  - [x] 7.1 In `App.xaml.cs`: added `services.AddSingleton<IRuleEngineService, RuleEngineService>()`
  - [x] 7.2 No `IRuleTriggerService` (superseded by direct methods)
  - [x] 7.3 Placeholder comment added — `// ILinkRule implementations registered by Story 8.15+`; engine receives `IEnumerable<ILinkRule>`
  - [x] 7.4 No arming step needed (direct methods); nothing to resolve at startup

- [x] Task 8: Integration tests — integrity matrix (AC: #13)
  - [x] 8.1 Create `GoogleCalendarManagement.Tests/Integration/RuleEnginePipelineTests.cs`
  - [x] 8.2 Test helpers: `StubRule(ruleId, logic)` + `OverlapLinkRule` (event-aware) stand in for `CreateTestRule`
  - [x] 8.3 Test: **Idempotency** — run twice; same row count; unchanged row's `action_group_id`/`updated_at` stable
  - [x] 8.4 Test: **Manual sacred** — manual link present; rule proposes a different link; manual link unchanged
  - [x] 8.5 Test: **Auto link applied** — unlinked datapoint; `origin='auto_rule'`, `rule_id` set, `action_group_id` non-null
  - [x] 8.6 Test: **First-rule-wins** — two rules propose for same datapoint; only first rule's `rule_id` wins
  - [x] 8.7 Test: **Auto link cleaned by reversal** — auto link + empty re-run rule → old auto link deleted
  - [x] 8.8 Test: **Manual survives reversal** — manual link on overlapping datapoint → intact after reversal
  - [x] 8.9 Test: **Event delete triggers reversal** — `RunForEventDeleteAsync` removes auto link, leaves manual link
  - [x] 8.10 Test: **Generate-candidate op** — new `Event(lifecycle='candidate')` created; link connects datapoint with `origin='auto_rule'`
  - [x] 8.11 Test: **Converges after move** — auto-linked datapoint; move event; `RunForEventEditTimeAsync` removes stale auto link
  - [x] 8.12 Test: **Action group** — 3 link ops in one run share one `action_group_id`

### Review Findings

- [x] [Review][Patch] GenerateCandidate is not idempotent on repeated pipeline runs [Services/RuleEngineService.cs:238]
- [x] [Review][Patch] GenerateCandidate event creation is not atomic with link writes [Services/RuleEngineService.cs:238]
- [x] [Review][Patch] Time-change trigger reverses the new event range instead of only running the pipeline [Services/RuleEngineService.cs:150]
- [x] [Review][Patch] Orphan cleanup can delete candidate events not generated by the rule engine [Services/RuleEngineService.cs:295]
- [x] [Review][Patch] GenerateCandidate ops can create candidate events without required fields [Services/RuleEngineService.cs:256]
- [x] [Review][Patch] ReverseAndRerunAsync uses DateTime.UtcNow when event times are missing [Services/RuleEngineService.cs:79]
- [x] [Review][Patch] Eligible datapoint ordering is nondeterministic [Services/RuleEngineService.cs:178]
- [x] [Review][Patch] RuleProposedOp is a reference record despite AC #2 requiring a value type [Services/Rules/RuleProposedOp.cs:18]
- [x] [Review][Patch] Batch undo behavior is claimed but not verified by integration tests [GoogleCalendarManagement.Tests/Integration/RuleEnginePipelineTests.cs:209]

---

## Dev Notes

### Hard prerequisites: what 8.12 and 8.13 must provide

This story cannot start until 8.12 and 8.13 are merged. Expected contracts:

**From 8.12 — `ILinkOperationService`:**
```csharp
// Services/ILinkOperationService.cs
public interface ILinkOperationService
{
    // Link a datapoint to an event (manual or rule-driven)
    Task LinkAsync(string dataPointId, string eventId, string origin, string? ruleId,
                   string actionGroupId, CancellationToken ct = default);

    // Mark datapoint as intentionally ignored (no event)
    Task IgnoreAsync(string dataPointId, string origin, string? ruleId,
                     string actionGroupId, CancellationToken ct = default);

    // Remove the link row entirely (returns to unlinked pool)
    Task UnlinkAsync(string dataPointId, CancellationToken ct = default);

    // Undo all link/ignore operations in the given group (atomic)
    Task UndoActionGroupAsync(string actionGroupId, CancellationToken ct = default);
}
```

`RuleEngineService` calls `ILinkOperationService` in `ApplyOpsAsync` rather than writing to the `link` table directly. This ensures undo semantics are owned in one place.

**From 8.12 — `link` table entity:**
```csharp
// Data/Entities/Link.cs
public class Link
{
    public long LinkId { get; set; }                // PK, auto-increment
    public string DataPointId { get; set; }         // FK → data_point (ON DELETE CASCADE)
    public string? EventId { get; set; }            // FK → event, nullable (null = ignored)
    public string State { get; set; }               // 'linked' | 'ignored'
    public string Origin { get; set; }              // 'manual' | 'auto_rule'
    public string? RuleId { get; set; }             // nullable — rule that set this
    public string ActionGroupId { get; set; }       // groups a multi-op action for undo
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**From 8.13 — `ILinkPickerService`** (not directly used by the engine, but rules may use it for "find concurrent events"):
```csharp
// Services/ILinkPickerService.cs
public interface ILinkPickerService
{
    // Returns events sorted: concurrent first, then all others.
    // Used by rules to find candidate target events for a datapoint.
    Task<IReadOnlyList<Event>> GetCandidateEventsAsync(
        string dataPointId, CancellationToken ct = default);
}
```

The Spotify rule (Story 8.15) will use `ILinkPickerService.GetCandidateEventsAsync` to find approved events that overlap a stream. The engine itself does not use it — rules use it internally.

### `DataPoint` entity (from 8.7)

```csharp
// Data/Entities/DataPoint.cs
public class DataPoint
{
    public string DataPointId { get; set; }   // PK
    public string SourceKey { get; set; }     // e.g. 'spotify_stream', 'toggl_entry'
    public string SourceRef { get; set; }     // pointer to raw record
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }      // equals StartUtc for instants
    public DateTime CreatedAt { get; set; }
}
```

Indexes expected (from 8.7): `(start_utc)`, `(source_key, start_utc)`. The engine's `BuildEligibleListAsync` query uses these indexes via:
```sql
WHERE dp.start_utc < @scopeEndUtc AND dp.end_utc >= @scopeStartUtc
```

### Engine algorithm (pseudocode)

```
RunPipelineAsync(scope):
  1. eligibleList ← BuildEligibleListAsync(scope)
     - JOIN data_point LEFT JOIN link ON link.data_point_id = data_point.data_point_id
     - WHERE overlap(scope) AND (link IS NULL OR link.origin = 'auto_rule')
     - Filter by scope.SourceKeyFilter if set
  
  2. actionGroupId ← Guid.NewGuid().ToString("N")
  
  3. allProposals ← []
     FOR each rule in _rules (ordered):
       proposals ← await rule.ProposeOpsAsync(scope, eligibleList, ct)
       allProposals.Add((rule, proposals))
  
  4. finalOps ← AggregateProposals(allProposals)
     // First-rule-wins: track seen DataPointIds; skip later proposals for same id
  
  5. ApplyOpsAsync(finalOps, actionGroupId, ct)
     // Single transaction — calls ILinkOperationService for each op
  
  6. Log: "Rule pipeline applied {N} ops for scope {FromDate}–{ToDate}, group {actionGroupId}"
```

### Reversal algorithm (pseudocode)

```
ReverseAndRerunAsync(eventId):
  1. event ← IEventRepository.GetByEventIdAsync(eventId)
     If null: log warning "Event {eventId} not found for reversal"; proceed with cleanup only
  
  2. scopeStart ← DateOnly.FromDateTime(event.StartDatetime ?? DateTime.UtcNow)
     scopeEnd   ← DateOnly.FromDateTime(event.EndDatetime ?? event.StartDatetime ?? DateTime.UtcNow)
  
  3. In a single transaction:
     DELETE FROM link
     WHERE origin = 'auto_rule'
       AND data_point_id IN (
         SELECT data_point_id FROM data_point
         WHERE start_utc < eventEndUtc AND end_utc >= eventStartUtc
       )
  
  4. Collect deleted link rows' event_ids where state='linked' and that event had
     lifecycle='candidate'. If those candidate events have no remaining link rows,
     delete the candidate event and send EventUpdatedMessage(candidateEventId).
     (Prevents orphaned auto-generated candidates from cluttering the calendar.)
  
  5. await RunPipelineAsync(new RuleScope(scopeStart, scopeEnd))
```

### Trigger wiring: event time-change detail

The time-change trigger is the trickiest case because the old range needs reversal before the event is updated to the new time. The pattern:

```csharp
// In the service/VM that applies a time change to an event:
// 1. Send EventTimeChangedMessage BEFORE saving the new time to DB:
WeakReferenceMessenger.Default.Send(new EventTimeChangedMessage(
    EventId: event.EventId,
    OldStartUtc: event.StartDatetime ?? ...,
    OldEndUtc: event.EndDatetime ?? ...,
    NewStartUtc: newStart,
    NewEndUtc: newEnd));

// 2. Save the updated event to DB (EventRepository.UpsertAsync)
// 3. RuleTriggerService handles the message:
//    - ReverseAndRerunAsync uses OldStart/OldEnd from message (not from DB — event is already updated)
//    - Then RunPipelineAsync for the new date range
```

`ReverseAndRerunAsync` receives the old time range from the message payload (`OldStartUtc`, `OldEndUtc`) rather than loading from DB (the DB already has the new time by the time the handler runs). Add an overload to handle this:

```csharp
// RuleEngineService — additional overload for the time-change case:
Task ReverseRangeAndRerunAsync(DateTime oldStartUtc, DateTime oldEndUtc, CancellationToken ct = default);
```

This overload skips the "load event" step and goes straight to the DELETE + re-run using the provided UTC bounds. Add this overload to `IRuleEngineService`.

### `ILinkOperationService` call vs direct DB write

Do NOT write to the `link` table directly in `RuleEngineService`. Always go through `ILinkOperationService`. This ensures:
- Undo (`UndoActionGroupAsync`) works correctly for rule-generated links
- The write path is in one place
- Tests can verify `ILinkOperationService` contract independently

The `ApplyOpsAsync` helper calls `ILinkOperationService.LinkAsync` / `IgnoreAsync` per op, all sharing the same `actionGroupId`. Since all calls share one group, a single `UndoActionGroupAsync(actionGroupId)` can roll back an entire pipeline run.

### `GenerateCandidate` op — event creation + linking

When a rule proposes `GenerateCandidate`:
1. `RuleEngineService.ApplyOpsAsync` mints a new `event_id` via `IEventIdentityService.MintEventId()`
2. Creates `Event` entity: `lifecycle='candidate'`, `publish='local_only'`, `source_system='auto_rule'`, `summary=op.GeneratedEventSummary`, `start_datetime=op.GeneratedEventStart`, `end_datetime=op.GeneratedEventEnd`, `calendar_id='primary'`
3. Saves the `Event` via `IEventRepository.UpsertAsync`
4. Calls `ILinkOperationService.LinkAsync(op.DataPointId, newEventId, "auto_rule", op.RuleId, actionGroupId)`
5. Sends `WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(newEventId))`

When the generated candidate is rejected (user deletes it), `EventDeletedMessage` fires → `ReverseAndRerunAsync` → the `link` row pointing to the deleted event is removed (the cascaded delete from `link.event_id → event` will handle it if ON DELETE CASCADE is set, or the reversal deletion handles it explicitly).

### Idempotency: upsert detail

`ILinkOperationService.LinkAsync` must support upsert semantics (insert or update). When the rule proposes an op that matches an existing `auto_rule` link (same `data_point_id`, `event_id`, `state`), the upsert should be a no-op. The `action_group_id` will differ between runs — that is acceptable. The integrity guarantee is:
- Same inputs → same proposed ops → same resulting `link` rows
- The `action_group_id` changes per run (each run is a distinct undoable action)

### Message types: check before creating

Before creating any message type in Task 5, grep for existing definitions:
```
grep -rn "DataImportCompletedMessage\|EventLifecycleChangedMessage\|EventTimeChangedMessage\|EventDeletedMessage" Messages/ Services/ ViewModels/
```
If any already exist with different signatures, adapt to the existing signature rather than creating duplicates. Importing services likely already send some form of completion message.

### `CalendarDbContext` — link and data_point DbSets

From 8.7 and 8.12, `CalendarDbContext` should expose:
```csharp
public DbSet<DataPoint> DataPoints { get; set; }
public DbSet<Link> Links { get; set; }
```
Verify these exist before writing queries. If they are not yet on the context (because 8.7 / 8.12 are not merged), add `// PREREQ 8.7/8.12: verify DbSets exist` comments in the engine code.

### Testing framework

xUnit + FluentAssertions. Integration tests use in-memory SQLite:
```csharp
_connection = new SqliteConnection("Data Source=:memory:");
_connection.Open();
var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
using var context = new CalendarDbContext(options);
context.Database.EnsureCreated();
_contextFactory = new TestDbContextFactory(options);
```

Test stub rule helper pattern:
```csharp
private static ILinkRule CreateStubRule(string ruleId,
    Func<IReadOnlyList<EligibleDataPoint>, IReadOnlyList<RuleProposedOp>> logic)
{
    var mock = new Mock<ILinkRule>();
    mock.Setup(r => r.RuleId).Returns(ruleId);
    mock.Setup(r => r.ProposeOpsAsync(It.IsAny<RuleScope>(), It.IsAny<IReadOnlyList<EligibleDataPoint>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((RuleScope _, IReadOnlyList<EligibleDataPoint> pts, CancellationToken _) =>
            logic(pts));
    return mock.Object;
}
```

### What this story does NOT do

- Does NOT implement any concrete `ILinkRule` implementations — that is Story 8.15 (Spotify auto-link, Outlook generate-candidate)
- Does NOT implement the `ILinkOperationService` undo UI or undo stack — that is handled by link operation service from 8.12
- Does NOT implement coverage computation — Story 8.10
- Does NOT implement the Linking panel UI — Epic 9
- Does NOT change the `CalendarEventSourceKind` enum or rendering — Story 8.5 (already done)
- Does NOT add `LinkOrder` (source ranking) — that is a non-operational concept used only for Linking panel ordering, not by the engine

### Project structure notes

New files:
- `Services/Rules/ILinkRule.cs`
- `Services/Rules/RuleScope.cs`
- `Services/Rules/RuleProposedOp.cs`
- `Services/Rules/ProposedOpKind.cs`
- `Services/Rules/EligibleDataPoint.cs`
- `Services/IRuleEngineService.cs`
- `Services/RuleEngineService.cs`
- `Services/IRuleTriggerService.cs`
- `Services/RuleTriggerService.cs`
- `Messages/DataImportCompletedMessage.cs` (if not already existing)
- `Messages/EventLifecycleChangedMessage.cs` (if not already existing)
- `Messages/EventTimeChangedMessage.cs` (if not already existing)
- `Messages/EventDeletedMessage.cs` (if not already existing)
- `GoogleCalendarManagement.Tests/Integration/RuleEnginePipelineTests.cs`

Modified files:
- `App.xaml.cs` — DI registration + `IRuleTriggerService.Initialize()` call at startup

All C# files: `namespace GoogleCalendarManagement.Services;` (or `.Services.Rules` for the types folder, or `.Tests.Integration` for tests). Match the namespace of neighboring files exactly.

### References

- Canonical rule semantics: [concepts.md §7 Rules](../concepts.md) — manual-sacred, deterministic, idempotent, reversal, auditable, atomic invariants
- Link table schema: [concepts.md §5 Links](../concepts.md)
- Data point registry: [concepts.md §4 Datapoint registry](../concepts.md)
- Epic overview story 8.14 spec: [epic-overview.md §Phase 2 Story 8.14](../epic-overview.md)
- Story 8.12 (link table + operations): [8-12-link-table-and-link-ignore-unlink-operations.md](./8-12-link-table-and-link-ignore-unlink-operations.md) (not yet created — verify contracts from its epic-overview spec)
- Story 8.13 (link picker): [8-13-link-to-any-event-picker.md](./8-13-link-to-any-event-picker.md) (not yet created — verify contracts from its epic-overview spec)
- Story 8.15 (first rules): will register its `ILinkRule` implementations against this engine
- `IEventRepository` / `IEventIdentityService`: `Services/IEventRepository.cs`, `Services/IEventIdentityService.cs` (from 8.3)
- Messaging pattern: `WeakReferenceMessenger.Default.Send` — `CommunityToolkit.Mvvm.Messaging`
- `IDbContextFactory` pattern: `Services/GcalEventRepository.cs` (from 8.3) or `Services/EventRepository.cs`
- DI registration: `App.xaml.cs` (~lines 268–310)
- Testing pattern: `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs`
- Testing pattern: `GoogleCalendarManagement.Tests/Integration/GoogleCalendarSyncTests.cs`

---

## Change Log

| Date       | Change                                                                                  |
|------------|-----------------------------------------------------------------------------------------|
| 2026-06-16 | Implemented rule engine pipeline (core types, engine, reversal, direct triggers) + 10 integrity tests. Adapted to merged `int`/`ILinkService` schema; triggers via direct `RunFor*` methods per user decision; pipeline runs are undoable via shared `action_group_id`. |

## Dev Agent Record

### Agent Model Used

Opus 4.8

### Debug Log References

- `dotnet build GoogleCalendarManagement.csproj` — succeeded, 0 errors.
- `dotnet test --filter RuleEnginePipelineTests` — 11/11 passed.
- `dotnet test` (full suite) — 550 passed, 0 failed, 19 skipped (pre-parked legacy tests).

### Completion Notes List

**Two approved deviations from the original spec** (the story was drafted before its dependencies 8.12/8.13 merged; both were confirmed with the user before building):

1. **Schema/contract adaptation.** The merged schema uses `int DataPointId` (not `string`), and the
   link operation service is `ILinkService` (not the hypothetical `ILinkOperationService`). All engine
   types use `int`, and the engine routes every `link`-table write through `ILinkService` (never direct).
   `ILinkPickerService`/`IEventPickerService` is not used by the engine (rules use it in 8.15).

2. **Triggers are direct methods, not message subscriptions** (user decision). `IRuleEngineService`
   exposes `RunForImportAsync(sourceKey)`, `RunForEventApproveAsync(eventId)`,
   `RunForEventEditTimeAsync(eventId, oldStartUtc, oldEndUtc)`, and
   `RunForEventDeleteAsync(eventId, startUtc, endUtc)` — matching the downstream consumer Story 8.15,
   which explicitly expects this API and wires the import calls itself. No `IRuleTriggerService` and no
   new message types were created. The existing `DataSourceImportCompletedMessage` (which carries no
   date range) is left untouched; `RunForImportAsync` derives the scope from the source's own datapoints.
   Pipeline order is date-range invariant (registration order), independent of import order.

**Undo semantics (AC #9, user decision: pipeline runs are undoable).** Added
`ILinkService.WriteAutoBatchAsync(writes)` which applies a whole run's auto writes in one transaction
under a single shared `action_group_id` and registers them on the undo stack — so an entire pipeline
run is reversible via `UndoActionGroupAsync`. This intentionally extends 8.12's original "auto writes
are not undoable" stance. `WriteAutoBatchAsync` is also where **idempotency** lives: a write that
exactly matches an existing `auto_rule` row (state/event_id/rule_id) is skipped — no EF change, no undo
snapshot — so re-running a scope on unchanged data produces zero net change. Manual rows are never
overwritten (defensively skipped, since the eligible set already excludes them).

**Reversal.** Added `ILinkService.DeleteAutoLinksForDataPointsAsync(ids)` (deletes only `auto_rule`
rows, returns snapshots). `ReverseRangeAndRerunAsync` deletes overlapping auto links, then cleans up
orphaned auto-generated **candidate** events (lifecycle=`candidate` with no remaining link rows are
removed and an `EventUpdatedMessage` is sent), then re-runs the pipeline for the range. The `link→event`
FK is `OnDelete(Restrict)`, so deleting the auto link first is required before the candidate event can
be removed — the order is handled correctly.

**GenerateCandidate.** The engine mints the event id (`IEventIdentityService`), creates an
`Event(lifecycle='candidate', publish='local_only', source_system='auto_rule', calendar_id='primary')`
via `IEventRepository.UpsertAsync`, then links the datapoint to it and sends `EventUpdatedMessage`. A
forward-compatible optional `RuleProposedOp.GeneratedEventSourceSystem` lets 8.15's Outlook rule stamp
`source_system='outlook'` instead of the default `auto_rule`.

**Not wired in this story (by design).** Call-site wiring of the trigger methods into import services
and the event edit/delete/approve UI paths is deferred to the consuming stories: Story 8.15 wires the
import triggers (Spotify/Outlook), and Epic 9 / event-edit flows wire the reversal triggers. With zero
concrete `ILinkRule` implementations registered until 8.15, every trigger is currently a safe no-op, so
wiring the lifecycle call-sites now would add regression risk with no functional benefit.

**Tests.** 11 integration tests (in-memory SQLite, real `EventRepository`/`EventIdentityService`/
`LinkService`) cover the full integrity matrix: idempotency, manual-sacred, auto-link application,
first-rule-wins, reversal cleanup, manual-survives-reversal, event-delete reversal, generate-candidate,
convergence-after-move (event-aware `OverlapLinkRule`), and shared action-group grouping.

### File List

**New:**
- `Services/Rules/ProposedOpKind.cs`
- `Services/Rules/RuleProposedOp.cs`
- `Services/Rules/RuleScope.cs`
- `Services/Rules/EligibleDataPoint.cs`
- `Services/Rules/ILinkRule.cs`
- `Services/AutoLinkWrite.cs`
- `Services/IRuleEngineService.cs`
- `Services/RuleEngineService.cs`
- `GoogleCalendarManagement.Tests/Integration/RuleEnginePipelineTests.cs`

**Modified:**
- `Services/ILinkService.cs` — added `WriteAutoBatchAsync` + `DeleteAutoLinksForDataPointsAsync`
- `Services/LinkService.cs` — implemented the two new methods (batch auto write under shared undoable group; auto-link deletion)
- `App.xaml.cs` — registered `IRuleEngineService` → `RuleEngineService` (singleton); placeholder comment for 8.15 rule registration

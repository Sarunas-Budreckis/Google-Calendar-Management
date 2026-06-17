# Story 8.15: First Concrete Rules — Spotify Auto-Link, Outlook Generate-Candidate

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** done
**Agent:** Opus · **Effort:** high
**Prerequisites:** Story 8.14 (Rule engine pipeline: `ILinkingRule`, `IRuleEngine`, trigger wiring) must be merged

---

## Story

As the rule engine owner building the first concrete rules on the 8.14 pipeline,
I want Spotify stream datapoints to auto-link when covered by exactly one approved event, and Outlook import to generate candidate events (or ignore suppressed ones),
so that raw data from the two most structured sources is automatically accounted for without manual intervention.

---

## Acceptance Criteria

1. **Spotify auto-link — single cover:** When exactly one approved event's time range overlaps a Spotify `data_point`'s `[start_utc, end_utc]`, the pipeline auto-links that datapoint to the event (`state=linked, origin=auto_rule, rule_id="spotify_auto_link"`).
2. **Spotify auto-link — multi-cover no-op:** When two or more approved events overlap a Spotify datapoint, the rule proposes no operation (datapoint stays unlinked or retains any pre-existing auto link unchanged).
3. **Spotify auto-link — zero-cover no-op:** When no approved event overlaps the datapoint, no operation is proposed (stays unlinked).
4. **Spotify reversal:** When an approved event that was linked to a Spotify datapoint is moved, deleted, or un-approved, the engine reverses the auto link (removes the `link` row for `origin=auto_rule` datapoints) and re-evaluates the Spotify rule for affected datapoints; manual links (`origin=manual`) are untouched.
5. **Outlook generate-candidate — unsuppressed:** On Outlook import, for each new or re-imported `OutlookEvent` with `IsSuppressed=false`, the rule proposes `generate-candidate`, creating an `event(lifecycle=candidate, publish=local_only, source_system="outlook")` with `summary=Subject`, `start/end=StartDatetime/EndDatetime`, and linking the Outlook datapoint to the new candidate event (`state=linked, origin=auto_rule, rule_id="outlook_generate_candidate"`).
6. **Outlook generate-candidate — suppressed:** For each `OutlookEvent` with `IsSuppressed=true`, the rule proposes `ignore` on the datapoint (`state=ignored, origin=auto_rule, rule_id="outlook_generate_candidate"`, `event_id=null`).
7. **Outlook idempotency:** Re-running the Outlook rule on already-processed datapoints (e.g., re-import with no changes) produces no new operations — the pipeline detects existing auto-rule links and does not duplicate them.
8. **Outlook suppression toggle:** If `IsSuppressed` is toggled on an `OutlookEvent` after initial import, re-running the rule (triggered by the pipeline's import trigger for the `"outlook"` source key) transitions the datapoint between linked↔ignored states accordingly.
9. **Candidate events are translucent on the calendar** (Opacity=0.6, `lifecycle=candidate`) — this flows automatically from Story 8.5's `CalendarQueryService.MapEventToDisplayModel` (no additional rendering work needed here).
10. **Suppressed Outlook datapoints (ignored) remain accessible** in the Linking panel (Epic 9) — the `link` row with `state=ignored` is retained and queryable; they are not deleted.
11. **Both rules registered** with the rule engine and invoked via the appropriate triggers defined in 8.14.
12. **Tests:** Spotify rule unit tests cover all three cases (single-cover link, multi-cover no-op, zero-cover no-op) and reversal; Outlook rule tests cover unsuppressed→candidate, suppressed→ignore, idempotency, and suppression-toggle scenarios.

---

## Tasks / Subtasks

> **Implementation note (deviations from the original draft — see Completion Notes for full rationale):**
> The draft predated the merged 8.14 interface. Adaptations: (a) the rule contract is
> `ILinkRule.ProposeOpsAsync(RuleScope scope, IReadOnlyList<EligibleDataPoint> eligible, ct)` returning
> `IReadOnlyList<RuleProposedOp>` (factory methods `RuleProposedOp.Link/Ignore/GenerateCandidate`) — there
> is no `RuleContext`/`SourceKey` interface member, so each rule filters `eligible` by source key and reads
> the DB read-only for the data it needs. (b) `DataPointId` is `int`. (c) The "removal" op does not exist —
> reversal is the engine's job; rules just re-evaluate to no-op and the engine deletes stale auto links.
> (d) Import-trigger wiring lives in `DataSourceSummaryViewModel` **after** projection (not in the import
> service `finally`), because datapoint projection (`RunPostImportAsync`) runs in the VM after the import
> service returns — firing in `finally` would run before any datapoints exist.

- [x] Task 1: Implement `SpotifyAutoLinkRule` (AC: #1, #2, #3, #4)
  - [x] 1.1 Create `Services/Rules/SpotifyAutoLinkRule.cs` implementing `ILinkRule` (8.14 actual interface)
  - [x] 1.2 `RuleId` = `"spotify_auto_link"`; filters `eligible` to `SpotifyImportService.SourceKey` (`"spotify"`)
  - [x] 1.3 `ProposeOpsAsync`: for each eligible spotify datapoint, count approved (non-deleted) events overlapping `[StartUtc, EndUtc]` (one batched event query over the eligible window, coverage counted in memory)
  - [x] 1.4 Single-cover branch: propose `RuleProposedOp.Link(dataPointId, eventId, RuleId)`
  - [x] 1.5 Multi-cover (2+) branch: propose nothing — verified the engine's reversal pass removes any stale `auto_rule` link when the linked event moves/deletes (8.14 `ReverseRangeAndRerunAsync`); the rule never proposes removal
  - [x] 1.6 Zero-cover branch: no-op
  - [x] 1.7 Overlap rule `event.start < dp.EndUtc AND event.end > dp.StartUtc AND lifecycle="approved" AND !IsDeleted` — direct EF query (no `RuleContext` helper exists); timings read straight from `data_point` (8.9 owns the math)

- [x] Task 2: Register `SpotifyAutoLinkRule` and wire triggers (AC: #4, #11)
  - [x] 2.1 Registered in `App.xaml.cs` as `services.AddSingleton<ILinkRule, SpotifyAutoLinkRule>()` (8.14's `IEnumerable<ILinkRule>` pattern)
  - [x] 2.2 Import trigger `RunForImportAsync(SourceKey)` wired in `DataSourceSummaryViewModel` **after** `RunPostImportAsync` projection (deviation from the `finally`-block suggestion — see note above)
  - [x] 2.3 Reversal triggers (`RunForEventDeleteAsync`/`RunForEventEditTimeAsync`/`RunForEventApproveAsync`) are handled by the 8.14 pipeline; the rule declares no triggers. Verified end-to-end by `Engine_Reversal_RemovesAutoLink_WhenCoveringEventMovesAway`

- [x] Task 3: Implement `OutlookGenerateCandidateRule` (AC: #5, #6, #7, #8)
  - [x] 3.1 Create `Services/Rules/OutlookGenerateCandidateRule.cs` implementing `ILinkRule`
  - [x] 3.2 `RuleId` = `"outlook_generate_candidate"`; filters `eligible` to `OutlookImportService.SourceKey` (`"outlook"`)
  - [x] 3.3 `ProposeOpsAsync`: resolve each datapoint's `SourceRef` (= `OutlookEventId`, looked up from `data_point` since `EligibleDataPoint` carries no `SourceRef`), resolve the `OutlookEvent`, read `IsSuppressed`
  - [x] 3.4 Unsuppressed + not already linked: propose `RuleProposedOp.GenerateCandidate(..., summary=Subject, start=StartDatetime, end=EndDatetime, sourceSystem="outlook")`; engine stamps `lifecycle=candidate`, `publish=local_only`, `colorId=null`
  - [x] 3.5 Suppressed + not already ignored: propose `RuleProposedOp.Ignore(dataPointId, RuleId)`
  - [x] 3.6 Idempotency + toggle: reads current `link.State`; already-linked-and-unsuppressed or already-ignored-and-suppressed → propose nothing; flipped `IsSuppressed` → propose the transition op (linked→Ignore, ignored→GenerateCandidate)
  - [x] 3.7 `GenerateCandidate` execution is owned by the 8.14 engine (`ApplyOpsAsync` mints the event via `IEventIdentityService`/`IEventRepository` and writes the link atomically) — no stub needed

- [x] Task 4: Register `OutlookGenerateCandidateRule` and wire import trigger (AC: #8, #11)
  - [x] 4.1 Registered in `App.xaml.cs` as `services.AddSingleton<ILinkRule, OutlookGenerateCandidateRule>()`
  - [x] 4.2 Import trigger wired generically in `DataSourceSummaryViewModel` (same post-projection hook as Spotify; `RunForImportAsync("outlook")` fires when the Outlook source is imported)
  - [x] 4.3 Reversal round-trip (candidate deleted/un-approved → engine removes the `auto_rule` link → re-run re-proposes) is handled by the 8.14 pipeline. (Suppression-toggle orphan note in Completion Notes.)

- [x] Task 5: Tests (AC: #12)
  - [x] 5.1 `GoogleCalendarManagement.Tests/Unit/Services/Rules/SpotifyAutoLinkRuleTests.cs` — single-cover→Link, multi-cover→nothing, zero-cover→nothing, candidate/deleted events excluded, source filtering; engine-level: writes auto link, manual link never overridden, reversal removes stale auto link when the covering event moves away
  - [x] 5.2 `GoogleCalendarManagement.Tests/Unit/Services/Rules/OutlookGenerateCandidateRuleTests.cs` — unsuppressed→GenerateCandidate (field assertions), suppressed→Ignore, idempotent no-ops both directions, toggle false→true→Ignore, toggle true→false→GenerateCandidate, empty-subject placeholder, unresolvable ref→nothing, source filtering; engine-level: candidate minted with `source_system="outlook"`, idempotent second run, suppressed writes ignored row with no event

### Review Findings

- [x] [Review][Patch] Re-imported Outlook events do not refresh existing generated candidates [Services/Rules/OutlookGenerateCandidateRule.cs:92]
- [x] [Review][Patch] Suppression/reversal can leave generated Outlook candidate events orphaned [Services/RuleEngineService.cs:249]
- [x] [Review][Patch] Existing Outlook suppression UI does not rerun the rule pipeline [ViewModels/OutlookDrilldownViewModel.cs:54]

---

## Dev Notes

### Hard prerequisites from Story 8.14

Story 8.15 cannot start until 8.14 is merged. Expected types from 8.14:

```csharp
// Services/ILinkingRule.cs
public interface ILinkingRule
{
    string RuleId { get; }
    // Determines which triggers this rule responds to (import, event edit, etc.)
    // Exact signature TBD by 8.14 — match whatever 8.14 defines
    Task<IReadOnlyList<ProposeOp>> ProposeAsync(RuleContext ctx, CancellationToken ct = default);
}

// Services/IRuleEngine.cs (or IRulePipeline.cs)
public interface IRuleEngine
{
    Task RunForImportAsync(string sourceKey, CancellationToken ct = default);
    Task RunForEventApproveAsync(string eventId, CancellationToken ct = default);
    Task RunForEventEditTimeAsync(string eventId, CancellationToken ct = default);
    Task RunForEventDeleteAsync(string eventId, CancellationToken ct = default);
}

// Services/ProposeOp.cs — discriminated union of operations
// 8.14 defines the op types; expect at minimum:
//   OpLink(dataPointId, eventId)
//   OpIgnore(dataPointId)
//   OpGenerateCandidate(dataPointId, CandidateSpec)
//   OpRemoveAutoLink(dataPointId)   ← for reversal

// Services/RuleContext.cs — carries scope info + helpers
// e.g., RuleContext.DataPoints, RuleContext.ApprovedEvents, RuleContext.GetLinkForDataPoint(id)
```

If 8.14 uses a different interface name, signature, or op type naming, **match 8.14's actual interface — do not invent your own**. Read the 8.14 story file and merged code before starting.

### Spotify: time-overlap definition

A Spotify stream's datapoint `[start_utc, end_utc]` overlaps an approved event `[ev.start, ev.end]` when:

```
ev.start_datetime < dp.end_utc AND ev.end_datetime > dp.start_utc
```

Spotify streams are point-in-time (track plays with duration). The datapoint's `start_utc` is `PlayedAt` (the end-of-track UTC timestamp from stats.fm — when the track finished playing) minus `MsPlayed` to reconstruct the approximate start; the `end_utc` is `PlayedAt`. Check the Story 8.9 projector implementation for `SpotifyStream → data_point` to confirm the exact `start_utc`/`end_utc` values used.

**Critical:** do not recalculate timings in this rule — use the `start_utc`/`end_utc` already stored in `data_point`. The projector in 8.9 owns that calculation.

### Spotify: `source_ref` format

The natural key for Spotify streams established by Story 8.9 is `"{PlayedAt:O}|{TrackName}"` (ISO-8601 UTC timestamp + pipe separator + track name). Read the 8.9 story or the implemented projector to confirm the exact format before resolving back to `SpotifyStream` rows if needed. For the auto-link rule itself, `source_ref` resolution back to the raw record is **not needed** — only `start_utc`/`end_utc` from `data_point` are used.

### Outlook: `source_ref` = `OutlookEventId`

The `OutlookEvent.OutlookEventId` string is the Outlook Graph API event ID. Story 8.9's projector sets `source_ref = OutlookEventId`. To resolve an `OutlookEvent` from a datapoint: `context.OutlookEvents.FirstOrDefaultAsync(e => e.OutlookEventId == dp.SourceRef, ct)`.

### Outlook: `IsSuppressed` field

`OutlookEvent.IsSuppressed` (bool) already exists on the entity and is set to `false` by `OutlookImportService` on insert. It is set to `true` via a user action in the Linking panel (Epic 9 story 9.3 or similar). The `IsSuppressed` flag persists across re-imports because `OutlookImportService.UpsertEventsAsync` does not overwrite it on update (verify this — if the update block overwrites `IsSuppressed`, add a guard to preserve it).

### Outlook: candidate event color

Outlook candidate events should have no special color (`colorId = null`) so they render in the calendar's default color for candidates (translucent gray or calendar default). Do not assign a color in `OpGenerateCandidate` unless the design doc specifies one.

### Rule invariants to honor (from concepts §7)

These invariants apply to both rules and are enforced by the 8.14 pipeline — do not re-implement them in the rule itself:

- **Manual decisions are sacred:** only touch datapoints that are `unlinked` or that this rule previously set (`origin=auto_rule, rule_id=<this rule>`). Never propose ops on `origin=manual` rows.
- **Idempotent:** same inputs → same proposed ops. Do not write side-effectful logic in `ProposeAsync`.
- **Reversal is pipeline responsibility:** when an event is deleted/moved, the pipeline calls `RunForEventDeleteAsync` / `RunForEventEditTimeAsync`, which should invoke the Spotify rule for affected datapoints. Verify 8.14 correctly scopes re-evaluation to datapoints that were linked to the changed event.
- **Atomic:** the pipeline applies all proposed ops in one transaction. `ProposeAsync` must not write to the database — only return op objects.

### Trigger wiring — where to call the pipeline

Both import services currently fire `DataSourceImportCompletedMessage` in their `finally` block. The cleanest hook is to add `await _ruleEngine.RunForImportAsync(SourceKey, ct)` immediately after that message (still inside `finally`, use `CancellationToken.None` to avoid cancelling mid-run if the user cancelled the import UI):

```csharp
// SpotifyImportService.cs (inside the finally block, after WeakReferenceMessenger send)
await _ruleEngine.RunForImportAsync(SourceKey, CancellationToken.None);

// OutlookImportService.cs (same pattern)
await _ruleEngine.RunForImportAsync(SourceKey, CancellationToken.None);
```

Inject `IRuleEngine` into both services. This matches the existing `IConfigRepository` injection pattern — add to constructor, add to DI registration in `App.xaml.cs`.

### Files to create

- `Services/Rules/SpotifyAutoLinkRule.cs`
- `Services/Rules/OutlookGenerateCandidateRule.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/Rules/SpotifyAutoLinkRuleTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/Rules/OutlookGenerateCandidateRuleTests.cs`

### Files to modify

- `Services/SpotifyImportService.cs` — inject `IRuleEngine`, call `RunForImportAsync` in `finally`
- `Services/OutlookImportService.cs` — same pattern; also verify `IsSuppressed` is not overwritten on re-import
- `App.xaml.cs` — register `SpotifyAutoLinkRule` and `OutlookGenerateCandidateRule` with DI (per 8.14's registration convention)

### Do NOT touch

- `Data/Entities/OutlookEvent.cs` — schema is correct as-is; `IsSuppressed` exists
- `Data/Entities/SpotifyStream.cs` — no schema changes needed
- `Services/OutlookImportHandler.cs` — UI handler; trigger lives in the service layer
- `Services/SpotifyImportHandler.cs` — same
- `Services/OutlookCardProvider.cs` / `OutlookDrilldownViewModel.cs` — Outlook raw-data UI stays intact (removed from calendar rendering in 8.5, not from data layer)
- Any `data_point` or `link` table schema — those are defined in 8.7 and 8.12

### Post-8.15 REVISIT note

Per epic overview: after 8.15 lands, review rule behavior in practice and plan the next batch of the 10+ rule catalog. Also evaluate whether a rules/automation visibility panel (concepts §10, deferred) needs scheduling. These are out of scope for this story.

### Project Structure Notes

- Rules live under `Services/Rules/` namespace/folder (new subfolder; create it)
- Follow the existing service pattern: constructor injection, `ILogger` optional with null default, sealed class
- Test project mirrors the source tree: `Tests/Unit/Services/Rules/`
- `SpotifyImportService.SourceKey = "spotify"` and `OutlookImportService.SourceKey = "outlook"` — use these constants, do not hardcode strings

### References

- [Epic 8 overview](../epic-overview.md) — §Story 8.15, §Phase 2 rule engine phase
- [Concepts §5 links](../concepts.md) — `link` table schema, state/origin/rule_id/action_group_id
- [Concepts §7 rules](../concepts.md) — rule invariants, operations, trigger list, reversal semantics
- [Concepts §9 source notes](../concepts.md) — Outlook suppressed = ignored datapoints; Spotify natural keys
- [Story 8.12](8-12-link-table-and-link-ignore-unlink-operations.md) — `link` table definition, `OpLink`/`OpIgnore` semantics
- [Story 8.14](8-14-rule-engine-pipeline.md) — rule engine interface to implement against (**read this first**)
- [Story 8.9](8-9-project-all-sources-into-datapoints.md) — Spotify and Outlook projectors; `source_ref` format
- [Story 8.5](8-5-rendering-and-drilldowns-mint-candidates.md) — candidate rendering (Opacity=0.6 from `lifecycle=candidate`) — no new rendering work needed in 8.15
- `Services/SpotifyImportService.cs` — `SourceKey`, `UpsertStreamsAsync`, `finally` block pattern to extend
- `Services/OutlookImportService.cs` — `SourceKey`, `UpsertEventsAsync`, `IsSuppressed` handling
- `Data/Entities/OutlookEvent.cs` — full entity schema including `IsSuppressed`
- `Data/Entities/SpotifyStream.cs` — `PlayedAt`, `MsPlayed`, `DurationMs` (for understanding projector math)

---

## Change Log

| Date       | Change                                                                                                          |
|------------|-----------------------------------------------------------------------------------------------------------------|
| 2026-06-17 | Implemented the first two concrete rules (`SpotifyAutoLinkRule`, `OutlookGenerateCandidateRule`) against 8.14's `ILinkRule`/`RuleProposedOp` contract, registered both with the engine, wired the post-projection import trigger in `DataSourceSummaryViewModel`, and added 21 unit/engine tests. Adapted to the real interface (no `RuleContext`/`SourceKey`, `int DataPointId`, engine-owned reversal/generate-candidate); moved the import-trigger call out of the import-service `finally` (datapoints are not projected until the VM's `RunPostImportAsync` runs). |

## Dev Agent Record

### Agent Model Used

Opus 4.8

### Debug Log References

- `dotnet build GoogleCalendarManagement.Tests` — succeeded, 0 errors.
- `dotnet test --filter "Rules.SpotifyAutoLinkRuleTests|Rules.OutlookGenerateCandidateRuleTests"` — 21/21 passed.
- `dotnet test` (full suite) — 571 passed, 0 failed, 19 skipped (pre-existing parked tests).

### Completion Notes List

**Matched 8.14's actual interface (no invention).** Rules implement
`ILinkRule.ProposeOpsAsync(RuleScope, IReadOnlyList<EligibleDataPoint>, ct) → IReadOnlyList<RuleProposedOp>`
and emit ops via `RuleProposedOp.Link/Ignore/GenerateCandidate`. There is no `RuleContext` or `SourceKey`
interface member: each rule filters the supplied `eligible` list by its source key and opens a read-only
`CalendarDbContext` for the extra data it needs (approved events for Spotify; `SourceRef`→`OutlookEvent`
plus current `link.State` for Outlook). `EligibleDataPoint` carries no `SourceRef`, so the Outlook rule
resolves `OutlookEventId` from `data_point` itself. Rules are pure (read-only); the engine performs all writes.

**Reversal is the engine's job — rules never "remove".** The draft's "propose removal of the auto link"
test scenarios don't map to the real design: rules only return Link/Ignore/GenerateCandidate. When a
linked event moves/deletes/un-approves, the engine's `ReverseRangeAndRerunAsync` deletes the stale
`auto_rule` link and re-runs; on re-run the Spotify rule simply re-evaluates to zero/multi-cover and
proposes nothing. This is covered end-to-end by `Engine_Reversal_RemovesAutoLink_WhenCoveringEventMovesAway`
and (manual-sacred) `Engine_ManualLink_IsNeverOverridden`.

**Import-trigger wiring moved to the VM (deviation from the draft's `finally`-block note).** Datapoint
projection runs in `DataSourceSummaryViewModel.ImportAsync`/`CsvImportAsync` via
`IDataPointReconciliationSweepService.RunPostImportAsync` **after** the import service returns. Calling
`RunForImportAsync` in the import service `finally` would run before any datapoints exist (the engine derives
its scope from the source's datapoints), making it a no-op. So the call is added right after projection in a
shared `ReconcileAndRunRulesAsync` helper. `IRuleEngineService` is injected as an optional ctor param into
`DataSourceSummaryViewModel` and threaded from `DataSourcePanelViewModel` (also optional, to keep existing
test call-sites compiling). The hook is source-agnostic, so it is `RunForImportAsync(SourceKey)` for every
source — a safe no-op for sources without a registered rule (per 8.14's design). The import services and
handlers were left untouched.

**Outlook idempotency & suppression toggle (AC #7, #8).** The rule reads the datapoint's current
`link.State`: unsuppressed + already `linked` → no-op; suppressed + already `ignored` → no-op; a flipped
`IsSuppressed` yields the transition op (linked→`Ignore`, ignored→`GenerateCandidate`). Empty Outlook
subjects map to the placeholder `"(No subject)"` so the engine's required-summary guard passes.
`IsSuppressed` is already preserved across re-imports (`OutlookImportService.UpsertEventsAsync` does not
write it on update), so no guard was needed there.

**Known follow-up — candidate orphaned on a linked→ignored suppression toggle.** When suppression is
toggled on for a datapoint that already had a generated candidate, the rule proposes `Ignore`; the engine
flips the link to `ignored` but does **not** delete the now-unreferenced candidate event (its
orphan-cleanup only runs in the reversal pass, not in the forward apply). This is out of scope for 8.15's
ACs (AC #10 requires the *link row* to be retained, not the event) and is best owned by Epic 9's suppression
action, which should delete the candidate event — that fires `EventDeletedMessage` → reversal → the link
returns to unlinked, then re-running the rule proposes `Ignore` cleanly. Documented here for that story.

### File List

**New:**
- `Services/Rules/SpotifyAutoLinkRule.cs`
- `Services/Rules/OutlookGenerateCandidateRule.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/Rules/SpotifyAutoLinkRuleTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/Rules/OutlookGenerateCandidateRuleTests.cs`

**Modified:**
- `App.xaml.cs` — registered both rules as `AddSingleton<ILinkRule, …>()`
- `ViewModels/DataSourceSummaryViewModel.cs` — inject optional `IRuleEngineService`; run `RunForImportAsync(SourceKey)` after projection (new `ReconcileAndRunRulesAsync` helper)
- `ViewModels/DataSourcePanelViewModel.cs` — inject optional `IRuleEngineService`; thread it to both `DataSourceSummaryViewModel` construction sites

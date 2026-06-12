# Story 8.15: First Concrete Rules — Spotify Auto-Link, Outlook Generate-Candidate

**Epic:** 8 — Event Model & Raw Data Linking Engine
**Status:** ready-for-dev
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

- [ ] Task 1: Implement `SpotifyAutoLinkRule` (AC: #1, #2, #3, #4)
  - [ ] 1.1 Create `Services/Rules/SpotifyAutoLinkRule.cs` implementing `ILinkingRule` (from 8.14)
  - [ ] 1.2 `RuleId` property: `"spotify_auto_link"`; `SourceKey` property: `SpotifyImportService.SourceKey` (`"spotify"`)
  - [ ] 1.3 `ProposeAsync(RuleContext ctx)`: query `data_point` for `source_key="spotify"` in scope; for each unlinked-or-auto-linked datapoint, count approved events overlapping `[start_utc, end_utc]`
  - [ ] 1.4 Single-cover branch: propose `OpLink(data_point_id, event_id)` with `rule_id="spotify_auto_link"`
  - [ ] 1.5 Multi-cover (2+) branch: propose nothing (if current row is `auto_rule`, pipeline removes the stale link during reversal pass — verify this is how 8.14 handles it)
  - [ ] 1.6 Zero-cover branch: no-op
  - [ ] 1.7 Overlap query: `event.start_datetime < dp.end_utc AND event.end_datetime > dp.start_utc AND event.lifecycle="approved"` — check whether 8.14's `RuleContext` provides a helper or whether direct EF query is used

- [ ] Task 2: Register `SpotifyAutoLinkRule` and wire triggers (AC: #4, #11)
  - [ ] 2.1 Register `SpotifyAutoLinkRule` with the pipeline (via DI registration in `App.xaml.cs` and whatever registration pattern 8.14 defines — e.g., `services.AddLinkingRule<SpotifyAutoLinkRule>()`)
  - [ ] 2.2 Confirm the import trigger (`RunForImportAsync("spotify", ct)`) fires after `SpotifyImportService` completes — check 8.14's trigger wiring; if not already wired, add the call inside `SpotifyImportService.ImportAsync` (in the `finally` block, after `DataSourceImportCompletedMessage`)
  - [ ] 2.3 Confirm reversal triggers (`RunForEventDeleteAsync`, `RunForEventEditAsync`, `RunForEventApproveAsync`) run the Spotify rule for affected datapoints — this should be handled by the 8.14 pipeline; if Spotify rule needs to declare which triggers it responds to, implement that

- [ ] Task 3: Implement `OutlookGenerateCandidateRule` (AC: #5, #6, #7, #8)
  - [ ] 3.1 Create `Services/Rules/OutlookGenerateCandidateRule.cs` implementing `ILinkingRule`
  - [ ] 3.2 `RuleId` property: `"outlook_generate_candidate"`; `SourceKey` property: `OutlookImportService.SourceKey` (`"outlook"`)
  - [ ] 3.3 `ProposeAsync(RuleContext ctx)`: query `data_point` for `source_key="outlook"` in scope; for each datapoint, resolve the `OutlookEvent` via `source_ref` (= `OutlookEventId`) and read `IsSuppressed`
  - [ ] 3.4 Unsuppressed + no existing `auto_rule` link: propose `OpGenerateCandidate(data_point_id, candidateSpec)` where `candidateSpec` carries `summary=Subject`, `start=StartDatetime`, `end=EndDatetime`, `source_system="outlook"`, `lifecycle="candidate"`, `publish="local_only"`, `colorId=null`
  - [ ] 3.5 Suppressed + no existing `auto_rule` link: propose `OpIgnore(data_point_id)`
  - [ ] 3.6 Idempotency: if the datapoint already has `origin=auto_rule` link from this rule and `IsSuppressed` is unchanged, propose nothing; if `IsSuppressed` changed, propose the transition op (link→ignore or ignore→link)
  - [ ] 3.7 `OpGenerateCandidate` execution in the pipeline (this may be implemented in 8.14 as a pipeline operation type; if not yet defined, stub the op and coordinate with 8.14's interface): mint a new `event_id` via `IEventRepository.CreateAsync(event)` and write the `link` row linking `data_point_id` to the new `event_id`

- [ ] Task 4: Register `OutlookGenerateCandidateRule` and wire import trigger (AC: #8, #11)
  - [ ] 4.1 Register `OutlookGenerateCandidateRule` with the pipeline
  - [ ] 4.2 Wire `RunForImportAsync("outlook", ct)` into `OutlookImportService.ImportAsync` `finally` block (after `DataSourceImportCompletedMessage`) — matches the pattern used for Spotify in Task 2.2
  - [ ] 4.3 Confirm reversal: if a candidate event generated by this rule is deleted or un-approved, the Outlook datapoint returns to unlinked (pipeline's reversal pass removes the `auto_rule` link row); re-running the rule then re-proposes the candidate — verify this round-trip works

- [ ] Task 5: Tests (AC: #12)
  - [ ] 5.1 `GoogleCalendarManagement.Tests/Unit/Services/Rules/SpotifyAutoLinkRuleTests.cs`
    - Single-cover → proposes `OpLink` to that event
    - Two-cover → proposes nothing (or proposes removal of stale auto link if one existed)
    - Zero-cover → proposes nothing
    - Reversal simulation: single-cover link exists, event removed from scope → rule proposes removal of the auto link
    - Manual link (`origin=manual`) present → rule proposes nothing (does not override)
  - [ ] 5.2 `GoogleCalendarManagement.Tests/Unit/Services/Rules/OutlookGenerateCandidateRuleTests.cs`
    - Unsuppressed, no prior link → proposes `OpGenerateCandidate` with correct event fields
    - Suppressed, no prior link → proposes `OpIgnore`
    - Unsuppressed, already has `auto_rule` linked candidate → proposes nothing (idempotent)
    - IsSuppressed toggled false→true: proposes removal of existing `linked` auto link + `OpIgnore`
    - IsSuppressed toggled true→false: proposes removal of existing `ignored` row + `OpGenerateCandidate`
    - Manual link present → proposes nothing (does not override)

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

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

# Epic 9: Linking Panel & Workflows

**Author:** Sarunas Budreckis
**Date:** 2026-06-11
**Status:** Ready for Story Creation
**Tier:** 4 (data accountability layer — UX)
**Concepts reference:** [../epic-8-data-linking/concepts.md](../epic-8-data-linking/concepts.md)

---

## Goal

Deliver the **user experience** for total raw-data accountability on top of the Epic 8 engine:
a third left panel (**Linking panel**) hosting the four linking workflows, a left **icon
strip** to switch panels, gap visualization on the calendar, and coverage indicators
throughout. Focus on ease of linking and readability of what is/ isn't accounted for.

---

## Agent / effort legend

- **Agent** — `Opus` / `Sonnet` / `Codex` (see Epic 8 overview for definitions).
- **Effort** — `high` / `medium` / `low` thinking budget.

Epic 9 is mostly bounded WinUI feature work → predominantly **Sonnet**; the two lenses with
nontrivial interaction/coalescing logic carry **high** effort.

---

## Story candidates

| Story | Title | Agent | Effort |
|-------|-------|-------|--------|
| 9.1 | Left icon strip + 3 selectable panels | Sonnet | medium |
| 9.2 | Sources panel coverage rollup + rule-driven ordering | Sonnet | medium |
| 9.3 | Linking panel — By Source lens (W1+W2) | Sonnet | high |
| 9.4 | Linking panel — By Event lens (W3) | Sonnet | medium |
| 9.5 | Linking panel — Gaps lens (W4) | Sonnet | high |
| 9.6 | Gap calendar rendering (outlines + `+`) | Sonnet | medium |
| 9.7 | Gap detail panel (top-4 sources, vertical dots) | Sonnet | medium |
| 9.8 | Coverage indicators everywhere + certified-day unlinked dot | Sonnet | low |

---

### Story 9.1 — Left icon strip + 3 selectable panels
**Agent:** Sonnet · **Effort:** medium
Add a VS-Code-style left icon strip selecting between three panels: **Sources** (global
source list), **Day Detail** (single day), **Linking**. Replace today's auto-switch-on-
selection with explicit selection; the Day Detail panel shows a "select a day" prompt when no
day is chosen.
**Key AC:** strip switches panels independently of calendar selection; Day/Sources panels
preserve their existing content; selecting a day no longer force-switches panels; last panel
remembered.
**Prereqs:** Epic 5 panels.
**Notes:** **REVISIT (Sarunas):** first appearance of the panel strip — review the switching
model in the running app.

### Story 9.2 — Sources panel coverage rollup + rule-driven ordering
**Agent:** Sonnet · **Effort:** medium
Enhance the Sources panel with per-source coverage (`● / ◐ / ○`) and `x/y datapoints linked`.
Order the list by **link order** (every source named in a rule appears even with no data);
rule-less sources sort to the end and appear only if they have data.
**Key AC:** rollup matches the coverage service (Epic 8 §6); ordering follows hardcoded link
order; counts are per-datapoint; rule-less empty sources hidden.
**Prereqs:** 8.10 (coverage), 8.14/8.15 (rule membership for ordering).

### Story 9.3 — Linking panel: By Source lens (W1 + W2 merged)
**Agent:** Sonnet · **Effort:** high
The By-Source workflow: pick/iterate a source, see its **unlinked clumps**, act with link /
ignore / **unlink** / **+ event**; Next/Prev clump (keyboard-drivable). Scope toggle:
**view-following** or an **independent date range**. Shows concurrent-event hints per clump;
applies link order as soft, non-blocking prompts.
**Key AC:** clumps come from the Epic 8 block/clump provider; link-to-any-event via 8.13;
`+ event` creates a translucent candidate; linked/ignored clumps stay visible; date-range
scope works independent of the calendar view; Next/Prev steps unlinked clumps.
**Prereqs:** 8.11, 8.12, 8.13, 9.1.
**Notes:** **REVISIT (Sarunas):** core linking workflow — exercise with real multi-source data.

### Story 9.4 — Linking panel: By Event lens (W3)
**Agent:** Sonnet · **Effort:** medium
For a selected event (or right-click event → "show concurrent raw data"): list all
time-overlapping datapoints grouped by source with current link state (incl. auto-links);
bulk **link all / ignore all** to this event; show event coverage.
**Key AC:** right-click context menu entry opens this lens scoped to the event; auto-linked
datapoints shown as such; bulk actions write grouped undoable links; coverage updates live.
**Prereqs:** 8.12, 8.13, 9.1.

### Story 9.5 — Linking panel: Gaps lens (W4)
**Agent:** Sonnet · **Effort:** high
Raw data in scope **not** covered by any approved event, clumped **cross-source by blank
period** (gaps ≤ 24h). Each gap → **create event from gap** (pre-filled time from the data
extent → translucent candidate) or **ignore all**.
**Key AC:** gap detection excludes time covered by approved events; cross-source clumping by
blank period; create-from-gap spawns a translucent candidate and auto-links contributors;
ignore-all resolves the gap's datapoints.
**Prereqs:** 8.12, 8.13, 8.11, 9.1.

### Story 9.6 — Gap calendar rendering (outlines + `+`)
**Agent:** Sonnet · **Effort:** medium
When the Gaps lens is active, render empty gray outlines with a `+` icon in place of events
for each gap window; clicking a gap (on the calendar or in the lens) selects it.
**Key AC:** outlines appear only while Gaps lens active; clicking a gap opens its detail
(9.7); gaps never exceed 24h; outlines clear when leaving the lens.
**Prereqs:** 9.5.

### Story 9.7 — Gap detail panel (top-4 sources, vertical dots)
**Agent:** Sonnet · **Effort:** medium
Clicking a gap shows a gap-detail panel: the **top-4 sources** with data during the gap, each
as a vertical timeline of **color-coded dots** (source color). Dots show **instant tooltips**;
high-volume sources (e.g. 1000+ search entries) render a clickable, expandable dot.
**Key AC:** top-4 by datapoint count; dots placed by timestamp in source color; hover =
instant tooltip; overflow collapses to an expandable control; from here the user can create an
event for the gap.
**Prereqs:** 9.6, 8.11.
**Notes:** **REVISIT (Sarunas):** later story — fine detail (tooltip content, dot interaction,
expansion behavior) to be designed when reached.

### Story 9.8 — Coverage indicators everywhere + certified-day unlinked dot
**Agent:** Sonnet · **Effort:** low
Surface coverage glyphs (`● / ◐ / ○`) consistently across Sources, Day Detail, and Linking
panels; add a **tiny dot** on a **certified** day that still has unlinked datapoints.
**Key AC:** glyphs shared from one component; certified-day-with-unlinked-data shows the dot;
day certification remains independent of linkage (no gating).
**Prereqs:** 8.10, 9.1.

---

## Dependencies

- **Epic 8 complete** (event model, datapoint registry, coverage, link model, rule engine).
- Epic 5 three-panel layout + day/global panels.
- Event multi-select (Story 4.6) for bulk actions where applicable.

---

## Out of scope (Epic 9)

- Engine/data concerns (all in Epic 8).
- Rules/automation visibility panel (unscoped — concepts §10).
- Per-source rendering specifics beyond the shared dot/clump patterns (deferred).
- Playback / embedding (future epic).

---

## Success criteria

- Three panels selectable via the icon strip; day selection no longer force-switches panels.
- All four workflows usable from the Linking panel: By Source (date-range or view scope),
  By Event (incl. right-click), Gaps (with calendar outlines + gap detail).
- Coverage is readable at a glance everywhere; certified days with unlinked data are flagged.
- Linking a clump, creating an event from a gap, and bulk event-linking are all a few clicks
  and undoable.

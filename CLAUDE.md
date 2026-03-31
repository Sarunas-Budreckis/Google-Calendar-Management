# Google Calendar Management — Claude Code Instructions

## Project Story & Sprint Conventions

**Story files MUST be placed at:**
```
docs/epic-{N}/stories/{story_key}.md
```
For example: `docs/epic-3/stories/3-4-create-event-details-panel.md`

**Sprint status file is at:**
```
docs/sprint-status.yaml
```

**Do NOT write stories to `_bmad-output/implementation-artifacts/`.**
The `implementation_artifacts` config variable resolves to `docs/`, and story output paths include the `epic-{N}/stories/` subdirectory.

## Planning Artifacts

Planning documents (PRD, architecture, UX, epics) live under:
```
_bmad-output/planning-artifacts/
```

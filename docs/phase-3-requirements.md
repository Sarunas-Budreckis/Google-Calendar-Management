# Phase 3 Requirements - Data Sources for Easier Editing (Automation)

**Project:** Google Calendar Management
**Date:** 2026-01-30
**Status:** Ready for Planning

---

## Overview

**Goal:** Transform backfilling from tedious chore into a satisfying life-review ritual through intelligent automation.

**Core Question:** "Does this data source assistance make backfilling enjoyable?"

**Phase 3 builds on Phase 1 & 2** - All previous capabilities maintained, with added automation to dramatically reduce backfilling time.

---

## Scope

### What's in Phase 3

**Data Source Integrations:**
1. **Toggl Track API** - Time entries with phone activity coalescing
2. **iOS Call Logs** - iMazing CSV parsing with duration filtering
3. **YouTube Watch History** - Google Takeout JSON + session coalescing + video metadata
4. **Outlook Calendar** - Microsoft Graph API OAuth 2.0 (or .ics fallback)

**Coalescing Algorithms:**
- **8/15 Rounding** - Keep 15-min blocks with ≥8 minutes activity
- **Phone Activity Coalescing** - Sliding window with 15-min gap auto-stop
- **YouTube Session Coalescing** - Sliding window (duration + 30 min)
- Auto-generate candidate events from data sources

**Event Hover System:**
- 0-100ms tooltip appears on hover
- Data source timeline view (vertical time-aligned)
- Shows which sources contributed to event
- Move cursor → previous box closes, next opens instantly
- Non-modal, doesn't block calendar view

**Approval Workflow:**
- Auto-generated events appear as translucent overlays
- Click to edit before approving
- Select individual events, day, or date range
- "Approve Selected" button with count badge
- Batch publish approved events to GCal

**Day Naming:**
- Give each date a memorable name (all-day event)
- Contextualize dates for future recall
- Stored as special event type

**Weekly Status Tracking:**
- Calculate completion per data source per ISO 8601 week
- Track: Call Logs, Sleep, YouTube, Toggl, Full Walkthrough, Days Named
- Status: "Yes" (all 7 days), "Partial" (some days), "No" (zero days)

**Excel Cloud Sync:**
- Sync weekly status to user's existing Excel file
- Via Microsoft Graph API (OneDrive/SharePoint)
- Bidirectional sync (read user's manual edits)
- Auto-sync when week status changes

**Date State Tracking:**
- Per-date flags for each data source published
- Contiguity edge calculation (last fully backfilled date)
- Gap tracking (mark periods to fill later)
- "Fill to present" workflow from contiguity edge

---

## Success Criteria

✅ All Phase 1 and 2 capabilities maintained
✅ All 4 data sources successfully import and process
✅ Coalescing algorithms produce accurate events
✅ Hover system appears instantly (0-100ms)
✅ Auto-generated events clearly distinguishable
✅ Approval workflow feels satisfying (like inbox zero)
✅ Day naming workflow smooth and enjoyable
✅ Weekly status syncs to Excel reliably
✅ Backfilling 1 week takes <1 hour (down from 2-4 hours)
✅ Contiguity edge stays within 7 days of present

---

## Implementation Strategy

**Focus:** Intelligent automation and life-review ritual

1. Pick one data source first (Toggl Track recommended)
2. Implement coalescing algorithm (8/15 rounding, phone coalescing)
3. Event hover system with data source timeline (0-100ms)
4. Auto-generate candidate events
5. Approval workflow UI
6. Add remaining data sources incrementally (Calls, YouTube, Outlook)
7. Day naming workflow
8. Date state tracking and contiguity management
9. Weekly status tracking
10. Excel cloud sync via Microsoft Graph
11. "Fill to present" workflow

**Validation:** Backfilling becomes enjoyable weekly habit

---

## Feature List

| Feature | Purpose |
|---------|---------|
| **Toggl Track Integration** | Time entries with phone coalescing |
| **iOS Call Logs Integration** | iMazing CSV with filtering |
| **YouTube Integration** | Takeout JSON + API metadata + session coalescing |
| **Outlook Integration** | Microsoft Graph API + .ics fallback |
| **8/15 Rounding Algorithm** | Convert precise times to 15-min blocks |
| **Phone Activity Coalescing** | Sliding window, 15-min gap auto-stop |
| **YouTube Session Coalescing** | Sliding window (duration + 30 min) |
| **Event Hover System** | Data source timeline (0-100ms tooltips) |
| **Approval Workflow** | Review and approve auto-generated candidates |
| **Data Source Timeline View** | Vertical time-aligned source visualization |
| **Auto-Generate Candidate Events** | Create events from data sources |
| **Batch Import from Sources** | Import multiple data sources at once |
| **Day Naming** | Contextualize each date with memorable name |
| **Gap Tracking** | Manage incomplete periods explicitly |
| **Contiguity Management** | Track backfill progress, know where you left off |
| **Weekly Status Tracking** | Progress reports per data source |
| **Excel Cloud Sync** | External progress tracking via Microsoft Graph |
| **Pattern Analysis Dashboard** | Life KPIs and insights (future enhancement) |

---

## Data Source Details

### Toggl Track API
- Fetch time entries for date ranges
- Identify "Phone" or "ToDelete" entries for coalescing
- Identify "sleep" entries for special handling
- Apply 8/15 rounding to all entries
- Filter entries <5 minutes (configurable)

### iOS Call Logs (iMazing CSV)
- Parse CSV: call type, datetime, duration, contact, service
- Filter: Duration ≥ 3 minutes, Service ≠ "Teams Audio", Contact ≠ "GV"
- Apply 8/15 rounding after filtering
- Event format: "[Call type] call from [Contact] ([number]) for [duration]"

### YouTube Watch History (Google Takeout + API)
- Parse Google Takeout JSON for video IDs and watch times
- Batch query YouTube Data API for durations (50 IDs at once)
- Apply session coalescing algorithm
- Event format: "YouTube - [Channel1], [Channel2], [Channel3]"

### Outlook Calendar (Microsoft Graph API)
- OAuth 2.0 with 90-day refresh token
- Fetch work calendar events
- User selects which to import to personal calendar
- Fallback: .ics file upload

---

## Core Algorithms

### 8/15 Rounding Rule
1. Divide time range into 15-minute blocks
2. Keep blocks with ≥8 minutes of activity
3. Always include at least 1 block (end time)
4. Threshold configurable in database (default: 8 minutes)

**Example:**
```
Activity: 14:37-15:12 (35 minutes)
Blocks:
- 14:30-14:45: 8 min → KEEP
- 14:45-15:00: 15 min → KEEP
- 15:00-15:15: 12 min → KEEP
Result: 14:30-15:15 (rounded event)
```

### Phone Activity Coalescing
1. Sort Toggl "Phone"/"ToDelete" entries by time
2. Sliding window: Include next entry if within 15 minutes
3. Quality check: If <50% phone activity, retry with 5-min gaps
4. Discard windows <5 minutes total
5. Apply 8/15 rounding to final window
6. Create single event, link all sources in `generated_event_source`

### YouTube Session Coalescing
1. Sort videos by watch start time
2. Sliding window: Include next video if within (duration + 30 min)
3. Allow overlaps (next video starts before previous ends)
4. Total duration = first start to (last start + last duration)
5. Apply 8/15 rounding
6. Event title: "YouTube - [unique channels]"

---

## The Special Moment

**Hover System:**
When hovering over an event, the data source timeline appears showing exactly what you were doing during that time - Toggl entries, YouTube videos, call logs, all time-aligned. This transforms "What was I doing?" into instant, confident recall: "Oh right, I was watching those Rust videos while on that phone call."

**Spaced Repetition for Life:**
The approval process becomes memory reinforcement. Reviewing events triggers recall: "Oh right, that's when I talked to Mom" or "I watched 3 videos on Rust programming - that was a good evening."

---

## Use While Building

**Phase 3 Value:**
- Dramatically reduced backfilling time (<1 hour/week)
- Single-pane view with all context visible
- Confident approval decisions (not exhausting guesswork)
- Transform backfilling into enjoyable weekly ritual
- Maintain practice sustainably for decades

**Future Enhancements:**
- Additional data sources (Spotify, Screen Time, Google Search, Maps Timeline, NFC tags)
- Privacy layers (secret events, fake events, encryption)
- AI-powered insights and pattern recognition
- Multi-device sync (web/mobile)

---

**Document Version:** 1.0
**Last Updated:** 2026-01-30
**Next Review:** Before Phase 3 implementation begins

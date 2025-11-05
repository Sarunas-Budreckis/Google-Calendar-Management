# Key Architectural & Design Decisions

**Project:** Google Calendar Management
**Date:** 2025-11-05
**Status:** Phase 1 Design Complete

## Overview

This document captures critical decisions made during the planning and design phase, along with the rationale behind each choice.

---

## 1. Technology Stack

### Decision: .NET 9 + WinUI 3

**Alternatives Considered:**
- Electron (cross-platform)
- WPF (mature, stable)
- .NET MAUI (cross-platform)

**Chosen:** .NET 9 with WinUI 3

**Rationale:**
- **Native Windows performance** - Better than web-based Electron
- **Future-proof** - Microsoft's recommended path for Windows apps
- **Modern Fluent Design** - Built-in Windows 11 aesthetic
- **CalendarView control** - Perfect for our use case
- **Extensible architecture** - Data layer separate from UI enables future web/mobile UIs
- **Cross-platform later** - Can add .NET MAUI or web frontend in future phases

**Trade-offs Accepted:**
- ✅ Windows-only for Phase 1 (acceptable for primary user)
- ✅ Slightly steeper learning curve than WPF (worth it for future)

---

## 2. Database Architecture

### Decision: SQLite + Entity Framework Core

**Alternatives Considered:**
- JSON/XML flat files
- PostgreSQL/MySQL (server-based)
- Hybrid (SQLite + log files)

**Chosen:** SQLite for structured data + separate log files for API responses

**Rationale:**
- **Single file** - Easy backup and portability
- **No server** - Desktop app doesn't need client-server complexity
- **Cross-platform** - Works on Windows, Linux, Mac
- **EF Core integration** - Excellent ORM support, migrations
- **Performance** - More than adequate for expected data volume
- **Complex queries** - Date ranges, state filtering, version history
- **Transaction support** - Critical for rollback functionality

**Log Files Separate:**
- Raw API responses in `logs/` folder
- Deletable without affecting core data
- Useful for debugging, not required for operation

**Trade-offs Accepted:**
- ✅ Not as human-readable as JSON (mitigated by EF Core tooling)
- ✅ Schema changes require migrations (good practice anyway)

---

## 3. Table Naming Convention

### Decision: Singular table names

**Alternatives Considered:**
- Plural table names (Rails convention)

**Chosen:** Singular (e.g., `gcal_event` not `gcal_events`)

**Rationale:**
- **Best practice in .NET/EF Core** - Aligns with entity class names
- **Clarity** - `toggl_data` table contains multiple records, each is a "datum"
- **Consistency** - Matches C# class naming (singular)

**Implementation:**
- `date_state` (not dates_states)
- `gcal_event` (not gcal_events)
- `toggl_data` (not toggl_data or toggl_entries)
- `youtube_data` (not youtube_data or youtube_videos)

---

## 4. Approval Workflow

### Decision: Approval state in memory, only persist after publish

**Alternatives Considered:**
- Store `user_approved` boolean in database before publishing
- Two-step process: approve → save to DB → publish → update IDs

**Chosen:** Approval lives in UI state until publish

**Rationale:**
- **Simpler** - One source of truth: what's in `gcal_event` table IS published
- **Clearer state machine** - `published_to_gcal = TRUE` means it's on Google Calendar
- **No orphaned approvals** - If user closes app before publishing, no lingering state
- **Matches user mental model** - "Approve" and "Publish" are one atomic action

**Implementation:**
```
User selects events → in-memory list
User clicks "Publish" → send to Google
Receive event IDs → save to database
published_to_gcal = TRUE
```

**Trade-offs Accepted:**
- ✅ If app crashes during publish, some events may be published but not recorded (mitigated by sync from GCal on restart)

---

## 5. Version History Strategy

### Decision: Maintain our own version history, use Google's ETags for sync

**Alternatives Considered:**
- Rely on Google Calendar's version tracking (doesn't exist)
- Only use ETags (insufficient for rollback)
- Store diffs instead of full snapshots

**Chosen:** Full snapshot in `gcal_event_version` + ETags in `gcal_event`

**Rationale:**
**Google's Limitation:**
- ❌ Google Calendar API does NOT provide version history
- ✅ ETags only for optimistic concurrency control (prevent conflicts)
- ❌ Cannot rollback to previous ETag

**Our Solution:**
- Store complete event data in `gcal_event_version` for each change
- Track `changed_by` and timestamp
- Rollback sends UPDATE request with previous values
- Google generates new ETag (can't revert ETags themselves)

**Benefits:**
- ✅ Full audit trail
- ✅ Rollback to any save point
- ✅ Can query "what changed" between versions
- ✅ Conflict detection via ETags

**Trade-offs Accepted:**
- ✅ More storage (full snapshots) - acceptable for expected data volume
- ✅ Rollback creates new version on Google's side - acceptable, content is restored

---

## 6. Data Source Storage

### Decision: Store raw data separately from published events

**Alternatives Considered:**
- Single table with source data + published state
- Only store what's published (discard source data)

**Chosen:** Separate tables for source data (`toggl_data`, `youtube_data`, etc.) and published events (`gcal_event`)

**Rationale:**
- **Preserve everything** - Never lose source data
- **Reprocessing** - Can change coalescing algorithms and regenerate
- **Audit trail** - Know exactly where each event came from
- **Flexibility** - Store source data even if not published
- **Version history** - Track changes over time

**Mapping:**
- Source tables: `published_gcal_event_id` links to `gcal_event`
- Generated events: `generated_event_source` table tracks multiple sources

**Benefits:**
- ✅ Can unpublish event but keep source data
- ✅ Can republish with different processing
- ✅ Complete data warehouse for future analysis

**Trade-offs Accepted:**
- ✅ More storage (acceptable)
- ✅ Slightly more complex queries (mitigated by EF Core navigation properties)

---

## 7. YouTube Watch History

### Decision: Google Takeout + YouTube API for Phase 1, Chrome extension investigation for Phase 2+

**Alternatives Considered:**
- Build Chrome extension immediately
- Only use Takeout (no metadata)
- Try to use YouTube MyActivity scraping

**Chosen:** Manual Takeout download + API metadata fetch

**Rationale:**
**Research Findings:**
- ❌ YouTube Data API does NOT provide watch history access (deprecated 2016)
- ❌ MyActivity not accessible programmatically
- ✅ Google Takeout provides watch history in JSON format
- ⚠️ Takeout doesn't include video duration
- ✅ YouTube Data API provides metadata by video ID

**Phase 1 Solution:**
1. User downloads Takeout manually
2. App parses JSON for video IDs and watch times
3. Batch query YouTube API for durations
4. Cache everything locally
5. Apply coalescing

**Phase 2+ Enhancement:**
- Chrome extension for real-time tracking (user already has historical data from Takeout)
- Investigate existing extensions: "Local YouTube Video History Tracker", "Watchmarker"
- Or build custom extension with export feature

**Benefits:**
- ✅ Access to historical data (years back)
- ✅ No dependency on extension development for Phase 1
- ✅ Complete metadata (duration, channel, title)

**Trade-offs Accepted:**
- ✅ Manual download process (acceptable for Phase 1)
- ✅ Not real-time (future enhancement)

---

## 8. Outlook Calendar Integration

### Decision: Microsoft Graph API with delegated auth, .ics fallback

**Alternatives Considered:**
- Only .ics file upload
- Only API (no fallback)
- Direct Outlook COM automation (Windows-specific)

**Chosen:** API primary, file upload fallback

**Rationale:**
**API Benefits:**
- ✅ Works on personal device with work account
- ✅ OAuth 2.0 delegated permissions
- ✅ Refresh token for 90 days
- ✅ Automatic future sync

**Fallback Benefits:**
- ✅ Works if API auth fails
- ✅ Works with any calendar (Google, Yahoo, etc.)
- ✅ No special permissions needed

**Implementation:**
User chooses on first import. App remembers preference.

**Trade-offs Accepted:**
- ✅ 90-day refresh token expiration (user must re-auth periodically)
- ✅ Requires Azure AD app registration (one-time setup)

---

## 9. Coalescing Algorithms

### Decision: Source-specific coalescing with configurable parameters

**Alternatives Considered:**
- Generic coalescing for all sources
- No coalescing (show every source entry)
- User-defined coalescing rules

**Chosen:** Source-specific algorithms with configurable thresholds

**Rationale:**
**Phone Activity:**
- Problem: iOS shortcuts create many tiny Toggl entries (Toggl API limits, frequent app switching)
- Solution: Sliding window with 15-min gaps (50% activity threshold), fallback to 5-min gaps
- Why: Matches real-world usage patterns

**YouTube Viewing:**
- Problem: User watches multiple videos in one session, don't need separate events
- Solution: Sliding window with (video duration + 30 min) threshold
- Why: Reasonable break between viewing sessions

**Other Sources:**
- No coalescing by default
- User selects which entries to publish

**Configurability:**
All thresholds stored in `config` table:
- `phone_coalesce_gap_minutes` = 15
- `youtube_coalesce_gap_minutes` = 30
- `min_event_duration_minutes` = 5
- `call_min_duration_minutes` = 3

**Benefits:**
- ✅ Adapts to each data source's characteristics
- ✅ User can adjust if needed
- ✅ Preserves granular data in database

**Trade-offs Accepted:**
- ✅ More complex logic (worth it for better UX)
- ✅ User must understand coalescing (documentation needed)

---

## 10. 8/15 Rounding Rule

### Decision: Universal rounding rule for all time blocks

**Alternatives Considered:**
- Different rounding for each source
- No rounding (use precise times)
- Simple rounding to nearest 15 minutes

**Chosen:** 8/15 rule (keep 15-min blocks with ≥8 minutes activity)

**Rationale:**
**Problem:**
Google Calendar rounds to 15-minute increments. Precise times look odd (14:37-15:12).

**Simple Rounding Issues:**
- Activity 14:37-14:48 (11 minutes) → rounds to 14:30-15:00 (30 minutes) - too much
- Activity 14:52-15:03 (11 minutes) → rounds to 15:00-15:00 (0 minutes) - disappears!

**8/15 Rule:**
1. Divide time into 15-minute segments
2. Keep segments with ≥8 minutes of activity
3. Always show at least 1 segment (end time)

**Example:**
```
Activity: 14:37-15:12 (35 minutes)

Segments:
14:30-14:45: 8 min activity → KEEP
14:45-15:00: 15 min activity → KEEP
15:00-15:15: 12 min activity → KEEP

Result: 14:30-15:15 (45 minutes rounded)
```

**Benefits:**
- ✅ More accurate than simple rounding
- ✅ Doesn't artificially inflate or deflate durations
- ✅ Consistent across all sources
- ✅ Configurable threshold (8 minutes in `config` table)

**Trade-offs Accepted:**
- ✅ More complex than simple rounding (worth it for accuracy)
- ✅ Slightly longer durations than precise times (acceptable for calendar visualization)

---

## 11. Date State Tracking

### Decision: Multi-dimensional state tracking per date

**Alternatives Considered:**
- Single "complete" boolean per date
- Track only Google Calendar state
- Track only user approval

**Chosen:** Multiple independent state dimensions

**Rationale:**
**States Tracked:**
1. Data source publication (`call_log_data_published`, `sleep_data_published`, etc.)
2. Named status (`named`)
3. Walkthrough approval (`complete_walkthrough_approval`)
4. Gap status (`part_of_tracked_gap`)

**Why Multiple Dimensions:**
- User may want to track progress incrementally
- "Did I import call logs for this week?" → Check specific state
- "Is this date completely done?" → Check walkthrough approval
- "Which data sources am I missing?" → Compare all publication states

**Contiguity Rule:**
```
Date is "verified" if:
    complete_walkthrough_approval == TRUE
    OR part_of_tracked_gap == TRUE
```

**Benefits:**
- ✅ Granular progress tracking
- ✅ Can see which data sources are incomplete
- ✅ User can fill data sources in any order
- ✅ Weekly status easily calculated

**Trade-offs Accepted:**
- ✅ More columns in database (acceptable)
- ✅ More complex state management (mitigated by EF Core)

---

## 12. Weekly Status & Excel Sync

### Decision: Local weekly state + bidirectional Excel cloud sync

**Alternatives Considered:**
- Excel as single source of truth
- No Excel integration (local only)
- One-way sync (only push to Excel)

**Chosen:** Local state with bidirectional sync

**Rationale:**
**User's Existing Workflow:**
- Already tracks weekly status in Excel
- Excel has additional features (week names, manual notes)
- Wants automation but not full replacement

**Implementation:**
- Compute weekly status from `date_state` table
- Store in `weekly_state` table
- Sync to Excel via Microsoft Graph API when changed
- Read Excel on startup (user may have edited manually)

**Week Calculation:**
- ISO 8601: Weeks start Monday, Week 1 has ≥4 days of new year
- `System.Globalization.ISOWeek.GetWeekOfYear()` - built-in!

**Status Values:**
- "Yes" = all 7 days complete
- "Partial" = some days complete
- "No" = no days complete

**Benefits:**
- ✅ Automates user's existing workflow
- ✅ Preserves user's manual Excel edits
- ✅ Works offline (syncs when connection available)

**Trade-offs Accepted:**
- ✅ Complexity of bidirectional sync (conflict resolution needed)
- ✅ Requires Microsoft account authentication
- ✅ Excel schema must match (user can't change column order without app update)

---

## 13. Save/Restore Granularity

### Decision: Snapshot only Google Calendar state

**Alternatives Considered:**
- Full database backup (all tables)
- Snapshot all data including source tables
- Snapshot individual operations (event-by-event)

**Chosen:** Snapshot `gcal_event` table only

**Rationale:**
**User Need:**
"I published a bunch of events to Google Calendar. Oops, something's wrong. Undo to yesterday."

**What to Restore:**
Only Google Calendar state (what's visible to user)

**What NOT to Restore:**
- Source data (Toggl, YouTube, etc.) - never modified
- Local approvals - ephemeral
- Date states - recalculated from GCal state

**Implementation:**
```json
{
  "event_id_1": {
    "summary": "...",
    "description": "...",
    "start_datetime": "...",
    "end_datetime": "...",
    "color_id": "...",
    "etag": "..."
  }
}
```

**Restore Process:**
1. Load snapshot
2. For each event, send UPDATE to Google with old values
3. Google generates new ETag (can't revert ETags)
4. Content is restored

**Separate Database Backup:**
User can manually copy `calendar.db` file for complete local backup.

**Benefits:**
- ✅ Fast to create
- ✅ Focused on user-visible state
- ✅ Minimal storage (JSON snapshot)
- ✅ Easy to understand

**Trade-offs Accepted:**
- ✅ Doesn't restore local source data (acceptable, source data never modified)
- ✅ Creates new versions on Google's side (acceptable, content is correct)

---

## 14. App-Published Event Notation

### Decision: Append note to description on every modification

**Alternatives Considered:**
- Only on first publish
- Separate metadata field
- No notation

**Chosen:** Append to description, update on every change

**Rationale:**
**Purpose:**
- User can see which events came from this app vs. manual entry
- Debugging: When was this event last modified?
- Audit trail visible in Google Calendar

**Format:**
```
[Event description content]

Published by Google Calendar Management on 2025-11-05 14:30:00
```

**Update Policy:**
Every time app modifies event, update the timestamp.

**Benefits:**
- ✅ Visible in Google Calendar (no special app needed to view)
- ✅ Clear audit trail
- ✅ User can distinguish app events from manual events
- ✅ Useful for debugging

**Trade-offs Accepted:**
- ✅ Slightly longer descriptions (acceptable)
- ✅ Must be careful not to duplicate notation (check before appending)

---

## 15. API Caching Strategy

### Decision: Aggressive local caching with user-triggered refresh

**Alternatives Considered:**
- Always query API (fresh data, many API calls)
- Periodic auto-refresh (complexity)
- Only cache on explicit user action

**Chosen:** Cache everything locally, user can refresh any date range

**Rationale:**
**Problems with Always Query:**
- Slow performance
- API rate limits
- Unnecessary network traffic
- Offline doesn't work

**Benefits of Caching:**
- ✅ Fast app startup
- ✅ Works offline (view cached data)
- ✅ Reduced API calls
- ✅ Historical data preserved even if deleted from source
- ✅ Can reprocess data with different algorithms

**Implementation:**
- On first sync, fetch all data and cache
- Track last refresh per source in `data_source_refresh`
- User can "Refresh" button for specific date range
- Google Calendar uses incremental sync (sync tokens) for efficiency

**Benefits:**
- ✅ Predictable API usage
- ✅ Fast performance
- ✅ User control
- ✅ Offline-capable

**Trade-offs Accepted:**
- ✅ Data may be stale (acceptable, user can refresh)
- ✅ More local storage (acceptable)

---

## 16. Filtering vs. Hiding

### Decision: Store all source data, use `visible_as_event` flag

**Alternatives Considered:**
- Don't import filtered data at all
- Delete filtered data after import
- Separate "filtered" table

**Chosen:** Store all, flag `visible_as_event = FALSE` for filtered items

**Rationale:**
**Examples:**
- Toggl entries <5 minutes → `visible_as_event = FALSE`
- Calls <3 minutes → `visible_as_event = FALSE`
- Service = "Teams Audio" → `visible_as_event = FALSE`

**Why Store Filtered Data:**
- ✅ Complete audit trail
- ✅ Can change filtering rules later
- ✅ User can override (show hidden items)
- ✅ Data analysis (e.g., "How many short calls do I get?")

**Benefits:**
- ✅ Never lose data
- ✅ Flexible filtering
- ✅ Reversible decisions

**Trade-offs Accepted:**
- ✅ More database rows (acceptable, still small)
- ✅ Must filter in queries (mitigated by indexed column)

---

## 17. Error Handling Philosophy

### Decision: Fail gracefully with user-friendly messages and retry policies

**Alternatives Considered:**
- Strict (crash on any error)
- Silent (hide errors from user)
- Verbose (show all technical details)

**Chosen:** Graceful with context

**Principles:**
1. **Network errors:** Retry with exponential backoff (Polly)
2. **Auth errors:** Clear message + re-auth flow
3. **Data errors:** Log, show user, allow skip
4. **API limits:** Rate limit handling, queue requests
5. **User errors:** Validation before submission

**Logging:**
- All errors to `audit_log` table
- Structured logging with Serilog
- Debug logs in separate file

**User Messages:**
- Friendly language
- Actionable guidance
- Technical details in "More info" section

**Benefits:**
- ✅ Resilient to transient failures
- ✅ User understands what went wrong
- ✅ Complete audit trail for debugging

**Trade-offs Accepted:**
- ✅ More code complexity (worth it for UX)

---

## 18. Future Extensibility

### Decision: Design for extensibility from Day 1

**Principles:**
1. **Separate data layer** - Can add web/mobile UI later
2. **Plugin-style data sources** - Easy to add new sources
3. **Configurable algorithms** - Can adjust without code changes
4. **API-first design** - Core logic separate from UI
5. **Modular architecture** - Services, repositories, managers

**Examples:**
- New data source: Implement `IDataSource` interface
- New coalescing algorithm: Add to config + algorithm factory
- New UI: Reference shared data layer library

**Benefits:**
- ✅ Future-proof
- ✅ Easy to add Phase 2+ features
- ✅ Testable (mock data sources)
- ✅ Maintainable

**Trade-offs Accepted:**
- ✅ More initial architecture (worth it for long-term)
- ✅ May over-engineer for Phase 1 (acceptable, we know Phase 2+ is coming)

---

## Summary of Key Decisions

| Decision | Chosen Approach | Key Rationale |
|----------|----------------|---------------|
| UI Framework | WinUI 3 | Modern, future-proof, native performance |
| Database | SQLite + EF Core | Single file, portable, excellent tooling |
| Table Naming | Singular | .NET best practice, matches entity names |
| Approval | In-memory until publish | Simpler state machine, clearer to user |
| Version History | Own history + ETags | Google doesn't provide, rollback needed |
| Source Storage | Separate tables | Preserve everything, enable reprocessing |
| YouTube History | Takeout + API | API doesn't provide watch history |
| Outlook | Graph API + fallback | Works with work account, .ics as backup |
| Coalescing | Source-specific | Each source has different patterns |
| Rounding | 8/15 rule | More accurate than simple rounding |
| Date States | Multi-dimensional | Granular progress tracking |
| Weekly Status | Local + Excel sync | Automates existing workflow |
| Save/Restore | GCal state only | User-visible state, fast to create |
| Event Notation | Append on modify | Audit trail, visible in GCal |
| API Caching | Aggressive, user-refresh | Fast, offline-capable, user control |
| Filtering | Store all + flag | Never lose data, flexible |
| Error Handling | Graceful + retry | Resilient, user-friendly |
| Extensibility | Design for future | Phase 2+ features anticipated |

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Status:** Finalized for PRD Development

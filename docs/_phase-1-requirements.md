# Phase 1 Requirements - Google Calendar Management

**Version:** 1.0
**Date:** 2025-11-05
**Status:** Ready for PRD Development

## Project Vision

For the last 5 years, I've retroactively tracked every hour of my life in Google Calendar. This has proven very useful, but also time-consuming and frustrating with lots of human context switching when backfilling. I want to semi-automate the process of gathering all the information I already have about my previous activities into a local self-hosted server and have it update the Google Calendar as needed.

**Long-term Vision:**
Store 100% of activity data locally, using Google Calendar as the primary UI, with a complete set of tools to import, export, and navigate data developed over time.

---

## Phase 1 Scope

### Primary Goal
Create a local Windows .NET application with GUI that can:
1. Parse all data since the last confirmed update
2. Allow manual editing of event names, times, and details
3. Publish to Google Calendar after human approval
4. Track calendar contiguity and completion status

### Platform
- **Primary:** Windows desktop application
- **Secondary usage:** iOS and web via Google Calendar sync
- **Architecture:** Highly extensible for future data sources

---

## Phase 1 Data Sources

### 1. Toggl Track API ✅

**Source:** Toggl Track time tracking service
**Access:** RESTful API v9
**Data:** Years of granular time entries with descriptions, projects, tags

**Special Processing:**
- **Sleep data:** Entries with description "sleep" or "Sleep"
  - Rounded to nearest 15 minutes
  - Dedicated workflow for bulk import

- **Phone activity:** Entries named "Phone" or "ToDelete"
  - Apply sliding window coalescing algorithm
  - Merge multiple small activities into blocks

- **Other entries:** User selectively imports
  - Pre-filter entries <5 minutes (configurable)
  - Show remaining for user selection

**Current Status:**
Still actively used alongside Google Calendar. Toggl has minute/second precision, while Google Calendar rounds to 15 minutes.

---

### 2. iOS Call Logs ✅

**Source:** iMazing CSV export
**Access:** File upload
**Data:** Complete call history (incoming, outgoing, missed)

**CSV Format:**
- Call type
- Date & time (11/3/2025 5:39:26 PM format)
- Duration (HH:MM:SS format)
- Phone number
- Contact name
- Location (not used)
- Service

**Filtering Rules:**
- Duration < 3 minutes → Exclude
- Service = "Teams Audio" → Exclude
- Contact = "GV" → Exclude

**Event Format:**
```
Title: <Call type> call from <Contact> (<number>) at <datetime> for <duration>

Description:
Type: Incoming
Date: 2025-11-05
Time: 14:30
Duration: 5m 23s
Number: +1-555-1234
Contact: John Doe

Published by Google Calendar Management on 2025-11-05 16:45
```

**Processing:** Apply 8/15 rounding rule after filtering

---

### 3. Outlook Calendar (Work Calendar) ✅

**Source:** Microsoft Outlook (work account)
**Access:** Microsoft Graph API with OAuth 2.0 OR .ics file upload
**Data:** Work meetings and appointments

**Primary Method:** API Integration
- User authenticates with work account
- App fetches calendar events
- Refresh token valid for 90 days

**Fallback Method:** .ics file upload
- User exports calendar from Outlook
- App parses .ics format

**Publishing:** User reviews and selects which work events to add to personal Google Calendar

---

### 4. YouTube Watch History ✅

**Source:** Google Takeout JSON export
**Access:** File upload
**Data:** Complete YouTube viewing history

**Process:**
1. User manually downloads Google Takeout
   - Deselect all except "YouTube and YouTube Music"
   - History only, JSON format
   - Download `watch-history.json`

2. App parses JSON for:
   - Video IDs
   - Watch start times
   - Video titles (from Takeout)
   - Channel names (from Takeout)

3. App batch queries YouTube Data API v3 for:
   - Video duration (not in Takeout)
   - Additional metadata

4. Apply coalescing algorithm to create viewing session blocks

5. User approves generated events

**Event Format:**
```
Title: YouTube - <channel1>, <channel2>, <channel3>
Color: Yellow/Banana
Description: [Video links and details]
```

**Coalescing Algorithm:**
- Sliding window from first video start time
- Look for next video within (current video duration + 30 minutes)
- OK if videos overlap
- Window ends when no video found
- Total duration = first start to (last start + last duration)
- Apply 8/15 rounding rule

**Future Enhancement:** Chrome extension for real-time tracking (Phase 2+)

**Problem with Current Data:**
Previous years don't have Chrome extension tracking, so Takeout is necessary for historical backfill.

---

## Core Algorithms

### 8/15 Rounding Rule

**Purpose:** Convert precise time data to Google Calendar's 15-minute blocks

**Algorithm:**
1. Divide time span into 15-minute segments
2. For each segment, check if ≥8 minutes contains activity
3. Keep segments meeting threshold
4. Always include at least 1 segment (the one containing end time)
5. Merge consecutive kept segments into final event

**Example:**
```
Raw activity: 14:37:23 - 15:12:45

Segments:
- 14:30-14:45: 8 minutes (14:37-14:45) → KEEP
- 14:45-15:00: 15 minutes → KEEP
- 15:00-15:15: 13 minutes (15:00-15:13) → KEEP

Final event: 14:30-15:15
```

**Configuration:** Threshold of 8 minutes is configurable in database

---

### Phone Activity Coalescing

**Purpose:** Merge fragmented phone usage into cohesive blocks

**Input:** Toggl Track entries named "Phone" or "ToDelete"

**Algorithm:**
1. Sort entries by start time
2. **Primary sliding window:**
   - Start from first phone entry
   - Look ahead for next entry within 15 minutes
   - If found, extend window
   - If not found, close window

3. **Quality check:**
   - If window has <50% phone activity (too many gaps), retry with 5-minute gap threshold

4. **Filter:**
   - Discard windows <5 minutes total duration

5. **Chunking:**
   - Apply 8/15 rule to final window
   - Must show at least 1 chunk (end time)

6. **Event generation:**
   - Create single event in `gcal_event` table
   - Link all source entries in `generated_event_source`

**Rationale:**
iOS shortcuts create many tiny Toggl entries when switching apps frequently. This coalesces them into meaningful "phone usage" blocks while preserving granular data in database.

---

### YouTube Session Coalescing

**Purpose:** Group consecutive video watches into viewing sessions

**Algorithm:**
1. Sort videos by watch start time
2. **Sliding window:**
   - Start from first video
   - Calculate end time = start + video duration
   - Look for next video within (end time + 30 minutes)
   - If found, extend window to include it
   - Allow overlaps (next video starts before previous ends)
   - Continue until no video found within threshold

3. **Duration calculation:**
   - Total = first video start to (last video start + last video duration)

4. **Chunking:**
   - Apply 8/15 rule to total duration
   - Generate event with summary of channels watched

**Event Title Generation:**
- Format: `"YouTube - <channel1>, <channel2>, <channel3>"`
- Concatenate unique channel names with commas
- Future: Character limits based on event duration
  - <90 min: 40 chars
  - 90+ min: 80 chars
  - +40 chars per additional 30 min

**Future Enhancement:** LLM summarization of video topics

---

## Date State Management

### Contiguity Tracking

**Concept:** Track which dates have been "completely verified" vs. which still need work

**States per Date:**
1. `call_log_data_published` - Call logs imported and published
2. `sleep_data_published` - Sleep entries imported and published
3. `youtube_data_published` - YouTube sessions imported and published
4. `toggl_data_published` - Other Toggl entries imported and published
5. `named` - Date has an all-day name event
6. `complete_walkthrough_approval` - User has given final approval
7. `part_of_tracked_gap` - Explicitly marked as gap (to fill later)

**Contiguity Rule:**
```
Set contiguity_start_date (arbitrary, user-chosen)

For each date after contiguity_start_date:
    IF complete_walkthrough_approval == TRUE
        OR part_of_tracked_gap == TRUE
    THEN: Date is "verified"
    ELSE: Date is "unverified"

Edge of verified calendar = First unverified date
```

**"Fill to Present" Process:**
Starts at edge of verified calendar, works forward to current date.

---

### Gap Management

**Purpose:** Handle periods where data is missing or incomplete

**User Operations:**
1. **Mark gap:** User selects date range to designate as gap
2. **Split gap:** During backfill, user can split gap into two ranges
3. **Complete gap:** All dates in range approved → mark gap complete

**Use Case:**
"I didn't fill in dates for a week, then resumed. I'll mark that week as a gap and backfill it later."

**Storage:** Date ranges in `tracked_gap` table

---

## Core Workflows

### App Launch

**Display:**
1. Last contiguous fill date
2. Current date
3. Gap between them (days remaining)
4. Calendar view showing existing events
5. Sync button to refresh from Google Calendar

**UI Components:**
- Main calendar display (WinUI 3 CalendarView)
- Status panel showing contiguity edge
- Data source buttons (Sleep, Calls, YouTube, Toggl, Work Calendar)

---

### Data Import Workflows

#### 1. Sync from Google Calendar

**Purpose:** Load latest state from Google Calendar into local cache

**Process:**
1. Query Google Calendar API for changes since last sync
2. Store in `gcal_event` table
3. Create version history entries
4. Update `data_source_refresh` table

**Frequency:** On app launch, or user-triggered

---

#### 2. Fill Sleep Schedule

**Source:** Toggl Track entries named "sleep"

**Process:**
1. Fetch Toggl entries for date range
2. Filter for description containing "sleep" or "Sleep"
3. Round to nearest 15 minutes
4. Display in calendar view (distinct color)
5. User approves:
   - Individual events, or
   - Day-by-day, or
   - Date range bulk approval
6. Publish approved events to Google Calendar
7. Update `date_state.sleep_data_published = TRUE` for affected dates

---

#### 3. Upload iOS Call Logs

**Source:** iMazing CSV file

**Process:**
1. User clicks "Import Call Logs"
2. File picker opens
3. User selects CSV file
4. App parses CSV:
   - Parse datetime from iMazing format
   - Parse duration from HH:MM:SS
   - Store all records in `call_log_data` table
5. Apply filtering rules:
   - Duration ≥ 3 minutes
   - Service ≠ "Teams Audio"
   - Contact ≠ "GV"
6. Apply 8/15 rounding to filtered calls
7. Display in calendar view
8. User approves
9. Publish to Google Calendar
10. Update `date_state.call_log_data_published = TRUE`

---

#### 4. Update Work Calendar

**Source:** Outlook calendar via Microsoft Graph API

**Process:**
1. User clicks "Import Work Calendar"
2. If first time:
   - OAuth flow initiates
   - User logs in with work account
   - App stores refresh token
3. Fetch events from Outlook calendar
4. Display in calendar view
5. User selects which events to import (may not want all work events in personal calendar)
6. Publish selected events to Google Calendar
7. Update `date_state` (not a separate field, part of general tracking)

**Alternative Flow:** User uploads .ics file instead of API

---

#### 5. Pick Toggl Events

**Source:** Toggl Track non-sleep, non-phone entries

**Process:**
1. Fetch Toggl time entries for date range
2. Coalesce phone activities first (separate events)
3. Filter remaining entries:
   - Exclude sleep (handled separately)
   - Exclude <5 minutes (configurable)
4. Display filtered entries
5. User selects which to keep (checkboxes)
6. Apply 8/15 rounding to selected entries
7. User approves final list
8. Publish to Google Calendar
9. Update `date_state.toggl_data_published = TRUE`

---

#### 6. Import YouTube Watch History

**Source:** Google Takeout JSON + YouTube Data API

**Process:**
1. User clicks "Import YouTube History"
2. File picker for `watch-history.json`
3. App parses JSON:
   - Extract video IDs
   - Extract watch start times
   - Store in `youtube_data` table (without durations)
4. Batch query YouTube Data API for video metadata:
   - Duration
   - Channel details
   - Update `youtube_data` records
5. Apply coalescing algorithm to create viewing sessions
6. Display generated events in calendar
7. User can edit event titles before approval
8. User approves
9. Publish to Google Calendar
10. Update `date_state.youtube_data_published = TRUE`

---

### Approval & Publishing

**Key Principle:** All events require human approval before publishing to Google Calendar

**Approval Modes:**
1. **Event-by-event:** User reviews and approves individual events
2. **Day-by-day:** User approves all events for a specific day
3. **Date range:** User approves all events from start date to end date

**Approval in Memory:**
User selections stored in UI state, NOT in database until published.

**Publishing Process:**
1. User selects events to publish (in-memory approval)
2. User clicks "Publish" button
3. App sends batch request to Google Calendar API
4. For each event:
   - Send INSERT request
   - Receive `gcal_event_id` from Google
   - Store in database:
     - `gcal_event` table (full event data)
     - Source table's `published_gcal_event_id` field
     - `published_to_gcal = TRUE`
     - If generated event, create links in `generated_event_source`
5. Update `date_state` for affected dates
6. Create version history entry in `gcal_event_version`
7. Log operation in `audit_log`
8. Update `weekly_state` if week completed
9. Sync to Excel cloud if needed

---

### Save & Restore

**Purpose:** Allow user to rollback Google Calendar to previous state

**Save Process:**
1. User clicks "Create Save Point"
2. User enters name and description
3. App snapshots `gcal_event` table:
   - Event IDs
   - Summary, description, start, end, color
   - Current ETag
4. Store as JSON in `save_state` table

**Restore Process:**
1. User selects save point from list
2. App loads snapshot data
3. For each event in snapshot:
   - Compare current state to snapshot
   - If different, send UPDATE request to Google with snapshot values
   - Google generates new ETag (can't revert ETags)
   - Content is restored
4. User sees confirmation of events updated
5. Log restore operation in `audit_log`

**Note:** Undo only affects Google Calendar state. Local database has separate backup strategy.

---

## Weekly Status Tracking

### Purpose
Track weekly completion of different data source imports and sync status to Excel sheet.

### Weekly States

**Tracked Columns:**
1. Call Log Data
2. Job Calendar Data
3. Toggl Track / Real Time Data
4. Full Walkthrough Approval
5. Days Named

**Values:**
- `"Yes"` - All 7 days (Mon-Sun) completed
- `"Partial"` - ≥1 day completed, but not all 7
- `"No"` - 0 days completed

### Week Calculation

**ISO 8601 Standard:**
- Weeks start on Monday
- Week 1 = first week with ≥4 days in the new year
- Use `System.Globalization.ISOWeek.GetWeekOfYear()`

### Excel Cloud Sync

**User's Existing Excel Sheet:**
- Stored on Microsoft cloud (OneDrive/SharePoint)
- Each row = one week
- Columns for each data source status
- Additional columns (week names) not synced by app

**Sync Process:**
1. When a date's state changes, recalculate affected week's status
2. Determine if week is now "Yes", "Partial", or "No" for that column
3. If week status changed, update Excel via Microsoft Graph API
4. Bidirectional: Read user's manual edits on next sync

**Frequency:** Real-time after publishing events that change week status

---

## User Interface Requirements

### Main Window

**Layout:**
```
┌─────────────────────────────────────────────────┐
│  Google Calendar Management                     │
├─────────────────────────────────────────────────┤
│  Status Panel:                                   │
│  Last Contiguous Date: 2025-10-15               │
│  Current Date: 2025-11-05                       │
│  Days Remaining: 21                             │
│                                                  │
│  [ Sync from Google Calendar ]                  │
├─────────────────────────────────────────────────┤
│                                                  │
│           Calendar View                          │
│     (WinUI 3 CalendarView Component)            │
│                                                  │
│  - Shows existing GCal events                   │
│  - Shows generated/pending events               │
│    (yellow/banana color)                        │
│                                                  │
├─────────────────────────────────────────────────┤
│  Import Buttons:                                │
│  [ Fill Sleep ]  [ Call Logs ]                  │
│  [ Work Calendar ]  [ Toggl Events ]            │
│  [ YouTube History ]                            │
│                                                  │
│  [ Create Save Point ]  [ Restore ]             │
└─────────────────────────────────────────────────┘
```

### Event Display

**Existing Events:** Normal calendar colors
**Generated/Pending Events:** Yellow/banana color (before publish)
**Published by App:** Include notation in description

**Interaction:**
- Click event → Edit title, times, description
- Right-click → Approve, Reject, Edit
- Multi-select → Bulk approve

---

## Data Persistence

### Local Storage

**SQLite Database:** `{AppData}/GoogleCalendarManagement/calendar.db`
- All tables per schema document
- Regular backups

**Raw API Logs:** `logs/{source}/{datetime}_{call_type}.json`
- Debugging only
- Can be deleted

**Config File:** `config.json` (not tracked in git)
- API keys and secrets
- OAuth refresh tokens
- User preferences

### Backup Strategy

**Phase 1:**
- User manually copies database file
- Future: Automated backup to cloud storage

---

## Configuration

### User-Configurable Settings

**Stored in `config` table:**
- Minimum event duration (default: 5 minutes)
- Phone coalesce gap (default: 15 minutes)
- YouTube coalesce gap (default: 30 minutes)
- Call minimum duration (default: 3 minutes)
- 8/15 threshold (default: 8 minutes)
- Contiguity start date

**API Credentials:**
- Google Calendar Client ID & Secret
- YouTube API Key
- Toggl Track API Token
- Microsoft Graph Client ID & Tenant ID

---

## Future Phases (Documented for Extensibility)

### Phase 2+ Data Sources

1. **Chrome Extension for YouTube**
   - Real-time watch tracking
   - Export to JSON
   - Investigate existing extensions vs. custom

2. **Google Search History**
   - Heatmap/overlay visualization
   - Search queries over time

3. **Spotify Listen History**
   - Via stats.fm integration
   - Event descriptions include music listened during activity

4. **Apple Screen Time**
   - App usage tracking
   - Alternative browser tracking solution

5. **Google Maps Timeline**
   - Location history integration
   - Existing HTML viewer integration

6. **Physical NFC Tags**
   - Real-world buttons to start/stop activities
   - IoT integration

7. **Google Takeout (General)**
   - Automated periodic exports

### Phase 2+ Features

1. **24/7 Server Backend**
   - Background sync
   - Email reminders when too long since last update

2. **Google AppScripts Integration**
   - Auto-recolor events based on rules

3. **Data Analysis Dashboard**
   - Time budgets
   - Activity breakdowns
   - Colored activity reports
   - Top activities by time spent
   - Alerts for over-budget activities

4. **Fuzzy Search**
   - Index all previous event names
   - Fast search across years of data

5. **Privacy Layers**
   - Secret events (local only, not in GCal)
   - Fake events (different content in GCal vs. local)
   - Encryption of sensitive data

6. **Custom Local UI**
   - If moving away from Google Calendar
   - Full-featured calendar application
   - Local-first with optional GCal sync

7. **Chrome Extension / GCal Add-on**
   - Enhanced Google Calendar UI
   - Additional features in main GCal interface

8. **Webhooks**
   - Integration with Toggl Track
   - Other service integrations

---

## Success Criteria - Phase 1

### Minimum Viable Product

**Must Have:**
1. ✅ Import and publish Toggl Track data (sleep, phone, other)
2. ✅ Import and publish iOS call logs
3. ✅ Import and publish YouTube watch history
4. ✅ Import and publish Outlook calendar events
5. ✅ Display all data in calendar view
6. ✅ Human approval before publishing
7. ✅ Track date states and contiguity
8. ✅ Save/restore Google Calendar state
9. ✅ Sync weekly status to Excel

**User Workflow Validation:**
1. User can load app and see current contiguity status
2. User can import each data source successfully
3. User can review and edit generated events
4. User can publish approved events to Google Calendar
5. User can track progress (which dates are complete)
6. User can rollback to previous save point

### Performance Targets

- Import 1,000 Toggl entries: <10 seconds
- Import 1,000 YouTube videos: <30 seconds (API calls)
- Display calendar month: <1 second
- Publish 100 events to GCal: <15 seconds

---

## Known Limitations - Phase 1

1. **YouTube Watch History:** Manual Google Takeout download required
2. **Outlook Calendar:** Refresh token expires every 90 days (re-auth needed)
3. **Toggl API:** 3-month historical limit (requires pagination for large ranges)
4. **Google Calendar:** No version history from Google (we maintain our own)
5. **Excel Sync:** Requires Microsoft account authentication
6. **Windows Only:** No Mac/Linux support in Phase 1

---

## Risk Mitigation

### API Rate Limits

**Toggl Track:**
- Implement retry with exponential backoff
- Paginate large date ranges
- Cache aggressively

**Google Calendar:**
- Use batch requests
- Incremental sync with sync tokens
- 1M queries/day limit (more than sufficient)

**YouTube Data API:**
- Batch video metadata requests (50 IDs at once)
- Cache all fetched metadata

### Data Loss Prevention

**Strategy:**
1. All source data preserved in database
2. Never delete source data automatically
3. Save states for rollback
4. Audit log for all operations
5. User-triggered database backup

### Authentication Failures

**Handling:**
1. Graceful error messages
2. Re-authentication flows
3. Refresh token storage and renewal
4. Fallback to file upload where applicable

---

## Development Phases

### Phase 1.1: Foundation
- Database schema implementation
- EF Core setup and migrations
- Basic WinUI 3 UI with calendar view
- Configuration management

### Phase 1.2: Google Calendar Integration
- OAuth flow
- Sync from GCal
- Publish to GCal
- Version history tracking

### Phase 1.3: Toggl Track Integration
- API client implementation
- Sleep data import
- Phone activity coalescing
- Other entry selection

### Phase 1.4: Call Logs & YouTube
- iMazing CSV parser
- YouTube Takeout parser
- YouTube API metadata fetch
- Coalescing algorithms

### Phase 1.5: Outlook & Excel
- Microsoft Graph OAuth
- Calendar event fetch
- Excel cloud sync

### Phase 1.6: State Management
- Date state tracking
- Contiguity calculation
- Gap management
- Weekly status updates

### Phase 1.7: Save/Restore & Polish
- Save point creation
- Rollback functionality
- UI refinements
- Error handling
- User testing

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Next Step:** Create Product Brief, then PRD

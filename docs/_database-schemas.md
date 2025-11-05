# Database Schemas - Google Calendar Management

**Database Type:** SQLite
**ORM:** Entity Framework Core (.NET 9)
**Date Created:** 2025-11-05

## Overview

This document contains the complete database schema design for the Google Calendar Management application. The system uses SQLite for local data storage with separate audit logging for debugging.

## Schema Design Principles

1. **Singular table names** - Following best practices
2. **Separate raw data from published state** - Source data tables store original API responses
3. **Version history tracking** - Full audit trail for Google Calendar events
4. **ISO 8601 compliance** - Week calculations using standard
5. **ETags for sync** - Google Calendar's version identifiers
6. **Local caching** - All API data cached locally for performance

---

## Core Tables

### 1. date_state

Tracks the publication and approval state for each date.

```sql
CREATE TABLE date_state (
    date DATE PRIMARY KEY,

    -- Data source publication states (only TRUE when published to GCal)
    call_log_data_published BOOLEAN DEFAULT FALSE,
    sleep_data_published BOOLEAN DEFAULT FALSE,
    youtube_data_published BOOLEAN DEFAULT FALSE,
    toggl_data_published BOOLEAN DEFAULT FALSE,

    -- Other states
    named BOOLEAN DEFAULT FALSE,  -- Has all-day name event
    named_event_gcal_id TEXT,  -- Reference to all-day name event
    complete_walkthrough_approval BOOLEAN DEFAULT FALSE,
    part_of_tracked_gap BOOLEAN DEFAULT FALSE,

    -- Timestamps (separate for each data source)
    call_log_data_published_at DATETIME,
    sleep_data_published_at DATETIME,
    youtube_data_published_at DATETIME,
    toggl_data_published_at DATETIME,
    named_at DATETIME,
    complete_walkthrough_approval_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (named_event_gcal_id) REFERENCES gcal_event(gcal_event_id)
);
```

**Purpose:** Central tracking of what data has been published for each date. Used to calculate contiguity and determine what work remains.

**Key Fields:**
- `*_published` - Only set TRUE when data is actually published to Google Calendar
- `complete_walkthrough_approval` - Final approval state for the date
- `part_of_tracked_gap` - Explicitly marked as part of a gap (to be filled later)

**Contiguity Rule:**
Starting from `contiguity_start_date`, every date must have `complete_walkthrough_approval = TRUE` OR `part_of_tracked_gap = TRUE` to maintain contiguity.

---

### 2. tracked_gap

Stores date ranges that are explicitly marked as gaps in the calendar.

```sql
CREATE TABLE tracked_gap (
    gap_id INTEGER PRIMARY KEY AUTOINCREMENT,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,

    backfill_completed BOOLEAN DEFAULT FALSE,
    completed_at DATETIME,

    -- Track if this gap was created via split
    split_date DATETIME DEFAULT NULL,

    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Purpose:** Tracks gaps in complete walkthrough approvals. Users can mark date ranges as gaps to fill in asynchronously.

**Operations:**
- **Create** - User marks date range as gap
- **Split** - User can split gap into two ranges during backfill (delete original, create two new with same `created_at`, set `split_date`)
- **Complete** - Set `backfill_completed = TRUE` when all dates in range approved

---

### 3. gcal_event

Google Calendar events cache with metadata.

```sql
CREATE TABLE gcal_event (
    gcal_event_id TEXT PRIMARY KEY,  -- Google's event ID
    calendar_id TEXT NOT NULL,
    summary TEXT,
    description TEXT,
    start_datetime DATETIME,
    end_datetime DATETIME,
    is_all_day BOOLEAN,
    color_id TEXT,

    -- Version tracking
    gcal_etag TEXT,  -- Google's version identifier
    gcal_updated_at DATETIME,
    is_deleted BOOLEAN DEFAULT FALSE,

    -- Source tracking
    source_system TEXT,  -- 'gcal', 'toggl', 'youtube', 'call_log', 'manual', 'generated'
    app_published BOOLEAN DEFAULT FALSE,
    app_published_at DATETIME,
    app_last_modified_at DATETIME,  -- Updated every time app modifies event

    -- Recurring event tracking
    recurring_event_id TEXT,
    is_recurring_instance BOOLEAN DEFAULT FALSE,

    -- Sync tracking
    last_synced_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_gcal_event_date ON gcal_event(start_datetime, end_datetime);
CREATE INDEX idx_gcal_recurring ON gcal_event(recurring_event_id);
CREATE INDEX idx_gcal_source ON gcal_event(source_system);
```

**Purpose:** Local cache of all Google Calendar events, including those published by the app and those synced from Google.

**Key Fields:**
- `gcal_etag` - Google's version identifier (used for conflict detection, not rollback)
- `source_system` - Tracks which system created the event
- `app_published` - TRUE if this app published the event
- `recurring_event_id` - Links instances of recurring events to parent

**Event Description Format:**
All app-published events include at the end:
```
Published by Google Calendar Management on {datetime}
```
Updated every time the app modifies the event.

**Important:** Google Calendar API does NOT provide version history or rollback. We maintain our own history in `gcal_event_version`.

---

### 4. gcal_event_version

Full version history for Google Calendar events.

```sql
CREATE TABLE gcal_event_version (
    version_id INTEGER PRIMARY KEY AUTOINCREMENT,
    gcal_event_id TEXT NOT NULL,
    gcal_etag TEXT,  -- Google's etag at this version

    -- Snapshot of event data
    summary TEXT,
    description TEXT,
    start_datetime DATETIME,
    end_datetime DATETIME,
    is_all_day BOOLEAN,
    color_id TEXT,

    -- Change tracking
    changed_by TEXT,  -- 'user_approval', 'gcal_sync', 'rollback', 'manual_edit'
    change_reason TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (gcal_event_id) REFERENCES gcal_event(gcal_event_id)
);

CREATE INDEX idx_version_event ON gcal_event_version(gcal_event_id, created_at DESC);
```

**Purpose:** Maintains complete version history for rollback functionality.

**Rollback Process:**
1. User selects save state
2. App reads snapshot from `save_state` table
3. For each event, sends UPDATE request to Google with previous values
4. Google generates new etag (cannot revert etags themselves)
5. Content is restored to previous state

**Note:** ETags are opaque strings from Google used for optimistic concurrency control, not version numbers. We use our own `version_id` for ordering.

---

## Data Source Tables

### 5. toggl_data

Toggl Track time entries cache.

```sql
CREATE TABLE toggl_data (
    toggl_id INTEGER PRIMARY KEY,  -- Toggl's time entry ID
    description TEXT,
    start_time DATETIME NOT NULL,
    end_time DATETIME,
    duration_seconds INTEGER,
    project_name TEXT,
    tags TEXT,  -- JSON array

    -- Processing
    visible_as_event BOOLEAN DEFAULT TRUE,  -- FALSE if filtered out (<5 min)
    published_to_gcal BOOLEAN DEFAULT FALSE,
    published_gcal_event_id TEXT,

    -- Sync tracking
    last_synced_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (published_gcal_event_id) REFERENCES gcal_event(gcal_event_id)
);

CREATE INDEX idx_toggl_date ON toggl_data(start_time, end_time);
CREATE INDEX idx_toggl_description ON toggl_data(description);
```

**Purpose:** Stores raw Toggl Track time entries. Processing logic determines which entries become calendar events.

**Processing Rules:**
- **Sleep entries:** Description contains "sleep" or "Sleep" → special workflow
- **Phone entries:** Description = "Phone" or "ToDelete" → coalescing algorithm
- **Other entries:** User selects which to publish
- **Filtering:** Entries <5 minutes → `visible_as_event = FALSE` (still saved)

**Coalescing (Phone Activity):**
1. Sliding window from first to last phone entry (exact minute times)
2. Auto-stop at 15+ minute gaps
3. If <50% phone activity, retry with 5-minute gap threshold
4. Discard windows <5 minutes total
5. Apply 8/15 chunking rule
6. Create generated event in `gcal_event` table
7. Link sources in `generated_event_source`

---

### 6. youtube_data

YouTube watch history cache.

```sql
CREATE TABLE youtube_data (
    youtube_id INTEGER PRIMARY KEY AUTOINCREMENT,
    video_id TEXT NOT NULL,
    video_title TEXT,
    channel_name TEXT,
    channel_id TEXT,
    watch_start_time DATETIME NOT NULL,
    video_duration_seconds INTEGER,
    video_description TEXT,

    -- Processing
    visible_as_event BOOLEAN DEFAULT TRUE,
    published_to_gcal BOOLEAN DEFAULT FALSE,
    published_gcal_event_id TEXT,

    last_synced_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (published_gcal_event_id) REFERENCES gcal_event(gcal_event_id)
);

CREATE INDEX idx_youtube_date ON youtube_data(watch_start_time);
```

**Purpose:** Stores YouTube watch history parsed from Google Takeout JSON and enriched with metadata from YouTube Data API v3.

**Data Flow:**
1. User exports Google Takeout (YouTube history in JSON format)
2. App parses JSON for video IDs and watch start times
3. Batch query YouTube Data API v3 for video metadata (duration, channel)
4. Cache everything in this table
5. Apply coalescing algorithm to generate events

**Coalescing Algorithm:**
1. Sliding window from first video start time
2. Look for next video within (current video duration + 30 minutes)
3. OK if videos overlap
4. Window ends when no video found within threshold
5. Total duration = first start to (last start + last duration)
6. Apply 8/15 rule to generated event
7. Event format: `"YouTube - <channel1>, <channel2>, ..."`

**Character Limits (Future):**
- <90 minutes: 40 characters
- 90+ minutes: 80 characters
- +40 characters per additional 30 minutes

---

### 7. call_log_data

iOS call logs from iMazing export.

```sql
CREATE TABLE call_log_data (
    call_id INTEGER PRIMARY KEY AUTOINCREMENT,
    call_type TEXT,  -- 'Incoming', 'Outgoing', 'Missed'
    call_datetime DATETIME NOT NULL,  -- Parsed from iMazing date column
    duration_seconds INTEGER,  -- Parsed from HH:MM:SS format
    phone_number TEXT,
    contact_name TEXT,
    location TEXT,  -- From iMazing but not used
    service TEXT,

    -- Processing
    visible_as_event BOOLEAN DEFAULT TRUE,  -- FALSE if filtered
    published_to_gcal BOOLEAN DEFAULT FALSE,
    published_gcal_event_id TEXT,

    -- Import tracking
    imported_from_file TEXT,
    imported_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (published_gcal_event_id) REFERENCES gcal_event(gcal_event_id)
);

CREATE INDEX idx_call_date ON call_log_data(call_datetime);
```

**Purpose:** Stores call logs exported from iMazing (iOS call history).

**CSV Format (iMazing):**
- Call type, Date (11/3/2025 5:39:26 PM), Duration (00:01:26), Number, Contact, Location, Service

**Filtering Rules:**
- Duration < 3 minutes → `visible_as_event = FALSE`
- Service = "Teams Audio" → `visible_as_event = FALSE`
- Contact = "GV" → `visible_as_event = FALSE`

**Event Format:**
```
<Call type> call from <Contact> (<number>) at <datetime> for <duration>

[Full details in description:]
Type: Incoming
Date: 2025-11-05
Time: 14:30
Duration: 5m 23s
Number: +1-555-1234
Contact: John Doe

Published by Google Calendar Management on 2025-11-05 16:45
```

**Processing:** Apply 8/15 rounding rule after filtering.

---

### 8. generated_event_source

Links generated events to their source data.

```sql
CREATE TABLE generated_event_source (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    gcal_event_id TEXT NOT NULL,
    source_table TEXT NOT NULL,  -- 'toggl_data', 'youtube_data', etc.
    source_id INTEGER NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    FOREIGN KEY (gcal_event_id) REFERENCES gcal_event(gcal_event_id)
);

CREATE INDEX idx_generated_gcal ON generated_event_source(gcal_event_id);
CREATE INDEX idx_generated_source ON generated_event_source(source_table, source_id);
```

**Purpose:** Tracks which source records contributed to each generated event (coalesced phone activity, YouTube blocks, etc.).

**Usage:**
- When coalescing creates a single event from multiple source records
- Query this table to trace back from published event to source data
- Example: One "Phone" event might link to 15 individual Toggl entries

---

## System Tables

### 9. audit_log

Complete audit trail of all operations.

```sql
CREATE TABLE audit_log (
    log_id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    operation_type TEXT NOT NULL,  -- 'sync', 'publish', 'approve', 'undo', 'save', 'import', 'rollback'
    operation_details TEXT,  -- JSON with operation specifics
    affected_dates TEXT,  -- Date or date range
    affected_events TEXT,  -- JSON array of event IDs
    user_action BOOLEAN,  -- TRUE if user initiated, FALSE if automatic
    success BOOLEAN,
    error_message TEXT
);

CREATE INDEX idx_audit_timestamp ON audit_log(timestamp);
CREATE INDEX idx_audit_operation ON audit_log(operation_type);
```

**Purpose:** Comprehensive logging of all system operations for debugging and compliance.

**Operation Types:**
- `sync` - API data refresh
- `publish` - Events published to Google Calendar
- `approve` - User approval actions
- `undo` - Rollback operations
- `save` - Save state creation
- `import` - File import (CSV, JSON)
- `rollback` - Restore from save state

---

### 10. system_state

Application-level state storage.

```sql
CREATE TABLE system_state (
    state_id INTEGER PRIMARY KEY AUTOINCREMENT,
    state_name TEXT UNIQUE,
    state_value TEXT,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Purpose:** Key-value store for application state.

**Examples:**
- `contiguity_start_date` - Starting point for contiguity checking
- `last_gcal_sync` - Last time Google Calendar was synced
- `default_calendar_id` - Primary calendar ID

---

### 11. save_state

Snapshots for rollback functionality.

```sql
CREATE TABLE save_state (
    save_id INTEGER PRIMARY KEY AUTOINCREMENT,
    save_name TEXT NOT NULL,
    save_description TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,

    -- JSON snapshot: {gcal_event_id: {summary, description, start, end, color, etag}}
    snapshot_data TEXT
);
```

**Purpose:** Stores snapshots of Google Calendar state for rollback.

**Snapshot Content (JSON):**
```json
{
  "event_id_1": {
    "summary": "Event Title",
    "description": "Event description...",
    "start_datetime": "2025-11-05T14:30:00Z",
    "end_datetime": "2025-11-05T15:30:00Z",
    "color_id": "9",
    "etag": "\"3381649226364000\""
  }
}
```

**Note:** Only snapshots `gcal_event` table. Local data backups handled separately.

---

### 12. config

Application configuration storage.

```sql
CREATE TABLE config (
    config_key TEXT PRIMARY KEY,
    config_value TEXT,
    config_type TEXT,  -- 'string', 'integer', 'boolean', 'json'
    description TEXT,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**Purpose:** Configurable application settings.

**Examples:**
```sql
INSERT INTO config VALUES
    ('min_event_duration_minutes', '5', 'integer', 'Minimum duration to show events'),
    ('phone_coalesce_gap_minutes', '15', 'integer', 'Max gap for phone coalescing'),
    ('youtube_coalesce_gap_minutes', '30', 'integer', 'Gap after video duration for YouTube'),
    ('call_min_duration_minutes', '3', 'integer', 'Minimum call duration to import'),
    ('youtube_char_limit_short', '40', 'integer', 'Char limit for events <90min'),
    ('eight_fifteen_threshold', '8', 'integer', 'Minutes required in 15-min block');
```

---

### 13. weekly_state

Weekly status tracking for Excel sync.

```sql
CREATE TABLE weekly_state (
    week_id INTEGER PRIMARY KEY AUTOINCREMENT,
    week_start_date DATE UNIQUE NOT NULL,  -- Always a Monday
    week_number INTEGER NOT NULL,  -- ISO 8601 week (1-53)
    year INTEGER NOT NULL,

    -- Status columns (Yes/Partial/No)
    call_log_status TEXT CHECK(call_log_status IN ('Yes', 'Partial', 'No', NULL)),
    job_calendar_status TEXT CHECK(job_calendar_status IN ('Yes', 'Partial', 'No', NULL)),
    toggl_realtime_status TEXT CHECK(toggl_realtime_status IN ('Yes', 'Partial', 'No', NULL)),
    walkthrough_approval_status TEXT CHECK(walkthrough_approval_status IN ('Yes', 'Partial', 'No', NULL)),
    days_named_status TEXT CHECK(days_named_status IN ('Yes', 'Partial', 'No', NULL)),

    -- Sync tracking
    excel_last_synced_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_week_date ON weekly_state(week_start_date);
CREATE INDEX idx_week_number ON weekly_state(year, week_number);
```

**Purpose:** Tracks weekly completion status for Excel cloud sync.

**Week Calculation:**
Use `System.Globalization.ISOWeek.GetWeekOfYear()` for ISO 8601 compliance:
- Weeks start on Monday
- Week 1 contains at least 4 days of the new year

**Status Values:**
- `"Yes"` - All 7 days (Mon-Sun) have this state completed
- `"Partial"` - ≥1 day completed, but not all 7
- `"No"` - 0 days completed

**Excel Sync:**
Auto-update Microsoft cloud Excel sheet via Microsoft Graph API when week state changes.

---

### 14. data_source_refresh

Tracks API cache refresh operations.

```sql
CREATE TABLE data_source_refresh (
    refresh_id INTEGER PRIMARY KEY AUTOINCREMENT,
    source_name TEXT NOT NULL,  -- 'toggl', 'gcal', 'youtube', 'call_log'
    start_date DATE,
    end_date DATE,
    last_refreshed_at DATETIME,
    records_fetched INTEGER,
    success BOOLEAN,
    error_message TEXT
);

CREATE INDEX idx_refresh_source ON data_source_refresh(source_name, last_refreshed_at);
```

**Purpose:** Tracks when API data was last refreshed for each source and date range.

**Usage:**
- User can refresh cache for any date range
- System tracks last refresh to avoid unnecessary API calls
- Error tracking for failed refreshes

---

## File System Storage

### Raw API Responses

**Location:** `logs/{source}/{YYYY-MM-DD_HHmmss}_{api_call_type}.json`

**Examples:**
- `logs/toggl/2025-11-05_143022_time_entries.json`
- `logs/gcal/2025-11-05_143045_list_events.json`
- `logs/youtube/2025-11-05_143100_video_metadata.json`

**Purpose:** Debugging and audit trail. Deletable at any time for space.

### Outlook Calendar Imports

**Location:** `data/outlook_imports/{filename}.ics`

**Purpose:** Store .ics files if user chooses file upload instead of API integration.

---

## Key Algorithms

### 8/15 Rounding Rule

For any time block, chunk into 15-minute segments. Keep segments with ≥8 minutes of activity. Always show at least one chunk (the one containing end time).

**Example:**
```
Activity: 14:37 - 15:12 (35 minutes)

15-minute blocks:
- 14:30-14:45: 8 minutes activity (14:37-14:45) → KEEP
- 14:45-15:00: 15 minutes activity → KEEP
- 15:00-15:15: 12 minutes activity (15:00-15:12) → KEEP

Result: 14:30-15:15 (rounded event)
```

### Database Naming Conventions

- **Table names:** Singular, lowercase with underscores
- **Boolean fields:** Descriptive (e.g., `published_to_gcal` not just `published`)
- **Timestamps:** Suffix with `_at` (e.g., `created_at`, `published_at`)
- **Foreign keys:** Reference full field name (e.g., `published_gcal_event_id`)
- **Indexes:** Prefix with `idx_` followed by table and field(s)

---

## Migration Strategy

**Entity Framework Core Migrations:**

1. Initial migration creates all tables
2. Future schema changes via EF Core migrations
3. Seed data for `config` table with default values
4. ISO 8601 week calculation helpers in code, not database

**Version Control:**
- Track migrations in git
- Document breaking changes in migration comments
- Backup database before applying migrations in production

---

## Performance Considerations

**Indexes:**
- All date range queries indexed
- Foreign keys indexed automatically
- Consider composite indexes for common queries

**Query Optimization:**
- Use `AsNoTracking()` for read-only queries
- Batch inserts for large imports
- Connection pooling enabled

**Data Volume Estimates:**
- 5 years × 365 days = ~1,825 date_state records
- Toggl entries: ~10,000+ (multiple per day)
- YouTube: ~5,000+ videos
- Call logs: ~3,000+ calls
- GCal events: ~15,000+ (all sources combined)

**SQLite is well-suited for this data volume** with proper indexing.

---

## Future Enhancements

**Planned Schema Additions:**
1. Spotify listening history (`spotify_data`)
2. Google search history (`google_search_data`)
3. Apple Screen Time data (`screen_time_data`)
4. Google Maps timeline (`maps_timeline_data`)
5. NFC tag tracking (`nfc_activity_data`)
6. Privacy levels/views (encryption fields)

**Encryption:**
Future phase will add:
- Encrypted `description` fields for sensitive events
- Privacy level markers (`public`, `private`, `secret`)
- Alternate event data for "fake" public events

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Status:** Phase 1 Design - Ready for Implementation

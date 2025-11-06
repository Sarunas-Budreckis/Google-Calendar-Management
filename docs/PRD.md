# Google Calendar Management - Product Requirements Document

**Author:** Sarunas Budreckis
**Date:** 2025-11-06
**Version:** 1.0

---

## Executive Summary

Google Calendar Management transforms the tedious process of retroactive life tracking into a meaningful and **fun ritual**. After 5 years of manually tracking every hour across multiple data sources (Toggl Track, iOS call logs, YouTube history, Outlook calendar), the cognitive overload has reached a breaking point. This Windows desktop application consolidates all sources into a single-pane-of-glass view with an intelligent approval workflow.

The transformation: From juggling 6+ browser tabs with fragmented context → To a streamlined "spaced repetition for life" experience where reviewing events becomes an opportunity to reinforce memories. The visual color-coded calendar serves as a personal KPI dashboard revealing patterns in mental modes: passive consumption, education, social activities, work, and personal growth.

Built for decades of use with local-first architecture, this isn't just about efficiency - it's about **reducing friction to enable joy** and making the backfilling process genuinely enjoyable in a practice you're committed to maintaining for life.

### What Makes This Special

The special moment that makes this product unique is when **backfilling transforms from dreaded chore into a fun, nostalgic life review ritual** - like spaced repetition for lived experiences. When you see all your data sources unified in one view and click to approve an event, you're not just doing data entry - you're reinforcing the memory: "Oh right, that's when I talked to Mom for an hour" or "I watched 3 videos on Rust programming - that was a good evening."

The color-coded calendar becomes a **visual autobiography** with **aesthetic beauty** at its core - a consciousness taxonomy where colors represent mental states (Azure for eudaimonia, Yellow for passive consumption, Navy for building your future). Looking at a week reveals life balance at a glance as a beautiful, meaningful tapestry.

The experience is designed to be satisfying and enjoyable - like clicking through inbox zero, but for life moments. The magic compounds over decades: What takes 2-4 tedious hours today becomes <1 hour of engaging, fulfilling reflection. The upfront investment creates a lifetime of compounding value and joy.

---

## Project Classification

**Technical Type:** Desktop Application (Windows)
**Domain:** Personal Productivity / Life Tracking
**Complexity:** Medium (Standard desktop app with API integrations)

This is a **Windows native desktop application** using WinUI 3 with:
- Local-first architecture (SQLite database)
- Multiple API integrations (Google Calendar, Toggl Track, YouTube Data, Microsoft Graph)
- Human-in-the-loop automation (approval workflows)
- Long-term personal use (decades timeframe)
- Single-user focus (no multi-tenancy concerns)

---

## Success Criteria

### Primary Success: Experience Transformation

**The real win:** Backfilling shifts from dreaded chore to satisfying, fun ritual that you look forward to.

**Qualitative Indicators:**
- Weekly calendar review becomes nostalgic life reflection
- Spaced repetition effect: "Oh right, that's when X happened"
- Process feels engaging and fulfilling, not draining
- Backfilling becomes a regular weekly habit (not avoided)

**Measurable Outcomes:**
- **Time efficiency:** <1 hour/week to backfill (down from current 2-4 hours) - 50-75% reduction
- **Consistency:** Weekly backfilling maintained for 3+ consecutive months
- **Contiguity edge:** Last complete date stays within 7 days of present (currently 2+ weeks behind)
- **Data completeness:** No "lost weeks" - all gaps explicitly tracked and managed

### Data Quality Goals

**Calendar Coverage:**
- All 4 Phase 1 data sources (Toggl, calls, YouTube, Outlook) regularly imported
- Events correctly categorized by mental state color
- Honest tracking (no aspirational coloring)
- Pattern recognition enabled by accurate data

**Operational Success:**
- Single application replaces juggling 6+ browser tabs
- All data visible in unified view
- Confident approval decisions (not exhausting guesswork)
- Events appear correctly in Google Calendar with no data loss

### Long-Term Sustainability

**The ultimate success metric: Still using this in 10 years**

Measured by:
- Maintained regular backfilling habit
- System extensibility allows new data sources as life evolves
- Local data ownership provides independence from platform changes
- Pattern insights become effortless with local API and filtering
- Beautiful visual representation maintains aesthetic appeal

**Not measured by:**
- User count, revenue, market share (personal project)
- Generic productivity metrics
- Arbitrary KPIs - this is about lived experience quality

---

## Product Scope

### MVP - Minimum Viable Product (Phase 1)

**Core Value:** Single-pane view with approval workflow for 4 data sources

**Data Source Integration:**
1. **Toggl Track API** - Time entries with phone activity coalescing (sliding window, 8/15 rounding)
2. **iOS Call Logs** - iMazing CSV parsing with duration filtering
3. **YouTube Watch History** - Google Takeout JSON with session coalescing and video metadata
4. **Outlook Calendar** - Microsoft Graph API OAuth 2.0 (or .ics fallback)

**Core Workflows:**
- **Single Calendar View:** WinUI 3 CalendarView displaying existing Google Calendar events + pending approvals (yellow/banana overlay)
- **Approval Workflow:** Click to edit events, select individual/day/range to approve, batch publish to Google Calendar
- **8/15 Rounding Algorithm:** Divide time into 15-min blocks, keep blocks with ≥8 minutes activity
- **Phone Activity Coalescing:** Sliding window with 15-min gap auto-stop, quality checks
- **YouTube Session Coalescing:** Sliding window including videos within (duration + 30 min)
- **Date State Tracking:** Per-date flags (sleep_published, youtube_published, etc.), contiguity edge calculation
- **Save/Restore System:** Create save points, rollback to previous state, undo recent publishes

**Data Management:**
- **Local SQLite Database:** 14 tables storing all data with complete version history
- **App-Published Notation:** Append "Published by Google Calendar Management on {datetime}" to event descriptions
- **Weekly Status Tracking:** Calculate completion per data source, sync to Excel cloud via Microsoft Graph

**Must Work For Launch:**
- All 4 data sources successfully import and publish
- Single-app experience (no tab juggling)
- Reliable save/restore functionality
- No data loss
- Backfilling 1 week takes <2 hours

### Growth Features (Phase 2+)

**Additional Data Sources:**
- Chrome extension for real-time YouTube tracking (eliminate delay)
- Google search history with heatmap overlay
- Spotify via stats.fm (music context for events)
- Apple Screen Time (app usage patterns)
- Google Maps timeline (location context)
- Physical NFC tags (manual activity triggers)

**Enhanced Workflows:**
- "Fill to present" automated workflow from contiguity edge
- Bulk editing operations (multi-event updates)
- Smart suggestions based on patterns
- Conflict detection and resolution

**Privacy & Sharing:**
- Secret events (local only, not in Google Calendar)
- Fake event substitution (public vs private view)
- Multiple audience views
- Encryption for sensitive data

**Analysis & Insights:**
- Time budget dashboards (% by color, actual vs desired)
- Pattern correlations (sleep → productivity, etc.)
- Multi-year trend visualization
- Weekly Obsidian dashboard integration
- "When did I do that?" searchable index

### Vision (Decade Future)

**Platform Expansion:**
- Web interface (view local data from browser)
- Mobile apps (iOS/Android viewers, quick entry)
- Custom local UI (independence from Google Calendar)
- Public API for integration with other tools

**Advanced Features:**
- Color system evolution (split mental state from activity type)
- Sub-colors and gradients
- AI-powered color suggestions
- 24/7 server backend with email reminders
- Google AppScripts integration (auto-recolor)
- Fuzzy search across decade of events

**The Decade Vision:**
In 10 years, this system contains a decade of rich personal history, supports evolved color taxonomy as life changes, integrates with technologies that don't exist yet, remains independent of platform changes, and enables insights impossible to see today.

---

## Desktop Application Specific Requirements

### Platform Support

**Target Platform:** Windows 10/11 (version 1809 or later)

**Framework:** .NET 9 with WinUI 3 (Windows App SDK)
- Modern native Windows UI framework
- Access to platform-specific features
- High-performance rendering for calendar views
- Native Windows integration (notifications, file system)

**No Cross-Platform Support in MVP:**
- Windows-only for Phase 1 (single user, known environment)
- Architecture separates UI from data layer for future portability
- Local-first design enables platform expansion later (web, mobile)

### System Integration

**File System Access:**
- Import CSV files (iOS call logs via iMazing export)
- Import JSON files (YouTube history via Google Takeout)
- Local SQLite database storage (user data directory)
- Optional .ics file import (Outlook calendar fallback)

**API Integrations:**
- **Google Calendar API:** OAuth 2.0, event CRUD operations, batch requests
- **Toggl Track API:** API token authentication, time entry queries
- **YouTube Data API:** API key authentication, video metadata retrieval
- **Microsoft Graph API:** OAuth 2.0, calendar access, Excel cloud sync

**System Notifications:**
- OAuth token expiration warnings (Outlook 90-day refresh)
- Import completion notifications
- Error alerts for API failures

**Clipboard Integration:**
- Copy event details for external use
- Paste text into event descriptions

### Offline Capabilities

**Full Offline Operation:**
- **Data Storage:** Complete local SQLite database with all historical data
- **Calendar View:** Display all locally cached Google Calendar events
- **Editing:** Create, edit, approve events offline (queue for publish)
- **Data Sources:** Import files (call logs, YouTube JSON) without internet
- **Queued Publishing:** Store pending approvals, publish when online

**Online-Required Operations:**
- Google Calendar sync (fetch existing events, publish approved events)
- Toggl Track data fetching (API access)
- YouTube video metadata retrieval (API access)
- Outlook calendar sync (API or .ics file)
- Weekly Excel status sync (Microsoft Graph API)

**Sync Strategy:**
- Fetch Google Calendar events on app launch (or manual refresh)
- Publish approved events in batches (minimize API calls)
- Handle offline-to-online transition gracefully
- Conflict detection (if Google Calendar changed externally)

### Update Strategy

**Manual Updates for MVP:**
- Personal single-user application (no auto-update requirement)
- Manual installation of new versions as needed
- Database schema migrations handled on app launch
- Backwards compatibility for local database

**Future Auto-Update (Post-MVP):**
- Click-once deployment or similar
- Automatic check for updates on launch
- Optional update notifications
- Seamless database migrations

---

## User Experience Principles

### Design Philosophy: Aesthetic Beauty Meets Function

**Core Principle:** Make backfilling a **fun, satisfying ritual** through beautiful, intuitive design

**Visual Identity:**
- **Clean, modern interface** that respects Windows 11 design language
- **Color as primary language:** The color-coded calendar is the hero - make it gorgeous
- **Minimalist chrome:** UI elements serve the calendar, don't compete with it
- **Satisfying interactions:** Smooth animations, clear feedback, delightful micro-interactions
- **Information density:** Dense when needed (data view), spacious when reflecting (approval mode)

**Emotional Goals:**
- **Calm, not chaotic:** Single pane replaces tab-juggling anxiety
- **Confident, not confused:** Clear states (pending, approved, published)
- **Engaging, not exhausting:** Like inbox zero for life moments
- **Beautiful, not boring:** Aesthetic quality makes you want to open the app

### Key Interactions

**1. Calendar View (Primary Interface)**
- Month/week/day views with smooth transitions
- Existing events in their assigned colors
- Pending approval events overlaid in yellow/banana (distinctive but non-intrusive)
- Click event → inline edit panel (title, time, description, color)
- Hover shows preview without modal interruption
- Keyboard navigation for power users

**2. Approval Workflow (Core Experience)**
- **Select mode:** Click to select individual events, shift-click for ranges, "select day" button
- **Visual feedback:** Selected events highlighted with checkmark or border
- **Batch actions:** "Approve selected" button with count badge
- **One-click publish:** Big, satisfying "Publish to Google Calendar" button
- **Success animation:** Smooth transition from yellow → final color with subtle celebration
- **Undo safety net:** Recent publish visible with quick undo option

**3. Data Import (Frequent Operation)**
- Drag-and-drop for files (call logs CSV, YouTube JSON)
- "Fetch from Toggl/Outlook" buttons with last sync timestamp
- Progress indication for API calls
- Import summary: "Found 47 new events, 3 conflicts"
- Auto-navigate to date range with new data

**4. Color Picker & Event Editing**
- Quick color selector showing all 9 colors with labels
- Recent colors for fast access
- Time picker with 15-minute increments (align with 8/15 rounding)
- Smart duration suggestions based on source data
- Description templates for common patterns

**5. Navigation & Context**
- Date picker with indicators: green (complete), yellow (partial), grey (empty)
- "Jump to today," "Jump to contiguity edge" quick nav
- Breadcrumb showing current view and date range
- Search/filter by color, source, date range

**6. Save/Restore System**
- "Create save point" button with timestamp name
- Save point list with preview
- "Restore to this point" with confirmation
- Visual diff showing what will change

### Accessibility & Polish

**Not WCAG Compliance Focus (Personal App):**
- Primary user is sighted, mouse/keyboard capable
- Focus on smooth, beautiful experience over universal access

**Polish Details:**
- Keyboard shortcuts for power user workflows
- Dark mode support (respect Windows theme)
- Responsive to window resizing
- Fast launch time (<2 seconds)
- No loading spinners - use skeleton screens or instant cached views

---

## Functional Requirements

Requirements organized by capability, each with acceptance criteria and domain constraints.

### FR-1: Data Source Management

**FR-1.1: Toggl Track Integration**
- **Capability:** Fetch time entries from Toggl Track API using API token authentication
- **Filters:** Duration-based filtering (e.g., exclude entries <5 minutes)
- **Processing:** Identify "Phone" or "ToDelete" entries for coalescing
- **Acceptance Criteria:**
  - Successfully authenticate with Toggl Track API token
  - Fetch all time entries for specified date range
  - Parse entry structure (start time, end time, duration, description, tags)
  - Store raw entries in local database before processing
  - Handle API errors gracefully with user notification

**FR-1.2: iOS Call Logs Import**
- **Capability:** Parse iMazing CSV export of iOS call logs and create calendar events
- **Filters:** Duration threshold (e.g., only calls >2 minutes), service type (phone/FaceTime/etc.)
- **Format:** Description shows caller/recipient, call type, duration
- **Acceptance Criteria:**
  - Parse CSV columns: date, time, duration, contact, service type
  - Filter by user-configured duration threshold
  - Format event description: "Call: [Contact] ([Duration]) - [Service]"
  - Handle missing contact names (show phone number)
  - Apply 8/15 rounding to call durations

**FR-1.3: YouTube Watch History Import**
- **Capability:** Parse Google Takeout YouTube history JSON and fetch video metadata
- **Processing:** Coalesce viewing sessions, fetch titles/channels via YouTube Data API
- **Format:** Event description lists channels watched during session
- **Acceptance Criteria:**
  - Parse Google Takeout JSON format (videoId, timestamp)
  - Fetch video metadata via YouTube Data API (title, channel, duration)
  - Cache metadata locally (avoid redundant API calls for rewatches)
  - Coalesce videos into sessions using sliding window algorithm
  - Format: "YouTube - Channel1, Channel2, Channel3"
  - Handle API quota limits and errors

**FR-1.4: Outlook Calendar Integration**
- **Capability:** Fetch events from Outlook calendar via Microsoft Graph API or .ics file
- **Authentication:** OAuth 2.0 with 90-day refresh token expiry
- **Fallback:** Manual .ics file import if API unavailable
- **Acceptance Criteria:**
  - Authenticate via Microsoft Graph OAuth 2.0
  - Fetch all calendar events for date range
  - Parse event structure (title, start, end, location, description)
  - Warn user 7 days before token expiration
  - Support .ics file import as fallback option
  - Handle event recurrence and exceptions

### FR-2: Data Processing Algorithms

**FR-2.1: 8/15 Rounding Algorithm**
- **Purpose:** Convert continuous time ranges into discrete 15-minute calendar blocks
- **Logic:**
  - Divide time range into 15-minute blocks
  - Keep blocks with ≥8 minutes of activity
  - Always show at least 1 block (end time)
  - Threshold configurable by user
- **Acceptance Criteria:**
  - Correctly calculate block boundaries
  - Apply threshold consistently
  - Handle edge cases (midnight crossing, sub-15-min activities)
  - User can adjust threshold (default: 8 minutes)

**FR-2.2: Phone Activity Coalescing**
- **Purpose:** Merge fragmented phone usage entries into single calendar events
- **Logic:**
  - Sliding window from first "Phone" or "ToDelete" entry to last
  - Auto-stop at gaps ≥15 minutes
  - Quality check: Retry with 5-min gaps if <50% of window is phone activity
  - Discard windows <5 minutes total duration
- **Acceptance Criteria:**
  - Correctly identify phone activity entries
  - Apply sliding window with configurable gap threshold
  - Quality check validation
  - Minimum duration enforcement
  - Handle edge cases (single entry, long gaps)

**FR-2.3: YouTube Session Coalescing**
- **Purpose:** Group multiple YouTube videos into viewing sessions
- **Logic:**
  - Sliding window from first video
  - Include next video if within (previous video duration + 30 minutes)
  - Apply 8/15 rounding to total session duration
  - List unique channels in event description
- **Acceptance Criteria:**
  - Correctly calculate session boundaries
  - Include videos within time threshold
  - Apply 8/15 rounding to final session
  - Format description with channel list
  - Handle overlapping sessions and edge cases

### FR-3: Calendar Display & Interaction

**FR-3.1: Unified Calendar View**
- **Capability:** Display existing Google Calendar events alongside pending approval events in single view
- **Visual Distinction:**
  - Published events: Show in their assigned Google Calendar colors
  - Pending events: Yellow/banana overlay (distinctive but harmonious)
  - Selected events: Highlight with checkmark or border
- **View Modes:** Month, week, day with smooth transitions
- **Acceptance Criteria:**
  - Fetch and cache Google Calendar events on launch
  - Render both published and pending events simultaneously
  - Visual distinction between states clear at a glance
  - Smooth transitions between view modes
  - Performance: <1 second to render month with 200+ events

**FR-3.2: Event Editing**
- **Capability:** Click any event to edit title, start/end times, description, color
- **Interface:** Inline edit panel (not modal) for fluid workflow
- **Time Picker:** 15-minute increments aligned with 8/15 rounding
- **Color Picker:** Show all 9 colors with labels (Azure, Purple, Yellow, etc.)
- **Acceptance Criteria:**
  - Click event → inline editor appears
  - All fields editable with validation
  - Time picker enforces 15-minute increments
  - Color picker shows custom color codes
  - Changes saved to local database immediately
  - Esc key cancels edit, Enter confirms

**FR-3.3: Event Selection & Batch Operations**
- **Capability:** Select multiple events for batch approval
- **Selection Modes:**
  - Click individual events
  - Shift-click for ranges
  - "Select day" button selects all pending events on that day
  - "Select date range" for bulk selection
- **Visual Feedback:** Selected events show checkmark or highlight
- **Acceptance Criteria:**
  - Multiple selection modes work correctly
  - Visual feedback immediate and clear
  - Selection state persists during navigation
  - "Clear selection" button
  - Selection count badge visible

### FR-4: Approval & Publishing Workflow

**FR-4.1: Event Approval**
- **Capability:** Review and approve events before publishing to Google Calendar
- **Workflow:**
  - Select events to approve (individual, day, range)
  - "Approve selected" marks events ready for publish
  - Approved events remain yellow until published
- **Acceptance Criteria:**
  - Approval state stored in local database
  - Approved events distinguishable from pending (subtle indicator)
  - Batch approval of multiple events
  - Unapprove capability (change mind)

**FR-4.2: Batch Publishing**
- **Capability:** Publish approved events to Google Calendar in batch
- **Workflow:**
  - "Publish to Google Calendar" button shows count of approved events
  - Batch API request to Google Calendar (minimize quota usage)
  - Receive event IDs, update local database
  - Success animation: Yellow → final color transition
- **Acceptance Criteria:**
  - Batch publish 50+ events reliably
  - Handle API rate limits with retries
  - Store Google Calendar event IDs
  - Update local database with published status
  - Success/error notifications clear
  - Undo option for recent publish

**FR-4.3: App-Published Notation**
- **Capability:** Mark events published by app in Google Calendar description
- **Format:** Append "Published by Google Calendar Management on {datetime}" to description
- **Purpose:** Distinguish app-published from manually created events
- **Acceptance Criteria:**
  - Notation appended to all app-published events
  - Timestamp uses ISO 8601 format
  - Doesn't interfere with user-entered descriptions
  - Visible in Google Calendar web/mobile

### FR-5: Date State Tracking

**FR-5.1: Per-Date State Flags**
- **Capability:** Track which data sources have been published for each date
- **Flags:**
  - `call_log_data_published`
  - `sleep_data_published` (Toggl sleep timer)
  - `youtube_data_published`
  - `toggl_data_published`
  - `named` (date has been reviewed and named/contextualized)
  - `complete_walkthrough_approval` (all sources reviewed, date complete)
  - `part_of_tracked_gap` (intentionally incomplete, e.g., vacation)
- **Acceptance Criteria:**
  - Flags stored per date in database
  - Auto-update when events published
  - Manual override capability
  - Visual indicators in calendar view (green/yellow/grey dots)

**FR-5.2: Contiguity Edge Calculation**
- **Capability:** Calculate the last date with complete walkthrough approval
- **Purpose:** Know where to start "fill to present" workflow
- **Logic:** Most recent date where `complete_walkthrough_approval = true` and all subsequent dates are incomplete (except tracked gaps)
- **Acceptance Criteria:**
  - Algorithm correctly identifies contiguity edge
  - Edge displayed prominently in UI
  - "Jump to edge" navigation button
  - Updates automatically when dates completed

### FR-6: Save/Restore System

**FR-6.1: Create Save Points**
- **Capability:** Snapshot current Google Calendar state for rollback
- **Storage:** Store event details, timestamps, colors, descriptions in local database
- **Naming:** Auto-generate timestamp-based names, allow custom naming
- **Acceptance Criteria:**
  - Create save point captures all events in specified date range
  - Stored locally with metadata (name, timestamp, date range, event count)
  - Save point list shows preview
  - No limit on number of save points

**FR-6.2: Restore to Save Point**
- **Capability:** Rollback Google Calendar to previous saved state
- **Mechanism:** Send UPDATE requests to Google Calendar API (Google doesn't track versions)
- **Preview:** Show diff of what will change before confirming
- **Acceptance Criteria:**
  - Diff preview shows added/removed/modified events
  - Confirmation required before restore
  - Restore operation sends updates to Google Calendar
  - Handle conflicts (events deleted externally since save)
  - Success notification with change summary
  - New save point auto-created before restore (safety)

**FR-6.3: Undo Recent Publish**
- **Capability:** Quick undo for just-published events
- **Mechanism:** Delete events from Google Calendar using stored event IDs
- **Scope:** Last publish operation only
- **Acceptance Criteria:**
  - Undo available immediately after publish
  - One-click undo with confirmation
  - Deletes events from Google Calendar
  - Updates local database (marks unpublished)
  - Undo expires after next publish or app close

### FR-7: Weekly Status Tracking

**FR-7.1: Calculate Weekly Completion**
- **Capability:** Calculate completion status per data source per ISO 8601 week
- **Logic:**
  - Week starts Monday
  - Week 1 = first week with ≥4 days in year
  - Per source: "Yes" (all 7 days published), "Partial" (some days), "No" (zero days)
- **Acceptance Criteria:**
  - Correct ISO 8601 week calculation
  - Accurate completion status per source
  - Historical weeks calculated on demand
  - Current week updates in real-time

**FR-7.2: Sync to Excel Cloud**
- **Capability:** Write weekly status to Excel file on OneDrive/SharePoint via Microsoft Graph
- **Format:** Rows = weeks, Columns = data sources, Values = Yes/Partial/No
- **Authentication:** OAuth 2.0 (shared with Outlook integration)
- **Acceptance Criteria:**
  - Authenticate via Microsoft Graph
  - Create Excel file if doesn't exist
  - Update specific cells without overwriting entire file
  - Handle conflicts (file open in Excel)
  - Manual sync trigger + auto-sync on publish

### FR-8: Local Database Management

**FR-8.1: SQLite Storage**
- **Capability:** Store all application data locally in SQLite database
- **Schema:** 14 tables (see `_database-schemas.md`)
- **Key Tables:**
  - `GoogleCalendarEvents` (complete version history)
  - `TogglTimeEntries`, `CallLogs`, `YouTubeVideos` (source data)
  - `ApprovedEvents` (pending publications)
  - `DateStates` (per-date flags)
  - `SavePoints` (rollback snapshots)
  - `AuditLog` (all operations)
- **Acceptance Criteria:**
  - Database created on first launch
  - Schema migrations on app updates
  - Complete data preservation (never delete source data)
  - Version history for all Google Calendar events
  - Audit log for all operations
  - Database integrity checks on launch

**FR-8.2: Data Export**
- **Capability:** Export data to standard formats for backup/analysis
- **Formats:** CSV, JSON for each table
- **Scope:** Full export or filtered by date range
- **Acceptance Criteria:**
  - Export all tables individually or full database
  - CSV format for spreadsheet analysis
  - JSON format for programmatic access
  - Date range filtering
  - Progress indicator for large exports

### FR-9: Import Workflows

**FR-9.1: File Import (Drag & Drop)**
- **Capability:** Drag CSV/JSON files into app for import
- **Supported:** Call log CSV, YouTube history JSON, Outlook .ics
- **Processing:** Auto-detect file type, parse, process, show summary
- **Acceptance Criteria:**
  - Drag-and-drop target area visible
  - Auto-detect file type from content/extension
  - Parse and validate files
  - Show import summary before processing
  - Cancel option after preview
  - Error handling for invalid files

**FR-9.2: API Fetch Buttons**
- **Capability:** One-click fetch from Toggl/Outlook/YouTube
- **Display:** Last sync timestamp, estimated new events
- **Progress:** Show progress during API calls
- **Acceptance Criteria:**
  - Buttons show last sync time
  - Click initiates API fetch for date range
  - Progress indicator with cancel option
  - Import summary after fetch
  - Auto-navigate to date range with new data

### FR-10: Color System Management

**FR-10.1: Custom Color Definitions**
- **Capability:** Define and manage 9 custom colors representing mental states
- **Colors:** Azure, Purple, Yellow, Navy, Sage, Grey, Flamingo, Orange, Lavender
- **Each Color:**
  - Hex code (e.g., Azure = #0088CC)
  - Label (short name)
  - Description (mental state meaning)
  - Default for new events (Azure)
- **Acceptance Criteria:**
  - Colors stored in database
  - Edit color definitions (hex, label, description)
  - Set default color
  - Changes reflected immediately in UI
  - Export/import color definitions

**FR-10.2: Color Assignment**
- **Capability:** Assign colors to events (both pending and published)
- **Interface:** Color picker showing all 9 colors with labels
- **Batch:** Apply color to multiple selected events
- **Acceptance Criteria:**
  - Color picker shows hex preview
  - Apply to single event or selection
  - Update Google Calendar color (limited to GCal palette - map custom colors)
  - Store exact custom color locally even if GCal uses closest match

### Magic Moments

**Requirements delivering the special experience:**
- **FR-3.1 (Unified View):** Single pane eliminates tab-juggling cognitive load
- **FR-4.2 (Approval Workflow):** Satisfying click-to-approve ritual like inbox zero
- **FR-2 (Coalescing):** Intelligent algorithms reduce decision fatigue
- **FR-10 (Color System):** Visual autobiography reveals life patterns at a glance
- **FR-5.2 (Contiguity Edge):** Always know where you left off - no lost context

---

## Non-Functional Requirements

Only documenting NFRs that directly impact this product's success.

### Performance

**Why It Matters:** Backfilling experience must feel fluid and responsive for the ritual to be enjoyable.

**NFR-P1: UI Responsiveness**
- Calendar view renders in <1 second for month with 200+ events
- Event editing appears instantly on click (<100ms)
- Selection feedback immediate (<50ms)
- Smooth 60 FPS animations for view transitions

**NFR-P2: Data Operations**
- Import 500+ events completes in <5 seconds
- Coalescing algorithms process week of data in <2 seconds
- Database queries <100ms for typical operations
- Google Calendar sync (fetch/publish) <10 seconds for 50 events

**NFR-P3: App Launch**
- Cold start <2 seconds to usable UI
- Database initialization <500ms
- Google Calendar cache loaded in background (non-blocking)

**Rationale:** Slow operations break the flow state. Fast feedback reinforces the "satisfying ritual" experience.

### Security

**Why It Matters:** Protecting API credentials and personal life data.

**NFR-S1: Credential Storage**
- OAuth tokens encrypted at rest using Windows DPAPI
- API keys encrypted in local database
- No credentials in memory longer than needed
- Secure token refresh without re-authentication

**NFR-S2: Data Privacy**
- All personal data stored locally (no cloud sync except explicit integrations)
- Database file permissions restricted to user account
- No telemetry, analytics, or usage tracking
- Export/backup files include privacy warning

**NFR-S3: API Communication**
- All API calls over HTTPS/TLS 1.2+
- Certificate validation enforced
- OAuth 2.0 flows follow best practices
- Refresh tokens rotated on use

**NFR-S4: Local Database**
- SQLite database file encrypted (optional, user preference)
- No plain-text passwords
- Audit log captures all data modifications
- Automatic backups before destructive operations

**Rationale:** 5+ years of intimate personal data requires protection. API tokens access calendar, email, activity logs.

### Data Integrity & Reliability

**Why It Matters:** Decades of irreplaceable personal history must never be lost.

**NFR-D1: Data Loss Prevention**
- SQLite WAL mode for crash recovery
- Auto-save before all destructive operations (restore, delete)
- Complete version history for Google Calendar events
- Never delete source data (mark inactive instead)

**NFR-D2: Sync Reliability**
- Google Calendar API retries with exponential backoff
- Conflict detection when calendar modified externally
- Failed publishes queued for retry
- Clear error messages with recovery options

**NFR-D3: Database Integrity**
- Foreign key constraints enforced
- Database integrity check on app launch
- Schema migration tests before deployment
- Automatic backup before migrations

**NFR-D4: Audit Trail**
- All operations logged with timestamp and user action
- Rollback capability for all published events
- Import history with source file checksums
- Undo/redo for last 10 operations

**Rationale:** Data is irreplaceable. Trust in the system is critical for sustained use.

### Integration Reliability

**Why It Matters:** 4 external APIs must work reliably despite quota limits, outages, token expiration.

**NFR-I1: API Resilience**
- **Google Calendar API:**
  - Quota: 1M requests/day (far exceeds needs)
  - Batch requests (max 50 events) to minimize calls
  - Exponential backoff on rate limit errors
  - Graceful degradation if service unavailable
- **Toggl Track API:**
  - Rate limit: 1 request/second
  - Cache time entries locally (avoid re-fetching)
  - Clear error messages on API token issues
- **YouTube Data API:**
  - Quota: 10K units/day (watch: 1 unit, search: 100 units)
  - Cache video metadata aggressively
  - Fallback to video ID if quota exceeded
  - Warn user approaching quota limit
- **Microsoft Graph API:**
  - OAuth token refresh 7 days before expiry
  - Clear re-authentication flow
  - .ics file fallback if API unavailable

**NFR-I2: Offline Operation**
- All calendar viewing works offline
- Pending approvals queued for online publish
- File imports (CSV, JSON) work offline
- Clear indication of online/offline state

**NFR-I3: Error Handling**
- User-friendly error messages (no technical jargon)
- Actionable recovery steps ("Reconnect Toggl" button)
- Non-blocking errors don't halt workflow
- Critical errors auto-save state before exit

**Rationale:** External dependencies shouldn't break the workflow. Graceful degradation preserves experience.

### Usability & User Experience

**Why It Matters:** Must feel intuitive from day 1 and enjoyable for decades.

**NFR-U1: Learnability**
- First-time setup wizard (<5 minutes)
- Contextual tooltips for complex features
- Inline help for coalescing algorithms
- Sample data walkthrough in empty state

**NFR-U2: Discoverability**
- Keyboard shortcuts shown in hover tooltips
- Common actions visible without menus
- Progressive disclosure (advanced features hidden until needed)
- Clear visual hierarchy

**NFR-U3: Error Prevention**
- Confirmation dialogs for destructive actions
- Undo available for all bulk operations
- Validation before publish (detect overlaps, missing titles)
- Save points auto-created before risky operations

**NFR-U4: Consistency**
- Consistent color picker across all contexts
- Predictable date navigation
- Uniform error message structure
- Single design language throughout

**Rationale:** Complex system must feel simple. Decades of use requires muscle memory, not constant re-learning.

### Maintainability & Extensibility

**Why It Matters:** Personal project maintained by one developer for decades.

**NFR-M1: Code Quality**
- .NET 9 best practices
- Entity Framework Core for database abstraction
- Dependency injection for testability
- Clear separation: UI, business logic, data access

**NFR-M2: Testability**
- Unit tests for algorithms (8/15 rounding, coalescing)
- Integration tests for API clients
- Database migration tests
- No untested destructive operations

**NFR-M3: Extensibility**
- Data source interface for adding new sources
- Plugin architecture for future enhancements
- API versioning for future web/mobile clients
- Documented extension points

**NFR-M4: Documentation**
- Inline code documentation
- Architecture decision records
- Database schema documentation
- API integration guides

**Rationale:** Future self (or AI assistants) must understand code years later. New data sources inevitable as life evolves.

### Excluded NFRs (Not Applicable)

**Accessibility (WCAG):** Personal app for sighted, mouse/keyboard user. Standard Windows accessibility support sufficient.

**Scalability (Multi-User):** Single-user desktop app. No concurrent access, no multi-tenancy, no cloud scale.

**Internationalization:** English only (user's language). No l10n/i18n requirements.

**Browser Compatibility:** Desktop app, not web app.

**Mobile Responsiveness:** Windows desktop only in MVP.

---

## Implementation Planning

### Epic Breakdown Required

This comprehensive PRD must be decomposed into implementable epics and bite-sized stories optimized for 200k context AI development agents.

**Recommended Approach:** Run `workflow create-epics-and-stories` in a fresh session to:
- Transform 21 functional requirements into logical epic groupings
- Create user stories with acceptance criteria
- Establish implementation sequence
- Enable focused, incremental development

**Epic Structure Preview:**
Based on requirements above, expect ~8-12 epics:
1. Foundation & Database Setup
2. Google Calendar Integration & Sync
3. Data Source Integrations (Toggl, Calls, YouTube, Outlook)
4. Data Processing Algorithms (8/15 rounding, coalescing)
5. Calendar UI & Event Display
6. Approval Workflow & Publishing
7. Save/Restore System
8. Date State Tracking & Contiguity
9. Weekly Status & Excel Integration
10. Color System Management
11. Import Workflows & File Handling
12. Polish, Error Handling, UX Refinements

Each epic will contain bite-sized stories (<200 lines of code) suitable for AI agent development.

---

## References

- **Product Brief:** `docs/product-brief-google-calendar-management-2025-11-05.md`
- **Technology Stack Research:** `docs/_technology-stack.md`
- **Database Schemas:** `docs/_database-schemas.md`
- **Color Definitions:** `docs/_color-definitions.md`
- **Key Decisions:** `docs/_key-decisions.md`
- **Phase 1 Requirements:** `docs/_phase-1-requirements.md`

---

## Next Steps

1. **Epic & Story Breakdown** - Run: `workflow epics-stories`
2. **UX Design** (if UI) - Run: `workflow ux-design`
3. **Architecture** - Run: `workflow create-architecture`

---

## PRD Summary

**Vision:** Transform retroactive life tracking from dreaded chore into a fun, nostalgic ritual through a single-pane Windows desktop app that consolidates 4 data sources with intelligent approval workflow.

**Project Type:** Desktop Application (Windows, WinUI 3, .NET 9, local-first SQLite)

**MVP Scope:**
- 4 data sources (Toggl, iOS calls, YouTube, Outlook)
- Single calendar view with approval workflow
- 8/15 rounding and coalescing algorithms
- Save/restore system with version history
- Date state tracking and contiguity edge
- Weekly Excel status sync

**Success:** <1 hour/week backfilling (from 2-4), maintained weekly habit for decades, contiguity edge <7 days behind present

**Requirements:** 21 functional requirements across 10 capability areas, 6 NFR categories (performance, security, data integrity, integration, usability, maintainability)

**Magic:** Backfilling becomes "spaced repetition for life" - memory reinforcement through beautiful visual autobiography with aesthetic appeal. The calendar isn't just a log; it's a consciousness taxonomy revealing life balance through color.

---

_This PRD captures the essence of Google Calendar Management - transforming calendar backfilling from burden into a beautiful, fun ritual of life reflection that compounds value over decades._

_Created through collaborative discovery between Sarunas Budreckis and AI facilitator on 2025-11-06._

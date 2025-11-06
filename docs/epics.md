# Google Calendar Management - Epic Breakdown

**Author:** Sarunas Budreckis
**Date:** 2025-11-06
**Project Level:** Medium (Desktop Application)
**Target Scale:** Single-user Windows desktop application

---

## Overview

This document provides the complete epic and story breakdown for Google Calendar Management, decomposing the requirements from the [PRD](./PRD.md) into implementable stories.

### Epic Summary

This project is decomposed into **10 epics** that deliver incremental value, starting with foundation and building toward the complete life-review ritual experience.

**Epic 1: Foundation & Core Infrastructure**
Establish technical foundation (project setup, database, OAuth)

**Epic 2: Google Calendar Integration & Sync**
Connect to and control Google Calendar

**Epic 3: Calendar UI & Visual Display**
Single-pane view with event interaction, offline viewing of arbitrary date ranges loaded to local database

**Epic 4: Data Source Integrations**
Consolidate Toggl, Calls, YouTube, Outlook

**Epic 5: Data Processing & Coalescing Algorithms**
Intelligent automation (8/15 rounding, coalescing)

**Epic 6: Approval Workflow & Publishing**
The satisfying life-review ritual

**Epic 7: Date State & Progress Tracking**
Track completion, know where you left off

**Epic 8: Save/Restore & Version Management**
Safety net, never lose data

**Epic 9: Import Workflows & Data Management**
Effortless ingestion and backup

**Epic 10: Polish & Production Readiness**
Delightful, reliable daily experience

**Sequencing Strategy:** Foundation → Core Calendar → Data Sources → Processing → Approval Workflow → Advanced Features → Polish

Each epic contains 3-8 bite-sized stories designed for single-session completion by development agents.

---

## Epic 1: Foundation & Core Infrastructure

**Goal:** Establish the technical foundation that enables all subsequent development - project structure, database with Entity Framework Core, core dependencies, and OAuth 2.0 infrastructure for API integrations.

### Story 1.1: Project Setup & Initial Infrastructure

As a developer,
I want a properly scaffolded .NET 9 WinUI 3 project with core dependencies,
So that I have a solid foundation for building the application.

**Acceptance Criteria:**

**Given** a new development environment
**When** the project is created
**Then** the solution contains a WinUI 3 project targeting .NET 9
**And** the project includes necessary NuGet packages (WinUI 3, Entity Framework Core, dependency injection)
**And** the project builds successfully without errors
**And** the application launches with a basic empty window

**Prerequisites:** None (first story)

**Technical Notes:**
- Use Windows App SDK for WinUI 3
- Install packages: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.Extensions.DependencyInjection
- Set up proper project structure: /Models, /Services, /Views, /ViewModels
- Configure app.manifest for appropriate Windows capabilities

### Story 1.2: SQLite Database Schema & Entity Framework Setup

As a developer,
I want Entity Framework Core configured with the complete SQLite schema,
So that the application can persist all required data locally.

**Acceptance Criteria:**

**Given** the project foundation exists
**When** Entity Framework is configured
**Then** all 14 database tables are defined as EF Core entities
**And** DbContext is configured for SQLite with appropriate relationships
**And** migrations are created for the initial schema
**And** the database is created on first application launch
**And** database integrity checks pass on startup

**Prerequisites:** Story 1.1

**Technical Notes:**
- Implement all 14 tables from PRD: GoogleCalendarEvents (with version history), TogglTimeEntries, CallLogs, YouTubeVideos, YouTubeVideoMetadata, ApprovedEvents, DateStates, SavePoints, SavePointEvents, AuditLog, ColorDefinitions, AppSettings, OutlookEvents, WeeklyStatusRecords
- Use SQLite WAL mode for crash recovery
- Configure foreign key constraints
- Set up automatic timestamp tracking (CreatedAt, UpdatedAt)
- Database file location: %LocalAppData%/GoogleCalendarManagement/calendar.db

### Story 1.3: Dependency Injection & Service Architecture

As a developer,
I want dependency injection configured with core service interfaces,
So that the application has a clean, testable architecture.

**Acceptance Criteria:**

**Given** the database is configured
**When** dependency injection is set up
**Then** service interfaces are defined for all major components
**And** services are registered in the DI container
**And** ViewModels can inject required services
**And** database context is available via DI with proper lifetime scope

**Prerequisites:** Story 1.2

**Technical Notes:**
- Define interfaces: ICalendarService, IDatabaseService, IAuthService, IDataSourceService
- Register DbContext as scoped
- Register services appropriately (singleton for app-wide state, scoped for per-operation)
- Set up ViewModel locator pattern for WinUI 3
- Use CommunityToolkit.Mvvm for MVVM helpers

### Story 1.4: OAuth 2.0 Infrastructure & Credential Storage

As a developer,
I want OAuth 2.0 authentication infrastructure with secure credential storage,
So that API integrations can authenticate securely without exposing tokens.

**Acceptance Criteria:**

**Given** the service architecture is established
**When** OAuth infrastructure is implemented
**Then** OAuth 2.0 flow helper methods are available
**And** tokens are encrypted using Windows DPAPI before storage
**And** tokens are stored in SQLite database with encryption
**And** token refresh logic is implemented with automatic retry
**And** token expiration warnings can be triggered

**Prerequisites:** Story 1.3

**Technical Notes:**
- Use System.Security.Cryptography.ProtectedData for DPAPI encryption
- Store encrypted tokens in AppSettings table with scope/provider identification
- Implement IAuthService with methods: AuthenticateAsync, RefreshTokenAsync, GetValidTokenAsync
- Support multiple OAuth providers (Google, Microsoft)
- Implement token lifetime tracking for expiration warnings

### Story 1.5: Application Shell & Navigation Framework

As a user,
I want a consistent application shell with navigation structure,
So that I can access different features of the application.

**Acceptance Criteria:**

**Given** the core architecture is ready
**When** the application launches
**Then** the main window displays with navigation menu
**And** the navigation menu includes placeholders for: Calendar, Import, Settings
**And** clicking navigation items switches the content area
**And** the application remembers window size and position between sessions

**Prerequisites:** Story 1.4

**Technical Notes:**
- Use NavigationView control from WinUI 3
- Implement frame navigation pattern
- Store window state in AppSettings table
- Create placeholder pages for main sections (to be filled in later epics)
- Apply Windows 11 theme awareness (light/dark mode)

---

## Epic 2: Google Calendar Integration & Sync

**Goal:** Connect to Google Calendar API, authenticate securely, fetch existing events to local cache, and publish events with proper error handling and resilience.

### Story 2.1: Google Calendar API Authentication

As a user,
I want to authenticate with Google Calendar using OAuth 2.0,
So that the application can access my calendar data securely.

**Acceptance Criteria:**

**Given** the application is launched for the first time
**When** I initiate Google Calendar connection
**Then** a browser window opens for Google OAuth consent
**And** I can grant calendar access permissions
**And** the OAuth token is received and encrypted in the database
**And** subsequent app launches use the stored token without re-authentication
**And** token refresh happens automatically when expired

**Prerequisites:** Story 1.4 (OAuth infrastructure)

**Technical Notes:**
- Use Google.Apis.Calendar.v3 NuGet package
- Implement OAuth 2.0 web flow (local redirect server)
- Scopes required: calendar.events (full calendar access)
- Store access token and refresh token encrypted
- Implement automatic token refresh before expiration
- Handle revoked token scenario with re-authentication prompt

### Story 2.2: Fetch Google Calendar Events to Local Cache

As a user,
I want to fetch my existing Google Calendar events for any date range,
So that I can view and work with my calendar offline.

**Acceptance Criteria:**

**Given** Google Calendar authentication is complete
**When** I specify a date range to fetch
**Then** all events in that range are retrieved via Google Calendar API
**And** events are stored in the GoogleCalendarEvents table
**And** complete event details are preserved (title, start, end, description, color, ID)
**And** fetch progress is displayed during API calls
**And** I can fetch additional date ranges as needed
**And** previously fetched events are updated if changed in Google Calendar

**Prerequisites:** Story 2.1

**Technical Notes:**
- Use Google Calendar API events.list with timeMin/timeMax parameters
- Implement pagination to handle >250 events per request
- Store Google Calendar event ID for later updates
- Track last sync timestamp per event
- Implement incremental sync (fetch only changed events using updatedMin parameter)
- Handle API rate limits with exponential backoff

### Story 2.3: Publish Events to Google Calendar

As a user,
I want to publish approved events to Google Calendar,
So that they appear in my calendar across all devices.

**Acceptance Criteria:**

**Given** I have approved events ready to publish
**When** I trigger the publish operation
**Then** events are created in Google Calendar via API
**And** Google Calendar event IDs are stored locally
**And** "Published by Google Calendar Management on {datetime}" is appended to descriptions
**And** events appear in Google Calendar with correct title, time, description, and color
**And** publish progress is shown with count of successful/failed events

**Prerequisites:** Story 2.2

**Technical Notes:**
- Use Google Calendar API events.insert for new events
- Implement batch requests (max 50 events per batch) to minimize API calls
- Map custom colors to Google Calendar's color palette (closest match)
- Store exact custom color locally even if Google uses approximation
- Handle API errors gracefully (show which events failed, allow retry)
- Implement retry logic with exponential backoff for transient failures

### Story 2.4: Update Existing Google Calendar Events

As a user,
I want to update events that were previously published,
So that I can correct mistakes or refine event details.

**Acceptance Criteria:**

**Given** an event exists in both local database and Google Calendar
**When** I modify the event locally and publish updates
**Then** the existing Google Calendar event is updated (not duplicated)
**And** the event retains its Google Calendar ID
**And** all modified fields are reflected in Google Calendar
**And** version history is maintained in the local database

**Prerequisites:** Story 2.3

**Technical Notes:**
- Use Google Calendar API events.update with stored event ID
- Implement conflict detection (check if event was modified externally)
- Prompt user on conflicts with options: keep local, keep remote, merge
- Store complete version history in GoogleCalendarEvents table
- Update LastModifiedTimestamp on each change

### Story 2.5: Sync Resilience & Error Handling

As a user,
I want robust error handling during Google Calendar sync,
So that temporary failures don't lose my data or break my workflow.

**Acceptance Criteria:**

**Given** I'm performing Google Calendar operations
**When** API failures occur (network issues, rate limits, server errors)
**Then** operations are retried automatically with exponential backoff
**And** failed operations are queued for later retry
**And** clear error messages explain what went wrong
**And** I can manually retry failed operations
**And** no data is lost during failures

**Prerequisites:** Story 2.4

**Technical Notes:**
- Implement retry policy: 3 retries with exponential backoff (1s, 2s, 4s)
- Queue failed operations in database with retry count
- Handle specific error codes: 401 (re-auth), 403 (quota), 429 (rate limit), 5xx (server error)
- Show user-friendly messages: "Connection lost - will retry automatically"
- Implement background retry queue that processes during idle time
- Log all API errors to AuditLog table

---

## Epic 3: Calendar UI & Visual Display

**Goal:** Single-pane-of-glass view that eliminates cognitive overload - beautiful calendar display with month/week/day views, offline viewing of cached events, event interaction, and multi-selection capabilities.

### Story 3.1: Calendar View Component with Month/Week/Day Views

As a user,
I want to view my calendar in month, week, or day layouts,
So that I can see events at different levels of detail.

**Acceptance Criteria:**

**Given** the application is open and events are cached locally
**When** I navigate to the calendar page
**Then** the calendar displays in month view by default
**And** I can switch between month, week, and day views
**And** view transitions are smooth (60 FPS animations)
**And** the current view mode is persisted between sessions

**Prerequisites:** Story 2.2 (events cached locally)

**Technical Notes:**
- Use WinUI 3 CalendarView as base, extend for week/day views
- Implement custom CalendarViewModel to manage view state
- Use XAML animations for smooth transitions
- Store view preference in AppSettings table
- Consider custom rendering for week/day views if CalendarView insufficient
- Target <1 second render time for month with 200+ events

### Story 3.2: Event Rendering with Visual State Distinction

As a user,
I want to see both published and pending events with clear visual distinction,
So that I know which events are already in Google Calendar versus awaiting approval.

**Acceptance Criteria:**

**Given** the calendar view is displayed
**When** events are rendered
**Then** published events appear in their assigned Google Calendar colors
**And** pending approval events appear in yellow/banana overlay color
**And** selected events show checkmark or highlight border
**And** event titles are readable without overlapping
**And** multi-day events span appropriately

**Prerequisites:** Story 3.1

**Technical Notes:**
- Create custom event template for CalendarView items
- Use data binding to event state (Published, Pending, Selected)
- Apply different visual styles via DataTemplate selectors
- Implement text truncation with ellipsis for long titles
- Use hover tooltips to show full event details
- Ensure color contrast for accessibility (even though not WCAG-focused)

### Story 3.3: Event Selection (Individual, Day, Range)

As a user,
I want to select multiple events for batch operations,
So that I can approve or modify groups of events efficiently.

**Acceptance Criteria:**

**Given** events are displayed in the calendar
**When** I interact with events
**Then** clicking an event toggles its selection state
**And** shift-clicking selects a range of events
**And** "Select day" button selects all pending events on that date
**And** "Select range" dialog allows date range selection
**And** selected count badge shows number of selected events
**And** "Clear selection" button deselects all

**Prerequisites:** Story 3.2

**Technical Notes:**
- Implement SelectionMode in CalendarViewModel (toggle, range, day, dateRange)
- Track selected event IDs in ObservableCollection
- Use visual state manager for selected event appearance
- Implement shift-click range detection (from last clicked to current)
- Create "Select day" command button on date headers
- Show selection count in status bar or header

### Story 3.4: Inline Event Editing Panel

As a user,
I want to click any event to edit its details inline,
So that I can quickly adjust titles, times, descriptions, and colors without modal interruptions.

**Acceptance Criteria:**

**Given** an event is displayed
**When** I click to edit the event
**Then** an inline editing panel appears adjacent to the event
**And** I can modify title, start time, end time, description, and color
**And** time picker shows 15-minute increments
**And** color picker displays all 9 custom colors with labels
**And** changes are saved to the local database immediately
**And** pressing Esc cancels edits, Enter confirms

**Prerequisites:** Story 3.3

**Technical Notes:**
- Create EventEditPanel user control (flyout or side panel)
- Use WinUI 3 TimePicker with 15-minute increment configuration
- Implement custom color picker showing ColorDefinitions from database
- Bind to event ViewModel with two-way binding
- Auto-save changes to database on field blur (don't wait for "Save" button)
- Show validation messages for invalid time ranges
- Implement keyboard shortcuts (Esc, Enter, Tab navigation)

### Story 3.5: Date Navigation & Jump-To Features

As a user,
I want quick navigation controls to jump to specific dates,
So that I can efficiently move through my calendar without tedious scrolling.

**Acceptance Criteria:**

**Given** the calendar view is displayed
**When** I use navigation controls
**Then** I can jump to today's date with one click
**And** I can select any date via date picker
**And** I can navigate forward/backward by view period (day, week, month)
**And** breadcrumb shows current date range being viewed
**And** keyboard arrows navigate through dates
**And** navigation is instant (<100ms response)

**Prerequisites:** Story 3.4

**Technical Notes:**
- Add navigation toolbar with: Today, Previous, Next, Date Picker buttons
- Implement breadcrumb showing current view range (e.g., "November 2025" or "Nov 4-10, 2025")
- Bind keyboard shortcuts: Left/Right arrows, Page Up/Down, Home/End
- Optimize rendering to only load visible date range + buffer
- Update URL/navigation stack to support back/forward navigation
- Smooth scroll animations when navigating

### Story 3.6: Offline Calendar Viewing with Arbitrary Date Range Loading

As a user,
I want to view any date range of my calendar offline using cached data,
So that I can review and work with events without internet connection.

**Acceptance Criteria:**

**Given** I have previously fetched Google Calendar events for specific date ranges
**When** I'm offline or choose offline mode
**Then** I can view all cached events without internet connection
**And** I can specify and load arbitrary date ranges that haven't been cached yet (when back online)
**And** the UI clearly indicates which date ranges are cached vs not cached
**And** I can request to fetch and cache additional date ranges
**And** cached events are available instantly without API calls

**Prerequisites:** Story 2.2 (local event cache), Story 3.5

**Technical Notes:**
- Add DateRangeCacheStatus table tracking which ranges are fully cached
- Implement visual indicators on calendar (green = cached, grey = not cached)
- Add "Fetch this range" button that appears when viewing uncached dates
- Load events from local database (GoogleCalendarEvents table) for offline viewing
- Implement cache management UI showing total cached events and date coverage
- Allow manual cache refresh to update stale cached data

---

## Epic 4: Data Source Integrations

**Goal:** Consolidate all life-tracking data sources (Toggl Track, iOS call logs, YouTube watch history, Outlook calendar) into the unified view, enabling comprehensive backfilling from multiple sources.

### Story 4.1: Toggl Track API Integration

As a user,
I want to fetch time entries from Toggl Track,
So that my work and activity tracking flows into the calendar automatically.

**Acceptance Criteria:**

**Given** I have configured my Toggl Track API token
**When** I fetch Toggl data for a date range
**Then** all time entries are retrieved and stored locally
**And** entries include start time, end time, duration, description, tags, and project
**And** duration filtering is applied (configurable minimum duration)
**And** "Phone" and "ToDelete" tagged entries are identified for coalescing
**And** raw entries are preserved in TogglTimeEntries table before processing

**Prerequisites:** Story 1.4 (auth infrastructure), Story 2.2 (local storage pattern)

**Technical Notes:**
- Use Toggl Track API v9 (REST API)
- Authentication via API token in HTTP header: "Authorization: Basic {base64(api_token:api_token)}"
- Endpoint: GET /api/v9/me/time_entries with start_date and end_date params
- Respect rate limit: 1 request per second
- Cache time entries locally to avoid re-fetching
- Store raw JSON in TogglTimeEntries for debugging
- Implement configurable duration filter (default: exclude <5 minute entries)

### Story 4.2: iOS Call Logs CSV Import

As a user,
I want to import call logs exported from iMazing as CSV,
So that my phone calls are tracked in the calendar.

**Acceptance Criteria:**

**Given** I have exported call logs from iMazing as CSV
**When** I import the CSV file
**Then** the file is parsed correctly with all columns
**And** calls are filtered by duration threshold (configurable, default >2 minutes)
**And** call details are formatted: "Call: [Contact] ([Duration]) - [Service Type]"
**And** calls are stored in CallLogs table
**And** 8/15 rounding is applied to call durations
**And** invalid rows are skipped with error log

**Prerequisites:** Story 1.2 (database), Story 5.1 (8/15 rounding algorithm)

**Technical Notes:**
- Parse CSV columns: Date, Time, Duration, Contact Name, Phone Number, Service Type (Phone/FaceTime/etc)
- Handle missing contact names (show phone number instead)
- Duration format: parse various formats (HH:MM:SS, MM:SS, seconds)
- Apply duration threshold filter (user configurable in settings)
- Store all parsed calls in CallLogs table with processed flag
- Generate calendar events after 8/15 rounding
- Validate CSV structure before processing (show preview)

### Story 4.3: YouTube Watch History Import & Video Metadata Fetching

As a user,
I want to import my YouTube watch history from Google Takeout and enrich it with video metadata,
So that my viewing sessions are accurately tracked with channel information.

**Acceptance Criteria:**

**Given** I have YouTube watch history JSON from Google Takeout
**When** I import the JSON file
**Then** video IDs and timestamps are extracted
**And** video metadata (title, channel, duration) is fetched via YouTube Data API
**And** metadata is cached locally to avoid redundant API calls for rewatched videos
**And** videos are stored in YouTubeVideos table
**And** metadata is stored in YouTubeVideoMetadata table
**And** API quota usage is tracked and warnings shown when approaching limit

**Prerequisites:** Story 1.4 (auth infrastructure), Story 1.2 (database)

**Technical Notes:**
- Parse Google Takeout JSON structure: array of {title, titleUrl, time}
- Extract video ID from titleUrl: youtube.com/watch?v={videoId}
- Use YouTube Data API v3: videos.list with part=snippet,contentDetails
- API key authentication (not OAuth for this endpoint)
- Quota: 1 unit per video metadata fetch, 10K units/day limit
- Cache all fetched metadata (many videos are rewatched)
- Handle deleted videos gracefully (metadata unavailable)
- Show quota usage: "Used 347/10,000 units today"
- Batch API requests (max 50 video IDs per request)

### Story 4.4: Outlook Calendar Integration via Microsoft Graph API

As a user,
I want to fetch events from my Outlook calendar,
So that work meetings and commitments are included in the unified view.

**Acceptance Criteria:**

**Given** I have authenticated with Microsoft Graph OAuth 2.0
**When** I fetch Outlook calendar events
**Then** all events for the specified date range are retrieved
**And** event details are parsed: title, start, end, location, description
**And** recurring events and exceptions are handled correctly
**And** events are stored in OutlookEvents table
**And** I receive a warning 7 days before OAuth token expiration (90-day refresh limit)

**Prerequisites:** Story 1.4 (OAuth infrastructure), Story 1.2 (database)

**Technical Notes:**
- Use Microsoft Graph API: GET /me/calendar/events
- OAuth 2.0 scopes: Calendars.Read
- Query parameters: startDateTime, endDateTime, $top, $skip for pagination
- Handle recurrence: master events + exception instances
- Token lifetime: 90 days before refresh required
- Implement token expiration tracking and warning notification
- Store Microsoft Graph event ID for future reference
- Handle timezone conversions properly (events may be in different timezones)

### Story 4.5: Outlook .ics File Fallback Import

As a user,
I want to manually import Outlook calendar via .ics file as a fallback,
So that I can still get Outlook data if the API is unavailable.

**Acceptance Criteria:**

**Given** Microsoft Graph API is unavailable or authentication failed
**When** I import an .ics file exported from Outlook
**Then** the file is parsed correctly using iCalendar format standards
**And** events are extracted with title, start, end, description, location
**And** recurring events are expanded to individual instances for the import range
**And** events are stored in OutlookEvents table
**And** import summary shows count of events imported

**Prerequisites:** Story 4.4

**Technical Notes:**
- Use iCalendar parsing library: Ical.Net NuGet package
- Parse VEVENT components from .ics file
- Handle RRULE (recurrence rules) by expanding to instances
- Support VTIMEZONE for proper timezone handling
- Validate .ics structure before processing
- Show import preview with event count and date range
- Handle malformed .ics files gracefully with clear error messages

---

## Epic 5: Data Processing & Coalescing Algorithms

**Goal:** Intelligent automation that reduces decision fatigue - implement 8/15 rounding, phone activity coalescing, and YouTube session coalescing to transform fragmented data into meaningful calendar events.

### Story 5.1: 8/15 Rounding Algorithm Implementation

As a user,
I want continuous time ranges converted to discrete 15-minute calendar blocks,
So that my calendar shows realistic, non-fragmented time allocations.

**Acceptance Criteria:**

**Given** a continuous time range (e.g., 2:03 PM to 3:47 PM)
**When** the 8/15 rounding algorithm is applied
**Then** the time is divided into 15-minute blocks
**And** blocks with ≥8 minutes of activity are kept
**And** blocks with <8 minutes are discarded
**And** at least 1 block (end time) is always shown
**And** the threshold is user-configurable (default 8 minutes)
**And** edge cases are handled (midnight crossing, <15min activities)

**Prerequisites:** Story 1.2 (database for settings)

**Technical Notes:**
- Algorithm: Round start time down to nearest 15-min block, end time up
- Iterate through 15-min blocks, calculate overlap with activity range
- Keep block if overlap ≥ threshold minutes (default 8)
- Special case: If no blocks meet threshold, always keep the end time block
- Configurable threshold stored in AppSettings (allow 5-10 minute range)
- Handle midnight crossings by splitting into separate day segments
- Unit tests for edge cases: 23:58-00:02, 10:00-10:03, etc.

### Story 5.2: Phone Activity Coalescing Algorithm

As a user,
I want fragmented phone usage entries merged into single calendar events,
So that my phone time is represented as cohesive blocks rather than dozens of tiny entries.

**Acceptance Criteria:**

**Given** multiple "Phone" or "ToDelete" tagged Toggl entries in a time range
**When** phone coalescing is applied
**Then** entries are merged using sliding window from first to last entry
**And** auto-stop occurs at gaps ≥15 minutes
**And** quality check validates: if <50% of window is phone activity, retry with 5-min gap threshold
**And** windows <5 minutes total duration are discarded
**And** resulting event formatted: "Phone" with merged time range
**And** original entries are preserved in database with reference to coalesced event

**Prerequisites:** Story 4.1 (Toggl data), Story 5.1 (8/15 rounding)

**Technical Notes:**
- Identify entries with tags "Phone" or "ToDelete" (configurable)
- Sliding window: start = first entry start, end = last entry end
- Scan window for gaps ≥ gap threshold (default 15 min)
- Quality check: sum duration of phone entries / total window duration ≥ 50%
- If quality check fails, retry with 5-minute gap threshold
- Minimum window duration: 5 minutes (configurable)
- Apply 8/15 rounding to final coalesced event
- Store coalescing metadata (which entries were merged) in database
- Unit tests for various patterns: continuous phone use, sporadic use, single entry

### Story 5.3: YouTube Session Coalescing Algorithm

As a user,
I want individual YouTube videos grouped into viewing sessions,
So that my YouTube time appears as meaningful blocks rather than per-video fragments.

**Acceptance Criteria:**

**Given** multiple YouTube video watch records in chronological order
**When** session coalescing is applied
**Then** videos are grouped using sliding window from first video
**And** next video is included if within (previous video duration + 30 minutes)
**And** session continues until no more videos meet the threshold
**And** 8/15 rounding is applied to total session duration
**And** event description lists unique channels: "YouTube - Channel1, Channel2, Channel3"
**And** overlapping sessions are handled gracefully

**Prerequisites:** Story 4.3 (YouTube data), Story 5.1 (8/15 rounding)

**Technical Notes:**
- Sort YouTube videos by timestamp
- Sliding window: start with first video, calculate window end = video end + 30 min
- Check if next video starts before window end; if yes, include and extend window
- Session end = last included video end time
- Apply 8/15 rounding to (session start, session end)
- Extract unique channel names from all videos in session
- Format description: "YouTube - " + comma-separated channel list (max 5 channels, then "and N more")
- Handle edge cases: overlapping videos (user skipping), videos watched simultaneously
- Store session metadata linking to constituent videos

### Story 5.4: Coalescing Configuration UI

As a user,
I want to configure coalescing algorithm parameters,
So that I can fine-tune the algorithms to match my usage patterns.

**Acceptance Criteria:**

**Given** I access the settings page
**When** I navigate to coalescing configuration
**Then** I can adjust the 8/15 rounding threshold (5-10 minutes)
**And** I can set phone coalescing gap threshold (5-20 minutes)
**And** I can configure YouTube session window (10-60 minutes)
**And** I can set minimum durations for each algorithm
**And** changes are saved and applied immediately to new processing
**And** I can reset to default values

**Prerequisites:** Story 5.1, 5.2, 5.3

**Technical Notes:**
- Add settings page with NumberBox controls for each parameter
- Validate ranges (prevent invalid values like 0 or negative)
- Store settings in AppSettings table
- Provide "Reset to Defaults" button
- Show helpful tooltips explaining what each parameter does
- Add "Preview" mode showing before/after with current settings
- Bind settings to processing algorithms via configuration service

---

## Epic 6: Approval Workflow & Publishing

**Goal:** The satisfying life-review ritual - transform backfilling from chore into "spaced repetition for life" through intuitive approval workflow and batch publishing with delightful feedback.

### Story 6.1: Approval State Management

As a developer,
I want approval state tracked for all pending events,
So that users can review and mark events ready for publishing.

**Acceptance Criteria:**

**Given** events are generated from data sources
**When** approval state is managed
**Then** each event has an approval state: Pending, Approved, Published
**And** approval state is stored in ApprovedEvents table
**And** state transitions are tracked with timestamps
**And** unapprove capability exists (change from Approved back to Pending)
**And** visual indicators show state clearly in UI

**Prerequisites:** Story 3.2 (event rendering with visual distinction)

**Technical Notes:**
- Add ApprovalState enum: Pending, Approved, Published
- ApprovedEvents table stores event ID, state, approval timestamp
- Implement state machine: Pending → Approved → Published (with Approved → Pending rollback)
- Track who approved (in multi-user future) and when
- Subtle visual indicator for Approved state (maybe slight opacity change from Pending yellow)
- Persist approval state across app sessions

### Story 6.2: Batch Approval Operations

As a user,
I want to approve multiple events at once,
So that I can efficiently process a day's or week's worth of events.

**Acceptance Criteria:**

**Given** I have selected multiple pending events
**When** I click "Approve selected"
**Then** all selected events transition to Approved state
**And** the approved count badge updates
**And** events show subtle visual change (remain yellow but marked approved)
**And** I can approve individual events, entire days, or date ranges
**And** approval action is logged in audit trail

**Prerequisites:** Story 3.3 (event selection), Story 6.1 (approval state)

**Technical Notes:**
- Implement ApproveCommand bound to selection
- Batch update ApprovedEvents table for all selected event IDs
- Show notification: "47 events approved"
- Update UI reactively using observable collections
- Log batch approval in AuditLog table
- Keyboard shortcut: Ctrl+A to approve selected
- Implement "Approve all pending" button for power users

### Story 6.3: Batch Publishing to Google Calendar

As a user,
I want to publish all approved events to Google Calendar in one operation,
So that I can confidently commit reviewed events without repetitive actions.

**Acceptance Criteria:**

**Given** I have approved events ready to publish
**When** I click "Publish to Google Calendar"
**Then** all approved events are sent to Google Calendar API in batches
**And** progress is shown: "Publishing 47 events... (23/47)"
**And** successful publishes receive Google Calendar event IDs stored locally
**And** events transition from Approved to Published state
**And** success animation shows yellow → final color transition
**And** failures are clearly indicated with retry option

**Prerequisites:** Story 2.3 (publish API), Story 6.2 (batch approval)

**Technical Notes:**
- Use Google Calendar API batch requests (max 50 events per batch)
- Show progress dialog with cancellation option
- Store returned event IDs in GoogleCalendarEvents table
- Update ApprovalState to Published
- Implement success animation: smooth color transition over 300ms
- Handle partial failures: show list of failed events, allow selective retry
- Log publish operation in AuditLog with event count and timestamp
- Create automatic save point before publishing (safety net)

### Story 6.4: App-Published Notation in Descriptions

As a system,
I want to append publication metadata to event descriptions,
So that app-published events are distinguishable from manually created ones.

**Acceptance Criteria:**

**Given** an event is being published to Google Calendar
**When** the API request is made
**Then** the description includes user content followed by notation
**And** notation format: "\n\nPublished by Google Calendar Management on {ISO 8601 datetime}"
**And** notation doesn't interfere with user-entered descriptions
**And** notation is visible in Google Calendar web and mobile apps

**Prerequisites:** Story 6.3 (batch publishing)

**Technical Notes:**
- Append to description before API call: `description += "\n\nPublished by Google Calendar Management on " + DateTime.UtcNow.ToString("o")`
- ISO 8601 format ensures unambiguous timestamp
- Handle null/empty descriptions (just add notation)
- Store original description separately in local database (without notation)
- Update detection: don't duplicate notation on re-publish

### Story 6.5: Undo Recent Publish

As a user,
I want to quickly undo a publish operation if I notice mistakes,
So that I can correct errors without manual cleanup in Google Calendar.

**Acceptance Criteria:**

**Given** I just published events to Google Calendar
**When** I click "Undo publish"
**Then** a confirmation dialog shows what will be undone
**And** all events from the last publish are deleted from Google Calendar
**And** events transition back to Approved state locally
**And** Google Calendar event IDs are cleared
**And** undo option expires after next publish or app close
**And** undo action is logged in audit trail

**Prerequisites:** Story 6.3 (batch publishing)

**Technical Notes:**
- Track most recent publish operation in memory: event IDs and timestamp
- Implement UndoCommand with confirmation dialog
- Use Google Calendar API events.delete for each event ID
- Batch delete requests (max 50 per batch)
- Clear Published state, revert to Approved
- Remove Google Calendar event IDs from database
- Show notification: "Undone: 47 events removed from Google Calendar"
- Expire undo buffer on app close or next publish operation
- Log undo in AuditLog table

### Story 6.6: Success Feedback & Delightful Animations

As a user,
I want satisfying visual feedback when publishing succeeds,
So that the approval ritual feels rewarding and fun.

**Acceptance Criteria:**

**Given** events are successfully published
**When** the operation completes
**Then** a success animation plays (yellow → final color smooth transition)
**And** a success message appears: "✓ 47 events published successfully"
**And** subtle celebration effect (confetti or pulse animation - optional)
**And** smooth animations maintain 60 FPS
**And** animations can be disabled in settings for users who prefer minimal UI

**Prerequisites:** Story 6.3 (batch publishing)

**Technical Notes:**
- Implement color transition animation using WinUI 3 Storyboard
- Duration: 300-500ms for smooth but noticeable transition
- Use easing functions: EaseOutQuad for natural feel
- Optional: confetti effect using Lottie animations (CommunityToolkit.WinUI.Lottie)
- Success notification with checkmark icon
- Settings toggle: "Enable success animations" (default: on)
- Ensure animations don't block UI (run asynchronously)

---

## Epic 7: Date State & Progress Tracking

**Goal:** Always know where you left off - systematic tracking of completion status per data source per date, visual indicators of progress, and contiguity edge calculation.

### Story 7.1: Date State Flags Schema & Management

As a developer,
I want per-date state flags tracked in the database,
So that completion status can be managed granularly by data source.

**Acceptance Criteria:**

**Given** the database is initialized
**When** dates are processed
**Then** DateStates table tracks 7 flags per date:
- call_log_data_published
- sleep_data_published
- youtube_data_published
- toggl_data_published
- named
- complete_walkthrough_approval
- part_of_tracked_gap
**And** flags default to false for new dates
**And** flags update automatically when relevant events are published
**And** manual flag override is supported

**Prerequisites:** Story 1.2 (database)

**Technical Notes:**
- DateStates table: Date (PK), 7 boolean flags, LastUpdated timestamp
- Auto-update flags when events published (e.g., publishing YouTube events sets youtube_data_published = true for those dates)
- Implement date state service with methods: GetDateState, UpdateFlag, BulkUpdateFlags
- Track "named" flag for dates that have been reviewed and contextualized
- "complete_walkthrough_approval" set when all relevant sources published and user confirms date complete
- "part_of_tracked_gap" for intentionally incomplete dates (vacations, etc.)

### Story 7.2: Visual Completion Indicators in Calendar

As a user,
I want to see which dates are complete, partial, or empty at a glance,
So that I know where to focus my backfilling efforts.

**Acceptance Criteria:**

**Given** the calendar is displayed
**When** I view any month or week
**Then** dates show color-coded indicators:
- Green dot: complete_walkthrough_approval = true
- Yellow dot: some flags true, but not complete
- Grey dot: no data published
**And** hovering shows detailed status (which sources are complete)
**And** indicators are visible in month, week, and day views
**And** indicators don't obscure event content

**Prerequisites:** Story 3.1 (calendar views), Story 7.1 (date state tracking)

**Technical Notes:**
- Add indicator dots to date header in calendar view
- Use data binding to DateStates for reactive updates
- Tooltip on hover showing: "✓ Toggl, ✓ YouTube, ✗ Calls, ✗ Outlook"
- Position indicators in corner or header without blocking events
- Use accessible colors (green, amber, grey)
- Update indicators in real-time as flags change

### Story 7.3: Contiguity Edge Calculation

As a user,
I want the system to calculate the last complete date (contiguity edge),
So that I know exactly where to start "fill to present" workflow.

**Acceptance Criteria:**

**Given** date states are tracked
**When** the contiguity edge is calculated
**Then** the edge is identified as the most recent date where complete_walkthrough_approval = true
**And** all subsequent dates are incomplete (except tracked gaps)
**And** the edge date is displayed prominently in the UI
**And** edge updates automatically when dates are completed
**And** "Jump to edge" button navigates directly to that date

**Prerequisites:** Story 7.1 (date states), Story 7.2 (visual indicators)

**Technical Notes:**
- Algorithm: SELECT MAX(Date) FROM DateStates WHERE complete_walkthrough_approval = true
- Exclude future tracked gaps: WHERE NOT part_of_tracked_gap OR Date <= TODAY
- Display edge in status bar or header: "Contiguity edge: Nov 3, 2025 (3 days behind)"
- Calculate days behind: TODAY - edge date
- Implement "Jump to contiguity edge" navigation button
- Recalculate edge on any date state update
- Cache edge calculation (only recompute when states change)

### Story 7.4: "Fill to Present" Workflow Initialization

As a user,
I want to start the fill-to-present workflow from the contiguity edge,
So that I can systematically backfill up to today.

**Acceptance Criteria:**

**Given** the contiguity edge is calculated
**When** I click "Fill to present"
**Then** the workflow navigates to the first incomplete date after the edge
**And** data sources for that date are pre-loaded for review
**And** I'm guided through approving events for each data source
**And** completing the date updates the contiguity edge forward
**And** workflow continues to next date until present is reached

**Prerequisites:** Story 7.3 (contiguity edge)

**Technical Notes:**
- Implement wizard-style workflow: "Filling Nov 4, 2025 (3 days remaining)"
- For each date: show data sources, events to approve, completion status
- Button to mark date complete (sets complete_walkthrough_approval = true)
- Auto-advance to next date on completion
- Show overall progress: "Day 1 of 3" or progress bar
- Allow pausing workflow (resume later from same point)
- Celebrate when reaching present: "All caught up! 🎉"

---

## Epic 8: Save/Restore & Version Management

**Goal:** Safety net for experimentation and error recovery - never lose data through comprehensive versioning, save points, and rollback capabilities.

### Story 8.1: Create Save Points (Snapshot Google Calendar State)

As a user,
I want to create save points of my current Google Calendar state,
So that I can experiment safely knowing I can roll back if needed.

**Acceptance Criteria:**

**Given** I want to create a safety checkpoint
**When** I create a save point
**Then** all events in the specified date range are snapshot to local database
**And** save point metadata is stored: name, timestamp, date range, event count
**And** I can provide a custom name or use auto-generated timestamp name
**And** save point creation is fast (<5 seconds for 500 events)
**And** save points list shows all available checkpoints with previews

**Prerequisites:** Story 2.2 (local event cache), Story 1.2 (database)

**Technical Notes:**
- SavePoints table: ID, Name, CreatedAt, DateRangeStart, DateRangeEnd, EventCount
- SavePointEvents table: SavePointID, full event snapshot (title, start, end, description, color, GoogleCalEventID)
- Implement CreateSavePoint command: fetch current GCal state, store in SavePointEvents
- Default name: "Save Point - {datetime}"
- Allow custom naming via dialog
- Show save point list with: name, date, event count, date range
- Limit date range to reduce snapshot size (default: 1 year, user configurable)

### Story 8.2: Restore to Save Point with Diff Preview

As a user,
I want to restore Google Calendar to a previous save point,
So that I can undo bulk changes or recover from mistakes.

**Acceptance Criteria:**

**Given** I have created save points
**When** I select a save point to restore
**Then** a diff preview shows what will change:
- Events to be added (in save point but not in current GCal)
- Events to be removed (in current GCal but not in save point)
- Events to be modified (different in save point vs current GCal)
**And** I must confirm the restore operation
**And** a new save point is auto-created before restoring (safety)
**And** restore operation sends updates to Google Calendar
**And** conflicts are handled (events deleted externally since save)

**Prerequisites:** Story 8.1 (create save points), Story 2.4 (update events)

**Technical Notes:**
- Fetch current Google Calendar state for date range
- Compare with SavePointEvents to generate diff
- Display diff in three categories: Add (green), Remove (red), Modify (yellow)
- Confirmation dialog: "This will change 47 events. Continue?"
- Auto-create "Before restore" save point
- Use Google Calendar API: events.insert for adds, events.delete for removes, events.update for modifications
- Handle conflicts: if event deleted externally, skip and log warning
- Show restore progress: "Restoring... (23/47)"
- Log restore operation in AuditLog

### Story 8.3: Save Point Management UI

As a user,
I want to manage my save points (view, delete, rename),
So that I can keep my checkpoints organized.

**Acceptance Criteria:**

**Given** I have multiple save points
**When** I access the save point management page
**Then** all save points are listed with metadata
**And** I can preview event count and date range for each
**And** I can rename save points
**And** I can delete old/unnecessary save points
**And** deletion requires confirmation
**And** I can export save points for external backup

**Prerequisites:** Story 8.1 (save points)

**Technical Notes:**
- Create SavePointsPage with ListView showing all save points
- Columns: Name, Created Date, Date Range, Event Count, Actions
- Implement RenameCommand (inline edit or dialog)
- Implement DeleteCommand with confirmation: "Delete save point '{name}'?"
- Delete removes entries from SavePoints and SavePointEvents tables
- Export save point: save as JSON file with all event data
- Import save point: restore from exported JSON file
- Sort save points by creation date (newest first)

---

## Epic 9: Import Workflows & Data Management

**Goal:** Effortless data ingestion and operational workflows - drag & drop imports, API fetch buttons, data export capabilities, and weekly status tracking with Excel cloud sync.

### Story 9.1: Drag & Drop File Import Interface

As a user,
I want to drag files into the application for import,
So that I can quickly ingest data without navigating file dialogs.

**Acceptance Criteria:**

**Given** the application is open
**When** I drag a file (CSV, JSON, .ics) into the app window
**Then** the file type is auto-detected
**And** an import preview shows: file type, estimated events, date range
**And** I can confirm or cancel the import
**And** supported formats: Call logs CSV, YouTube JSON, Outlook .ics
**And** invalid files show clear error messages

**Prerequisites:** Story 4.2, 4.3, 4.5 (file parsers)

**Technical Notes:**
- Implement drag & drop zone using WinUI 3 DragDrop APIs
- Auto-detect file type: extension and content sniffing
- Parse file and generate preview without committing to database
- Show import dialog: "Import 47 call logs from Nov 1-7, 2025?"
- Support multiple file drop (queue imports)
- Validate file structure before showing preview
- Error handling: "Invalid CSV structure - missing Duration column"

### Story 9.2: API Fetch Buttons with Progress Indicators

As a user,
I want one-click buttons to fetch data from APIs,
So that I can effortlessly retrieve new data from Toggl, Outlook, and YouTube.

**Acceptance Criteria:**

**Given** I'm authenticated with data source APIs
**When** I click a fetch button (Toggl, Outlook, YouTube)
**Then** a date range picker appears (default: contiguity edge to today)
**And** fetch progress is shown: "Fetching Toggl data... (47 entries found)"
**And** I can cancel the fetch operation
**And** import summary shows new events count and date range
**And** last sync timestamp is displayed on each button
**And** auto-navigate to date range with new data after import

**Prerequisites:** Story 4.1, 4.3, 4.4 (API integrations)

**Technical Notes:**
- Create FetchDataPage with buttons for each API source
- Show last sync timestamp from AppSettings
- Date range picker defaults to [contiguity_edge + 1, today]
- Progress dialog with cancellation token
- Update last sync timestamp after successful fetch
- Show import summary: "Fetched 47 Toggl entries, 12 Outlook events"
- Navigate to earliest date with new data after import
- Handle API errors gracefully with retry option

### Story 9.3: Data Export to CSV/JSON

As a user,
I want to export my data to standard formats,
So that I can backup or analyze data externally.

**Acceptance Criteria:**

**Given** I want to export data
**When** I select export options
**Then** I can choose which tables to export (all or specific)
**And** I can filter by date range
**And** I can select format: CSV or JSON
**And** export generates files for each selected table
**And** progress is shown for large exports
**And** exported files include privacy warning header

**Prerequisites:** Story 1.2 (database access)

**Technical Notes:**
- Implement ExportService with methods: ExportTable, ExportAll
- Support formats: CSV (for spreadsheet analysis), JSON (for programmatic access)
- Date range filter applies to date-based tables
- CSV: proper escaping of commas and quotes
- JSON: pretty-print with indentation
- Add header/metadata to exports: "Exported from Google Calendar Management on {date}"
- Privacy warning: "This file contains personal data. Handle securely."
- Show progress: "Exporting GoogleCalendarEvents... (1,247 rows)"
- Save to user-selected folder with default naming: {TableName}_{Date}.csv

### Story 9.4: Weekly Status Calculation

As a system,
I want to calculate weekly completion status per data source,
So that users can track their backfilling progress systematically.

**Acceptance Criteria:**

**Given** date states are tracked
**When** weekly status is calculated
**Then** status is computed per ISO 8601 week per data source:
- "Yes" if all 7 days have that data source published
- "Partial" if some days (1-6) have it published
- "No" if zero days have it published
**And** week numbering follows ISO 8601 (Monday start, Week 1 has ≥4 days)
**And** historical weeks can be calculated on demand
**And** current week updates in real-time as data is published

**Prerequisites:** Story 7.1 (date states)

**Technical Notes:**
- Implement ISO 8601 week calculation: week starts Monday, Week 1 = first week with ≥4 days in year
- Query DateStates grouped by ISO week
- For each week + source: count days where source flag = true
- Mapping: 7 days = "Yes", 1-6 days = "Partial", 0 days = "No"
- Store in WeeklyStatusRecords table: Year, Week, TogglStatus, YouTubeStatus, CallsStatus, OutlookStatus
- Recalculate on publish operations (affected weeks only)
- Show weekly status view: calendar weeks with colored cells (green/yellow/red)

### Story 9.5: Excel Cloud Sync via Microsoft Graph

As a user,
I want weekly status automatically synced to Excel on OneDrive,
So that I can view my completion tracking in spreadsheet form.

**Acceptance Criteria:**

**Given** I'm authenticated with Microsoft Graph
**When** weekly status sync is triggered
**Then** Excel file is created on OneDrive if doesn't exist
**And** weekly status is written: rows = weeks, columns = data sources, values = Yes/Partial/No
**And** existing data is updated without overwriting entire file
**And** conflicts are handled (file open in Excel)
**And** manual sync trigger available
**And** auto-sync after publish operations (optional, configurable)

**Prerequisites:** Story 9.4 (weekly status calculation), Story 1.4 (OAuth for Microsoft Graph)

**Technical Notes:**
- Use Microsoft Graph API: /me/drive/root:/path/to/file.xlsx:/content
- Create Excel file structure: Column headers = Toggl, YouTube, Calls, Outlook; Row headers = "2025-W01", "2025-W02", etc.
- Use Microsoft Graph Excel API: PATCH /workbook/worksheets/{id}/range(address='A1:E10')
- Update specific cells without overwriting formulas/formatting user may have added
- Handle 423 Locked errors (file open in Excel): retry with backoff
- Configurable file location: OneDrive or SharePoint
- Show sync status: "Last synced: 2 minutes ago"
- Manual sync button + auto-sync setting (default: on after publish)

---

## Epic 10: Polish & Production Readiness

**Goal:** Delightful, reliable daily experience - performance optimization, comprehensive error handling, keyboard shortcuts, theming, and final UX refinements for decades of sustained use.

### Story 10.1: Performance Optimization

As a user,
I want the application to feel fast and responsive,
So that my workflow is never interrupted by lag or slowness.

**Acceptance Criteria:**

**Given** the application is in use
**When** I perform common operations
**Then** calendar month view renders in <1 second for 200+ events
**And** event selection feedback appears in <50ms
**And** inline editing opens instantly (<100ms)
**And** app cold start completes in <2 seconds
**And** database queries complete in <100ms for typical operations
**And** Google Calendar sync (50 events) completes in <10 seconds

**Prerequisites:** All previous stories (performance testing on complete app)

**Technical Notes:**
- Profile with Visual Studio Performance Profiler
- Optimize database queries: add indexes on Date, ApprovalState, GoogleCalEventID
- Use virtualization for calendar view (only render visible events)
- Lazy load event details (fetch on demand when editing)
- Cache frequently accessed data (ColorDefinitions, AppSettings) in memory
- Use async/await properly to avoid blocking UI thread
- Minimize XAML overhead: use data virtualization, template recycling
- Batch database operations where possible
- Implement background loading with skeleton screens instead of spinners

### Story 10.2: Comprehensive Error Handling & User-Friendly Messages

As a user,
I want clear, actionable error messages when something goes wrong,
So that I know what happened and how to fix it.

**Acceptance Criteria:**

**Given** an error occurs
**When** the error is presented to the user
**Then** the message is user-friendly (no technical jargon)
**And** actionable recovery steps are provided
**And** critical errors auto-save state before exit
**And** non-blocking errors don't halt workflow
**And** all errors are logged to AuditLog for debugging

**Prerequisites:** All previous stories (comprehensive error handling across app)

**Technical Notes:**
- Replace generic exceptions with user-friendly messages:
  - Bad: "NullReferenceException at line 247"
  - Good: "Couldn't load event details. Try refreshing the calendar."
- Provide action buttons in error dialogs: "Retry", "Reconnect Toggl", "Report Issue"
- Implement global exception handler that catches unhandled exceptions
- Auto-save application state before crash (SavePoints, ApprovedEvents buffer)
- Log all errors to AuditLog with stack trace for developer debugging
- Network errors: "Connection lost. Changes saved locally and will sync when online."
- API quota errors: "YouTube API limit reached. Quota resets at midnight PT."

### Story 10.3: Keyboard Shortcuts for Power Users

As a power user,
I want keyboard shortcuts for common actions,
So that I can work efficiently without relying on mouse.

**Acceptance Criteria:**

**Given** the application is in use
**When** I press keyboard shortcuts
**Then** common actions execute immediately:
- Ctrl+A: Approve selected events
- Ctrl+P: Publish approved events
- Ctrl+S: Create save point
- Ctrl+Z: Undo recent publish
- Ctrl+F: Focus search/filter
- Ctrl+T: Jump to today
- Arrow keys: Navigate dates
- Esc: Cancel/close current operation
**And** shortcuts are shown in tooltips and help documentation

**Prerequisites:** Story 3.1+ (calendar interactions), Story 6+ (approval workflow)

**Technical Notes:**
- Implement KeyboardAccelerator in WinUI 3 for global shortcuts
- Use Commands pattern to centralize action logic (reused by buttons and shortcuts)
- Show shortcuts in button tooltips: "Approve Selected (Ctrl+A)"
- Create keyboard shortcuts help page: Ctrl+? opens reference
- Ensure shortcuts don't conflict with system or browser shortcuts
- Context-sensitive shortcuts (e.g., Ctrl+E for edit only when event selected)
- Support Vim-style navigation as easter egg (h/j/k/l for power users)

### Story 10.4: Dark Mode Support & Theme Awareness

As a user,
I want the application to respect my Windows theme preference,
So that it integrates seamlessly with my system appearance.

**Acceptance Criteria:**

**Given** I have set a Windows theme preference (light or dark)
**When** the application launches
**Then** the app theme matches Windows theme automatically
**And** I can manually override to force light or dark mode
**And** theme changes apply immediately without restart
**And** calendar colors remain vibrant and distinguishable in both themes
**And** text contrast meets readability standards in both themes

**Prerequisites:** Story 3+ (calendar UI)

**Technical Notes:**
- Use WinUI 3 ElementTheme: Default (system), Light, Dark
- Listen to system theme changes: UISettings.ColorValuesChanged
- Provide theme selector in settings: Auto (default), Light, Dark
- Adjust calendar color palette for dark mode (ensure sufficient contrast)
- Test all 9 custom colors in both themes for readability
- Use theme-aware colors for chrome/UI (accent colors from Windows)
- Store theme preference in AppSettings

### Story 10.5: Database Integrity Checks & Audit Log Viewer

As a user,
I want confidence that my data is intact,
So that I can trust the system with decades of personal history.

**Acceptance Criteria:**

**Given** the application manages critical personal data
**When** the app launches
**Then** database integrity check runs automatically
**And** foreign key constraints are validated
**And** orphaned records are detected and reported
**And** I can view the audit log to see all operations
**And** audit log includes: timestamp, operation type, affected records, user action
**And** I can filter audit log by date range, operation type, or table

**Prerequisites:** Story 1.2 (database), all subsequent stories (logging throughout)

**Technical Notes:**
- Implement database integrity check: PRAGMA integrity_check, PRAGMA foreign_key_check
- Check for orphaned records: events without corresponding source data, etc.
- Run integrity check on startup (async, non-blocking)
- Show warning if integrity issues found: "Database integrity check found 3 issues. View details?"
- Audit log viewer: paginated table with filters
- Audit log entries: {Timestamp, OperationType, TableName, RecordID, Description, Success}
- Log all writes, deletes, updates automatically using EF Core interceptors
- Export audit log to CSV for external analysis

### Story 10.6: Final UX Refinements & Micro-Interactions

As a user,
I want delightful micro-interactions and polished UX details,
So that using the application is genuinely enjoyable.

**Acceptance Criteria:**

**Given** the application is feature-complete
**When** I use it daily
**Then** hover states provide subtle feedback on interactive elements
**And** transitions between states are smooth (60 FPS)
**And** loading states use skeleton screens instead of spinners
**And** empty states provide helpful guidance (not just blank screens)
**And** success states celebrate completion appropriately
**And** overall aesthetic is clean, modern, and beautiful

**Prerequisites:** All previous stories (final polish pass)

**Technical Notes:**
- Add hover effects: subtle scale or opacity change on buttons
- Smooth transitions: use WinUI 3 animations (FadeIn, SlideIn, Scale)
- Loading skeletons: placeholder shimmer animations while fetching data
- Empty states: "No events yet. Import data to get started." with action button
- Success celebrations: checkmark animations, subtle confetti (from Story 6.6)
- Consistent spacing: 8px grid system throughout app
- Typography: clear hierarchy with WinUI 3 type ramp
- Icons: use Fluent Icons consistently
- Color consistency: accent color usage, semantic colors (success green, error red)
- Polish calendar rendering: smooth scrolling, crisp text, aligned grid lines

---

_For implementation: Use the `create-story` workflow to generate individual story implementation plans from this epic breakdown._

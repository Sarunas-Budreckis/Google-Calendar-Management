# Google Calendar Management - Epic Breakdown

**Author:** Sarunas Budreckis
**Date:** 2026-01-30
**Project Level:** Level 2 (Medium Complexity Desktop Application)
**Target Scale:** Personal single-user Windows desktop application

---

## Overview

This document provides the epic and story breakdown for **Epics 1, 2, and 3** of Google Calendar Management, decomposing the requirements from the [PRD](./PRD.md) into implementable stories.

### Epic Summary

This breakdown focuses on the foundational infrastructure, core Google Calendar integration, and primary user interface - the essential building blocks for the life-review ritual experience.

**Epic 1: Foundation & Project Setup**
- Establishes .NET 9 + WinUI 3 project structure with SQLite database
- Creates development environment and deployment pipeline
- Sets up Entity Framework Core with core database schema
- **Value:** Technical foundation enabling all subsequent development
- **Sequencing:** MUST be first - all other epics depend on this infrastructure

**Epic 2: Google Calendar Integration & Sync (Read-Only)**
- Implements OAuth 2.0 authentication with Google Calendar API
- Fetches existing events from Google Calendar and caches locally
- Automatic version history (new pulls overwrite current, old data preserved)
- Sync status indicators (green/grey per date)
- **Value:** View existing calendar in local app - Tier 1 delivery
- **Sequencing:** Second - provides immediate value (read-only calendar viewer)

**Epic 3: Local Calendar UI & Event Management**
- Builds WinUI 3 calendar views (year/month/week/day modes)
- Implements event creation and editing with visual state feedback
- Creates intuitive selection and navigation experience
- **Value:** Primary user experience - the "single pane of glass" transformation
- **Sequencing:** Third - builds on Epic 2's data layer to create user interaction surface

**Why This Grouping Makes Sense:**
- **Foundation First:** Epic 1 establishes infrastructure before any features
- **Value-Driven:** Each epic delivers clear business capability, not technical layers
- **Independently Valuable:** Each can be demonstrated and tested standalone
- **Logical Dependencies:** Clear sequential flow (infrastructure → data → UI)
- **Cohesive Scope:** Related stories that work together within each epic

---

## Epic 1: Foundation & Project Setup

**Goal:** Establish the technical foundation enabling all subsequent development

---

### Story 1.1: Create .NET 9 WinUI 3 Project Structure

As a **developer**,
I want **a properly configured .NET 9 WinUI 3 desktop application project**,
So that **I have a working Windows desktop app shell to build features upon**.

**Acceptance Criteria:**

**Given** a clean development environment
**When** I create the project using .NET 9 and Windows App SDK
**Then** the application compiles and launches with a basic window

**And** the project structure follows .NET best practices:
- Proper folder organization (Models, Views, ViewModels, Services, Data)
- Dependency injection configured in App.xaml.cs
- .editorconfig and .gitignore configured
- Target framework set to net9.0-windows10.0.19041.0 or later

**And** basic WinUI 3 window displays:
- MainWindow.xaml with placeholder UI
- Application launches to 1024x768 default window size
- Window is resizable with min-width/min-height constraints

**Prerequisites:** None - first story

**Technical Notes:**
- Use Visual Studio 2022 with Windows App SDK 1.5+
- Reference: FR-8.1 (local storage), NFR-M1 (code quality), Desktop Application Specific Requirements section
- Enable hot reload for faster development iterations
- **CRITICAL:** Create `GoogleCalendarManagement.Tests` project in solution from Story 1.1
- Set up xUnit test project with FluentAssertions, Moq packages
- Testing framework established in first story, used by all subsequent stories

---

### Story 1.2: Configure SQLite Database with Entity Framework Core

As a **developer**,
I want **Entity Framework Core configured with SQLite provider**,
So that **I have a robust ORM for local data persistence**.

**Acceptance Criteria:**

**Given** the .NET 9 WinUI 3 project from Story 1.1
**When** I configure Entity Framework Core with SQLite
**Then** the application creates and connects to a SQLite database on first launch

**And** the DbContext is properly configured:
- DbContext class created with dependency injection
- Database file stored in user's AppData folder
- Connection string managed via configuration
- Migrations system enabled

**And** database infrastructure is testable:
- DbContext can be instantiated
- Database file created at expected location
- Can execute basic CRUD operations
- Database schema version tracked

**Prerequisites:** Story 1.1 (project structure)

**Technical Notes:**
- Install NuGet packages: Microsoft.EntityFrameworkCore.Sqlite, Microsoft.EntityFrameworkCore.Design
- Database path: `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db`
- Enable WAL mode for crash recovery (NFR-D1)
- Reference: FR-8.1 (SQLite storage), NFR-D3 (database integrity)
- Set up database factory pattern for testability

---

### Story 1.3: Implement Core Database Schema (Tier 1 Tables)

As a **developer**,
I want **the initial database schema for Google Calendar events and app metadata**,
So that **I can store fetched calendar data locally**.

**Acceptance Criteria:**

**Given** Entity Framework Core configured from Story 1.2
**When** I create the initial database migration
**Then** the following tables are created with proper relationships:

**GoogleCalendarEvents table:**
- Id (GUID, primary key)
- GoogleEventId (string, nullable - null until published)
- Title (string, required)
- Description (string, nullable)
- StartTime (DateTime, required)
- EndTime (DateTime, required)
- ColorId (string, required)
- IsPublished (boolean, default false)
- CreatedAt (DateTime, required)
- UpdatedAt (DateTime, required)
- Proper indexes on GoogleEventId, StartTime, IsPublished

**gcal_event_version table (fielded snapshot — no JSON blob):**
- version_id (integer, primary key, auto-increment)
- gcal_event_id (string, FK → gcal_event with Restrict delete)
- gcal_etag, summary, description, start_datetime, end_datetime, is_all_day, color_id (snapshot of Google-facing fields)
- gcal_updated_at (nullable DateTime — Google's last-modified timestamp at snapshot time)
- recurring_event_id (nullable string)
- is_recurring_instance (boolean, default false)
- changed_by (string — e.g. "gcal_sync")
- change_reason (string — "updated" or "deleted")
- created_at (DateTime, required)
- Index on (gcal_event_id, created_at)

**AppMetadata table:**
- Key (string, primary key)
- Value (string, required)
- UpdatedAt (DateTime, required)

**And** the migration applies successfully:
- Database schema matches Entity Framework model
- Foreign key constraints enforced
- Default values applied correctly

**Prerequisites:** Story 1.2 (EF Core setup)

**Technical Notes:**
- Reference: FR-8.1 (database schema), NFR-D1 (version history), NFR-D4 (audit trail)
- Use EF Core fluent API for configuration, not data annotations
- gcal_event_version is a fielded snapshot table (not a JSON blob); each row captures the pre-overwrite state of the live gcal_event row; rollback implementation belongs to Epic 8
- This is Tier 1 schema - additional tables added in later epics (Toggl, Calls, YouTube, DateStates, SavePoints)
- Consider using DateTimeOffset for timezone handling

---

### Story 1.4: Implement Automatic Database Versioning and Migration System

As a **developer**,
I want **automatic database schema migrations on app startup**,
So that **schema updates are applied seamlessly when the app is updated**.

**Acceptance Criteria:**

**Given** a new or existing database
**When** the application starts
**Then** pending migrations are automatically detected and applied

**And** migration safety checks are performed:
- Database backup created before applying migrations
- Backup stored in same AppData folder with timestamp: `calendar_backup_{datetime}.db`
- Migration failures roll back cleanly
- Error logged if migration fails

**And** database integrity is validated:
- Database version stored in AppMetadata table (key: "DatabaseSchemaVersion")
- Integrity check runs on startup (NFR-D3)
- Corrupted database detected and reported to user

**And** migration history is auditable:
- Applied migrations tracked by EF Core
- Migration success/failure logged
- User notified if manual intervention required

**Prerequisites:** Story 1.3 (core schema)

**Technical Notes:**
- Reference: NFR-D1 (data loss prevention), NFR-D3 (database integrity)
- Use `context.Database.Migrate()` on app startup
- Consider using SQLite backup API for atomic backups
- Keep last 5 backups, delete older ones to save space
- Log to file in AppData folder: `logs/migrations.log`

---

### Story 1.5: Set Up Development Environment and Deployment Pipeline

As a **developer**,
I want **a documented development environment setup and build configuration**,
So that **I can build, test, and deploy the application reliably**.

**Acceptance Criteria:**

**Given** the project repository
**When** I follow the setup documentation
**Then** I can build and run the application locally

**And** development environment is documented:
- README.md with prerequisites (VS 2022, .NET 9 SDK, Windows App SDK)
- Step-by-step setup instructions
- Common troubleshooting issues documented
- Required Visual Studio workloads listed

**And** build configuration is optimized:
- Debug configuration for development (with symbols)
- Release configuration for deployment (optimized, trimmed)
- Version number managed in project file
- Build warnings treated as errors for critical issues

**And** basic deployment is functional:
- Can publish as self-contained executable
- Published app runs without .NET runtime installed
- Database migrations work in published build
- No hard-coded paths (all relative or AppData)

**Prerequisites:** Stories 1.1-1.4 (complete foundation)

**Technical Notes:**
- Reference: NFR-M1 (code quality), NFR-M4 (documentation)
- Update Strategy section mentions manual updates for MVP
- Consider publish profiles for different deployment scenarios
- Document minimum Windows version: Windows 10 version 1809 or later
- Include instructions for generating app package for local install

---

### Story 1.6: Implement Application Logging and Error Handling Infrastructure

As a **developer**,
I want **centralized logging and error handling infrastructure**,
So that **I can diagnose issues and track application behavior**.

**Acceptance Criteria:**

**Given** the application is running
**When** any operation occurs
**Then** relevant events are logged to a file

**And** logging system is properly configured:
- Log file location: `%LOCALAPPDATA%\GoogleCalendarManagement\logs\app_{date}.log`
- Log levels: Debug, Info, Warning, Error, Critical
- Automatic log rotation (daily files, keep last 30 days)
- Structured logging with timestamps, level, and context

**And** error handling is consistent:
- Global exception handler catches unhandled exceptions
- Critical errors save app state before exit
- User-friendly error messages (no stack traces shown to user)
- Technical details logged to file for debugging

**And** performance is monitored:
- Slow operations logged (>1 second)
- Database query times tracked
- API call durations recorded

**Prerequisites:** Story 1.1 (project structure)

**Technical Notes:**
- Use Serilog or Microsoft.Extensions.Logging
- Reference: NFR-I3 (error handling), NFR-D1 (auto-save on critical errors)
- Log rotation prevents disk space issues
- Consider separate log files for different concerns (app, database, api)
- Include log viewer in future (Tier 3+) or use external tools

---

## Epic 2: Google Calendar Integration & Sync (Read-Only)

**Goal:** Enable viewing of existing Google Calendar events in local application

---

### Story 2.1: Implement Google Calendar OAuth 2.0 Authentication

As a **user**,
I want **to securely authenticate with my Google account**,
So that **the app can access my Google Calendar data**.

**Acceptance Criteria:**

**Given** I launch the application for the first time
**When** I initiate the Google Calendar connection
**Then** a browser opens with Google OAuth consent screen

**And** OAuth flow completes successfully:
- User authenticates with Google account
- App receives OAuth access token and refresh token
- Tokens encrypted and stored using Windows DPAPI
- Tokens stored in AppMetadata table or secure credential store

**And** token management is robust:
- Access token automatically refreshed when expired
- Refresh token persists across app restarts
- Token expiration handled gracefully
- Clear error message if authentication fails

**And** re-authentication is supported:
- "Reconnect Google Calendar" button in settings
- Existing tokens cleared before re-authentication
- User can switch Google accounts

**Prerequisites:** Story 1.2 (database), Story 1.6 (logging)

**Technical Notes:**
- Use Google.Apis.Calendar.v3 NuGet package
- Reference: NFR-S1 (credential storage), NFR-S3 (API communication)
- OAuth scopes needed: `https://www.googleapis.com/auth/calendar` (full access)
- Store tokens encrypted using Windows Data Protection API (DPAPI)
- Follow Google OAuth 2.0 best practices (PKCE flow)
- Desktop app uses "installed application" OAuth flow

---

### Story 2.2: Fetch Google Calendar Events and Store Locally

As a **user**,
I want **to fetch my Google Calendar events and store them locally**,
So that **I can view my calendar offline and have a local backup**.

**Acceptance Criteria:**

**Given** I am authenticated with Google Calendar (Story 2.1)
**When** I click "Sync with Google Calendar" button
**Then** events are fetched from Google Calendar API

**And** events are fetched efficiently:
- Fetch events for user-specified date range (default: last 6 months)
- Use batch requests to minimize API calls (NFR-I1)
- Progress indicator shows sync status
- Can cancel sync operation mid-flight

**And** events are stored in local database:
- GoogleCalendarEvents table populated with fetched events
- GoogleEventId stored for future updates
- All event properties mapped (title, description, start, end, color)
- IsPublished = true (these came from GCal)

**And** sync handles edge cases:
- Empty calendars handled gracefully
- All-day events vs timed events both supported
- Recurring events expanded into individual instances
- Time zones handled correctly

**Prerequisites:** Story 2.1 (OAuth), Story 1.3 (database schema)

**Technical Notes:**
- Reference: FR-3.1 (unified calendar view), NFR-P2 (data operations <10s for 50 events)
- Google Calendar API quota: 1M requests/day (far exceeds needs - NFR-I1)
- Use `Events.List()` with `MaxResults` and pagination
- Map Google Calendar color IDs to custom color system (Epic 10)
- Handle API rate limits with exponential backoff

---

### Story 2.3: Implement Version History on Calendar Sync

As a **user**,
I want **new pulls from Google Calendar to overwrite current data while preserving history**,
So that **I never lose data and can see what changed**.

**Acceptance Criteria:**

**Given** I have existing Google Calendar events in local database
**When** I sync again from Google Calendar
**Then** updated events overwrite current data

**And** version history is preserved:
- Before overwriting, current event state saved to `gcal_event_version`
- Snapshot stores the prior Google-facing event fields needed for restore and comparison
- `changed_by = "gcal_sync"`
- `change_reason = "updated"`
- Historical rows are append-only and queryable by event and timestamp

**And** version history handles different change types:
- New events from GCal: Create new row in `gcal_event`
- Updated events: Save old version to history, update main row
- Deleted events: Save to history with `change_reason = "deleted"`, mark `is_deleted = true`

**And** history is queryable:
- Can view version history for any event
- Can see what changed between versions
- History preserved indefinitely (never auto-deleted)

**Prerequisites:** Story 2.2 (fetch events), Story 1.3 (EventVersionHistory table)

**Technical Notes:**
- Reference: NFR-D1 (data loss prevention), NFR-D4 (audit trail)
- Detect changes primarily from Google ETag, with field-level fallback if needed
- Use the existing `gcal_event_version` schema rather than a JSON snapshot column
- Automatic version history creation - no user intervention needed

---

### Story 2.3A: Harden Version History Schema and Sync Semantics

As a **developer**,
I want **the version-history model and sync overwrite rules hardened immediately after Story 2.3**,
So that **future sync, status, and rollback work builds on the correct data contract instead of carrying avoidable history and metadata bugs forward**.

**Acceptance Criteria:**

**Given** `gcal_event_version` stores pre-overwrite snapshots
**When** Story 2.3A is implemented
**Then** the history schema is expanded to capture additional rollback-relevant fields:
- `recurring_event_id`
- `is_recurring_instance`
- `gcal_updated_at`

**And** sync preserves local ownership metadata correctly:
- Re-sync overwrite does not forcibly reset `app_created`
- Re-sync overwrite does not forcibly reset `app_published`
- Re-sync overwrite does not forcibly reset `source_system`

**And** history retention is protected at the relational level:
- `gcal_event_version -> gcal_event` no longer uses cascade delete
- Hard deletion of a live row must not silently erase historical rows

**And** documentation is aligned to the implemented schema:
- Planning and story docs stop referring to `EventDataJson` as the active version-history contract
- Story examples and tests use the actual snapshot-table design

**And** rollback enhancements are intentionally staged:
- Snapshot-before-rollback is planned for Epic 8, not bolted into Epic 2
- High-fidelity archival of full Google event payloads is evaluated as Epic 8 follow-up work

**Prerequisites:** Story 2.3 (version-history sync path)

**Technical Notes:**
- This is a hardening story inserted before Stories 2.4 and 2.5 because background and repeated sync work should not amplify the current schema and overwrite semantics
- The change is still within Epic 2 because it adjusts the sync contract, not a separate product capability
- Future rollback work in Epic 8 should snapshot current live state before restoring an older version so rollback itself is undoable
- If exact-fidelity archival of every Google-observed version is desired later, prefer a clearly separate raw payload capture strategy instead of overloading the operational snapshot table by accident

---

### Story 2.4: Display Sync Status Indicators (Green/Grey per Date)

As a **user**,
I want **to see which dates are synced with Google Calendar**,
So that **I know my local data is up-to-date**.

**Acceptance Criteria:**

**Given** I have synced some events from Google Calendar
**When** I view the calendar
**Then** dates show sync status indicators

**And** status indicators are clear:
- **Green indicator:** Date has been synced from Google Calendar
- **Grey indicator:** Date has not been synced yet
- Indicator visible in year, month, and week views
- Tooltip shows last sync timestamp for date

**And** sync status is accurate:
- Status calculated from presence of events with GoogleEventId for that date
- Status updates immediately after sync completes
- Manual refresh available to recalculate status

**And** last sync time is tracked:
- AppMetadata table stores "LastGoogleCalendarSync" timestamp
- Displayed in UI ("Last synced: 2 hours ago")
- Manual sync button shows last sync time

**Prerequisites:** Story 2.2 (fetch events), Epic 3 Story 3.1 (calendar views)

**Technical Notes:**
- Reference: FR-3.1 (unified calendar view), FR-5.1 (date state flags - simplified for Tier 1)
- Green = date has at least one event with GoogleEventId != null
- Grey = date has no synced events (may have local-only events)
- In Tier 1, this is simpler than FR-5.1's full flag system
- Full date state flags (call_log_published, youtube_published, etc.) come in Tier 3

---

### Story 2.5: Implement Background Sync and Cache Management

As a **user**,
I want **the app to automatically sync in the background and manage cache efficiently**,
So that **I always have up-to-date data without manual intervention**.

**Acceptance Criteria:**

**Given** the application is running
**When** I haven't synced in the last 4 hours
**Then** the app automatically syncs in the background

**And** background sync is unobtrusive:
- Runs on background thread (non-blocking UI)
- Only syncs when online (checks network connectivity)
- Respects API rate limits
- Logs sync results but doesn't interrupt workflow

**And** cache is managed intelligently:
- Only fetch events that changed since last sync (use `updatedMin` parameter)
- Incremental sync for recent dates (last 30 days)
- Full sync available as manual option
- Cache size monitored (warn if database >500MB)

**And** offline mode works:
- Cached events viewable offline
- Offline status clearly indicated in UI
- Pending sync queued for when online
- No errors thrown when offline

**Prerequisites:** Story 2.2 (fetch events), Story 2.3 (version history)

**Technical Notes:**
- Reference: NFR-I2 (offline operation), NFR-P3 (background non-blocking)
- Use `Events.List()` with `updatedMin` for incremental sync
- Check network with `NetworkInformation` class
- Consider sync cadence: every 4 hours while app open, on app launch
- Background sync only fetches, doesn't modify Google Calendar (read-only Epic)

---

## Epic 3: Local Calendar UI & Event Management

**Goal:** Create intuitive calendar editing experience with visual feedback

---

### Story 3.1: Build Year/Month/Week/Day Calendar Views

As a **user**,
I want **to view my calendar in year, month, week, and day perspectives**,
So that **I can see my events at different levels of detail**.

**Acceptance Criteria:**

**Given** I have events in the local database
**When** I open the application
**Then** I see a calendar view with my events

**And** multiple view modes are available:
- **Year view:** 12-month grid, events shown as colored dots/bars (app launches here)
- **Month view:** Single month grid, events shown with titles
- **Week view:** 7-day columns, hourly time slots
- **Day view:** Single day, detailed hourly timeline

**And** view switching is smooth:
- Toggle buttons for Year/Month/Week/Day
- Smooth transitions between views (<300ms animation)
- Current view mode persisted across app restarts
- Keyboard shortcuts: Y (year), M (month), W (week), D (day)

**And** navigation is intuitive:
- Previous/Next buttons for each view
- "Today" button jumps to current date
- Date picker for jumping to specific date
- Scroll/swipe gestures supported

**And** performance is fast:
- Month view renders <1 second with 200+ events (NFR-P1)
- Smooth 60 FPS scrolling
- Events load progressively (don't block UI)

**Prerequisites:** Story 2.2 (events in database), Story 1.3 (database schema)

**Technical Notes:**
- Use WinUI 3 CalendarView control as foundation
- Reference: FR-3.1 (unified calendar view), NFR-P1 (UI responsiveness)
- Events fetched from GoogleCalendarEvents table
- Filter events by date range for current view
- Consider virtualization for large event lists
- Year view as default (per PRD: "app launches to year view")

---

### Story 3.2: Display Events with Color-Coded Visual System

As a **user**,
I want **to see events displayed in their assigned colors**,
So that **I can visually understand my life patterns at a glance**.

**Acceptance Criteria:**

**Given** I am viewing the calendar
**When** events are displayed
**Then** each event shows in its assigned color

**And** visual states are distinct:
- **Published events:** Full opacity (100%), solid color
- **Unpushed events:** Translucent (60% opacity) - NOT IN PHASE 1, prepare infrastructure
- Events rendered with proper color from ColorId field

**And** color display is consistent:
- Same color rendering across year/month/week/day views
- Color readable against white/dark backgrounds
- Text color auto-adjusts for contrast (white on dark colors, black on light)

**And** color mapping works:
- ColorId from database maps to custom color definitions
- Default color (Azure #0088CC) for events without color
- Google Calendar color IDs mapped to custom colors

**Prerequisites:** Story 3.1 (calendar views), Story 2.2 (events with ColorId)

**Technical Notes:**
- Reference: FR-3.1 (visual distinction), FR-10 (color system)
- Tier 1: All events are published (100% opacity)
- Tier 2 adds translucent unpushed events (60% opacity)
- Custom color system defined in Epic 10, but use for display now
- Consider WCAG contrast ratios for text on colored backgrounds
- Store color hex values in database for now, full color management in Epic 10

---

### Story 3.3: Implement Event Selection with Visual Feedback

As a **user**,
I want **to select events by clicking on them**,
So that **I can see event details and prepare for editing**.

**Acceptance Criteria:**

**Given** I am viewing the calendar with events
**When** I click on an event
**Then** the event is selected with clear visual feedback

**And** selection is clearly visible:
- Selected event highlighted with red 2px solid outline
- Only one event selected at a time (Tier 1 - single select)
- Selection persists when switching views (same date)
- Clear selection with Esc key or clicking empty space

**And** selection provides event preview:
- Tooltip/hover shows event title and time on hover
- Selected event shows full details in preview pane (see Story 3.4)

**And** selection is performant:
- Selection feedback appears <50ms after click (NFR-P1)
- No lag or jank during selection
- Visual state change is smooth

**Prerequisites:** Story 3.2 (colored events)

**Technical Notes:**
- Reference: FR-3.2 (event editing), FR-3.3 (event selection)
- Tier 1: Single-select only
- Tier 2 adds multi-select (shift-click, drag-select)
- Red outline distinguishes from color-coded event background
- Consider hover state (subtle outline/shadow) separate from selected state

---

### Story 3.4: Create Event Details Panel (Read-Only for Tier 1)

As a **user**,
I want **to view full event details when I select an event**,
So that **I can see all information about the event**.

**Acceptance Criteria:**

**Given** I have selected an event (Story 3.3)
**When** the event is selected
**Then** a details panel appears on the right side of the screen

**And** the panel displays all event information:
- Event title (large, prominent)
- Start and end date/time
- Color indicator (visual swatch + color name)
- Description (full text, scrollable if long)
- Source indicator ("From Google Calendar")
- Last updated timestamp

**And** the panel has good UX:
- Slides in from right (<200ms animation)
- Non-modal (doesn't block calendar view)
- Closes with Esc key or close button
- Persists selection when switching views

**And** the panel is read-only in Tier 1:
- No edit controls visible yet
- "Edit" button disabled with tooltip: "Coming in Tier 2"
- Prepares layout for future editing (Story 3.5)

**Prerequisites:** Story 3.3 (event selection)

**Technical Notes:**
- Reference: FR-3.2 (event editing - panel infrastructure)
- Tier 1: Read-only display
- Tier 2: Add editing controls (Story 3.5)
- Panel width: ~350-400px, full height
- Consider split view pattern (calendar | details panel)
- Use XAML data binding for reactive updates

---

### Story 3.5: Implement Event Editing Panel (Tier 2)

As a **user**,
I want **to edit event details directly in the application**,
So that **I can modify events before publishing to Google Calendar**.

**Acceptance Criteria:**

**Given** I have selected an event (Story 3.3)
**When** I open the event details panel
**Then** I can edit all event properties

**And** editing is smooth and intuitive:
- Click any field to edit in-place
- Title: Text input, required field validation
- Start/End time: Time picker with 15-minute increments
- Color: Color picker showing all 9 custom colors (Story 3.7)
- Description: Multi-line text area

**And** changes auto-save locally:
- Changes saved to local database immediately (0-lag - NFR-P1)
- No explicit "Save" button needed
- UpdatedAt timestamp updated automatically
- IsPublished remains false for new edits (Tier 2)

**And** editing provides feedback:
- Validation errors shown inline (e.g., "End time must be after start time")
- Unsaved indicator if still typing (debounced save after 500ms)
- Undo available for last edit (Ctrl+Z)

**Prerequisites:** Story 3.4 (details panel), Story 1.3 (database schema)

**Technical Notes:**
- Reference: FR-3.2 (event editing), NFR-P1 (0-lag editing)
- Auto-save debounced to avoid excessive database writes
- Use EF Core for database updates
- Validation prevents invalid states (end before start, empty title)
- Esc key closes panel with changes already saved
- Changes only affect local database - not pushed to GCal yet (Epic 7)

---

### Story 3.6: Implement Event Creation (Drag-to-Create and Button)

As a **user**,
I want **to create new events directly on the calendar**,
So that **I can add events that aren't in my Google Calendar yet**.

**Acceptance Criteria:**

**Given** I am viewing the calendar in week or day view
**When** I drag across a time range
**Then** a new event is created for that time span

**And** creation is intuitive:
- Drag vertically in day/week view to create time-based event
- New event appears immediately with default color (Azure)
- Event details panel opens automatically for editing
- Default title: "New Event"

**And** alternative creation method works:
- "+ Add Event" button in toolbar
- Button opens dialog to select date/time
- Creates event at selected time
- Opens details panel for editing

**And** newly created events are local-only:
- IsPublished = false
- GoogleEventId = null
- Displayed as translucent (60% opacity)
- Marked as "Not yet published to Google Calendar"

**Prerequisites:** Story 3.5 (editing panel), Story 3.2 (visual system)

**Technical Notes:**
- Reference: FR-3.2 (event creation), FR-4.1 (unpushed visual state)
- Drag-to-create only in week/day views (not year/month - too coarse)
- Snap to 15-minute increments during drag (align with 8/15 rounding)
- Default color: Azure (#0088CC)
- Creation instantly saved to database (auto-save system)
- Publishing to GCal comes in Epic 7 (approval workflow)

---

### Story 3.7: Implement Color Picker for Event Colors

As a **user**,
I want **to assign colors to events using a visual color picker**,
So that **I can categorize events by mental state**.

**Acceptance Criteria:**

**Given** I am editing an event (Story 3.5)
**When** I click the color field
**Then** a color picker appears with all 9 custom colors

**And** color picker is user-friendly:
- Shows all 9 colors with labels: Azure, Purple, Yellow, Navy, Sage, Grey, Flamingo, Orange, Lavender
- Each color shows hex value and name
- Current color highlighted
- Click color to apply immediately
- Picker closes automatically after selection

**And** color application works:
- Selected color saves to database immediately (ColorId field)
- Event re-renders with new color
- Change visible across all views
- Color change recorded in version history

**And** default color handling:
- New events default to Azure (#0088CC)
- Can set different default color in future (Epic 10)

**Prerequisites:** Story 3.5 (event editing)

**Technical Notes:**
- Reference: FR-3.2 (color picker), FR-10 (color system)
- Hardcode 9 colors for Tier 1-3
- Epic 10 adds full color management (edit colors, descriptions)
- ColorId stored as string in database (hex value or color name)
- Consider grid layout: 3 rows x 3 columns for color picker
- Dropdown or popover pattern for picker

---

### Story 3.8: Add Date Navigation and Jump-to-Date Features

As a **user**,
I want **to quickly navigate to any date in my calendar**,
So that **I can efficiently review different time periods**.

**Acceptance Criteria:**

**Given** I am viewing the calendar
**When** I want to navigate to a different date
**Then** I have multiple navigation options

**And** navigation controls are available:
- Previous/Next buttons (already in Story 3.1)
- "Today" button - jumps to current date
- "Jump to date" button - opens date picker for specific date
- Keyboard shortcuts: ← → for prev/next, T for today

**And** navigation is contextual:
- In year view: prev/next moves by year
- In month view: prev/next moves by month
- In week view: prev/next moves by week
- In day view: prev/next moves by day

**And** navigation state is preserved:
- Current view and date saved in AppMetadata
- App reopens to last viewed date and view mode
- Breadcrumb shows current date range ("January 2026")

**Prerequisites:** Story 3.1 (calendar views)

**Technical Notes:**
- Reference: FR-3.1 (navigation), UX Design Principles (satisfying interactions)
- Smooth animations during navigation (<300ms)
- Keyboard shortcuts discoverable via tooltips
- Breadcrumb format: "2026" (year), "January 2026" (month), "Jan 15-21, 2026" (week), "Monday, Jan 15, 2026" (day)
- Consider mini-calendar for date picker

---

## Testing Framework & Strategy

### Overview

To ensure long-term reliability and maintainability (NFR-M2: Testability), the application includes a comprehensive testing framework covering unit tests, integration tests, and regression tests.

**CRITICAL: Testing is integrated from Story 1.1 onwards - not added later.**

- **Story 1.1:** Creates test project in solution alongside main project
- **All subsequent stories:** Include tests as part of "Definition of Done"
- **Test-first approach:** For algorithms and critical business logic
- **Tests-alongside approach:** For features and integrations

This ensures every story delivers tested, production-ready code.

### Testing Framework Stack

**Unit Testing:**
- **xUnit** - Primary test framework (.NET standard)
- **FluentAssertions** - Readable assertion syntax
- **Moq** - Mocking framework for dependencies
- **AutoFixture** - Test data generation

**Integration Testing:**
- **xUnit** with real SQLite database (in-memory or file-based)
- **Microsoft.EntityFrameworkCore.InMemory** - For database integration tests
- **WireMock.Net** - Mock Google Calendar API responses

**UI Testing (Optional for MVP, recommended for Tier 2+):**
- **Appium for WinUI** - UI automation testing
- Manual testing protocol for critical workflows

**Test Coverage:**
- **Coverlet** - Code coverage collection
- **ReportGenerator** - Coverage reports

### Testing Scope by Epic

**Epic 1: Foundation & Project Setup**
- **Story 1.2-1.4:** Database infrastructure tests
  - DbContext instantiation and configuration
  - Migration application and rollback
  - Version history tracking
  - Database integrity checks
- **Story 1.6:** Logging infrastructure tests
  - Log file creation and rotation
  - Log levels and formatting
  - Error handling paths

**Epic 2: Google Calendar Integration & Sync**
- **Story 2.1:** OAuth flow unit tests (mocked)
  - Token storage and encryption
  - Token refresh logic
  - Error handling for auth failures
- **Story 2.2-2.3:** Sync logic integration tests
  - Event fetching and parsing
  - Database storage
  - Version history creation
  - Edge cases (empty calendar, deleted events, recurring events)
- **Story 2.5:** Background sync tests
  - Network connectivity detection
  - Incremental sync logic
  - Offline mode behavior

**Epic 3: Local Calendar UI & Event Management**
- **Story 3.1-3.2:** Calendar view logic tests
  - Date range filtering
  - Color rendering logic
  - View mode switching
- **Story 3.5-3.7:** Event editing tests
  - Auto-save debouncing
  - Validation logic (end time after start time, required fields)
  - Color assignment
  - Version history on edits
- **Story 3.6:** Event creation tests
  - Default values applied correctly
  - 15-minute snapping logic
  - Local-only event flagging

### Critical Tests Required (NFR-M2)

**Must be tested before production:**

1. **Data Processing Algorithms** (Future Epics 4-6)
   - 8/15 rounding algorithm (all edge cases)
   - Phone activity coalescing logic
   - YouTube session coalescing logic
   - Quality check validations

2. **Database Operations**
   - Version history preservation
   - Migration application without data loss
   - Foreign key constraint enforcement
   - Database backup before destructive operations

3. **API Integration**
   - Google Calendar sync (fetch, update, delete)
   - Batch request handling
   - Rate limit handling and retries
   - Token refresh flows

4. **Data Integrity**
   - No data loss on sync
   - Version history complete and queryable
   - Offline-to-online transition
   - Conflict resolution

### Test Organization

**Project Structure:**
```
GoogleCalendarManagement.sln
├── GoogleCalendarManagement/          # Main application
├── GoogleCalendarManagement.Tests/    # Unit & integration tests
│   ├── Unit/
│   │   ├── Data/                      # Database logic tests
│   │   ├── Services/                  # Business logic tests
│   │   └── Utils/                     # Algorithm tests
│   ├── Integration/
│   │   ├── Database/                  # EF Core integration tests
│   │   ├── GoogleCalendar/            # API integration tests (mocked)
│   │   └── Sync/                      # End-to-end sync tests
│   └── Fixtures/                      # Shared test data and helpers
└── GoogleCalendarManagement.UI.Tests/ # UI tests (optional for MVP)
```

### Test Execution Strategy

**During Development:**
- Run unit tests locally before committing
- Use test-first approach for algorithms (8/15 rounding, coalescing)
- Run full test suite on key milestones

**Before Deployment:**
- All tests pass (zero failures)
- Code coverage >70% for business logic
- Manual smoke test of critical workflows

**Continuous Testing (Future):**
- CI/CD pipeline with automated test runs (GitHub Actions or similar)
- Nightly full test suite execution
- Coverage reports generated automatically

### Regression Testing

**Automated Regression:**
- Full test suite re-runs on every change
- Database migration tests prevent schema breakage
- API mocking ensures external changes don't break app

**Manual Regression (Key Scenarios):**
1. **Full Sync Workflow:** Fresh install → OAuth → Sync 6 months → View calendar
2. **Event Editing Workflow:** Select event → Edit → Auto-save → Verify in database
3. **Offline Mode:** Disconnect network → View events → Edit locally → Reconnect → Sync
4. **Version History:** Sync → Edit event → Sync again → Verify old version preserved
5. **Migration Test:** Old database → App update → Migrations applied → Data intact

### Testing Guidelines for Development Agents

When implementing stories, development agents should:

1. **Write tests first for algorithms** (TDD approach)
   - Define expected behavior in tests
   - Implement to make tests pass
   - Refactor with tests as safety net

2. **Write tests alongside for features**
   - Add unit tests for business logic
   - Add integration tests for database operations
   - Document edge cases in test names

3. **Never skip tests for destructive operations**
   - Database migrations
   - Data deletion or modification
   - API write operations

4. **Use descriptive test names:**
   ```csharp
   [Fact]
   public void Sync_WhenEventUpdatedInGCal_SavesOldVersionToHistory()

   [Fact]
   public void EightFifteenRounding_WhenBlockHasSevenMinutes_ExcludesBlock()

   [Theory]
   [InlineData("2026-01-01 00:00", "2026-01-01 00:07", 0)] // <8 min
   [InlineData("2026-01-01 00:00", "2026-01-01 00:08", 1)] // >=8 min
   public void EightFifteenRounding_BlockInclusion(string start, string end, int expected)
   ```

5. **Mock external dependencies:**
   - Google Calendar API calls
   - Network connectivity checks
   - File system operations (where appropriate)

### Test Coverage Goals

**Epic 1 (Foundation):**
- Database migrations: 100%
- Logging infrastructure: 80%
- Configuration: 70%

**Epic 2 (Google Calendar Sync):**
- OAuth logic: 85%
- Sync algorithms: 90%
- Version history: 95%

**Epic 3 (Calendar UI):**
- View logic: 70%
- Event editing logic: 85%
- Validation: 90%

**Future Epics (Data Processing):**
- 8/15 rounding: 100% (critical algorithm)
- Coalescing algorithms: 100% (critical algorithm)
- Data source parsing: 90%

### Example Test Cases

**Story 1.3 - Database Schema:**
```csharp
public class GoogleCalendarEventTests
{
    [Fact]
    public void GoogleCalendarEvent_WhenCreated_HasDefaultValues()
    {
        var evt = new GoogleCalendarEvent { Title = "Test", StartTime = DateTime.Now, EndTime = DateTime.Now.AddHours(1) };
        Assert.False(evt.IsPublished);
        Assert.Equal("Azure", evt.ColorId);
    }

    [Fact]
    public async Task Database_WhenSavingEvent_EnforcesRequiredFields()
    {
        using var context = CreateTestContext();
        var evt = new GoogleCalendarEvent(); // Missing required fields
        context.Events.Add(evt);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
```

**Story 2.3 - Version History (fielded snapshot, no JSON blob):**
```csharp
// gcal_event_version stores individual columns, not EventDataJson.
// ChangeType is not used; change_reason is a plain string ("updated" / "deleted").
// See GoogleCalendarSyncTests.cs for the canonical integration-test examples.
[Fact]
public async Task SyncAsync_UpdatedEvent_CreatesSnapshotBeforeOverwrite()
{
    // seed existing event, run sync with updated data...
    var versions = await context.GcalEventVersions.ToListAsync();
    Assert.Single(versions);
    Assert.Equal("Old Title", versions[0].Summary);        // fielded column
    Assert.Equal("updated", versions[0].ChangeReason);     // string, not enum
    Assert.Equal("gcal_sync", versions[0].ChangedBy);
}
```

### References

- **NFR-M2:** Testability requirements (unit tests for algorithms, integration tests for APIs)
- **NFR-M1:** Code quality (dependency injection for testability)
- **NFR-D1:** Data loss prevention (tested via version history tests)
- **PRD Implementation Planning:** "No untested destructive operations"

---

## Epic Breakdown Summary

### Deliverables Complete

✅ **Epic 1: Foundation & Project Setup** (6 stories)
- Project structure with WinUI 3
- SQLite + Entity Framework Core
- Database schema with version history
- Automatic migrations with backups
- Development environment documentation
- Logging and error handling infrastructure
- **Testing infrastructure established in Story 1.1**

✅ **Epic 2: Google Calendar Integration & Sync (Read-Only)** (5 stories)
- OAuth 2.0 authentication with DPAPI encryption
- Fetch and cache events locally
- **Automatic version history** (new pulls overwrite current, old data preserved)
- Sync status indicators (green/grey per date)
- Background sync with offline support

✅ **Epic 3: Local Calendar UI & Event Management** (8 stories)
- Year/month/week/day calendar views
- Color-coded event display
- Event selection with red outline feedback
- Event details panel (read-only Tier 1, editable Tier 2)
- Event creation (drag-to-create + button)
- Color picker for 9 custom colors
- Date navigation and jump-to-date

### Total: 19 Stories Across 3 Epics

### Key Success Criteria

**Testing Integration:**
- ✅ Test project created in Story 1.1
- ✅ Tests written alongside each story implementation
- ✅ xUnit + FluentAssertions + Moq stack
- ✅ Coverage goals: 70-100% depending on criticality

**Story Sizing:**
- ✅ All stories bite-sized for single-session completion
- ✅ Clear BDD acceptance criteria (Given/When/Then)
- ✅ No forward dependencies
- ✅ Vertically sliced (complete functionality, not layers)

**Sequencing:**
- ✅ Epic 1 foundation blocks all other work
- ✅ Epic 2 read-only sync provides immediate value
- ✅ Epic 3 builds UI on Epic 2's data layer
- ✅ Each story has clear prerequisites

**PRD Alignment:**
- ✅ Tier 1 requirements covered (read-only calendar viewer)
- ✅ Tier 2 requirements included (event editing, creation)
- ✅ NFRs integrated (performance, security, data integrity, testability)
- ✅ All 21 functional requirements mapped to stories

### Next Steps

**Recommended Workflow Progression:**

1. **Architecture Design** (if not done)
   - Run: `/bmad:bmm:workflows:architecture`
   - Define technical stack, patterns, component structure
   - Create architectural decision records

2. **UX Design** (for UI-heavy apps like this)
   - Run: `/bmad:bmm:workflows:create-ux-design`
   - Design calendar views, color system, interaction patterns
   - Create wireframes and visual mockups

3. **Story Implementation**
   - Run: `/bmad:bmm:workflows:create-story` for each story in sequence
   - Start with Epic 1, Story 1.1 (foundation)
   - Each story generates detailed implementation plan
   - Dev agents implement with tests

4. **Sprint Planning** (when ready for Phase 4)
   - Run: `/bmad:bmm:workflows:sprint-planning`
   - Track story status through development lifecycle
   - Manage TODO → IN PROGRESS → READY FOR REVIEW → DONE

### References

- **[PRD](./PRD.md)** - Complete product requirements
- **[Product Brief](./product-brief-google-calendar-management-2025-11-05.md)** - Vision and goals
- **[Technology Stack](./\_technology-stack.md)** - Technical decisions
- **[Database Schemas](./\_database-schemas.md)** - Complete schema documentation
- **[Key Decisions](./\_key-decisions.md)** - Architectural choices

---

_This epic breakdown provides the foundation for autonomous AI agent development. Each story is self-contained, properly sequenced, and includes comprehensive acceptance criteria for test-driven implementation._

_Generated by BMad Method (BMM) workflows on 2026-01-30._

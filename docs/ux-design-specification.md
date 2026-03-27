# Google Calendar Management UX Design Specification

_Created on 2026-01-30 by Sarunas Budreckis_
_Generated using BMad Method - Create UX Design Workflow v1.0_

---

## Executive Summary

Google Calendar Management transforms retroactive life tracking from tedious chore into a fun, nostalgic ritual. After 5 years of manually tracking every hour across multiple data sources, the cognitive overload has reached a breaking point. This Windows desktop application consolidates all sources into a single-pane-of-glass view with instant responsiveness and intelligent automation.

**The Special Moment:** Backfilling transforms from dreaded chore into "spaced repetition for life" - reviewing events reinforces memories: "Oh right, that's when I talked to Mom" or "I watched 3 videos on Rust programming." The color-coded calendar becomes a visual autobiography revealing life balance through a consciousness taxonomy.

**Platform:** Windows desktop application (WinUI 3, .NET 9) with local-first SQLite architecture built for decades of use.

**Three-Phase Implementation:**
- **Phase 1:** Basic UI + Pull from GCal (read-only viewing and backup)
- **Phase 2:** Editing + Push to GCal (full manual calendar management)
- **Phase 3:** Data sources (automation to ease backfilling)

---

## 1. Design System Foundation

### 1.1 Design System Choice

**Framework:** WinUI 3 with .NET 9 (Windows App SDK)

**Visual Design System:** Fluent Design System (Windows 11 native)

**Calendar UI Foundation:**
- Leverage WinUI 3 CalendarView control as starting point
- Custom rendering for event blocks to achieve Google Calendar visual fidelity
- Research and evaluate calendar UI libraries during Tier 1 implementation

**Component Strategy:**
- Use native WinUI 3 controls for: buttons, inputs, dialogs, progress indicators
- Custom components for: calendar event rendering, date selection system, detail panel
- Fluent Design principles for spacing, typography, animations

**Rationale:**
- Native Windows performance and integration
- Future-proof framework (Microsoft's recommended path)
- Built-in Fluent Design System alignment
- CalendarView control provides foundation for custom calendar rendering

---

## 2. Core User Experience

### 2.1 Core UX Principles

**1. Performance is Non-Negotiable**
- **Instant responsiveness:** 0-100ms tooltips, 0-lag editing
- **60 FPS animations:** Smooth transitions between views
- **No flow interruption:** Auto-save to local DB, no blocking operations
- **Pre-rendered content:** Calendar data loaded and ready

**2. Progressive Disclosure via Hover**
- **Glanceable view:** Passively colorful calendar reveals patterns at a glance
- **Hover Layer 1 (0-100ms):** Event title, time, duration appear instantly
- **Hover Layer 2 (sustained hover):** Data source timeline with vertical time alignment (Tier 3+)
- **Click:** Full edit panel with complete event context

**3. Confirmations Only for Commits**
- **No confirmation dialogs for edits:** Instant editing, auto-save, undoable
- **Confirmations ONLY for irreversible operations:**
  - Push to Google Calendar (publishes events)
  - Create export file
  - Restore from save point
  - Delete operations (if not undoable)

**4. Google Calendar Visual Fidelity**
- **Week view:** Colors, event titles, start/end times visible (match GCal information density)
- **Month view:** Day numbers, compact event indicators
- **Year view:** Date selection grid with sync status
- **Visual language:** Adopt Google Calendar's proven UX patterns

**5. Visual State Language**
- **Published events:** Full opacity (100%) in final color (Azure, Purple, Yellow, Navy, etc.)
- **Unpushed/pending events:** Translucent (60% opacity) - distinguishes local edits
- **Selected events:** Red outline (2px solid red border) - clear selection feedback
- **Date sync status:** Green = synced with GCal, Grey = not synced

**6. Incremental Value Delivery**
- **Phase 1:** Immediately usable for viewing existing calendar and creating backups
- **Phase 2:** Replaces manual Google Calendar editing entirely
- **Phase 3:** Adds automation to dramatically ease backfilling
- **Use the app while building it:** Each phase provides real utility

**7. Three-Phase Implementation Model**
- **Phase 1:** Basic UI & Pull from GCal (read-only, year view launch, green/grey sync status)
- **Phase 2:** Editing & Push to GCal (event editing/creation, push, translucent pending, red outline)
- **Phase 3:** Data Sources for Easier Editing (Toggl, Calls, YouTube, Outlook, coalescing, hover, day naming)

### 2.2 Defining Experience - The Core Interaction

**The ONE thing users will do most:**
Review and manage calendar events with instant responsiveness and complete context.

**What must be absolutely effortless:**
- Viewing event details (0-100ms hover)
- Editing events (instant, no lag, auto-save)
- Navigating between dates
- Selecting date ranges for sync/backup
- Pushing changes to Google Calendar (one-click with confirmation)

**Most critical interaction to get right:**
**Phase 1:** View calendar → select date ranges → pull from GCal → see sync status
**Phase 2:** Click event → instant detail panel → edit fields → auto-save → push to GCal
**Phase 3:** Hover event → data source timeline appears → click to edit → approve candidates

**The Special Moment (Phase 3):**
When hovering over an event, the data source timeline appears showing exactly what you were doing during that time - Toggl entries, YouTube videos, call logs, all time-aligned. This transforms "What was I doing?" into instant, confident recall: "Oh right, I was watching those Rust videos while on that phone call."

---

## 3. Visual Foundation

### 3.1 Color System

The calendar uses a custom color taxonomy representing mental states, not just activities. See [_color-definitions.md](../_color-definitions.md) for complete details.

**Active Colors:**
- **Azure (#0088CC):** Eudaimonia - fulfillment activities (social, nature, sports, travel)
- **Purple:** Professional work (Mayo Clinic)
- **Yellow:** Passive consumption (YouTube, movies, games, eating)
- **Navy:** Personal engineering/admin (finances, programming, organizing, cooking)
- **Sage:** Wisdom/meta-reflection (updating calendar, Obsidian, meditation)
- **Grey:** Sleep and recovery
- **Flamingo:** Nerdsniped deep reading (LessWrong, rationality blogs)
- **Orange:** Physical training (gym)
- **Lavender:** In-between states (showering, transport, waiting)

**Typography & Spacing:**
- Match Google Calendar's proven typography hierarchy
- Base 8px spacing system for consistency
- Monospace font for time indicators
- Sans-serif for event titles

**Visual Density:**
- Match Google Calendar's information density
- Prioritize glanceability over whitespace
- Colors as primary visual language

**Interactive Visualizations:**

- Color Theme Explorer: [ux-color-themes.html](./ux-color-themes.html)

---

---

## 6. Component Library (Tiers 1-2)

### 6.1 Tier 1 Components

**1. Calendar Views**
- **Year Grid View:** Date selection with sync status indicators (green/grey per date)
- **Month View:** Google Calendar clone showing event blocks with colors and titles
- **Week View:** Vertical timeline with time slots, color-coded event blocks
- **Day View:** Detailed hourly view with event details

**2. Date Selection System**
- Click first date → Click second date → Range visibly highlighted
- Drag-to-select alternative
- Clear visual feedback for selected range
- Green/grey indicators persist after sync

**3. Top Strip Controls**
- **View Mode Switcher:** Radio buttons or tabs (Year | Month | Week | Day)
- **Action Buttons:**
  - "Pull from GCal" (primary action, shows progress)
  - "Save" (manual save)
  - "Export" (opens save file dialog with confirmation)
- **Status Display:**
  - "Last pulled: [timestamp]"
  - "Last saved: [timestamp]"

**4. Progress Indicators**
- Loading spinner for API calls
- Progress bar for bulk operations
- Toast notifications for success/error messages

---

### 6.2 Tier 2 Components

**5. Event Detail Panel (Right Side)**
- **Slide-in animation** (60 FPS, smooth)
- **Editable Fields:**
  - Title (text input, instant update on blur/Enter)
  - Start time (time picker, 15-min increments)
  - End time (time picker, 15-min increments)
  - Color (dropdown showing 9 custom colors with labels)
  - Description (multi-line text area)
- **Action Buttons:**
  - "Delete Event" (confirmation required)
  - Close button (X) in top-right corner
- **Auto-save:** Changes save to local DB instantly on field blur

**6. Event Creation**
- **Drag-to-create:** Drag on empty calendar space → Event created at that time with default duration (1 hour)
- **"+ Add Event" button:** Opens detail panel with blank form, default time = current time rounded to nearest 15 min
- **Default color:** Azure (eudaimonia)

**7. Event Selection Visual Feedback**
- **Red outline:** 2px solid red border on selected event(s)
- **Multi-select:** Shift+click adds to selection, drag-select for ranges
- **Selection counter:** Badge showing "3 events selected"

**8. Event State Visual Indicators**
- **Published events:** Full opacity (100%)
- **Unpushed events:** Translucent (60% opacity)
- **Hover state:** Subtle highlight or border (Tier 1: basic tooltip, Tier 2: interactive)

**9. Push to GCal Confirmation Dialog**
- Modal dialog centered on screen
- **Title:** "Push Events to Google Calendar?"
- **Message:** "You are about to publish [count] events to your Google Calendar. This action cannot be easily undone."
- **Buttons:** "Cancel" (default) | "Push to GCal" (primary, green)
- **Progress indicator** after confirmation

**10. Color Picker**
- Dropdown or popover showing 9 colors
- Each color shows:
  - Color swatch (circle or square)
  - Label (Azure, Purple, Yellow, etc.)
  - Hex code (#0088CC)
- Current color highlighted
- Clicking a color applies instantly (auto-save)

---

### 6.3 Design System Choice (Implementation)

**WinUI 3 Native Controls:**
- Leverage WinUI 3 CalendarView as foundation
- Fluent Design System for buttons, inputs, dialogs
- Custom rendering for event blocks (Google Calendar fidelity)
- Research calendar UI libraries during Tier 1 implementation

**Performance Requirements:**
- All interactions <100ms response time
- 60 FPS animations for view transitions
- Pre-render calendar data to avoid lag
- Virtualization for large date ranges

---

## 7. UX Pattern Decisions (Tiers 1-2)

### 7.1 Button Hierarchy

**Primary Actions:**
- "Pull from GCal" (Tier 1)
- "Push to GCal" (Tier 2)
- Styled with prominent color, larger size

**Secondary Actions:**
- "Save", "Export"
- Standard button styling

**Tertiary Actions:**
- View mode switcher
- Close buttons (X)
- Subtle styling, smaller

### 7.2 Feedback Patterns

**Success:**
- Toast notification: "142 events pulled from Google Calendar"
- Green checkmark icon
- Auto-dismiss after 5 seconds

**Error:**
- Toast notification: "Failed to connect to Google Calendar. Check internet connection."
- Red X icon
- Manual dismiss required

**Progress:**
- Loading spinner for <3 seconds
- Progress bar for >3 seconds
- Message: "Fetching events... 47 of 142"

**Warning:**
- Modal dialog for destructive actions
- Yellow caution icon
- Requires explicit confirmation

### 7.3 Form Patterns

**Label Position:** Above fields (standard vertical form layout)

**Required Fields:** All event fields optional except title

**Validation Timing:**
- On blur (lose focus)
- On save/push (final validation)
- Real-time for date/time pickers (prevent invalid ranges)

**Error Display:** Inline below field with red text and icon

### 7.4 Modal Patterns

**Confirmation Dialogs:**
- Center screen overlay with backdrop
- Escape key or backdrop click = Cancel
- Tab order: Cancel → Primary action

**Detail Panel:**
- Slide in from right (not modal - doesn't block calendar view)
- Click outside or X button to close
- Content scrollable if overflows

### 7.5 Navigation Patterns

**Keyboard Shortcuts:**
- Arrow keys: Navigate dates
- Escape: Close detail panel, clear selection
- Ctrl+S: Manual save
- Delete: Delete selected events (confirmation required)

**View Mode Memory:** Remember last view mode per session

---

## 8. Responsive Design & Accessibility

### 8.1 Window Sizing

**Minimum Window Size:** 1024x768 (standard desktop)

**Optimal Size:** 1920x1080 (full HD)

**Responsive Behavior:**
- Main panel (calendar) takes available space
- Right panel (Tier 2) fixed width (400px), slides in/out
- Top strip fixed height (60px)
- No mobile/tablet support in Tiers 1-2 (Windows desktop only)

### 8.2 Accessibility (Basic)

**Not WCAG Compliance Focus** (personal app), but basic considerations:

**Keyboard Navigation:**
- All interactive elements accessible via keyboard
- Visible focus indicators
- Logical tab order

**Color Contrast:**
- Event text readable against all 9 color backgrounds
- UI controls meet minimum contrast ratios

**Screen Reader:**
- Basic ARIA labels for buttons and inputs
- Not prioritized for Tiers 1-2

---

## 5. User Journey Flows

### 5.1 Tier 1: View & Backup Journey

**Goal:** View existing Google Calendar and create local backup.

**Flow:**
1. **Launch App** → App opens to **year view** showing days/months (can select previous years)
2. **Select date range** → Click start date, click end date → dates visibly selected
3. **Click "Pull from GCal"** → App fetches events from Google Calendar for selected range
4. **Progress indicator** → "Fetching events... 142 events loaded"
5. **View calendar** → Switch to month/week/day view, events displayed with colors
6. **Navigate dates** → Arrow keys, date picker, view mode switcher (year/month/week/day)
7. **Green/grey indicators** → Dates update to show sync status (green = synced)
8. **Select date range for backup** → Click/drag to select dates
9. **Click "Export"** → Confirmation dialog → Save file dialog
10. **Backup created** → "Exported 142 events from Nov 1-30 to backup.json"

**Success:** User has local backup file, can view calendar offline, dates show sync status.

---

### 5.2 Tier 2: Edit & Sync Journey

**Goal:** Edit an event and push changes to Google Calendar.

**Flow:**
1. **View calendar** → Events displayed (published = full opacity, unpushed = translucent)
2. **Create new event (Option A):** Drag on empty calendar space → event created with default time
3. **Create new event (Option B):** Click "+ Add Event" button → dialog opens
4. **Edit existing event:** Click event → Right panel opens with details instantly (0ms lag)
5. **Edit fields** → Modify title, time, color, description (instant, auto-save to local DB)
6. **Visual feedback** → Event becomes translucent (unpushed state)
7. **Red outline** → Selected event(s) highlighted
8. **Multi-select** → Shift+click or drag-select multiple events
9. **Click "Push to GCal"** → Confirmation dialog: "Push 3 events to Google Calendar?"
10. **Confirm** → Batch publish, progress indicator
11. **Success** → Events become full opacity, "3 events published to Google Calendar"

**Success:** Calendar changes persisted to Google Calendar, local and remote in sync.

---

### 5.3 Tier 3: Data Source Assisted Backfill Journey

**Goal:** Backfill a week using Toggl Track data with continuous tracking.

**Data Source Tracking Model:**
- Each data source has **start date** (beginning of continuous data)
- Each data source has **end date** (last updated date - no gaps between start and end)
- Assumption: Data is continuous from start to end (gaps handled separately in Tier 4+)
- "Update" button fetches from end date to today, extending continuous range

**Flow:**
1. **View data source panel** (Tier 3 UI addition)
   - 📊 **Toggl Track:** Start: Jan 1, 2020 | End: Oct 15, 2025 | [Update to Today]
   - 📞 **Call Logs:** Not configured
   - 🎥 **YouTube:** Not configured
2. **Click "Update to Today"** on Toggl → App fetches Oct 16-30, 2025
3. **Coalescing runs** → Phone activities merged, 8/15 rounding applied
4. **Candidate events appear** → Translucent overlays on calendar for Oct 16-30
5. **Hover over candidate (0-100ms)** → Data source timeline box appears to the right:
   - Vertical time-aligned display
   - 📊 Toggl entries shown as blocks
   - "Phone activity: 2:15pm - 3:45pm (4 entries coalesced)"
6. **Move cursor down** → Previous box closes, next event's box opens instantly
7. **Click event** → Right panel opens persistently with:
   - Event details (editable: title, time, color, description)
   - Data source evidence section showing Toggl entries
   - Individual "Approve" button or batch select
8. **Edit if needed** → Adjust title/time/color (instant, 0-lag)
9. **Select multiple events** → Red outline on selected
10. **Click "Approve Selected"** → Events marked for publishing
11. **Click "Push to GCal"** → Confirmation: "Push 12 events to Google Calendar?"
12. **Confirm** → Batch publish, progress indicator
13. **Success** → "12 events published. Oct 16-30 backfilled. Toggl end date updated to Oct 30."

**Data Source State Updated:**
- Toggl Track: Start: Jan 1, 2020 | End: Oct 30, 2025 (continuous)

**Success:** Two weeks backfilled efficiently, data source tracking updated, memories reinforced.

---

## 4. Design Direction

### 4.1 Layout Structure (Tiers 1-2)

**Top Strip (Always Visible):**
- View mode selector: Year | Month | Week | Day
- Action buttons: Pull from GCal | Save | Export | Push to GCal
- Status indicators: Last pulled: [timestamp] | Last saved: [timestamp]

**Main Panel:**
- Google Calendar clone (year/month/week/day views)
- Instant hover tooltips (0-100ms - Tier 1 shows basic event info)
- Click to select/edit (Tier 2)

**Right Panel (Tier 2, appears on event click):**
- Event details and editing fields
- Color picker (9 custom colors)
- Time picker (15-minute increments)
- Save/Delete buttons
- Close button (X)

**Tier 3+ Layout Additions:**
- Left Panel: Data source status tracking (TBD - revisit UX design for Tier 3)
- Enhanced hover: Data source timeline visualization (TBD)
- Approval workflow UI (TBD)

---

## 9. Implementation Guidance

### 9.1 Development Roadmap

**Tier 1 Focus (MVP):**
1. Year view with date selection
2. Pull from Google Calendar
3. Month/week/day views (read-only)
4. Save/Export functionality
5. Green/grey sync status indicators

**Tier 2 Focus:**
1. Event detail panel (right side)
2. Inline editing with auto-save
3. Event creation (drag + button)
4. Push to GCal with confirmation
5. Translucent unpushed state visual

**Tier 3 Focus (Future):**
- Revisit UX design before implementation
- Data source panel design
- Hover timeline visualization
- Approval workflow UI
- Coalescing algorithm feedback

### 9.2 Key Technical Decisions to Research

**During Tier 1 Implementation:**
1. Calendar UI library evaluation for WinUI 3
2. Custom event rendering approach
3. SQLite + EF Core setup for local database
4. Google Calendar API integration patterns
5. Date selection UI component choice

**Performance Targets:**
- Calendar render: <1 second for month view with 200+ events
- Hover tooltip: <100ms appearance
- Edit operations: 0-lag (instant auto-save to local DB)
- View transitions: 60 FPS smooth animations

### 9.3 UX Validation Checkpoints

**After Tier 1:**
- Can user view and backup calendar efficiently?
- Is sync status (green/grey) clear and accurate?
- Do calendar views match Google Calendar information density?

**After Tier 2:**
- Is editing truly instant (0-lag)?
- Is the unpushed state visually clear?
- Do users feel confident pushing to GCal?

**Before Tier 3:**
- Revisit this UX specification
- Design data source panel layout
- Design hover timeline visualization
- Plan approval workflow UI

---

## 10. Related Documents

### Core Planning Documents

- **Product Requirements:** [PRD.md](./PRD.md)
- **Product Brief:** [product-brief-google-calendar-management-2025-11-05.md](./product-brief-google-calendar-management-2025-11-05.md)
- **Two-Phase Alignment Model:** [two-phase-alignment-model.md](./two-phase-alignment-model.md)

### Technical Documentation

- **Technology Stack:** [_technology-stack.md](./_technology-stack.md)
- **Database Schemas:** [_database-schemas.md](./_database-schemas.md)
- **Key Decisions:** [_key-decisions.md](./_key-decisions.md)
- **Phase 1 Requirements:** [_phase-1-requirements.md](./_phase-1-requirements.md)

### Design Reference

- **Color Definitions:** [_color-definitions.md](./_color-definitions.md) - Complete color taxonomy and philosophy

---

## Appendix A: Design Rationale

### Why Google Calendar Visual Fidelity?

**User already spends years with Google Calendar's information density and layout.** Matching this proven UX reduces learning curve and leverages existing muscle memory.

**Information density matters:** The calendar must show patterns at a glance. Google Calendar's compact, color-first design achieves this. Deviating would sacrifice glanceability.

**Desktop app advantage:** Can achieve performance impossible in web GCal (instant tooltips, 0-lag editing, offline capabilities).

### Why Instant Responsiveness?

**Flow state is critical:** Backfilling should feel like a satisfying ritual, not a frustrating chore. Any lag breaks flow.

**Decades of use:** Small frictions compound over years. Investing in performance upfront pays dividends.

**Trust through speed:** Instant auto-save builds confidence that no work will be lost.

### Why Three-Tier Strategy?

**Use while building:** Tier 1 provides immediate utility (backup existing calendar). Tier 2 replaces manual GCal editing. Tier 3 adds automation.

**Risk mitigation:** Prove technical sync (Phase 1) before adding experiential backfill complexity (Phase 2).

**Incremental value:** Each tier delivers usability, not just partial features.

---

## Appendix B: Future Considerations (Post-Tier 3)

### Global View (Future Tier)
- Decade-scale calendar visualization
- Pattern recognition across years
- Life phase comparison

### Advanced Data Sources
- Spotify (music context)
- Screen Time (app usage)
- Google Search (query history)
- Maps Timeline (location context)

### Privacy Features
- Secret events (local only)
- Fake event substitution
- Multiple audience views
- Encryption for sensitive data

### Pattern Analysis
- Color-based life KPIs
- Time breakdown dashboards
- Correlation analysis
- Weekly Obsidian integration

---

## Version History

| Date       | Version | Changes                              | Author             |
| ---------- | ------- | ------------------------------------ | ------------------ |
| 2026-01-30 | 1.0     | Initial UX Design Specification (Tiers 1-2) | Sarunas Budreckis |

---

**Next Steps:**
1. Begin Tier 1 implementation with focus on calendar views and Pull from GCal
2. Research WinUI 3 calendar UI libraries
3. Validate UX decisions during development
4. Revisit this spec before starting Tier 3

---

_This UX Design Specification was created through collaborative design facilitation using the BMad Method. All decisions were made with user input and are documented with rationale._

---

## 6. Component Library

### 6.1 Component Strategy

{{component_library_strategy}}

---

## 7. UX Pattern Decisions

### 7.1 Consistency Rules

{{ux_pattern_decisions}}

---

## 8. Responsive Design & Accessibility

### 8.1 Responsive Strategy

{{responsive_accessibility_strategy}}

---

## 9. Implementation Guidance

### 9.1 Completion Summary

{{completion_summary}}

---

## Appendix

### Related Documents

- Product Requirements: [PRD.md](./PRD.md)
- Product Brief: [product-brief-google-calendar-management-2025-11-05.md](./product-brief-google-calendar-management-2025-11-05.md)

### Core Interactive Deliverables

This UX Design Specification was created through visual collaboration:

- **Color Theme Visualizer**: c:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Calendar Management\docs\ux-color-themes.html
  - Interactive HTML showing all color theme options explored
  - Live UI component examples in each theme
  - Side-by-side comparison and semantic color usage

- **Design Direction Mockups**: c:\Users\Sarunas Budreckis\Documents\Programming Projects\Google Calendar Management\docs\ux-design-directions.html
  - Interactive HTML with 6-8 complete design approaches
  - Full-screen mockups of key screens
  - Design philosophy and rationale for each direction

### Optional Enhancement Deliverables

_This section will be populated if additional UX artifacts are generated through follow-up workflows._

<!-- Additional deliverables added here by other workflows -->

### Next Steps & Follow-Up Workflows

This UX Design Specification can serve as input to:

- **Wireframe Generation Workflow** - Create detailed wireframes from user flows
- **Figma Design Workflow** - Generate Figma files via MCP integration
- **Interactive Prototype Workflow** - Build clickable HTML prototypes
- **Component Showcase Workflow** - Create interactive component library
- **AI Frontend Prompt Workflow** - Generate prompts for v0, Lovable, Bolt, etc.
- **Solution Architecture Workflow** - Define technical architecture with UX context

### Version History

| Date     | Version | Changes                         | Author        |
| -------- | ------- | ------------------------------- | ------------- |
| 2026-01-30 | 1.0     | Initial UX Design Specification | Sarunas Budreckis |

---

_This UX Design Specification was created through collaborative design facilitation, not template generation. All decisions were made with user input and are documented with rationale._

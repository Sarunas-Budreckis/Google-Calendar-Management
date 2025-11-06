# Product Brief: Google Calendar Management

**Date:** 2025-11-05
**Author:** Sarunas Budreckis
**Context:** Personal passion project with long-term vision

---

## Executive Summary

Google Calendar Management transforms the tedious process of retroactive life tracking into a meaningful ritual. After 5 years of manually tracking every hour, the cognitive load of juggling multiple data sources has reached a breaking point. This application consolidates data from Toggl Track, iOS call logs, YouTube watch history, and Outlook calendar into a single, streamlined workflow that turns backfilling from a chore into a form of life review - like spaced repetition for lived experiences.

The visual color-coded calendar serves as a personal KPI dashboard, revealing patterns in mental modes: passive consumption, education, social activities, work, and personal growth. With decades of tracking ahead, the upfront investment in better UX will compound into significant quality-of-life improvements.

---

## Core Vision

### Problem Statement

**The Cognitive Overload of Life Tracking**

For 5 years, every hour of life has been meticulously tracked in Google Calendar. The data is valuable - it provides insights, enables reflection, and creates a visual autobiography. But the process of maintaining it has become unsustainable.

When facing 2 weeks of missing data, the current workflow is overwhelming:
- **Too many contexts**: Separate tabs for Google search history, call logs, Toggl Track, YouTube history, Outlook calendar
- **Insufficient cognitive bandwidth**: "Not enough RAM or screen space" to hold all the information simultaneously
- **Constant context switching**: Each data source requires different mental models and interfaces
- **Decision fatigue**: Every event requires cross-referencing multiple sources to determine timing, duration, and categorization
- **Lost context**: By the time all sources are reviewed, the memory of what actually happened has faded

The irony: A system designed to capture life experiences has become so burdensome that it prevents living in the moment.

### Problem Impact

**Current Cost:**
- **Time**: 2-4 hours per week on manual backfilling
- **Mental energy**: High cognitive load prevents making it a regular habit
- **Gaps in data**: Weeks go unfilled because the process is too daunting
- **Loss of value**: The more time passes before backfilling, the less accurate and meaningful the data becomes

**Opportunity Cost:**
- **Joy lost**: What could be a nostalgic review of life becomes a dreaded task
- **Insights missed**: Patterns and trends go unnoticed because analysis is too difficult
- **Momentum killed**: The friction prevents building it into a daily/weekly rhythm

**Long-term Risk:**
With a commitment to maintain this calendar for **decades to come**, continuing with the current process means:
- Thousands of hours wasted on inefficient workflows
- Accumulated stress and decision fatigue
- Increasing likelihood of abandoning the practice entirely
- Loss of 5+ years of historical tracking investment

### Why Existing Solutions Fall Short

**Google Calendar alone:**
- ✗ No data aggregation from external sources
- ✗ No batch import with approval workflow
- ✗ No contextual view of multiple data sources side-by-side
- ✗ Limited color scheme (cannot do sub-colors or granular breakdowns)
- ✗ No privacy layers for sharing

**Individual data sources (Toggl, YouTube, etc.):**
- ✗ Each requires separate login and navigation
- ✗ No cross-referencing between sources
- ✗ No way to see holistic picture
- ✗ Export formats don't match Google Calendar's needs

**Generic calendar tools:**
- ✗ Not designed for retroactive tracking
- ✗ No concept of "approval workflow"
- ✗ No integration with life-tracking data sources
- ✗ No color-as-KPI visualization

**Time tracking apps:**
- ✗ Forward-looking (tracking as you go), not retroactive
- ✗ Don't integrate with calendar as primary UI
- ✗ Focused on productivity metrics, not life review

**The fundamental gap:** No tool understands that calendar backfilling can be transformed from data entry into a meaningful practice - a form of life reflection and memory reinforcement.

### Proposed Solution

**A Life Review System Disguised as Calendar Management**

Google Calendar Management is a local Windows desktop application that consolidates all life-tracking data sources into a single, streamlined workflow. But it's not just about efficiency - it's about transforming the experience.

**Core Innovation: Single-Pane-of-Glass Review**
- All data sources appear together in one calendar view
- No more juggling tabs or mental models
- Context is preserved: "What was I doing between 2pm and 5pm?" has all the clues visible
- Decisions become obvious instead of exhausting

**Human-in-the-Loop Automation**
- Data is automatically fetched, parsed, and coalesced
- Intelligent algorithms merge fragmented activities (phone usage, YouTube sessions)
- User reviews and approves events before publishing to Google Calendar
- Satisfying click-to-approve workflow (like inbox zero for life events)

**The "Spaced Repetition" Experience**
- Reviewing events becomes an opportunity to reinforce memories
- "Oh right, that's when I talked to Mom for an hour"
- "I watched 3 videos on Rust programming - that was a good evening"
- The act of approval becomes the act of remembering

**Colors as Consciousness Taxonomy**
- Custom color system tracks mental states, not just activities
- Azure (#0088CC) = Eudaimonia - the activities worth living for
- Purple = Professional work (Mayo Clinic)
- Yellow = Passive consumption (YouTube, scrolling, games)
- Navy = Personal engineering and admin
- Sage = Wisdom and reflection
- Grey = Sleep and recovery
- Each color represents both mental state ("spirit") and typical activities ("letter")
- Visual dashboard reveals life balance at a glance
- Future: Split mental state from activity type for deeper analysis

**Privacy-Aware Sharing**
- Different views for different audiences
- Secret events visible only locally, not in Google Calendar
- Ability to substitute fake events for sensitive activities
- Show your calendar without showing your entire life

**Local-First Architecture**
- 100% of data stored locally with complete control
- Google Calendar is the UI, not the database
- Extensible for future data sources
- Built to last for decades of use

### Key Differentiators

**Philosophy-Driven Design:**
Most productivity tools are about efficiency. This tool is about **reducing friction to enable joy**. It's built on the software engineer principle: tighten feedback loops, remove unnecessary decisions, make valuable actions easy.

**Long-Term Thinking:**
Not optimized for quick gains, but for compounding value over decades. Upfront investment justified by sustained use.

**Calendar as Memory Palace:**
Other tools track time for productivity analysis. This tool treats the calendar as a visual autobiography - a memory reinforcement system.

**Color as Language:**
Colors aren't just organization - they're a KPI dashboard for life. The visual tapestry tells the story of how mental energy was spent.

**Privacy Without Compromise:**
Share your calendar without sacrificing truth. Multiple layers let you control visibility without losing local detail.

---

## Target Users

### Primary User

**The Long-Term Life Tracker**

This is a tool built for one person initially: someone who has already committed to comprehensive life tracking and has years of data proving the value. Key characteristics:

**Commitment Level:**
- Already tracks every hour (or aspires to)
- Has experienced both the value and the pain
- Thinks in terms of decades, not months
- Willing to invest upfront for long-term payoff

**Technical Comfort:**
- Software engineer or technical hobbyist
- Comfortable with local applications
- Values control over convenience
- Appreciates well-designed systems

**Philosophical Alignment:**
- Believes in quantified self / life logging
- Values reflection and memory reinforcement
- Sees patterns and wants to understand them
- Treats personal data as precious

**Current Behavior:**
- Manually backfills calendar from multiple sources
- Struggles with cognitive overload of juggling contexts
- Experiences decision fatigue during data entry
- Delays backfilling because it's unpleasant
- Uses Google Calendar colors meaningfully

**Desired Future State:**
- Backfilling is a satisfying ritual, not a chore
- All data visible in one place
- Approve events with confidence, not guesswork
- Look forward to the weekly review process
- Maintain practice for decades without burnout

**Quote:**
> "I've decided that I will continue managing this calendar for decades to come, and so upfront investment to make the process more fun and less time consuming will pay great dividends."

---

## The Color System: A Taxonomy of Consciousness

_(See `docs/_color-definitions.md` for complete details)_

The calendar isn't just a log of activities - it's a visual representation of how mental energy was spent. Each color represents a distinct mental state:

**Azure** (#0088CC - Custom Cyan): Eudaimonia. The north star. Non-obligatory social time, nature, sports, travel, meaningful experiences. The activities worth living for. Default color for new events (because these happen away from computer).

**Purple**: Professional work mode. Mayo Clinic employment. Tracks actual hours worked vs expected 40/week. Future: Split deep work from meetings from admin.

**Yellow**: Passive consumption. YouTube, movies, scrolling, games, eating. The "fat" of life - necessary in moderation. First 30min phone + 1hr YouTube = "free" daily. More yellow correlates with procrastination or fatigue. Non-judgmental tracking.

**Navy**: Personal engineering and admin. Working for myself to enable future azure. Finances, programming, research, organizing, cleaning, personal projects. Building life infrastructure.

**Sage**: Wisdom and meta-reflection. Updating calendar, Obsidian daily log, meditation, contemplating life direction. The observation layer - consciousness observing consciousness.

**Grey**: Sleep and recovery. Tracked via Toggl timer before lights off. Future: Apple Watch integration.

**Flamingo**: Nerdsniped deep reading. LessWrong, rationality blogs, long-form content. Flow state in ideas.

**Orange**: Physical training. Currently just gym, but underspecified. Future: Clarify relationship between body movement and fulfillment.

**Lavender**: In-between states. Showering, transport, grooming, mindless delays. Life's overhead.

**Deprecated colors** (Red, Green, Peacock): Previously job definitions, available for repurposing as life changes.

### The Dual Purpose Problem

Colors currently serve two functions:
1. **Mental state** (spirit) - primary importance
2. **Activity type** (letter) - operational tracking

These don't always align (e.g., educational YouTube = yellow activity but flamingo spirit).

**Future vision:** Split these concepts. Show mental state in Google Calendar. Track both dimensions locally for analysis. This enables honest tracking of what matters while preserving objective activity correlation data.

### Colors as Life KPIs

Looking at a week or month, the color distribution tells a story:

**Healthy balance signals:**
- Rich azure (fulfillment)
- Moderate navy (building future)
- Regular sage (reflection)
- Controlled yellow (recovery without excess)

**Warning patterns:**
- Excessive purple (overwork)
- Yellow spikes (avoidance/burnout)
- Minimal azure (not living for anything)
- Sparse sage (drifting without reflection)

**Changing color definitions is expensive** - requires backfilling hundreds of events and rewiring brain associations. Only done when life circumstances change significantly or insights demand it.

---

## Success Metrics

### Primary Success: The Experience Transformation

**Qualitative Goals:**
- Backfilling shifts from dreaded chore to satisfying ritual
- Weekly calendar review becomes nostalgic life reflection
- Spaced repetition effect: "Oh right, that's when X happened"
- Looking forward to the process, not avoiding it

**Measurement:**
- Consistency of weekly backfilling (currently sporadic → ideally weekly habit)
- Time to backfill 1 week (currently 2-4 hours → target <1 hour)
- Subjective satisfaction rating (track over time)

### Data Quality & Completeness

**Contiguity Edge:**
- Last date with complete walkthrough approval
- Goal: Keep edge within 7 days of present
- Currently: Often 2+ weeks behind

**Data Source Coverage:**
- All 4 Phase 1 sources (Toggl, calls, YouTube, Outlook) regularly imported
- Gaps explicitly tracked and managed
- No "lost weeks" where data is missing

**Color Accuracy:**
- Events correctly categorized by mental state
- Honest tracking (no aspirational coloring)
- Pattern recognition enabled by accurate data

### Time Investment Payoff

**Upfront Investment:**
- Phase 1 development: Acceptable (building for decades)
- Learning curve: Worth it for long-term gains

**Ongoing Time Saved:**
- Current: 2-4 hours/week backfilling
- Target: <1 hour/week (50-75% reduction)
- Compounding value over decades

**Cognitive Load Reduction:**
- Single application vs juggling 6+ tabs
- Decision confidence vs exhausting cross-referencing
- Flow state during review vs fragmented frustration

### Pattern Insights Unlocked

**Currently impossible, future enabled:**
- Time breakdown analysis (% azure, purple, yellow, etc.)
- Pattern correlations (less sleep → more yellow?)
- Weekly dashboards in Obsidian
- "When did I do that?" indexable search
- Multi-year trend visualization

**Success = these analyses become effortless** with local API and robust filtering.

### Long-Term Sustainability

**The real success metric: Still using this in 10 years**

Measured by:
- Maintained regular backfilling habit
- No calendar gaps (except tracked gaps being filled asynchronously)
- System extensibility allows new data sources as life evolves
- Privacy features enable comfortable sharing
- Local data ownership provides independence from Google changes

---

## MVP Scope

### Core Features (Phase 1)

**Data Source Integration:**
1. **Toggl Track API** - Fetch time entries, filter by duration, coalesce phone activities
2. **iOS Call Logs** - Parse iMazing CSV, filter by duration/service, format descriptions
3. **YouTube Watch History** - Parse Google Takeout JSON, fetch video metadata via API, coalesce viewing sessions
4. **Outlook Calendar** - Microsoft Graph API with OAuth 2.0 (or .ics file fallback)

**8/15 Rounding Algorithm:**
- Divide time into 15-minute blocks
- Keep blocks with ≥8 minutes activity
- Always show at least 1 block (end time)
- Configurable threshold

**Phone Activity Coalescing:**
- Sliding window from first to last "Phone" or "ToDelete" entry
- Auto-stop at 15+ minute gaps
- Quality check: retry with 5-min gaps if <50% phone activity
- Discard windows <5 minutes

**YouTube Session Coalescing:**
- Sliding window from first video
- Include next video if within (duration + 30 minutes)
- Apply 8/15 rounding to total session
- Event format: "YouTube - Channel1, Channel2, ..."

**Single Calendar View:**
- WinUI 3 CalendarView component
- Display existing Google Calendar events
- Overlay generated/pending events (yellow/banana color)
- Click event to edit title, times, description

**Approval Workflow:**
- User selects events to publish (individual, day-by-day, or date range)
- Approval state in memory until "Publish" clicked
- Batch publish to Google Calendar
- Receive event IDs, update local database

**Date State Tracking:**
- Per-date states: call_log_data_published, sleep_data_published, youtube_data_published, toggl_data_published, named, complete_walkthrough_approval, part_of_tracked_gap
- Contiguity calculation: edge of verified calendar
- "Fill to present" workflow starts from edge

**Save/Restore System:**
- Create save points (snapshot Google Calendar state)
- Restore to previous save (sends UPDATEs to Google)
- Undo recent publishes

**Weekly Status Tracking:**
- Calculate weekly completion for each data source
- ISO 8601 week calculation (Monday start, Week 1 has ≥4 days)
- Sync status to Excel cloud via Microsoft Graph API
- Values: "Yes" (all 7 days), "Partial" (some days), "No" (zero days)

**Local Database (SQLite):**
- 14 tables storing all data (see `_database-schemas.md`)
- Complete version history for Google Calendar events
- Preserve all source data even if filtered out
- Audit log of all operations

**App-Published Event Notation:**
Append to event description: "Published by Google Calendar Management on {datetime}"

### Out of Scope for MVP

**Future data sources:**
- Chrome extension for YouTube real-time tracking
- Google search history
- Spotify listen history
- Apple Screen Time
- Google Maps timeline
- Physical NFC tags
- Google Takeout automation

**Advanced features:**
- 24/7 server backend with email reminders
- Google AppScripts integration (auto-recolor)
- Data analysis dashboard (time budgets, activity breakdowns)
- Fuzzy search across all events
- Privacy layers (secret events, fake events, encryption)
- Custom local UI (alternative to Google Calendar)
- Chrome extension / GCal Add-on

**Color system enhancements:**
- Sub-colors or gradients
- Split mental state from activity type (dual tracking)
- Automated color suggestions via AI
- Color mood tracking independent of activity

### MVP Success Criteria

**Technical:**
- All 4 data sources successfully import and publish
- Coalescing algorithms work correctly
- Save/restore functionality reliable
- Weekly Excel sync operational
- No data loss

**Experience:**
- Backfilling 1 week takes <2 hours (vs current 2-4)
- Single application, no tab juggling
- Events appear correctly in Google Calendar
- User can approve confidently

**Adoption:**
- Used weekly for 3+ consecutive months
- Contiguity edge stays within 14 days of present
- No abandoned weeks (all tracked or in gaps)

---

## Future Vision & Extensibility

### Phase 2+ Data Sources

Designed for easy addition of new sources:
- Chrome extension for YouTube (real-time tracking)
- Google search history (heatmap overlay)
- Spotify via stats.fm (music context for events)
- Apple Screen Time (app usage)
- Google Maps timeline (location context)
- Physical NFC tags (activity triggers)
- Any future quantified-self data source

### Advanced Analytics

When local database is rich with years of data:
- Time budget dashboards (actual vs desired)
- Pattern correlations (sleep → productivity, etc.)
- Multi-year trend visualization
- Predictive insights (heading toward burnout?)
- Life phases comparison (job changes, etc.)

### Privacy & Sharing

When sharing calendar becomes important:
- Secret events (local only, not in GCal)
- Fake event substitution (public vs private truth)
- Multiple views for different audiences
- Encryption for sensitive data

### Platform Expansion

Local-first architecture enables:
- Web interface (view local data from browser)
- Mobile apps (iOS/Android viewers, quick entry)
- API for other tools (Obsidian integration, custom scripts)
- Custom local UI (if Google Calendar breaks use case)

### The Decade Vision

In 10 years, this system should:
- Still track every hour faithfully
- Contain decade of rich personal history
- Support evolved color taxonomy (as life changes)
- Integrate with technologies that don't exist yet
- Remain independent of Google Calendar (local-first)
- Enable insights impossible to see today

---

## Risks & Assumptions

### Key Assumptions

**User commitment:**
Assumes continued dedication to life tracking for decades. If motivation fades, tool is wasted effort.
- **Mitigation:** Making the process enjoyable increases sustainability.

**Google Calendar longevity:**
Assumes Google Calendar remains viable as primary UI.
- **Mitigation:** Local-first architecture allows migration to custom UI if needed.

**Data source API stability:**
Assumes Toggl, YouTube, Microsoft Graph APIs remain accessible.
- **Mitigation:** Local caching, fallback to file uploads, extensible design for alternatives.

**Windows platform sufficiency:**
Assumes Windows desktop app meets primary use case.
- **Mitigation:** Separable data layer enables future platform expansion.

**Manual approval preference:**
Assumes human-in-the-loop is desired, not full automation.
- **Mitigation:** This is core to the "spaced repetition" experience - not a bug, a feature.

### Technical Risks

**ETags and rollback:**
Google Calendar doesn't provide version history. Our rollback creates new versions on Google's side.
- **Mitigation:** Store complete version history locally. Rollback sends previous data.

**API rate limits:**
YouTube, Toggl, Google Calendar have request quotas.
- **Mitigation:** Batch requests, local caching, exponential backoff retries.

**OAuth token expiration:**
Outlook refresh token expires in 90 days.
- **Mitigation:** Clear re-auth flow, user notification before expiration.

**SQLite limitations:**
Concurrent access, maximum database size.
- **Mitigation:** Single-user desktop app (no concurrency), expected data size well within limits.

### UX Risks

**Learning curve:**
Sophisticated system with many concepts (colors, states, coalescing).
- **Mitigation:** Progressive disclosure, tooltips, help documentation.

**Color definition complexity:**
Managing evolving color taxonomy requires discipline.
- **Mitigation:** Document definitions clearly, warn before changes, preserve history.

**Cognitive load during review:**
Even single-pane view could overwhelm with too much data.
- **Mitigation:** Smart filtering, progressive reveal, focus modes.

---

## Organizational Context

**Not applicable** - personal project for single user.

No stakeholders, no strategic alignment requirements, no change management, no compliance.

Pure individual autonomy and long-term personal value creation.

---

## Timeline

**Phase 1 Development:** Estimated 3-6 months of focused development

**Aggressive timeline** (if full-time):
- Month 1: Database, EF Core, basic UI, Google Calendar integration
- Month 2: Toggl Track, call logs, coalescing algorithms
- Month 3: YouTube, Outlook, state management, polish

**Realistic timeline** (evenings/weekends):
- Months 1-2: Foundation and Google Calendar sync
- Months 3-4: Data source integrations
- Months 5-6: Workflows, state tracking, refinement

**First value delivery:**
- Week 4: Basic Google Calendar sync and manual event entry
- Week 8: First automated data source (likely Toggl Track)
- Month 3: MVP usable for real backfilling

**Long-term milestones:**
- Month 6: Phase 1 complete, daily use
- Year 1: Habits formed, data accumulating
- Year 2+: Pattern insights emerge
- Decade: Irreplaceable personal artifact

---

## Supporting Materials

**Requirements Documentation:**
- `docs/_phase-1-requirements.md` - Detailed Phase 1 scope and workflows
- `docs/_database-schemas.md` - Complete SQLite schema (14 tables)
- `docs/_technology-stack.md` - Research on .NET 9, WinUI 3, all APIs
- `docs/_key-decisions.md` - 18 architectural decisions with rationale
- `docs/_color-definitions.md` - Complete color taxonomy and philosophy

**Existing Work:**
- 5 years of historical Google Calendar data
- Established color system with proven value
- Active use of Toggl Track, YouTube, Outlook for data
- Weekly manual backfilling provides continuous feedback

**Prior Research:**
All technical research completed during discovery phase. Stack validated:
- .NET 9 + WinUI 3 (UI framework)
- SQLite + EF Core (database)
- Google Calendar API, Toggl Track API, YouTube Data API, Microsoft Graph
- All libraries and tools identified

---

_This Product Brief captures the vision for Google Calendar Management._

_It was created through discovery conversation on 2025-11-05 and reflects the unique needs of a long-term personal life tracking practice._

_Next: PRD workflow will transform this brief into detailed product requirements and epic breakdown._

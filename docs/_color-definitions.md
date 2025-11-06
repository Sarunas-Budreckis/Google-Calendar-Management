# Color Definitions - Google Calendar Life Tracking System

**Author:** Sarunas Budreckis
**Date:** 2025-11-05
**Status:** Living document - definitions evolve over time

## Overview

This document defines the comprehensive color taxonomy used for life tracking in Google Calendar. Each color represents both a **mental state** ("spirit of the color") and corresponding **activities** ("letter of the law"). Over time, these definitions evolve as life circumstances change.

**Core Principle:** Colors are descriptive, not prescriptive. The goal is honest tracking for pattern recognition, not moral judgment.

**Dual Purpose:**
- **Primary (Spirit):** Mental state / mode of consciousness
- **Secondary (Letter):** Actual activities that typically correlate with that state

**Future Vision:** Split these concepts for separate analysis, showing mental state in Google Calendar while tracking both for deeper insights.

---

## Active Color Definitions

### Azure (#0088CC) - Eudaimonia

**Custom color:** Vibrant cyan, like a blue lightsaber

**Mental State:** The activities worth living for. Peak fulfillment. Eudaimonia.

**Activities:**
- Non-obligatory social interactions with friends/family
- Phone calls with loved ones
- Spending time in nature (hiking, biking, outdoor activities)
- Sports (volleyball, etc.)
- Playing card games with people
- Good memories and meaningful experiences
- Watching movies in theaters
- Travel and vacations (excluding pure transport)
- Quality texting sessions with friends
- Future: Time with wife and kids

**Why This Matters:**
This is the north star. The reason for organizing the rest of life. Navy activities enable more azure. Yellow needs to be balanced against azure. When looking at a week, azure density is the primary success metric.

**Default Behavior:**
Every event added to calendar defaults to azure (not because of aspiration, but because these activities typically happen away from computer, logged from phone).

**Future Improvements:**
- Could distinguish solo fulfillment vs social fulfillment
- Track quality/intensity of azure experiences
- Correlate azure density with overall life satisfaction

---

### Purple - Professional Work (Mayo Clinic)

**Mental State:** Employed work mode. Professional obligations.

**Activities:**
- All time spent working for Mayo Clinic
- Includes remote work with flexible hours

**Why This Matters:**
Tracks actual hours worked vs expected 40/week. Reveals true cost of employment. Helps maintain work-life balance.

**Current Granularity:**
Single purple for all work activities (meetings, programming, admin).

**Future Improvements:**
Could split into:
- **Purple-1:** Camera-off meetings (passive listening)
- **Purple-2:** Small group / 1-on-1 camera-on meetings (active participation)
- **Purple-3:** Deep programming work (flow state)
- **Purple-4:** Basic admin tasks
- **Purple-5:** Email and communication

This would reveal patterns like "too many meetings, not enough deep work."

---

### Grey - Sleep & Recovery

**Mental State:** Unconscious / resting.

**Activities:**
- Nighttime sleep
- Naps
- Meetings missed/attended while mostly asleep (overlap with sleep event)
  - Purple overlap = attended meeting with eyes closed
  - Grey overlap = completely skipped meeting that mattered
  - Delete event = skipped meeting that didn't matter

**Tracking Method:**
- **Start:** Toggl Track timer right before turning off lights and closing eyes
- **End:** When first meeting starts (if waking contiguously)
- **Challenge:** Wake up slow, sometimes start meeting half-asleep

**Why This Matters:**
- Track sleep onset time changes across week
- Average hours slept per night
- Correlate sleep with next-day activities
- Identify sleep debt patterns

**Future Improvements:**
- Integrate Apple Watch sleep data for accurate wake time
- Lutron light Pico integration (press → IFTTT → auto-start/stop timer)
- Track sleep quality, not just duration
- Distinguish naps from main sleep

---

### Yellow - Passive Consumption

**Mental State:** Passive / consuming / relaxing.

**Activities:**
- YouTube videos
- Movies and shows at home
- Scrolling (Reddit, Instagram, Threads, YouTube Shorts)
- Video games (mostly solo)
- Eating meals (almost always while watching content)

**The "Fat" Analogy:**
Small amount is healthy. Mental tracking of "free" consumption:
- First 30 minutes of phone per day
- First 1 hour of YouTube per day
- (Rationale: Need to eat, restroom breaks, etc.)

**Patterns Observed:**
More yellow when:
- Procrastinating difficult tasks
- Extra tired
- Avoiding something dreadful

**Why This Matters:**
Descriptive, not prescriptive. No moral judgment. Honest tracking enables pattern recognition. Future self can find correlations leading to habits.

**Limitations:**
Currently tied to actual activity, not mental state.

**Future Improvements:**
- Distinguish educational vs entertainment content
- Flag especially inspiring content
- Separate social games (azure) from solo games (yellow)
- Track consumption above "free" threshold in 7-day dashboard
- Mark useful/educational content differently than brainrot

---

### Navy - Personal Engineering & Administration

**Mental State:** "Working for myself." Building future. Enabling azure.

**Like US Navy protecting trade:** Everyday engineering and admin maintaining life infrastructure.

**Activities:**
- Managing finances
- Deep programming (personal projects like this calendar app!)
- Calling customer service
- Research and planning
- Organizing
- Physical work and repairs
- Emails and communication
- Cleaning and maintenance
- Cooking
- Solo shopping (in-person and Amazon)
- Working ON Obsidian system (vs thinking IN Obsidian = sage)

**Philosophy:**
Not just survival - enabling future azure hours. Investment in life infrastructure.

**Current Granularity:**
All personal work lumped together.

**Future Improvements:**
Split into:
- **Navy-1:** Admin/maintenance (necessary but not growth)
- **Navy-2:** Engineering/future-enablement (building, learning, creating)

This distinction matters because navy-2 has compounding value while navy-1 is just keeping the ship afloat.

---

### Sage - Wisdom & Meta-Reflection

**Mental State:** Looking inward. Thinking about thinking. Meta-optimization.

**Activities:**
- Updating this calendar (the act of reflection)
- Writing Daily Obsidian Log
- Meditation
- Contemplating life direction
- Using wisdom to evaluate life activities
- Meta-optimization (optimizing how I optimize)
- Introspection to understand own desires

**Why "Sage":**
Light green color, but "sage" captures the wisdom/reflection concept perfectly.

**Distinction from Navy:**
- Sage = Thinking about life IN Obsidian
- Navy = Working ON Obsidian system

**Why This Matters:**
This is the observation layer. The consciousness observing consciousness. The calendar reviewing the calendar.

**Spaced Repetition Connection:**
Sage activities often involve reviewing past events, reinforcing memories, extracting learnings.

---

### Flamingo - Nerdsniped Deep Reading

**Mental State:** Engrossed in learning complex concepts. Flow state in ideas.

**Activities:**
- Reading LessWrong and rationality blogs
- Long-form content consumption (text, not video)
- Deep dives into complicated topics
- Intellectual rabbit holes

**Why "Flamingo":**
Default Google Calendar color that evokes the distinct mental state of being nerdsniped.

**Distinction from Yellow:**
- Yellow = Passive video consumption
- Flamingo = Active reading engagement

**Future Improvements:**
- Could expand to all long-form reading (not just rationality)
- Track topics to see intellectual exploration patterns
- Measure reading velocity or comprehension

---

### Orange - Physical Training

**Mental State:** Body in motion. Physical challenge.

**Activities:**
- Lifting weights at gym

**Current Limitations:**
Underspecified. Often overlaps with azure (working out with friends) and other physical activities (biking, sports) are already azure.

**Future Vision:**
Somehow represent the connection between body movement and fulfillment in color space.

**Future Improvements:**
- Clarify when physical activity is orange vs azure
- Perhaps: Orange = solo fitness focus, Azure = social physical activity
- Or: Intensity-based distinction
- Or: Merge into azure or create new physical category

---

### Lavender - In-Between States

**Mental State:** Passive existence. Neither productive nor fulfilling. Default state.

**Activities:**
- Showering and grooming
- Cutting nails
- Preparing outfit before event
- Pure transport (driving, flying, bus, Uber)
- Medical visits (allergist, etc.)
- Light cleaning
- Refilling water
- Brushing teeth
- Mindless delaying/procrastinating
- Sitting outside basking in sun
- Any activity that doesn't match other states

**Why This Exists:**
Catch-all for life's in-between moments. Not every minute fits a defined mental state.

**Design Question:**
Should this exist, or should everything be categorized more specifically?

**Future Improvements:**
- Could split transport from self-care from waiting
- Or embrace it as "life's overhead" category
- Track percentage of life in lavender as efficiency metric

---

## Deprecated Colors (Available for Repurposing)

### Red - AEOP Internship (Historical)
**Status:** Not used anymore
**Original Use:** AEOP internship work
**Possible Future Use:** Hard AI alignment research (if pursued)

**Repurposing Challenge:**
Need to backfill hundreds of old events AND rewire brain to recognize new meaning.

---

### Green - Lutron Job (Historical)
**Status:** Not used anymore
**Original Use:** Previous full-time job at Lutron
**Possible Future Use:** Next job (if changing employers)

**Note:** Three colors have cycled through job definitions (Red, Light Green, Green). This shows the evolution of life circumstances reflected in color taxonomy.

---

### Peacock - Social Activities (Deprecated)
**Status:** Not used anymore
**Original Use:** Default social activity color
**Replaced By:** Azure now handles social activities
**Possible Future Use:** Distinguish a subset of azure if meaningful boundary emerges

---

## Color Evolution Philosophy

**Colors change over time** as life changes. This is expected and healthy.

**Changing a color is expensive:**
1. Backfill hundreds/thousands of historical events
2. Rewire brain to recognize new meaning
3. Maintain consistency for pattern analysis

**Criteria for changing a color definition:**
- Life circumstances change significantly (new job, deprecated activity)
- Current definition is underspecified or causing confusion
- New distinction emerges that provides meaningful insights
- Cost of change is justified by long-term value

**Historical preservation:**
Old events keep their original colors. The meaning is contextual to time period. This is fine - the calendar is a historical record.

---

## The Dual Purpose Problem (Spirit vs Letter)

**Current State:**
Colors try to capture both:
- **Mental state** (the "spirit" - how I feel, what mode I'm in)
- **Activity type** (the "letter" - what I'm physically doing)

**Misalignments:**
- Educational YouTube (yellow activity, but flamingo spirit)
- Video games with friends (yellow activity, but azure spirit)
- Working out with friends (orange activity, but azure spirit)
- Some programming is flow-state joy (navy activity, but flamingo spirit)

**Future Vision:**
Split these concepts entirely:
- **Primary color in GCal:** Mental state (visible to others, used for life KPIs)
- **Secondary metadata locally:** Activity type (for filtering, analysis, privacy)

This would allow:
- Honest mental state tracking (what really matters)
- Accurate activity tracking (for objective correlation)
- Separate analysis of each dimension
- Privacy control (show mental state, hide specific activities)

**Implementation Challenge:**
Google Calendar only supports one color per event. Would need:
- Local database to store both dimensions
- GCal shows only mental state color
- Analysis tools can query either or both

---

## Color-Based Life KPIs

**Visual Dashboard:**
Looking at a month or year, the color distribution tells a story:

**Healthy Balance Signals:**
- Rich azure (fulfillment)
- Moderate navy (building future)
- Regular sage (reflection)
- Controlled yellow (recovery without excess)
- Consistent grey (good sleep)

**Warning Signals:**
- Too much purple (overwork)
- Excessive yellow (avoidance/procrastination)
- Fragmented colors (context switching)
- Minimal azure (not living for anything)
- Sparse sage (not reflecting)

**Pattern Recognition:**
- Yellow spikes after stressful purple weeks?
- Azure correlates with better sleep (more grey)?
- Navy investments lead to future azure gains?
- Sage sessions prevent drift?

**Temporal Patterns:**
- Weekly rhythms (purple M-F, azure weekends)
- Seasonal changes (more yellow in winter?)
- Life phase shifts (new job changes purple density)

---

## Use Cases for Color Data

### 1. Time Breakdown Analysis
**Goal:** Understand how life is actually spent
**Requirement:** Accurate data, robust filtering
**Queries:**
- What percentage of waking hours is azure?
- How much purple per week (vs expected 40 hours)?
- Is yellow increasing over time?
- Navy investment trending up or down?

### 2. Pattern Correlation
**Goal:** Find what leads to more/less of each state
**Requirement:** Historical data, correlation tools
**Questions:**
- Does less sleep lead to more yellow?
- Does more navy create more azure later?
- What precedes high-azure weeks?
- What triggers yellow spikes?

### 3. Live Dashboards
**Goal:** Real-time awareness of current patterns
**Requirement:** API exposure to other apps (Obsidian)
**Displays:**
- Last 7 days color breakdown
- Azure density this week vs average
- Yellow above "free" threshold
- Purple hours this week (vs 40)

### 4. Offline Local Viewer
**Goal:** Independence from Google Calendar
**Requirement:** Local data storage, standalone viewer
**Use Case:** If Google Calendar breaks/changes, all data preserved locally

### 5. Indexable Search
**Goal:** "When did I do that?"
**Requirement:** Fast local search across all event titles and descriptions
**Use Case:** Memory recall, pattern finding, life review

### 6. Spaced Repetition for Life
**Goal:** Reinforce memories while approving events
**Requirement:** Track approved vs unprocessed data
**Experience:**
- Review event: "Oh right, that's when X happened"
- Add learning to Obsidian
- Approve event (like flashcard review)
- Memory reinforced

---

## Future Color Improvements

### Sub-Colors / Gradients
**Problem:** One color can't capture full nuance

**Examples Where Sub-Colors Would Help:**

**Purple (Work):**
- Purple-Dark: Deep focus programming
- Purple-Medium: Active meetings
- Purple-Light: Passive meetings or admin

**Yellow (Consumption):**
- Yellow-Gold: Educational/inspiring content
- Yellow-Standard: Entertainment
- Yellow-Pale: Brainrot/time-wasting

**Azure (Fulfillment):**
- Azure-Bright: Peak experiences
- Azure-Standard: Good social time
- Azure-Subtle: Pleasant but not transcendent

**Navy (Personal Work):**
- Navy-Deep: Future-building engineering
- Navy-Standard: Maintenance admin

**Implementation Options:**
- Saturation/brightness variations
- Pattern overlays
- Border colors
- Icons or symbols
- Local metadata + visual coding

### Automated Color Assignment
**Future Vision:** AI suggests colors based on:
- Activity type detected
- Historical patterns
- Context clues
- User corrections over time

**User Still Approves:**
But the suggestion is smart, not just default azure.

### Color Mood Tracking
**Beyond Activities:**
Track actual mood/energy independent of activity:
- High energy purple (engaged work)
- Low energy purple (dragging through work)
- Joyful yellow (genuine relaxation)
- Numbing yellow (avoidance)

This would be metadata, not primary color.

### Multi-Dimensional Color Analysis
**Future Analytics:**
- Color transition matrices (what follows what?)
- Time-of-day color patterns (mornings = navy?)
- Day-of-week distributions
- Seasonal variations
- Life-phase comparisons (job changes, etc.)

---

## Privacy Implications

**Showing Calendar to Others:**
Current challenge: Can't show calendar without revealing everything.

**Color Meanings Are Personal:**
Others see the colors but don't know:
- Yellow = passive consumption
- Purple = specific employer
- etc.

**Future Privacy Solution:**
- **Public view:** Generic color meanings or substitute colors
- **Personal view:** True color taxonomy
- **Secret events:** Only visible locally, not in GCal
- **Fake events:** Different content in GCal vs local

**Example:**
Public view sees "Meeting" (purple).
Personal view sees "Mayo Clinic all-hands" (purple) with notes about what was discussed.

---

## Design Principles

**1. Honesty Over Aspiration**
Colors reflect actual life, not ideal life. No moral judgment.

**2. Evolution Over Rigidity**
Definitions change as life changes. This is healthy.

**3. Insight Over Precision**
Approximate mental state tracking is more valuable than precise activity logging.

**4. Long-Term Patterns Over Daily Perfection**
Individual events might be miscategorized. Aggregate trends matter.

**5. User-Defined Taxonomy**
No universal color meanings. This system works for its creator. Others would define differently.

**6. Memory Reinforcement Over Efficiency**
The act of categorizing and reviewing is part of the value, not just the final data.

---

## Open Questions

1. **Should sub-colors be visual or just metadata?**
   - Visual makes patterns obvious
   - Metadata keeps GCal clean

2. **How to handle activities that span multiple mental states?**
   - Example: Conference = purple (professional) + flamingo (learning) + azure (social)
   - Current: Pick dominant state
   - Future: Multi-tag or time-split?

3. **Should color definitions be stored in the app?**
   - Enables tooltips and help text
   - But definitions are personal and evolving
   - Maybe user-editable taxonomy?

4. **How to visualize color balance in dashboard?**
   - Pie chart?
   - Stacked bar over time?
   - Heatmap calendar?
   - Custom visualization?

5. **Should the app enforce color rules?**
   - Example: "Sleep must be grey"
   - Or allow full user override?
   - Probably suggest but don't enforce

---

**Document Status:** Living document - will evolve with life
**Last Updated:** 2025-11-05
**Next Review:** When color definitions need updating or new patterns emerge

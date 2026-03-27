# Phase 2 Requirements - Editing & Push to GCal (Manual Control)

**Project:** Google Calendar Management
**Date:** 2026-01-30
**Status:** Ready for Implementation

---

## Overview

**Goal:** Enable full manual calendar management with instant editing and reliable publishing.

**Core Question:** "Can I edit and publish events with confidence?"

**Phase 2 builds on Phase 1** - All Phase 1 capabilities are maintained and enhanced with editing and publishing.

---

## Scope

### What's in Phase 2

**Event Editing:**
- Click event → Detail panel opens (right side)
- Edit title, start time, end time, color, description
- Instant editing with auto-save to local DB
- No confirmation dialogs (undo-friendly)
- 0-lag responsiveness

**Event Creation:**
- **Drag-to-create:** Drag on empty calendar space → event created
- **"+ Add Event" button:** Opens detail panel with default values
- Default duration: 1 hour
- Default color: Azure (eudaimonia)
- Time picker with 15-minute increments

**Event Selection:**
- Click to select individual events
- Shift+click for multi-select
- Drag-select for ranges
- **Red outline** for selected events (2px solid red border)
- Selection counter badge ("3 events selected")
- Clear selection button

**Visual State Indicators:**
- **Published events:** Full opacity (100%) in final color
- **Unpushed/pending events:** Translucent (60% opacity)
- **Selected events:** Red outline
- **Hover state:** Subtle highlight

**Push to GCal:**
- "Push to GCal" button shows count of pending events
- Confirmation dialog before publish
- Batch publish to minimize API calls
- Receive event IDs, update local database
- Success animation: Translucent → full opacity transition
- Update sync status accordingly

**Color Assignment:**
- 9 custom colors (Azure, Purple, Yellow, Navy, Sage, Grey, Flamingo, Orange, Lavender)
- Color picker dropdown with labels
- Apply to single event or selection
- Instant visual update

### What's NOT in Phase 2

❌ No data source integration (Toggl, Calls, YouTube, Outlook)
❌ No coalescing algorithms
❌ No hover system with data source timeline
❌ No day naming
❌ No weekly status tracking
❌ No Excel sync
❌ No auto-generated candidate events

---

## Success Criteria

✅ All Phase 1 capabilities maintained
✅ Click event → instant detail panel (0ms lag)
✅ Edit fields update instantly with auto-save
✅ Drag-to-create and button-create both work
✅ Red outline clearly shows selected events
✅ Translucent events clearly distinguishable from published
✅ Push to GCal publishes reliably with confirmation
✅ Visual feedback smooth and satisfying
✅ Can manage entire calendar from app (replace manual GCal editing)

---

## Implementation Checklist

**Focus:** Full manual control with delightful UX

1. Event detail panel (right side slide-in)
2. Inline editing with instant auto-save
3. Event creation (drag + button)
4. Red outline selection feedback
5. Translucent pending events (60% opacity)
6. Color picker (9 custom colors)
7. Push to GCal with confirmation
8. Batch publish operations

**Validation:** Can replace manual Google Calendar editing entirely

---

## Feature List

| Feature | Purpose |
|---------|---------|
| **Event Click → Detail Panel** | Edit event details |
| **Inline Event Editing** | Modify title, time, color, description |
| **Event Selection** | Multi-select for batch operations |
| **Red Outline for Selected** | Visual feedback (2px solid red border) |
| **Push to GCal** | Publish local changes |
| **Translucent Pending Events** | Distinguish unpushed (60% opacity) from published (100%) |
| **Manual Event Creation (Drag)** | Drag on calendar to create event |
| **Manual Event Creation (Button)** | "+ Add Event" button |
| **Event Deletion** | Remove events |
| **Color Assignment** | Apply custom color taxonomy |
| **Instant Responsiveness** | 0-lag editing, auto-save to local DB |

---

## UX Requirements

**Performance Targets:**
- Click event → detail panel: 0ms lag
- Edit field update: Instant auto-save to local DB
- Visual state transitions: 60 FPS smooth animations
- Red outline feedback: <50ms

**Visual State Language:**
- Published: 100% opacity in final color
- Unpushed: 60% opacity (translucent)
- Selected: 2px solid red outline

**User Workflows:**
1. **View calendar** → Events displayed (published = full opacity, unpushed = translucent)
2. **Create new event (Option A):** Drag on empty calendar space → event created
3. **Create new event (Option B):** Click "+ Add Event" button → panel opens
4. **Edit existing event:** Click event → Right panel opens instantly (0ms lag)
5. **Edit fields** → Modify title, time, color, description (instant auto-save)
6. **Visual feedback** → Event becomes translucent (unpushed state)
7. **Multi-select** → Shift+click or drag-select multiple events
8. **Click "Push to GCal"** → Confirmation dialog
9. **Confirm** → Batch publish, progress indicator
10. **Success** → Events become full opacity

---

## Use While Building

**Phase 2 Value:**
- Full calendar management in single app
- Replace manual Google Calendar editing entirely
- Instant editing with no lag
- Confident publishing with visual feedback
- Offline event creation (queued for publish)

**Next Phase:** After Phase 2 is stable, begin Phase 3 (Data Sources) to add intelligent automation and dramatically reduce backfilling time.

---

**Document Version:** 1.0
**Last Updated:** 2026-01-30
**Next Review:** After Phase 2 completion, before starting Phase 3

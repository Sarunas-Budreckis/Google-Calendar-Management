# Phase 1 Requirements - Basic UI & Pull from GCal (Read-Only)

**Project:** Google Calendar Management
**Date:** 2026-01-30
**Status:** Ready for Implementation

---

## Overview

**Goal:** Create a reliable foundation for viewing and backing up Google Calendar data locally.

**Core Question:** "Is my local system in sync with Google Calendar?"

**Phase 1 is strictly read-only** - no editing, no event creation, no push to GCal, no data sources.

---

## Scope

### What's in Phase 1

**Calendar Views:**
- **Year View** (Launch View) - App opens to year view showing months/days for date selection
- **Month View** - Google Calendar-style month view (read-only)
- **Week View** - Vertical timeline with time slots (read-only)
- **Day View** - Detailed hourly view (read-only)

**Google Calendar Sync:**
- Pull from GCal - Fetch events for selected date ranges
- Sync status tracking - Green = synced, Grey = not synced
- Last pulled timestamp visible
- Partial sync (select specific date ranges)
- Full sync option available

**Local Storage:**
- Save button - Manual save workspace
- Auto-save on close - Prevent data loss
- Export to file - Backup selected date ranges
- Import from file - Restore from backup
- SQLite database with complete event history

**Sync Status Indicators:**
- Green dates - Successfully synced with Google Calendar
- Grey dates - Not yet synced
- Last synced timestamp per date range

### What's NOT in Phase 1

❌ No event editing
❌ No event creation
❌ No push to GCal
❌ No data source integration (Toggl, Calls, YouTube, Outlook)
❌ No coalescing algorithms
❌ No day naming or contextualization
❌ No hover system
❌ No weekly status tracking
❌ No Excel sync

---

## Success Criteria

✅ App launches to year view (can select previous years)
✅ Pull events from Google Calendar for selected date ranges
✅ Display events in month/week/day views (read-only)
✅ Green/grey sync status accurate and visible
✅ Save/export functionality works reliably
✅ Local database contains complete event history
✅ Can restore from local backup if needed

---

## Implementation Checklist

**Foundation:**
1. WinUI 3 project setup with .NET 9
2. SQLite database with Entity Framework Core
3. Google Calendar API OAuth and sync
4. Year view with date selection
5. Month/week/day views (read-only display)
6. Green/grey sync status indicators
7. Save/export/import functionality
8. Auto-save on close

**Validation:** Can view and backup existing calendar reliably

---

## Feature List

| Feature | Purpose |
|---------|---------|
| **Year View (Launch View)** | Date selection grid with sync status |
| **Month View** | Display synced calendar data (read-only) |
| **Week View** | Time slots with event blocks (read-only) |
| **Day View** | Detailed hourly view (read-only) |
| **Pull from GCal** | Fetch remote data for selected ranges |
| **Date Selection** | Select ranges for sync/backup (click first, click second) |
| **Partial Backup** | Export selected date ranges to file |
| **Full Backup** | Export all data to file |
| **Save Button** | Manual save workspace |
| **Auto-save on Close** | Prevent data loss |
| **Export/Import Saved Events** | Safety net for development |
| **Sync Status Indicators** | Green/grey per date |
| **Last Pulled Timestamp** | Track sync freshness |
| **Read-Only Display** | View events without editing |

---

## Use While Building

**Phase 1 Value:**
- View existing calendar offline
- Create reliable backups
- Select specific date ranges for sync
- Have confidence in data safety net

**Next Phase:** After Phase 1 is stable, begin Phase 2 (Editing & Push to GCal) to enable full manual calendar management.

---

**Document Version:** 1.0
**Last Updated:** 2026-01-30
**Next Review:** After Phase 1 MVP completion, before starting Phase 2

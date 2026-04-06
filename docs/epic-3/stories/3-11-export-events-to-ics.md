# Story 3.11: Export Events to ICS File

Status: drafted

## Story

As a **user**,
I want **to export my locally synced calendar events to an ICS file**,
so that **I have a portable, human-readable backup that can be restored into this app or imported into any calendar application**.

## Acceptance Criteria

1. **AC-3.11.1 — Export trigger:** Given the app is open, the user can access an "Export to ICS…" action from a menu or toolbar button (exact placement to match the UI design established in prior stories). The action is available from any calendar view.

2. **AC-3.11.2 — Date range selection:** Given the user triggers export, a dialog opens allowing them to select a date range (start date and end date using `DatePicker` controls). The dialog pre-fills with the currently visible date range. No maximum range is enforced.

3. **AC-3.11.3 — File save dialog:** Given the user confirms the date range, the Windows file save picker opens with a default filename of `calendar-export-{YYYY-MM-DD}.ics` and a `.ics` file type filter.

4. **AC-3.11.4 — ICS file content — events:** Given the user confirms a save path, all non-deleted `gcal_event` rows whose date range intersects the selected export range are written to the file as `VEVENT` components within a `VCALENDAR` wrapper. Each `VEVENT` includes: `UID` (= `gcal_event_id`), `SUMMARY`, `DESCRIPTION` (omitted if null/empty), `DTSTART`, `DTEND`, `DTSTAMP` (= UTC now), `LAST-MODIFIED` (= `updated_at`).

5. **AC-3.11.5 — ICS format compliance:** Given the file is written, it conforms to RFC 5545 structure: `BEGIN:VCALENDAR`, `VERSION:2.0`, `PRODID:-//Google Calendar Management//EN`, one `VEVENT` block per event, `END:VCALENDAR`. All datetime values are UTC, written in `YYYYMMDDTHHmmssZ` format. All-day events use `DATE` format (`YYYYMMDD`) without time component.

6. **AC-3.11.6 — Success notification:** Given the export completes, the user sees a brief success notification ("Exported N events to [filename]"). Given no events exist in the selected range, the user sees a message indicating 0 events were found and no file is written.

7. **AC-3.11.7 — Error handling:** Given a file write error occurs (permissions, disk full, etc.), a non-blocking error message is shown and no partial file is left on disk.

8. **AC-3.11.8 — Soft-deleted events excluded:** Given a `gcal_event` row has `is_deleted = 1`, it is excluded from the export.

## Scope Boundaries

**IN SCOPE — this story:**
- Export of `gcal_event` rows (synced Google Calendar events)
- ICS format per RFC 5545
- Date range selection dialog
- Windows file save picker
- Success/error feedback

**OUT OF SCOPE — do NOT implement:**
- Export of `pending_event` rows (Tier 2 — no local-created events in Tier 1)
- CSV, JSON, or any other export format
- Import from ICS (Story 3.12)
- Scheduled or automatic export
- Email or cloud upload of the exported file

## Dev Notes

### ICS Generation

Do not use a third-party ICS library — the subset needed for this story is small enough to generate directly:

```csharp
public static class IcsExporter
{
    public static string GenerateIcs(IEnumerable<GcalEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Google Calendar Management//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");

        foreach (var e in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:{e.GcalEventId}");
            sb.AppendLine($"SUMMARY:{EscapeIcsText(e.Summary ?? "")}");

            if (e.IsAllDay == true)
            {
                sb.AppendLine($"DTSTART;VALUE=DATE:{e.StartDatetime:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{e.EndDatetime:yyyyMMdd}");
            }
            else
            {
                sb.AppendLine($"DTSTART:{e.StartDatetime:yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTEND:{e.EndDatetime:yyyyMMddTHHmmssZ}");
            }

            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine($"LAST-MODIFIED:{e.UpdatedAt:yyyyMMddTHHmmssZ}");

            if (!string.IsNullOrWhiteSpace(e.Description))
                sb.AppendLine($"DESCRIPTION:{EscapeIcsText(e.Description)}");

            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();
    }

    private static string EscapeIcsText(string text)
        => text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n").Replace("\r", "");
}
```

**Important:** `StartDatetime` and `EndDatetime` in the database are stored as UTC. For all-day events, treat the date component only (no timezone suffix).

### Windows File Save Picker

```csharp
var picker = new FileSavePicker();
picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
picker.FileTypeChoices.Add("iCalendar file", new List<string> { ".ics" });
picker.SuggestedFileName = $"calendar-export-{DateTime.Now:yyyy-MM-dd}";

// WinUI 3 requires window handle initialization
WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

var file = await picker.PickSaveFileAsync();
if (file != null)
{
    await FileIO.WriteTextAsync(file, icsContent);
}
```

### Date Range Query

Query using `IGcalEventRepository` (or `CalendarQueryService`):
- Filter: `StartDatetime < rangeEnd && EndDatetime > rangeStart && IsDeleted == false`
- Use `AsNoTracking()` — this is a read-only export operation

### Placement of Export Action

Add "Export to ICS…" to the top bar or a `…` overflow menu on `MainPage`. Use an existing menu pattern if one is already established. Do not add a dedicated toolbar button if the top bar is already crowded — an overflow/kebab menu is acceptable.

### Service Location

Add `IIcsExportService` / `IcsExportService` to `Services/` (flat, no sub-folders). Register as transient in `App.xaml.cs`.

### Error Handling

Wrap file write in try/catch. On exception:
1. Delete any partial file (`file.DeleteAsync()` if the file was already created)
2. Show error via `MainViewModel` notification pattern (consistent with existing notification patterns in the app)

### Unit Test Notes

- `IcsExporter.GenerateIcs()` is a pure static method — test it directly with sample `GcalEvent` objects
- Test: timed event uses `Z` suffix; all-day uses `DATE` format; description escaping; null description omitted
- Integration test: export to temp path, parse back, verify event count and UID values

---

## Tasks / Subtasks

- [ ] **Task 1: Add `IcsExporter` static utility** (AC: 3.11.4, 3.11.5, 3.11.8)
  - [ ] Create `Services/IcsExporter.cs` with `GenerateIcs(IEnumerable<GcalEvent> events)` method
  - [ ] Handle timed events (UTC `Z` suffix), all-day events (`DATE` format), null description, ICS text escaping

- [ ] **Task 2: Add `IIcsExportService` and implementation** (AC: 3.11.2, 3.11.4, 3.11.6, 3.11.7)
  - [ ] `Task<ExportResult> ExportToFileAsync(DateOnly from, DateOnly to, CancellationToken ct = default)`
  - [ ] Query `IGcalEventRepository` for date range, call `IcsExporter`, write to file
  - [ ] Return count and filename; handle and surface errors

- [ ] **Task 3: Add export date range dialog and file picker to `MainPage`** (AC: 3.11.1, 3.11.2, 3.11.3)
  - [ ] Add "Export to ICS…" menu item or toolbar overflow item
  - [ ] Open date range `ContentDialog` pre-filled with visible range
  - [ ] Invoke Windows `FileSavePicker` on confirm

- [ ] **Task 4: Show success/error notification** (AC: 3.11.6, 3.11.7)
  - [ ] Reuse existing notification pattern in `MainViewModel` / `MainPage`
  - [ ] "Exported N events to filename" on success; error message on failure; "No events found" if count = 0

- [ ] **Task 5: Register `IcsExportService` in DI** (AC: all)
  - [ ] Add `services.AddTransient<IIcsExportService, IcsExportService>()` in `App.xaml.cs`

- [ ] **Task 6: Unit and integration tests** (AC: 3.11.4, 3.11.5, 3.11.8)
  - [ ] `Unit/Services/IcsExporterTests.cs` — timed, all-day, escaping, null description
  - [ ] Integration test: export range, verify file content

- [ ] **Task 7: Build verification**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: export visible range → open file in text editor → verify VEVENT blocks match events

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

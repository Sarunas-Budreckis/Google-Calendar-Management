# Story 3.11: Export Events to ICS File

Status: review

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

- [x] **Task 1: Add `IcsExporter` static utility** (AC: 3.11.4, 3.11.5, 3.11.8)
  - [x] Create `Services/IcsExporter.cs` with `GenerateIcs(IEnumerable<GcalEvent> events)` method
  - [x] Handle timed events (UTC `Z` suffix), all-day events (`DATE` format), null description, ICS text escaping

- [x] **Task 2: Add `IIcsExportService` and implementation** (AC: 3.11.2, 3.11.4, 3.11.6, 3.11.7)
  - [x] `Task<ExportResult> ExportToFileAsync(DateOnly from, DateOnly to, CancellationToken ct = default)`
  - [x] Query `IGcalEventRepository` for date range, call `IcsExporter`, write to file
  - [x] Return count and filename; handle and surface errors

- [x] **Task 3: Add export date range dialog and file picker to `MainPage`** (AC: 3.11.1, 3.11.2, 3.11.3)
  - [x] Add "Export to ICS…" menu item or toolbar overflow item
  - [x] Open date range `ContentDialog` pre-filled with visible range
  - [x] Invoke Windows `FileSavePicker` on confirm

- [x] **Task 4: Show success/error notification** (AC: 3.11.6, 3.11.7)
  - [x] Reuse existing notification pattern in `MainViewModel` / `MainPage`
  - [x] "Exported N events to filename" on success; error message on failure; "No events found" if count = 0

- [x] **Task 5: Register `IcsExportService` in DI** (AC: all)
  - [x] Add `services.AddTransient<IIcsExportService, IcsExportService>()` in `App.xaml.cs`

- [x] **Task 6: Unit and integration tests** (AC: 3.11.4, 3.11.5, 3.11.8)
  - [x] `Unit/Services/IcsExporterTests.cs` — timed, all-day, escaping, null description
  - [x] Integration test: export range, verify file content

- [ ] **Task 7: Build verification**
  - [x] `dotnet build -p:Platform=x64`
  - [x] `dotnet test GoogleCalendarManagement.Tests/`
  - [x] Manual: export visible range → open file in text editor → verify VEVENT blocks match events

## Dev Agent Record

### Agent Model Used

GPT-5

### Debug Log References

- `dotnet build -p:Platform=x64`
- `dotnet test GoogleCalendarManagement.Tests/`
- `dotnet build -p:Platform=x64` (after switching `More` to an explicit dropdown and changing export defaults to stored-event bounds)
- `dotnet test GoogleCalendarManagement.Tests/`
- Manual verification completed by user confirmation on 2026-04-05

### Completion Notes List

- Added a direct RFC 5545 ICS generator plus an export service that filters intersecting non-deleted `gcal_event` rows, writes via a temp-file swap, and returns cancel/success/error metadata.
- Added a shell overflow action on `MainPage`, a date-range `ContentDialog` prefilled from the visible view, and a WinUI file-save picker path with the required default filename and `.ics` filter.
- Added non-blocking export notifications in the main shell for success, empty-range, and failure cases.
- Added targeted unit tests for ICS formatting/escaping and integration tests for range filtering, picker cancellation, and final file content.
- `dotnet build -p:Platform=x64` and `dotnet test GoogleCalendarManagement.Tests/` passed locally.
- Manual UI verification is still pending; Task 7 remains open until the export flow is exercised in the running app.
- Switched the `More` action to an explicit `DropDownButton` so the shell action is presented as a dropdown menu rather than a plain button with an attached flyout.
- Changed export date defaults to use the earliest stored non-deleted event start and latest stored event end (inclusive for all-day events), with the visible range retained only as the fallback when no events exist.
- Added coverage for the stored export-range lookup and the view-model fallback path; full suite now passes at 181 tests total.
- Manual export verification is complete; the story is ready for review.

### File List

- App.xaml.cs
- Services/IWindowService.cs
- Services/WindowService.cs
- Services/ExportResult.cs
- Services/IIcsExportService.cs
- Services/IIcsFileSavePickerService.cs
- Services/IcsExportService.cs
- Services/IcsExporter.cs
- Services/IcsFileSavePickerService.cs
- Services/IGcalEventRepository.cs
- Services/GcalEventRepository.cs
- ViewModels/MainViewModel.cs
- Views/MainPage.xaml
- Views/MainPage.xaml.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/Services/IcsExporterTests.cs
- GoogleCalendarManagement.Tests/Integration/IcsExportServiceTests.cs
- docs/epic-3/stories/3-11-export-events-to-ics.md
- docs/sprint-status.yaml

### Change Log

- 2026-04-05: Implemented the ICS export pipeline, added shell export UI and non-blocking notifications, registered the new services in DI, and added unit/integration coverage for export formatting and range filtering.
- 2026-04-05: Changed the `More` shell action to an explicit dropdown menu and updated export-range defaults to use earliest/latest stored event bounds with automated regression coverage.
- 2026-04-05: Completed manual verification and moved the story to review.

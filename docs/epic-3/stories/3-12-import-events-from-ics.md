# Story 3.12: Import Events from ICS File

Status: review

## Story

As a **user**,
I want **to import calendar events from an ICS file back into the app**,
so that **I can restore from a local backup or bring in events from another calendar application**.

## Acceptance Criteria

1. **AC-3.12.1 — Import trigger:** Given the app is open, the user can access an "Import from ICS…" action from a menu or toolbar (same location as Export, for discoverability). The action is available from any calendar view.

2. **AC-3.12.2 — File picker:** Given the user triggers import, the Windows file open picker opens filtered to `.ics` files.

3. **AC-3.12.3 — Parse ICS:** Given the user selects an ICS file, the app parses all `VEVENT` blocks. Events with missing or empty `UID`, `SUMMARY`, `DTSTART`, or `DTEND` are skipped and counted as invalid.

4. **AC-3.12.4 — Match by UID:** Given a `VEVENT` with a `UID` value, if a `gcal_event` row with `gcal_event_id = UID` already exists in the database, that event is updated with the imported values. If no matching row exists, a new `gcal_event` row is inserted.

5. **AC-3.12.5 — Fields imported:** Given a valid `VEVENT`, the following fields are written to the `gcal_event` row: `gcal_event_id` (= `UID`), `summary` (= `SUMMARY`), `description` (= `DESCRIPTION` or null), `start_datetime` (UTC), `end_datetime` (UTC), `is_all_day` (true if `DTSTART` uses `DATE` format, false otherwise), `updated_at` (= `DateTime.UtcNow`). All other fields retain their existing values on update or use safe defaults on insert (`is_deleted = false`, `app_published = false`, `color_id = "azure"`, `created_at = DateTime.UtcNow`).

6. **AC-3.12.6 — Version history on update:** Given an import updates an existing `gcal_event` row, the pre-import row state is written to `gcal_event_version` before the update, using `ChangedBy = "ics_import"` and `ChangeReason = "imported"`.

7. **AC-3.12.7 — Import summary:** Given the import completes, the user sees a summary notification: "Imported N events (X new, Y updated, Z skipped as invalid)". The calendar view refreshes to reflect any new or updated events in the currently visible range.

8. **AC-3.12.8 — Error handling:** Given the selected file cannot be read or is not valid ICS (no `BEGIN:VCALENDAR` found), a non-blocking error message is shown and no database changes are made.

9. **AC-3.12.9 — Import is non-destructive:** Given the import completes, no existing `gcal_event` rows outside the imported set are deleted or modified. Import is strictly additive/upsert — it does NOT delete events that are absent from the ICS file.

## Scope Boundaries

**IN SCOPE — this story:**
- Parse RFC 5545 `VEVENT` blocks from `.ics` files
- Upsert to `gcal_event` table (new insert or update of existing row matched by UID)
- Version history snapshot on update
- Windows file open picker
- Import summary notification
- Calendar view refresh after import

**OUT OF SCOPE — do NOT implement:**
- Importing into `pending_event` (Tier 2)
- Syncing imported events back to Google Calendar (Epic 7)
- Importing recurring event series (recurrence rules — `RRULE`) — skip with a counted warning
- Importing calendar attachments, alarms, or attendees
- Merging multiple ICS files in a single operation

## Dev Notes

### ICS Parser

A minimal RFC 5545 parser for the fields needed:

```csharp
public static class IcsParser
{
    public record ParsedEvent(
        string Uid,
        string Summary,
        string? Description,
        DateTime StartUtc,
        DateTime EndUtc,
        bool IsAllDay);

    public static IReadOnlyList<ParsedEvent> ParseIcs(string icsContent)
    {
        var results = new List<ParsedEvent>();
        // Split on CRLF or LF; unfold continuation lines (lines starting with space/tab)
        // Parse VEVENT blocks; extract properties; skip RRULE events with a warning count
        // ...
        return results;
    }
}
```

Key parsing rules:
- **Line unfolding:** Lines beginning with a space or tab are continuations of the previous line (RFC 5545 §3.1). Unfold before property parsing.
- **DATE vs DATETIME:** If `DTSTART` has `VALUE=DATE` parameter or no `T` in the value, it's all-day. Parse as `DateOnly`, convert to `DateTime` at midnight UTC.
- **DATETIME with TZID:** If `DTSTART` has a `TZID` parameter, convert to UTC using `TimeZoneInfo.FindSystemTimeZoneById`. If the timezone is unknown, fall back to treating the value as local time and converting to UTC.
- **DATETIME UTC (`Z` suffix):** Parse directly as UTC.
- **Text unescaping:** Reverse ICS escaping — `\\` → `\`, `\;` → `;`, `\,` → `,`, `\n` or `\N` → newline.
- **Skip RRULE events:** If a `VEVENT` contains a `RRULE` property, skip it (recurring series import is out of scope). Count as skipped-recurring in the summary.

### Upsert Logic

```csharp
// In ImportService or GcalEventRepository
var existing = await db.GcalEvents
    .FirstOrDefaultAsync(e => e.GcalEventId == parsed.Uid, ct);

if (existing != null)
{
    // Write version history snapshot first
    await WriteVersionHistoryAsync(db, existing, "ics_import", "imported", ct);
    // Update fields
    existing.Summary = parsed.Summary;
    // ... etc
    existing.UpdatedAt = DateTime.UtcNow;
}
else
{
    var newEvent = new GcalEvent
    {
        GcalEventId = parsed.Uid,
        Summary = parsed.Summary,
        // ... safe defaults
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsDeleted = false,
        AppPublished = false,
        ColorId = "azure"
    };
    db.GcalEvents.Add(newEvent);
}
await db.SaveChangesAsync(ct);
```

### Windows File Open Picker

```csharp
var picker = new FileOpenPicker();
picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
picker.FileTypeFilter.Add(".ics");
WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

var file = await picker.PickSingleFileAsync();
if (file != null)
{
    var content = await FileIO.ReadTextAsync(file);
    // proceed with parse + import
}
```

### Calendar View Refresh After Import

After import completes, publish a `SyncCompletedMessage` (or a dedicated `ImportCompletedMessage`) so `MainViewModel` refreshes `CurrentEvents` for the visible range. Reuse existing refresh infrastructure — do not add a second refresh path.

### Service Location

Add `IIcsImportService` / `IcsImportService` to `Services/`. Register as transient in `App.xaml.cs`.

### Unit Test Notes

- `IcsParser.ParseIcs()` — test timed event, all-day event, null description, RRULE skip, line unfolding, text unescaping
- Upsert logic — new insert, update existing, version history written on update
- Invalid file (no `BEGIN:VCALENDAR`) — returns 0 events without throwing

---

## Tasks / Subtasks

- [x] **Task 1: Add `IcsParser` static utility** (AC: 3.12.3, 3.12.5)
  - [x] Parse `VEVENT` blocks; extract `UID`, `SUMMARY`, `DESCRIPTION`, `DTSTART`, `DTEND`
  - [x] Handle `DATE` vs `DATETIME`, `TZID`, `Z` suffix, line unfolding, text unescaping
  - [x] Skip `RRULE` events (count as skipped-recurring)
  - [x] Skip events with missing required fields (count as invalid)

- [x] **Task 2: Add `IIcsImportService` and implementation** (AC: 3.12.4, 3.12.5, 3.12.6, 3.12.9)
  - [x] `Task<ImportResult> ImportFromFileAsync(StorageFile file, CancellationToken ct = default)`
  - [x] Parse ICS, upsert to `gcal_event`, write version history on updates, return summary counts

- [x] **Task 3: Add import trigger and file picker to `MainPage`** (AC: 3.12.1, 3.12.2)
  - [x] Add "Import from ICS…" to the same menu/overflow as Export
  - [x] Open Windows file open picker filtered to `.ics`

- [x] **Task 4: Show import summary notification** (AC: 3.12.7, 3.12.8)
  - [x] "Imported N events (X new, Y updated, Z skipped)" on success
  - [x] Error message on unreadable/invalid file

- [x] **Task 5: Refresh calendar view after import** (AC: 3.12.7)
  - [x] Trigger visible-range refresh via existing message infrastructure

- [x] **Task 6: Register `IcsImportService` in DI**
  - [x] `services.AddTransient<IIcsImportService, IcsImportService>()` in `App.xaml.cs`

- [x] **Task 7: Unit and integration tests** (AC: 3.12.3, 3.12.5, 3.12.6, 3.12.8, 3.12.9)
  - [x] Parser tests: timed, all-day, RRULE skip, invalid fields, line unfolding
  - [x] Upsert tests: new insert, existing update with version history
  - [x] Invalid file test: no database changes on parse failure

- [x] **Task 8: Build verification**
  - [x] `dotnet build -p:Platform=x64`
  - [x] `dotnet test GoogleCalendarManagement.Tests/`
  - [x] Manual: export then re-import → verify summary counts and no data loss

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet test .\GoogleCalendarManagement.Tests\GoogleCalendarManagement.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~IcsParserTests|FullyQualifiedName~IcsImportServiceTests|FullyQualifiedName~MainViewModelTests"`
- `dotnet build -p:Platform=x64`
- `dotnet test .\GoogleCalendarManagement.Tests\GoogleCalendarManagement.Tests.csproj -p:Platform=x64`

### Completion Notes List

- Added `IcsParser` with RFC 5545 line unfolding, text unescaping, `DATE`/`DATETIME` parsing, `TZID` handling, invalid-event counting, and RRULE skipping.
- Added `IIcsImportService` / `IcsImportService` to read `.ics` files, upsert `gcal_event` rows by UID, snapshot version history on updates, and preserve non-imported fields on update.
- Added the MainPage import action beside export, wired the Windows file open picker, and reused `SyncCompletedMessage` plus the existing InfoBar notification path to refresh the visible range after import.
- Added parser, integration, and ViewModel coverage for import flows; also isolated messenger-based unit tests into a non-parallel collection to prevent cross-test interference in the full suite.
- Automated validation passed, and manual export-then-reimport verification was completed successfully with no observed data loss.

### File List

- App.xaml.cs
- Services/IIcsImportService.cs
- Services/ImportResult.cs
- Services/IcsImportService.cs
- Services/IcsParser.cs
- ViewModels/MainViewModel.cs
- Views/MainPage.xaml
- Views/MainPage.xaml.cs
- GoogleCalendarManagement.Tests/Integration/IcsImportServiceTests.cs
- GoogleCalendarManagement.Tests/Unit/MessengerTestCollection.cs
- GoogleCalendarManagement.Tests/Unit/Services/CalendarSelectionServiceTests.cs
- GoogleCalendarManagement.Tests/Unit/Services/IcsParserTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs
- GoogleCalendarManagement.Tests/Unit/ViewModels/MainViewModelTests.cs

### Change Log

- 2026-04-05: Implemented ICS import parsing, upsert/version-history persistence, UI trigger/notification wiring, and automated plus manual verification. Story is ready for review.

# Story 3.5: Implement Event Editing Panel (Tier 2)

Status: moved

> **Moved to Epic 4.** This story has been relocated to Epic 4 (Event Editing — Tier 2) as it is out of scope for Tier 1.
> See: [docs/epic-4/stories/4-1-implement-event-editing-panel.md](../../epic-4/stories/4-1-implement-event-editing-panel.md)

## Story

As a **user**,
I want **to edit event details directly in the application**,
so that **I can modify events before publishing to Google Calendar**.

## Acceptance Criteria

1. **AC-3.5.1 — Edit Mode Activation:** Given the user selects an event and the details panel is open (Story 3.4), when the user clicks the "Edit" button (previously disabled in Story 3.4), the panel transitions to edit mode within 100 ms — all fields become editable in-place.

2. **AC-3.5.2 — Title Field:** Given edit mode is active, the title field renders as a `TextBox` with the current title pre-filled. A required-field validation error ("Title is required") appears inline below the field if the user clears it. The Save cannot complete with an empty title.

3. **AC-3.5.3 — Start/End Date+Time Pickers:** Given edit mode is active, start and end date/time each render as a `DatePicker` + `TimePicker` pair. The `TimePicker` has `MinuteIncrement = 15`. Both are pre-filled with current event values (UTC converted to local time for display). Validation error appears inline if end time is not after start time.

4. **AC-3.5.4 — Description Field:** Given edit mode is active, the description field renders as a multi-line `TextBox` (`AcceptsReturn="True"`, scrollable) pre-filled with the current description (empty string if null).

5. **AC-3.5.5 — Color Picker Stub:** Given edit mode is active, a color swatch placeholder is visible (labelled "Color: [current color name]") with a "Coming soon" tooltip. Story 3.7 will replace this stub with a full color picker — the layout slot must exist in Story 3.5 but must NOT implement color editing logic.

6. **AC-3.5.6 — Auto-Save (Debounced):** Given the user modifies any field and no validation errors exist, changes are saved to the local `gcal_event` database row automatically after 500 ms of inactivity (debounced). No explicit "Save" button is needed or shown.

7. **AC-3.5.7 — Unsaved Indicator:** Given the user is actively typing (within the 500 ms debounce window), a subtle indicator ("Saving…" text or pulsing dot) is visible. Once auto-save completes, the indicator shows "Saved" (or disappears) within 300 ms.

8. **AC-3.5.8 — Database Write:** Given auto-save fires, the following `gcal_event` columns are updated: `summary`, `description`, `start_datetime`, `end_datetime` (stored as UTC), `updated_at`, and `app_last_modified_at` (set to `DateTime.UtcNow`). `app_published` remains `false`. `gcal_event_id` is NEVER changed.

9. **AC-3.5.9 — Validation — End Before Start:** Given the user sets end time before start time, an inline validation message ("End time must be after start time") appears immediately and auto-save is blocked until corrected.

10. **AC-3.5.10 — Undo (Ctrl+Z):** Given the user presses Ctrl+Z while in edit mode, the last field change is reverted. Undo operates per-field (reverts the field to its value before the last keystroke batch). Only one undo level is required for Tier 2.

11. **AC-3.5.11 — Esc Closes Panel:** Given edit mode is active and changes have been auto-saved, pressing Esc closes the panel (same behavior as Story 3.4). If the debounce timer is still pending (changes NOT yet saved), pressing Esc triggers an immediate save before closing.

12. **AC-3.5.12 — `AppPublished` Stays False:** Given any edit is saved, `app_published = 0` in the database. The event remains local-only — not pushed to Google Calendar. Events with `app_published = false` are rendered at 100% opacity in Tier 2 (no translucency required until Epic 7 publishing workflow).

13. **AC-3.5.13 — Calendar View Refresh:** Given auto-save completes, the calendar view (Month/Week/Day/Year) refreshes to show the updated event title and any visible field changes without requiring full page reload. Only the affected event's display model is updated.

---

## Scope Boundaries (Tier 2 Only)

**IN SCOPE — this story:**
- Editing `summary`, `description`, `start_datetime`, `end_datetime` on existing `gcal_event` rows
- Auto-save with 500 ms debounce using `DispatcherTimer`
- Inline validation (empty title, end-before-start)
- Single-level Undo (Ctrl+Z)
- Color picker stub/placeholder (layout slot only — no editing logic)
- `IGcalEventRepository.UpdateAsync()` new method
- `EventDetailsPanelViewModel` edit mode state and save logic
- Refresh affected event in calendar view via `CalendarSelectionService`/`WeakReferenceMessenger`

**OUT OF SCOPE — do NOT implement:**
- Color picking / color change (Story 3.7)
- Event creation (Story 3.6)
- Pushing edits to Google Calendar (Epic 7)
- Multi-level undo
- `pending_event` table — **all edits write directly to `gcal_event`**
- Translucent (60% opacity) rendering for `app_published = false` events (Epic 7)
- Drag-to-reschedule in calendar view

---

## Dev Notes

### CRITICAL: Actual Project Structure

The project is a single flat WinUI 3 project — **NOT** the `src/Core/` hierarchy described in the architecture doc. All new files follow the same structure as existing code:

```
GoogleCalendarManagement/             ← project root (this IS the csproj root)
├── App.xaml / App.xaml.cs            ← DI registration in ConfigureServices()
├── Views/                            ← XAML + code-behind (SettingsPage, and epic 3 views)
├── ViewModels/                       ← ViewModels (SettingsViewModel, MainViewModel, EventDetailsPanelViewModel)
├── Services/                         ← All interfaces + implementations (flat, no sub-folders)
├── Messages/                         ← WeakReferenceMessenger messages
├── Models/                           ← CalendarEventDisplayModel, NavigationState, ViewMode
├── Data/                             ← EF Core (CalendarDbContext, Entities, Configurations, Migrations)
└── GoogleCalendarManagement.Tests/   ← Unit + Integration tests
```

**Namespace:** `GoogleCalendarManagement.Services`, `GoogleCalendarManagement.ViewModels`, `GoogleCalendarManagement.Views`, `GoogleCalendarManagement.Models`

**NEVER create a `Core/` folder.** Services live flat in `Services/`.

### Dependency: Stories 3.2–3.4 Must Be Complete First

Story 3.5 requires the following to exist before implementation begins:

| Prerequisite | Where it lives | What Story 3.5 needs from it |
|---|---|---|
| Story 3.2 | `ColorMappingService` fully implemented with all 9 colours | `IColorMappingService.AllColors` to display current colour name in stub |
| Story 3.3 | `CalendarSelectionService`, `EventSelectedMessage` | Event selection messaging to trigger edit mode |
| Story 3.4 | `EventDetailsPanelControl.xaml` + `EventDetailsPanelViewModel` | The read-only panel to EXTEND to edit mode |

**Do NOT reinvent `EventDetailsPanelControl` or `EventDetailsPanelViewModel`** — Story 3.5 EXTENDS them.

### GcalEvent Entity — Fields to Update

Only these `GcalEvent` fields are written in Story 3.5:

```csharp
// Data/Entities/GcalEvent.cs (already exists)
event.Summary = editedTitle;              // null-safe: store "" as null? Store as-is.
event.Description = editedDescription;   // nullable, store null if empty
event.StartDatetime = startUtc;          // Convert local→UTC before saving
event.EndDatetime = endUtc;              // Convert local→UTC before saving
event.AppLastModifiedAt = DateTime.UtcNow;
event.UpdatedAt = DateTime.UtcNow;
// AppPublished stays unchanged (false)
// GcalEventId NEVER modified
```

**UTC conversion:** The UI displays local time (`StartDatetime.ToLocalTime()`). Before saving back to DB: `DateTime.SpecifyKind(localPicker.Date.DateTime + localPicker.Time, DateTimeKind.Local).ToUniversalTime()`.

### New Repository Method

Add to `Services/IGcalEventRepository.cs`:

```csharp
Task UpdateAsync(GcalEvent updatedEvent, CancellationToken ct = default);
```

Implement in `Services/GcalEventRepository.cs`:

```csharp
public async Task UpdateAsync(GcalEvent updatedEvent, CancellationToken ct = default)
{
    await using var db = await _dbFactory.CreateDbContextAsync(ct);
    db.GcalEvents.Update(updatedEvent);
    await db.SaveChangesAsync(ct);
}
```

**Use `IDbContextFactory<CalendarDbContext>` (already in DI) — NOT scoped `CalendarDbContext`** — same pattern as all existing repository methods. The factory creates a fresh context per operation (from Story 3.1 learnings).

### EventDetailsPanelViewModel Changes

Extend the existing `EventDetailsPanelViewModel` (created in Story 3.4):

```csharp
public partial class EventDetailsPanelViewModel : ObservableObject
{
    // --- EXISTING (from Story 3.4) ---
    [ObservableProperty] private bool isPanelVisible;
    [ObservableProperty] private CalendarEventDisplayModel? selectedEvent;

    // --- NEW in Story 3.5 ---
    [ObservableProperty] private bool isEditMode;

    // Editable fields (bound to UI inputs)
    [ObservableProperty] private string editTitle = "";
    [ObservableProperty] private DateOnly editStartDate;
    [ObservableProperty] private TimeOnly editStartTime;
    [ObservableProperty] private DateOnly editEndDate;
    [ObservableProperty] private TimeOnly editEndTime;
    [ObservableProperty] private string editDescription = "";

    // Validation
    [ObservableProperty] private string titleError = "";
    [ObservableProperty] private string dateTimeError = "";

    // Save state indicator
    [ObservableProperty] private string saveStatusText = "";  // "Saving…" | "Saved" | ""

    // Undo
    private string? _undoTitle;
    private TimeOnly? _undoStartTime;
    private TimeOnly? _undoEndTime;
    private string? _undoDescription;

    // Debounce timer
    private DispatcherTimer? _debounceTimer;

    [RelayCommand] private void EnterEditMode() { ... }
    [RelayCommand] private async Task SaveNowAsync() { ... }  // called by Esc-to-close
    private void OnFieldChanged() { ... }   // called by any ObservableProperty Changed partial
    private void StartDebounce() { ... }    // resets _debounceTimer to 500ms
    public void UndoLastChange() { ... }    // called by Ctrl+Z handler in view
}
```

**Enter edit mode:** On `EnterEditMode()`, copy `SelectedEvent` fields into editable properties; set `IsEditMode = true`; snapshot current values into undo vars.

**Debounce pattern using `DispatcherTimer`:**

```csharp
private void StartDebounce()
{
    SaveStatusText = "Saving…";
    _debounceTimer?.Stop();
    if (_debounceTimer == null)
    {
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await SaveNowAsync();
        };
    }
    _debounceTimer.Start();
}
```

**Do NOT use `Task.Delay` for debounce on UI thread** — use `DispatcherTimer` (WinUI 3 thread-safe).

### XAML: Extending EventDetailsPanelControl

Story 3.4 builds a panel with a disabled "Edit" button. Story 3.5 extends that XAML using `VisualState` to switch between read-only and edit mode. Example pattern:

```xaml
<VisualStateManager.VisualStateGroups>
    <VisualStateGroup x:Name="EditStates">
        <!-- ReadOnly state (Story 3.4) -->
        <VisualState x:Name="ReadOnly">
            <VisualState.Setters>
                <Setter Target="TitleTextBlock.Visibility" Value="Visible"/>
                <Setter Target="TitleTextBox.Visibility" Value="Collapsed"/>
                <Setter Target="EditButton.IsEnabled" Value="True"/>
                <!-- ...etc... -->
            </VisualState.Setters>
        </VisualState>
        <!-- EditMode state (Story 3.5) -->
        <VisualState x:Name="EditMode">
            <VisualState.Setters>
                <Setter Target="TitleTextBlock.Visibility" Value="Collapsed"/>
                <Setter Target="TitleTextBox.Visibility" Value="Visible"/>
                <Setter Target="EditButton.IsEnabled" Value="False"/>
            </VisualState.Setters>
        </VisualState>
    </VisualStateGroup>
</VisualStateManager.VisualStateGroups>
```

**Title field:**
```xaml
<TextBox x:Name="TitleTextBox"
         Text="{x:Bind ViewModel.EditTitle, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         PlaceholderText="Event title (required)"/>
<TextBlock Text="{x:Bind ViewModel.TitleError, Mode=OneWay}"
           Foreground="{ThemeResource SystemFillColorCriticalBrush}"
           Visibility="{x:Bind ViewModel.TitleError, Mode=OneWay, Converter={StaticResource StringToVisibilityConverter}}"/>
```

**Date + Time Pickers (WinUI 3 has SEPARATE DatePicker and TimePicker controls):**
```xaml
<!-- Start -->
<DatePicker Date="{x:Bind ViewModel.EditStartDate, Mode=TwoWay, Converter={StaticResource DateOnlyToDateTimeOffsetConverter}}"/>
<TimePicker Time="{x:Bind ViewModel.EditStartTime, Mode=TwoWay, Converter={StaticResource TimeOnlyToTimeSpanConverter}}"
            MinuteIncrement="15"/>
<!-- End -->
<DatePicker Date="{x:Bind ViewModel.EditEndDate, Mode=TwoWay, ...}"/>
<TimePicker Time="{x:Bind ViewModel.EditEndTime, Mode=TwoWay, ...}"
            MinuteIncrement="15"/>
<TextBlock Text="{x:Bind ViewModel.DateTimeError, Mode=OneWay}" Foreground="..."/>
```

**WinUI 3 `DatePicker.Date` is `DateTimeOffset?`; `TimePicker.Time` is `TimeSpan`.** Create value converters `DateOnlyToDateTimeOffsetConverter` and `TimeOnlyToTimeSpanConverter` in `Views/Converters/`.

**Color picker stub:**
```xaml
<StackPanel Orientation="Horizontal">
    <Rectangle Width="16" Height="16" Fill="{x:Bind ViewModel.SelectedEvent.ColorHex, Mode=OneWay, Converter={StaticResource HexToSolidColorBrushConverter}}"/>
    <TextBlock Text="{x:Bind ViewModel.ColorDisplayName, Mode=OneWay}" Margin="4,0,0,0"/>
    <Button Content="✎" IsEnabled="False" ToolTipService.ToolTip="Color picking coming in Story 3.7"/>
</StackPanel>
```

**Description field:**
```xaml
<TextBox Text="{x:Bind ViewModel.EditDescription, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
         AcceptsReturn="True" TextWrapping="Wrap" MaxHeight="200"
         ScrollViewer.VerticalScrollBarVisibility="Auto"/>
```

**Save status indicator:**
```xaml
<TextBlock Text="{x:Bind ViewModel.SaveStatusText, Mode=OneWay}"
           FontSize="11" Opacity="0.6"/>
```

**Ctrl+Z binding** — in code-behind:
```csharp
protected override void OnKeyDown(KeyRoutedEventArgs e)
{
    if (e.Key == VirtualKey.Z &&
        InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
    {
        ViewModel.UndoLastChange();
        e.Handled = true;
    }
    base.OnKeyDown(e);
}
```

### Calendar View Refresh After Save

After `SaveNowAsync()` completes, publish a message so `MainViewModel` refreshes only the affected event in the calendar view. Use `WeakReferenceMessenger`:

```csharp
// Messages/EventUpdatedMessage.cs (NEW)
public record EventUpdatedMessage(string GcalEventId);
```

`MainViewModel` subscribes to `EventUpdatedMessage` and refreshes the `CurrentEvents` list — replacing only the updated `CalendarEventDisplayModel` (via `ICalendarQueryService.GetEventByGcalIdAsync(id)`). This avoids full reload.

### Existing Infrastructure to Reuse (Do NOT Reinvent)

| What | Where | Use |
|---|---|---|
| `IDbContextFactory<CalendarDbContext>` | Already in DI | All repository DB access |
| `GcalEvent` entity | `Data/Entities/GcalEvent.cs` | Fields to update (see above) |
| `CalendarEventDisplayModel` | `Models/CalendarEventDisplayModel.cs` | Read-only display model; create fresh instance after save |
| `IColorMappingService` | `Services/IColorMappingService.cs` | Get colour name for stub display (`AllColors` dict) |
| `WeakReferenceMessenger` | CommunityToolkit.Mvvm | Publish `EventUpdatedMessage` |
| `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` | CommunityToolkit.Mvvm | ViewModel base |
| `ILogger<T>` via Serilog | Already in DI | Log save success/failure |
| `HexToSolidColorBrushConverter` | Created in Story 3.2 (if exists) | Color swatch rendering |
| `DispatcherTimer` | `Microsoft.UI.Dispatching` (WinUI 3) | 500ms debounce |

### Previous Story Learnings (Applied Here)

- **Use `IDbContextFactory`, not scoped `CalendarDbContext`** in singleton services. Pattern: `await using var db = await _dbFactory.CreateDbContextAsync(ct);`
- **GcalEvent PK is `GcalEventId` (string)** — not an int. Never use `int id` to look up events.
- **All DB timestamps are UTC.** UI shows local time; convert back to UTC before saving: `DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime()`.
- **Filter `IsDeleted == false`** in all GcalEvent queries.
- **Build:** `dotnet build -p:Platform=x64` (x64 required for WinUI 3).
- **Test:** `dotnet test GoogleCalendarManagement.Tests/`
- **Data files are gitignored** — never commit `.db`, `.db-wal`, `.db-shm`.

### Database Schema — `pending_event` Table NOT Used

The `pending_event` table (described in `_database-schemas.md` Tier 2) is for **future push-to-GCal tracking** (Epic 7). Story 3.5 writes edits directly to `gcal_event`. No new tables or migrations are needed for this story.

Verify: `gcal_event` table already has `app_last_modified_at` column (added in Epic 2 migrations). Check `Data/Configurations/GcalEventConfiguration.cs` before assuming column names.

---

## Tasks / Subtasks

- [ ] **Task 1: Add `UpdateAsync` to `IGcalEventRepository`** (AC: 3.5.8)
  - [ ] Add `Task UpdateAsync(GcalEvent updatedEvent, CancellationToken ct = default)` to `Services/IGcalEventRepository.cs`
  - [ ] Implement in `Services/GcalEventRepository.cs` using `IDbContextFactory` (attach + update + SaveChangesAsync)
  - [ ] Add `EventUpdatedMessage` to `Messages/EventUpdatedMessage.cs`

- [ ] **Task 2: Create value converters** (AC: 3.5.3)
  - [ ] `Views/Converters/DateOnlyToDateTimeOffsetConverter.cs` — converts `DateOnly` ↔ `DateTimeOffset` for WinUI 3 `DatePicker.Date`
  - [ ] `Views/Converters/TimeOnlyToTimeSpanConverter.cs` — converts `TimeOnly` ↔ `TimeSpan` for WinUI 3 `TimePicker.Time`
  - [ ] Register in `App.xaml` resources (or in the view's `<Page.Resources>`)

- [ ] **Task 3: Extend `EventDetailsPanelViewModel`** (AC: 3.5.1–3.5.13)
  - [ ] Add `IsEditMode`, all editable properties, validation properties, `SaveStatusText`
  - [ ] Implement `EnterEditMode()` — copy `SelectedEvent` fields to editable props; snapshot for undo
  - [ ] Implement field-changed partial methods — call `ValidateFields()` + `StartDebounce()`
  - [ ] Implement `ValidateFields()` — sets `TitleError` and `DateTimeError`; returns bool
  - [ ] Implement `StartDebounce()` — `DispatcherTimer` 500ms, calls `SaveNowAsync()`
  - [ ] Implement `SaveNowAsync()` — only saves if `ValidateFields()` passes; writes UTC; sets `SaveStatusText`; publishes `EventUpdatedMessage`
  - [ ] Implement `UndoLastChange()` — reverts editable fields to undo snapshot
  - [ ] On `EventSelectedMessage` received while `IsEditMode = true` — save pending changes before switching
  - [ ] Add `ColorDisplayName` computed property using `IColorMappingService`

- [ ] **Task 4: Extend `EventDetailsPanelControl.xaml`** (AC: 3.5.1–3.5.13)
  - [ ] Add `VisualStateManager` with `ReadOnly` and `EditMode` states
  - [ ] Enable "Edit" button (was disabled in Story 3.4); bind `Command="{x:Bind ViewModel.EnterEditModeCommand}"`
  - [ ] Add edit-mode controls: `TitleTextBox`, date+time picker pairs, description `TextBox`, color stub, save status label
  - [ ] Add inline validation `TextBlock` for title and datetime errors
  - [ ] Add `OnKeyDown` override for Ctrl+Z in code-behind
  - [ ] Add Esc key handling: if `IsEditMode = true` and timer pending → `await ViewModel.SaveNowAsync()` before panel close
  - [ ] Transition `VisualState` in code-behind on `ViewModel.IsEditMode` property change

- [ ] **Task 5: Calendar view refresh on save** (AC: 3.5.13)
  - [ ] In `MainViewModel`, subscribe to `EventUpdatedMessage`
  - [ ] On receipt: call `ICalendarQueryService.GetEventByGcalIdAsync(id)` and replace matching item in `CurrentEvents`

- [ ] **Task 6: DI registration** (AC: all)
  - [ ] `EventUpdatedMessage` requires no DI (it's a plain record)
  - [ ] No new services require registration (UpdateAsync is added to existing repository)
  - [ ] Confirm `EventDetailsPanelViewModel` is still `AddSingleton` (was registered in Story 3.4)

- [ ] **Task 7: Unit tests** (AC: 3.5.6, 3.5.9, 3.5.10)
  - [ ] `Unit/ViewModels/EventDetailsPanelViewModelEditTests.cs`
    - [ ] Test: `EnterEditMode()` copies `SelectedEvent` fields correctly
    - [ ] Test: Valid edit → `ValidateFields()` returns true → save proceeds
    - [ ] Test: Empty title → `TitleError` set → save blocked
    - [ ] Test: End < Start → `DateTimeError` set → save blocked
    - [ ] Test: `UndoLastChange()` restores pre-edit field values
  - [ ] `Unit/Services/GcalEventRepositoryUpdateTests.cs`
    - [ ] Test: `UpdateAsync()` persists all changed fields; does NOT change `GcalEventId`; `app_published` stays `false`

- [ ] **Task 8: Build verification**
  - [ ] `dotnet build -p:Platform=x64` — must pass with 0 errors
  - [ ] `dotnet test GoogleCalendarManagement.Tests/` — all tests pass
  - [ ] Manual: select event → click Edit → modify title → wait 600ms → check DB (via logs or debug) → verify updated title; press Esc → verify closes; verify calendar view shows updated title

---

## Open Questions

**Q1:** Does `GcalEventConfiguration.cs` already map `app_last_modified_at`? Verify before writing to it — if the column name in the DB differs from what EF Core expects, the update will silently fail or throw. Check `Data/Configurations/GcalEventConfiguration.cs`.

**Q2:** Story 3.4 may use a `SplitView` pattern or a side-panel overlay for the details panel. The edit-mode `VisualState` approach above assumes it extends the same control. If Story 3.4 uses a separate window or dialog, revise accordingly.

**Q3:** Should an edited (locally modified) event that came from Google Calendar show a visual indicator (e.g., pencil icon) to distinguish it from unmodified synced events? **Proposed:** No visual indicator in Story 3.5 — this belongs to Epic 7's publishing workflow. Log it but don't add UI.

---

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

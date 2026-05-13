---
title: 'Delete UX — Auto-Stage & Candidate Undo Toast'
type: 'feature'
created: '2026-05-13'
status: 'draft'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Deleting a GCal event (staging) and deleting a local candidate event both show confirmation dialogs, adding unnecessary friction to common operations.

**Approach:** Remove both confirmation dialogs. For GCal event staging, proceed automatically. For candidate (Pending) event deletion, delete immediately then show a 5-second bottom-center "Undo Delete?" toast that lets the user restore the event before it is committed.

## Boundaries & Constraints

**Always:** Undo re-inserts the exact original `PendingEvent` record unchanged. Toast appears only for candidate (`CalendarEventSourceKind.Pending`) event deletion, not for GCal staging. The 3-choice dialog for GCal events with a pending edit (`ShowDeleteWithPendingEditAsync`) is unchanged. The "Already Staged for Deletion" informational message is unchanged.

**Ask First:** Any approach that requires a new NuGet package for the toast UI.

**Never:** Do not add undo to GCal event staging. Do not change the `ShowDeleteWithPendingEditAsync` dialog. Do not use a `Popup` control — use an overlay `Border` inside the existing XAML grid.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Delete candidate, no undo | Pending event selected, Delete clicked, 5 s elapse | Event removed from views, toast appears then auto-dismisses | N/A |
| Delete candidate, undo clicked | Pending event selected, Delete clicked, Undo clicked within 5 s | Event restored in calendar views, toast dismissed | If re-insert fails silently, log and leave toast dismissed |
| Stage GCal event, no pending edit | Google event selected (no pending row), Delete clicked | Event immediately staged (shown at 60 % opacity), no dialog | N/A |
| Delete while toast visible | New candidate delete while undo toast still showing | Previous toast replaced; prior delete committed (no undo possible) | N/A |

</frozen-after-approval>

## Code Map

- `ViewModels/EventDetailsPanelViewModel.cs` — `DeleteEventAsync()` lines 1703-1720 (candidate path) and 1727-1734 (staging path)
- `ViewModels/MainViewModel.cs` — notification/toast state hub; add undo toast properties and `RequestUndoToastMessage` handler
- `Views/MainPage.xaml` — add bottom-center undo toast overlay in `Grid.Row="2"`, `Grid.Column="1"`
- `Views/MainPage.xaml.cs` — add `_undoToastTimer` (5 s) wired to `IsUndoToastVisible` property change
- `Messages/RequestUndoToastMessage.cs` — new record for cross-VM communication carrying the undo callback

## Tasks & Acceptance

**Execution:**
- [ ] `Messages/RequestUndoToastMessage.cs` -- CREATE new sealed record with `string Message` and `Func<CancellationToken, Task> OnUndo` -- carries the undo callback from `EventDetailsPanelViewModel` to `MainViewModel`
- [ ] `ViewModels/EventDetailsPanelViewModel.cs` -- EDIT `DeleteEventAsync()`: (1) in the `CalendarEventSourceKind.Pending` branch, remove `ShowConfirmationAsync` call and its guard; capture a snapshot of `_pendingEventId` and the `PendingEvent` entity before deleting; after delete+hide+clear+send, send `RequestUndoToastMessage` whose `OnUndo` re-inserts the snapshot via `_pendingEventRepository.UpsertAsync` then sends `EventUpdatedMessage`; (2) in the GCal no-pending-edit branch, remove `ShowConfirmationAsync("Stage Deletion", ...)` and its guard, proceeding directly to upsert -- needed to remove friction
- [ ] `ViewModels/MainViewModel.cs` -- EDIT: add `bool IsUndoToastVisible`, `string UndoToastMessage` observable properties; add private `Func<CancellationToken, Task>? _pendingUndoAction`; add `UndoCommand` async relay command that calls `_pendingUndoAction` then `DismissUndoToast()`; add `DismissUndoToast()` public method that clears `_pendingUndoAction` and sets `IsUndoToastVisible = false`; register `RequestUndoToastMessage` handler that stores the callback, sets message, and sets `IsUndoToastVisible = true` -- needed to drive toast visibility from the ViewModel
- [ ] `Views/MainPage.xaml` -- EDIT: inside the `<Grid Grid.Row="2">`, add a `Border` as the last child with `Grid.Column="1"` `HorizontalAlignment="Center"` `VerticalAlignment="Bottom"` `Margin="0,0,0,24"` `Padding="12,8"` `CornerRadius="{StaticResource AppCornerRadiusMedium}"` `Background="{ThemeResource SystemFillColorSolidNeutralBrush}"` `Visibility="{x:Bind ViewModel.IsUndoToastVisible, Mode=OneWay, Converter={StaticResource BoolToVisibilityConverter}}"` containing a horizontal `StackPanel` with a `TextBlock` bound to `UndoToastMessage` and a `Button` with `Command="{x:Bind ViewModel.UndoCommand}"` `Content="Undo"` -- needed for bottom-center floating toast UI
- [ ] `Views/MainPage.xaml.cs` -- EDIT: add `DispatcherQueueTimer? _undoToastTimer`; in `ViewModel_PropertyChanged` add a case for `IsUndoToastVisible` that calls `UpdateUndoToastTimer()`; add `UpdateUndoToastTimer()` that starts a 5-second timer when visible and stops it otherwise; timer tick calls `ViewModel.DismissUndoToast()` -- needed for auto-dismiss without timer logic in the ViewModel

**Acceptance Criteria:**
- Given a candidate (Pending) event is selected, when Delete is clicked, then no dialog appears and the event is immediately removed from all calendar views
- Given the undo toast is visible, when the Undo button is clicked within 5 seconds, then the deleted candidate event reappears in the calendar views and the toast dismisses
- Given the undo toast is visible, when 5 seconds elapse without interaction, then the toast disappears automatically
- Given a Google Calendar event with no pending edits is selected, when Delete is clicked, then no dialog appears and the event is immediately staged for deletion (shown at reduced opacity)
- Given a Google Calendar event with a pending edit exists, when Delete is clicked, then the three-choice dialog still appears unchanged

## Spec Change Log

## Design Notes

The undo callback (`Func<CancellationToken, Task>`) is captured as a closure in `EventDetailsPanelViewModel`, which holds a reference to `_pendingEventRepository`. This is safe because both ViewModels share the DI lifetime of the page. If a second delete arrives while the toast is still showing, `MainViewModel` simply replaces `_pendingUndoAction` (the first delete is committed silently).

Check whether a `BoolToVisibilityConverter` is already registered in App.xaml/resources before adding one; if it exists under a different key, use that key instead.

## Verification

**Commands:**
- `dotnet build` -- expected: 0 errors, 0 warnings
- `dotnet test GoogleCalendarManagement.Tests` -- expected: all tests pass

**Manual checks (if no CLI):**
- Click Delete on a local candidate event: no dialog, event vanishes from calendar, undo toast appears at bottom-center of calendar panel
- Click Undo within 5 s: event reappears in calendar views
- Click Delete on a local candidate event and wait 5 s: toast disappears, event stays gone
- Click Delete on a plain GCal event (no staged edits): no dialog, event turns to staged-delete appearance immediately
- Click Delete on a GCal event that has a pending edit: three-choice dialog still appears

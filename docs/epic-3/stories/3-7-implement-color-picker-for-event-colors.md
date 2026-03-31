# Story 3.7: Implement Color Picker for Event Colors

Status: ready-for-dev

## Story

As a **user**,
I want **to assign an event color from the app's fixed visual palette while editing**,
so that **I can categorize the event by mental state without remembering color IDs or typing color names**.

## Acceptance Criteria

1. **AC-3.7.1 - Picker opens from the edit panel:** Given the event details panel is in edit mode (Story 3.5), when the user activates the color field, a light-dismiss picker opens anchored to that field within 100 ms.
2. **AC-3.7.2 - Exactly nine taxonomy colors are shown:** Given the picker is open, it shows exactly these 9 options in a compact grid with swatch + label for each: Azure, Purple, Grey, Yellow, Navy, Sage, Flamingo, Orange, Lavender.
3. **AC-3.7.3 - Current selection is obvious and keyboard reachable:** Given the picker is open, the currently selected color is visibly highlighted, each swatch is reachable by keyboard navigation, and each option exposes an accessible name such as `"Purple (#3F51B5)"`.
4. **AC-3.7.4 - Selection applies immediately:** Given the user clicks or presses Enter/Space on a color option, the picker closes automatically, the panel updates to the newly selected swatch/label immediately, and the change is persisted without waiting for the 500 ms text-edit debounce.
5. **AC-3.7.5 - Persistence uses the shared local color contract:** Given a color is selected, the chosen value is saved to `gcal_event.color_id` using the app's canonical semantic key (`azure`, `purple`, `grey`, `yellow`, `navy`, `sage`, `flamingo`, `orange`, `lavender`), `updated_at` and `app_last_modified_at` are updated, `app_published` remains `false`, and `gcal_event_id` is never changed.
6. **AC-3.7.6 - Event surfaces refresh without restart:** Given a color change has been saved, the selected event re-renders with the new colour in the details panel and in any currently visible Month, Week, or Day view without restarting the app.
7. **AC-3.7.7 - Version history is preserved for manual color edits:** Given an existing event's color is changed, the pre-change row state is written to `gcal_event_version` before the `gcal_event` row is updated, using `ChangedBy = "manual_edit"` and `ChangeReason = "color_changed"`.
8. **AC-3.7.8 - Fallback and dismiss behavior are safe:** Given an event has a null, empty, unknown, or legacy numeric `color_id`, the picker opens with Azure selected as the safe fallback and no exception is thrown. Given the user dismisses the picker without selecting a new color, no database write occurs.

## Scope Boundaries

**IN SCOPE**
- Replace Story 3.5's disabled color stub with a real fixed-palette picker in `EventDetailsPanelControl`
- Extend the shared color-mapping contract so the picker can render ordered options, display names, and canonical keys without duplicating dictionaries in the view
- Carry the selected color through the existing edit-view-model path and persist it to `gcal_event`
- Refresh the active calendar UI after save using the existing edit refresh path from Story 3.5
- Record manual color changes in `gcal_event_version` before overwrite

**OUT OF SCOPE**
- Arbitrary RGB/HSL spectrum picking with WinUI's general-purpose `ColorPicker`
- Editing the color taxonomy itself - Epic 10 owns color-management UX
- Push-to-Google behavior, Google API color translation, or publish workflow changes
- Year-view event indicator enhancements from Story 3.9
- `pending_event` schema or any new database table

## Tasks / Subtasks

- [ ] **Task 1: Finalize the shared color metadata contract** (AC: 3.7.2, 3.7.3, 3.7.5, 3.7.8)
  - [ ] Extend [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs) so the picker can consume ordered color metadata, display names, and canonical-key normalization from one place
  - [ ] Keep the mapping case-insensitive and continue accepting Google numeric IDs from synced events as aliases
  - [ ] Preserve Azure `#0088CC` as the fallback for unknown input

- [ ] **Task 2: Carry canonical color metadata through the display/query path** (AC: 3.7.2, 3.7.3, 3.7.8)
  - [ ] Update [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs) to include the event's current color key and display name if Stories 3.4/3.5 have not already done so
  - [ ] Update [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs) so event-display projection remains the single place that resolves colour metadata for UI consumption

- [ ] **Task 3: Extend the edit view model with picker state** (AC: 3.7.1, 3.7.4, 3.7.5, 3.7.8)
  - [ ] Extend [ViewModels/EventDetailsPanelViewModel.cs](../../../ViewModels/EventDetailsPanelViewModel.cs) from Story 3.5 rather than creating a second edit surface
  - [ ] Add bindable color-edit state such as `EditColorId`, `EditColorName`, `EditColorHex`, and `AvailableColors`
  - [ ] Add a command such as `SelectColorCommand` that normalizes the selected key, updates the editable color properties immediately, and bypasses the text-field debounce by calling the existing save path immediately

- [ ] **Task 4: Replace the Story 3.5 color stub with a real anchored picker** (AC: 3.7.1, 3.7.2, 3.7.3, 3.7.4, 3.7.8)
  - [ ] Update [Views/EventDetailsPanelControl.xaml](../../../Views/EventDetailsPanelControl.xaml) to replace the disabled placeholder button with an interactive field that opens a `Flyout`
  - [ ] Use a compact 3x3 swatch grid built from the shared ordered color options
  - [ ] Ensure the flyout is light-dismiss, Escape-dismissable, and anchored to the color field

- [ ] **Task 5: Persist manual color edits through the existing edit pipeline** (AC: 3.7.4, 3.7.5, 3.7.8)
  - [ ] Reuse the repository/update path introduced in Story 3.5; do not create a second direct-edit persistence path just for colors
  - [ ] Save the canonical semantic key to `GcalEvent.ColorId`
  - [ ] Update `AppLastModifiedAt` and `UpdatedAt` with UTC timestamps
  - [ ] Leave `AppPublished = false`

- [ ] **Task 6: Snapshot version history before overwrite** (AC: 3.7.7)
  - [ ] Reuse the existing `gcal_event_version` schema from Stories 2.3 and 2.3A
  - [ ] Add the version snapshot in the persistence layer, not in the view model or code-behind
  - [ ] Use `ChangedBy = "manual_edit"` and `ChangeReason = "color_changed"`

- [ ] **Task 7: Refresh the active calendar shell after save** (AC: 3.7.6)
  - [ ] If Story 3.5 already introduced `EventUpdatedMessage`, reuse it
  - [ ] If it has not landed yet, add the smallest shared message-based refresh path rather than reloading the entire window
  - [ ] Ensure the details panel and currently visible Month/Week/Day surfaces reflect the new color immediately after save

- [ ] **Task 8: Automated coverage** (AC: 3.7.2-3.7.8)
  - [ ] Extend [GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs](../../../GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs) for picker options, normalization, display-name lookup, and Azure fallback
  - [ ] Extend or create [GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelEditTests.cs](../../../GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelEditTests.cs) for current color state, immediate save, dismiss-without-save, and no-op same-color selection
  - [ ] Add or extend repository/integration tests for manual color-change persistence and history snapshot creation

- [ ] **Task 9: Validation**
  - [ ] Run `dotnet build -p:Platform=x64`
  - [ ] Run `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual verification:
    - [ ] open edit mode -> activate color field -> picker opens with 9 options
    - [ ] select a different color -> picker closes and panel swatch updates immediately
    - [ ] active calendar view repaints with the new color without restart
    - [ ] dismiss picker with Escape/outside click -> no save when no new selection was made

## Dev Notes

### Current Repository Truth

- The project is a **single flat WinUI 3 app at repo root**. New code belongs under `Views/`, `ViewModels/`, `Services/`, `Models/`, `Messages/`, and `Data/` at the project root. Do **not** create `Core/`, `src/`, or a second UI library.
- The current branch still shows the pre-3.2 color state:
  - [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs) only exposes `GetHexColor(...)`
  - [Services/ColorMappingService.cs](../../../Services/ColorMappingService.cs) still returns Azure for everything
  - [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs) currently lacks a canonical `ColorId` / `ColorName`
- The current branch also does **not** yet contain the Story 3.5 panel artifacts:
  - [ViewModels/EventDetailsPanelViewModel.cs](../../../ViewModels/EventDetailsPanelViewModel.cs)
  - [Views/EventDetailsPanelControl.xaml](../../../Views/EventDetailsPanelControl.xaml)
  - [Messages/EventUpdatedMessage.cs](../../../Messages/EventUpdatedMessage.cs)

This story therefore depends on **Story 3.5 being present on the implementation branch**. If 3.5 is not merged yet, implement/merge 3.5 first and then apply this story as an additive extension to that edit pipeline.

### Dependencies to Reuse Instead of Reinventing

- **Story 3.2** owns the shared 9-colour mapping and contrast behavior. If 3.2 landed with a slightly different but equivalent shared contract, adapt it minimally; do **not** duplicate the taxonomy in the panel.
- **Story 3.5** owns edit mode, validation, debounce, and panel save orchestration. This story extends that work; it must not create a second event-editing surface.
- **Stories 2.3 / 2.3A** already established the `gcal_event_version` snapshot contract and proved the schema in [Services/SyncManager.cs](../../../Services/SyncManager.cs). Reuse that table shape for manual color edits instead of inventing a JSON history payload or a second history table.

### Canonical Local Storage Contract

The planning docs are stale about `color_id` storage. For app-authored edits in this repo, use a **canonical semantic key**, not a raw hex string:

| Display Name | Canonical key | Hex |
|---|---|---|
| Azure | `azure` | `#0088CC` |
| Purple | `purple` | `#3F51B5` |
| Grey | `grey` | `#616161` |
| Yellow | `yellow` | `#F6BF26` |
| Navy | `navy` | `#33B679` |
| Sage | `sage` | `#0B8043` |
| Flamingo | `flamingo` | `#E67C73` |
| Orange | `orange` | `#F4511E` |
| Lavender | `lavender` | `#8E24AA` |

Rules:

- Persist the canonical key to `gcal_event.color_id` for app edits.
- Continue accepting Google numeric IDs from synced rows as input aliases in the shared color-mapping service.
- Do **not** store raw hex in `color_id`.
- Do **not** create a second event-color field.

### Preferred Shared Color Service Shape

If Story 3.2 has not already introduced an equivalent API, prefer a shared contract like:

```csharp
public sealed record CalendarColorOption(
    string Key,
    string DisplayName,
    string Hex,
    string ContrastTextHex);

public interface IColorMappingService
{
    IReadOnlyList<CalendarColorOption> PickerColors { get; }
    string GetHexColor(string? colorId);
    string GetDisplayName(string? colorId);
    string NormalizeColorKey(string? colorId);
}
```

Guardrails:

- `PickerColors` must be in the exact UX order used by the picker, not dictionary hash order
- `NormalizeColorKey(...)` must turn legacy numeric IDs and alias input into a canonical key
- The picker UI must bind to `PickerColors`; no private swatch list in XAML code-behind

### Preferred UI Pattern

Use an anchored `Flyout` with a custom 3x3 swatch grid, not WinUI's general-purpose spectrum `ColorPicker`.

Reasoning:

- The app has a fixed, named taxonomy of exactly 9 colours
- The UX requires immediate apply + auto-close on selection
- A simple swatch grid is faster and clearer than exposing an unrestricted RGB/HSV surface

`Flyout` gives the correct light-dismiss and Escape-dismiss behavior with minimal custom plumbing. Microsoft Learn's current WinUI control guidance supports this general anchored control pattern, while the `ColorPicker` control remains the broader freeform-color option. That is an inference from the official control docs and this story's fixed-palette requirements.

### Save and History Semantics

- Color swatch selection is a discrete action. Do **not** wait for Story 3.5's 500 ms text-edit debounce.
- Update editable color state, close the picker, and call the shared immediate save path (`SaveNowAsync()` or equivalent).
- Snapshot from the persistence layer only. The view model must not construct `GcalEventVersion` rows directly.
- If Story 3.5 already added `IGcalEventRepository.UpdateAsync(...)`, extend that path so the same save operation:
  - loads the current `GcalEvent`
  - writes a `gcal_event_version` snapshot when the color actually changed
  - updates `GcalEvent.ColorId`
  - updates `AppLastModifiedAt` and `UpdatedAt`
  - saves atomically

### Refresh Path Guardrails

The existing calendar pages ([Views/MonthViewControl.xaml.cs](../../../Views/MonthViewControl.xaml.cs), [Views/WeekViewControl.xaml.cs](../../../Views/WeekViewControl.xaml.cs), [Views/DayViewControl.xaml.cs](../../../Views/DayViewControl.xaml.cs)) rebuild when `MainViewModel.CurrentEvents` changes. Reuse that pattern.

Implication:

- After a successful color save, publish the existing event-refresh message from Story 3.5 if present
- `MainViewModel` or the shared shell owner should refresh or replace the changed display model entry
- Do **not** force a whole-window navigation or app restart just to repaint a color

### Anti-Patterns to Avoid

- Do **not** duplicate the 9-colour list in XAML, code-behind, and service code
- Do **not** store raw hex strings in `gcal_event.color_id`
- Do **not** bypass the existing edit pipeline with a panel-only direct DB update
- Do **not** write version-history rows from the UI layer
- Do **not** add `pending_event` or a new table for this story
- Do **not** use integer event IDs anywhere in this story; the repo uses `string GcalEventId`

### References

- [docs/epics.md](../../epics.md)
- [docs/epic-3/tech-spec.md](../tech-spec.md)
- [docs/ux-design-specification.md](../../ux-design-specification.md)
- [docs/_color-definitions.md](../../_color-definitions.md)
- [docs/epic-3/stories/3-2-display-events-with-color-coded-visual-system.md](./3-2-display-events-with-color-coded-visual-system.md)
- [docs/epic-3/stories/3-5-implement-event-editing-panel-phase-2.md](./3-5-implement-event-editing-panel-phase-2.md)
- [docs/epic-2/stories/2-3-implement-version-history-on-calendar-sync.md](../../epic-2/stories/2-3-implement-version-history-on-calendar-sync.md)
- [docs/epic-2/stories/2-3a-harden-version-history-schema-and-sync-semantics.md](../../epic-2/stories/2-3a-harden-version-history-schema-and-sync-semantics.md)
- [Services/IColorMappingService.cs](../../../Services/IColorMappingService.cs)
- [Services/ColorMappingService.cs](../../../Services/ColorMappingService.cs)
- [Services/CalendarQueryService.cs](../../../Services/CalendarQueryService.cs)
- [Services/GcalEventRepository.cs](../../../Services/GcalEventRepository.cs)
- [Services/SyncManager.cs](../../../Services/SyncManager.cs)
- [Models/CalendarEventDisplayModel.cs](../../../Models/CalendarEventDisplayModel.cs)
- [Data/Entities/GcalEvent.cs](../../../Data/Entities/GcalEvent.cs)
- [Data/Entities/GcalEventVersion.cs](../../../Data/Entities/GcalEventVersion.cs)
- [Data/Configurations/GcalEventConfiguration.cs](../../../Data/Configurations/GcalEventConfiguration.cs)
- [Data/Configurations/GcalEventVersionConfiguration.cs](../../../Data/Configurations/GcalEventVersionConfiguration.cs)
- [Views/MonthViewControl.xaml.cs](../../../Views/MonthViewControl.xaml.cs)
- [Views/WeekViewControl.xaml.cs](../../../Views/WeekViewControl.xaml.cs)
- [Views/DayViewControl.xaml.cs](../../../Views/DayViewControl.xaml.cs)
- [App.xaml.cs](../../../App.xaml.cs)
- Microsoft Learn - Controls for Windows apps: https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/
- Windows App SDK API reference - `Microsoft.UI.Xaml.Controls.ColorPicker`: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.colorpicker.color?view=windows-app-sdk-1.8

## Dev Agent Record

### Context Reference

- [Story Context XML](3-7-implement-color-picker-for-event-colors.context.xml) - Generated 2026-03-31

### Agent Model Used

GPT-5 (Codex)

### Debug Log References

- 2026-03-31: Story context generated from the Epic 3 planning artifacts, the current repo state, prior Epic 2/3 stories, and current official Microsoft WinUI control documentation.

### Completion Notes List

- Normalized the story around the real repo state: flat project layout, Azure-only current color service, and missing 3.5 panel artifacts on the present branch.
- Locked down a canonical local `color_id` storage rule so implementation does not drift into raw hex persistence.
- Routed color selection through the existing 3.5 edit/save path and the existing 2.3 version-history contract instead of inventing a second persistence flow.

### File List

- `docs/epic-3/stories/3-7-implement-color-picker-for-event-colors.md`
- `docs/epic-3/stories/3-7-implement-color-picker-for-event-colors.context.xml`

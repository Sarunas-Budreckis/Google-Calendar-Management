# Story 4.3: Implement Color Picker for Event Colors

Status: ready-for-dev

> **Moved from Epic 3:** This story was originally Story 3.7. It has been moved to Epic 4 (Event Editing — Tier 2) as the color picker is only relevant in edit mode. All content below is unchanged from the original 3.7 story file.

## Story

As a **user**,
I want **to assign an event color from the app's fixed visual palette while editing**,
so that **I can categorize the event by mental state without remembering color IDs or typing color names**.

## Acceptance Criteria

1. **AC-4.3.1 - Picker opens from the edit panel:** Given the event details panel is in edit mode (Story 4.1), when the user activates the color field, a light-dismiss picker opens anchored to that field within 100 ms.
2. **AC-4.3.2 - Exactly nine taxonomy colors are shown:** Given the picker is open, it shows exactly these 9 options in a compact grid with swatch + label for each: Azure, Purple, Grey, Yellow, Navy, Sage, Flamingo, Orange, Lavender.
3. **AC-4.3.3 - Current selection is obvious and keyboard reachable:** Given the picker is open, the currently selected color is visibly highlighted, each swatch is reachable by keyboard navigation, and each option exposes an accessible name such as `"Purple (#3F51B5)"`.
4. **AC-4.3.4 - Selection applies immediately:** Given the user clicks or presses Enter/Space on a color option, the picker closes automatically, the panel updates to the newly selected swatch/label immediately, and the change is persisted without waiting for the 500 ms text-edit debounce.
5. **AC-4.3.5 - Persistence uses the shared local color contract:** Given a color is selected, the chosen value is saved to `gcal_event.color_id` using the app's canonical semantic key (`azure`, `purple`, `grey`, `yellow`, `navy`, `sage`, `flamingo`, `orange`, `lavender`), `updated_at` and `app_last_modified_at` are updated, `app_published` remains `false`, and `gcal_event_id` is never changed.
6. **AC-4.3.6 - Event surfaces refresh without restart:** Given a color change has been saved, the selected event re-renders with the new colour in the details panel and in any currently visible Month, Week, or Day view without restarting the app.
7. **AC-4.3.7 - Version history is preserved for manual color edits:** Given an existing event's color is changed, the pre-change row state is written to `gcal_event_version` before the `gcal_event` row is updated, using `ChangedBy = "manual_edit"` and `ChangeReason = "color_changed"`.
8. **AC-4.3.8 - Fallback and dismiss behavior are safe:** Given an event has a null, empty, unknown, or legacy numeric `color_id`, the picker opens with Azure selected as the safe fallback and no exception is thrown. Given the user dismisses the picker without selecting a new color, no database write occurs.

## Scope Boundaries

**IN SCOPE**
- Replace Story 4.1's disabled color stub with a real fixed-palette picker in `EventDetailsPanelControl`
- Extend the shared color-mapping contract so the picker can render ordered options, display names, and canonical keys
- Carry the selected color through the existing edit-view-model path and persist it to `gcal_event`
- Refresh the active calendar UI after save using the existing edit refresh path from Story 4.1
- Record manual color changes in `gcal_event_version` before overwrite

**OUT OF SCOPE**
- Arbitrary RGB/HSL spectrum picking
- Editing the color taxonomy itself
- Push-to-Google behavior
- `pending_event` schema or any new database table

## Dev Notes

### Canonical Color Contract

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

Rules: persist canonical key to `gcal_event.color_id`, accept Google numeric IDs as input aliases, do NOT store raw hex.

### Preferred Color Service Shape

```csharp
public sealed record CalendarColorOption(string Key, string DisplayName, string Hex, string ContrastTextHex);

public interface IColorMappingService
{
    IReadOnlyList<CalendarColorOption> PickerColors { get; }
    string GetHexColor(string? colorId);
    string GetDisplayName(string? colorId);
    string NormalizeColorKey(string? colorId);
}
```

### Preferred UI Pattern

Use an anchored `Flyout` with a custom 3×3 swatch grid. Do NOT use WinUI's general-purpose spectrum `ColorPicker`. Color swatch selection is a discrete action — do NOT use the 500 ms text-edit debounce; call immediate save.

### Save and History Semantics

- Call `IGcalEventRepository.UpdateAsync(...)` after picking
- Write a `gcal_event_version` snapshot from the persistence layer (not the view model) before overwriting
- `ChangedBy = "manual_edit"`, `ChangeReason = "color_changed"`

---

## Tasks / Subtasks

- [ ] **Task 1: Finalize the shared color metadata contract** (AC: 4.3.2, 4.3.3, 4.3.5, 4.3.8)
  - [ ] Extend `Services/IColorMappingService.cs` with `PickerColors`, `GetDisplayName`, `NormalizeColorKey`

- [ ] **Task 2: Carry canonical color through display/query path** (AC: 4.3.2, 4.3.3, 4.3.8)
  - [ ] Ensure `CalendarEventDisplayModel` includes current color key and display name

- [ ] **Task 3: Extend edit view model with picker state** (AC: 4.3.1, 4.3.4, 4.3.5, 4.3.8)
  - [ ] Add `EditColorId`, `EditColorName`, `EditColorHex`, `AvailableColors`, `SelectColorCommand`

- [ ] **Task 4: Replace Story 4.1 color stub with real picker** (AC: 4.3.1, 4.3.2, 4.3.3, 4.3.4, 4.3.8)
  - [ ] Replace disabled placeholder with interactive `Flyout` containing 3×3 swatch grid

- [ ] **Task 5: Persist color edits through existing edit pipeline** (AC: 4.3.4, 4.3.5, 4.3.8)
  - [ ] Reuse `UpdateAsync` from Story 4.1; save canonical key; update timestamps

- [ ] **Task 6: Snapshot version history before overwrite** (AC: 4.3.7)
  - [ ] Write `gcal_event_version` row from persistence layer before updating `color_id`

- [ ] **Task 7: Refresh active calendar shell after save** (AC: 4.3.6)
  - [ ] Reuse `EventUpdatedMessage` from Story 4.1

- [ ] **Task 8: Automated coverage** (AC: 4.3.2–4.3.8)
  - [ ] Color service tests: picker options, normalization, display-name lookup, Azure fallback
  - [ ] ViewModel tests: immediate save, dismiss-without-save, no-op same-color selection
  - [ ] Repository/integration tests: color-change persistence and history snapshot

- [ ] **Task 9: Validation**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: open edit mode → activate color field → picker shows 9 options → select → closes and updates immediately → calendar repaints

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

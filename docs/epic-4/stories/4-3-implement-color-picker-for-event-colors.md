# Story 4.3: Implement Color Picker for Event Colors

Status: ready-for-dev

> **Moved from Epic 3:** This story was originally Story 3.7. It has been moved to Epic 4 (Event Editing — Tier 2) as the color picker is only relevant in edit mode. All content below is unchanged from the original 3.7 story file.

## Story

As a **user**,
I want **to assign an event color from the app's fixed visual palette while editing**,
so that **I can categorize the event by mental state without remembering color IDs or typing color names**.

## Acceptance Criteria

1. **AC-4.3.1 - Picker opens from the edit panel:** Given the event details panel is in edit mode (Story 4.1), when the user activates the color field, a light-dismiss picker opens anchored to that field within 100 ms.
2. **AC-4.3.2 - Exactly nine taxonomy colors are shown:** Given the picker is open, it shows exactly these 9 options in a compact 2×6 grid (2 rows × 6 columns) with swatch + label for each: Azure, Purple, Grey, Yellow, Navy, Sage, Flamingo, Orange, Lavender.
3. **AC-4.3.3 - Current selection is obvious and keyboard reachable:** Given the picker is open, the currently selected color is visibly highlighted, each swatch is reachable by keyboard navigation, and each option exposes an accessible name such as `"Purple (#3F51B5)"`.
4. **AC-4.3.4 - Selection applies immediately:** Given the user clicks or presses Enter/Space on a color option, the picker closes automatically, the panel updates to the newly selected swatch/label immediately, and the change is persisted without waiting for the 500 ms text-edit debounce.
5. **AC-4.3.5 - Persistence uses the pending_event path:** Given a color is selected, the chosen value is saved to `pending_event.color_id` using the app's canonical semantic key (`azure`, `purple`, `grey`, `yellow`, `navy`, `sage`, `flamingo`, `orange`, `lavender`) and `pending_event.updated_at` is refreshed. If no `pending_event` row exists for the event yet, one is created (upsert) — this is the same path used by text/time edits. The original `gcal_event` row is NOT modified. The event drops to 60% opacity immediately, consistent with any other pending change.
6. **AC-4.3.6 - Event surfaces refresh without restart:** Given a color change has been saved, the selected event re-renders with the new colour in the details panel and in any currently visible Month, Week, or Day view without restarting the app.
7. **AC-4.3.7 - No version history write on color change:** Given an event's color is changed via the picker, no `gcal_event_version` snapshot is written — because the change goes to `pending_event`, not `gcal_event`. Version history is written only when `gcal_event` is overwritten (i.e., on Push to GCal in Story 4.4). The prior color value is implicitly preserved in the original `gcal_event.color_id` until the push overwrites it.
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
- Push-to-Google behavior (Story 4.4)
- Any new database tables — color writes reuse the existing `pending_event` table and `IPendingEventRepository.UpsertAsync` path from Story 4.1

## Dev Notes

### Canonical Color Contract

| Display Name | Canonical key | Hex | Contrast Text |
|---|---|---|---|
| Azure | `azure` | `#0088CC` | `#FFFFFF` |
| Purple | `purple` | `#3F51B5` | `#FFFFFF` |
| Grey | `grey` | `#616161` | `#FFFFFF` |
| Yellow | `yellow` | `#F6BF26` | `#FFFFFF` |
| Navy | `navy` | `#33B679` | `#FFFFFF` |
| Sage | `sage` | `#0B8043` | `#FFFFFF` |
| Flamingo | `flamingo` | `#E67C73` | `#FFFFFF` |
| Orange | `orange` | `#F4511E` | `#FFFFFF` |
| Lavender | `lavender` | `#8E24AA` | `#FFFFFF` |

Rules: persist canonical key to `gcal_event.color_id`, accept Google numeric IDs as input aliases, do NOT store raw hex.

### Preferred Color Service Shape

```csharp
public sealed record CalendarColorOption(string Key, string DisplayName, string Hex, string ContrastTextHex);
// ContrastTextHex is always "#FFFFFF" for all 9 colors.

public interface IColorMappingService
{
    IReadOnlyList<CalendarColorOption> PickerColors { get; }
    string GetHexColor(string? colorId);
    string GetDisplayName(string? colorId);
    string NormalizeColorKey(string? colorId);
}
```

### Preferred UI Pattern

Use an anchored `Flyout` with a custom 2×6 swatch grid (2 rows × 6 columns; 9 swatches + 3 empty trailing slots). Do NOT use WinUI's general-purpose spectrum `ColorPicker`. Color swatch selection is a discrete action — do NOT use the 500 ms text-edit debounce; call immediate save.

### Save Semantics

- Call `IPendingEventRepository.UpsertAsync(...)` after picking (same path as text/time edits in Story 4.1)
- If no pending row exists for the event, create one (copying all current field values from `gcal_event` first)
- Set `pending_event.color_id` = selected canonical key; update `pending_event.updated_at`
- Do NOT call `IGcalEventRepository.UpdateAsync` — `gcal_event` is not touched
- No `gcal_event_version` snapshot is written at this point; version history occurs during Push to GCal (Story 4.4)

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

- [ ] **Task 5: Persist color edits via pending_event upsert** (AC: 4.3.4, 4.3.5, 4.3.8)
  - [ ] Reuse `IPendingEventRepository.UpsertAsync` from Story 4.1; if no pending row exists, create one copying all `gcal_event` fields first; set `color_id` = selected canonical key; update `updated_at`
  - [ ] Do NOT call `IGcalEventRepository.UpdateAsync` — `gcal_event` is not modified in this story

- [ ] **Task 6: ~~Snapshot version history~~ (removed)** (AC: 4.3.7 — no snapshot needed)
  - [ ] Confirm no `gcal_event_version` write in this story; version history occurs on Push to GCal (Story 4.4)

- [ ] **Task 7: Refresh active calendar shell after save** (AC: 4.3.6)
  - [ ] Reuse `EventUpdatedMessage` from Story 4.1

- [ ] **Task 8: Automated coverage** (AC: 4.3.2–4.3.8)
  - [ ] Color service tests: picker options, normalization, display-name lookup, Azure fallback
  - [ ] ViewModel tests: immediate save, dismiss-without-save, no-op same-color selection
  - [ ] Repository/integration tests: color-change persistence and history snapshot

- [ ] **Task 9: Validation**
  - [ ] `dotnet build -p:Platform=x64`
  - [ ] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: open edit mode → activate color field → picker shows 9 options in 2×6 grid → select → closes and updates immediately → calendar repaints

## Dev Agent Record

### Agent Model Used

<!-- to be filled by dev agent -->

### Debug Log References

<!-- to be filled by dev agent -->

### Completion Notes List

<!-- to be filled by dev agent -->

### File List

<!-- to be filled by dev agent -->

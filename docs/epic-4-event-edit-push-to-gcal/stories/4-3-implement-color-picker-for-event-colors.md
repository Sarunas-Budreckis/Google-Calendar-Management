# Story 4.3: Implement Color Picker for Event Colors

Status: in-progress

> **Moved from Epic 3:** This story was originally Story 3.7. It has been moved to Epic 4 (Event Editing — Tier 2) as the color picker is only relevant in edit mode. All content below is unchanged from the original 3.7 story file.

## Story

As a **user**,
I want **to assign an event color from the app's fixed visual palette while editing**,
so that **I can categorize the event by mental state without remembering color IDs or typing color names**.

## Acceptance Criteria

1. **AC-4.3.1 - Picker opens from the edit panel:** Given the event details panel is in edit mode (Story 4.1), when the user activates the color field, a light-dismiss picker opens anchored to that field within 100 ms.
2. **AC-4.3.2 - Google Calendar event colors plus Azure are shown:** Given the picker is open, it shows exactly these 12 options in a compact 2×6 grid (2 rows × 6 columns) with swatch + label for each, in this order: Red, Flamingo, Orange, Banana, Sage, Basil, Peacock, Navy, Lavender, Grape, Graphite, Azure.
3. **AC-4.3.3 - Current selection is obvious and keyboard reachable:** Given the picker is open, the currently selected color is visibly highlighted, each swatch is reachable by keyboard navigation, and each option exposes an accessible name such as `"Navy (#3F51B5)"`.
4. **AC-4.3.4 - Selection applies immediately:** Given the user clicks or presses Enter/Space on a color option, the picker closes automatically, the panel updates to the newly selected swatch/label immediately, and the change is persisted without waiting for the 500 ms text-edit debounce.
5. **AC-4.3.5 - Persistence uses the pending_event path:** Given a color is selected, the chosen value is saved to `pending_event.color_id` using the app's canonical color key (`red`, `flamingo`, `orange`, `banana`, `sage`, `basil`, `peacock`, `navy`, `lavender`, `grape`, `graphite`, `azure`) and `pending_event.updated_at` is refreshed. If no `pending_event` row exists for the event yet, one is created (upsert) — this is the same path used by text/time edits. The original `gcal_event` row is NOT modified. The event drops to 60% opacity immediately, consistent with any other pending change.
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

| Display Name | Canonical key | Google ID | Hex | Contrast Text |
|---|---|---|---|---|
| Red | `red` | `11` | `#D50000` | `#FFFFFF` |
| Flamingo | `flamingo` | `4` | `#E67C73` | `#FFFFFF` |
| Orange | `orange` | `6` | `#F4511E` | `#FFFFFF` |
| Banana | `banana` | `5` | `#F6BF26` | `#FFFFFF` |
| Sage | `sage` | `2` | `#33B679` | `#FFFFFF` |
| Basil | `basil` | `10` | `#0B8043` | `#FFFFFF` |
| Peacock | `peacock` | `7` | `#039BE5` | `#FFFFFF` |
| Navy | `navy` | `9` | `#3F51B5` | `#FFFFFF` |
| Lavender | `lavender` | `1` | `#7986CB` | `#FFFFFF` |
| Grape | `grape` | `3` | `#8E24AA` | `#FFFFFF` |
| Graphite | `graphite` | `8` | `#616161` | `#FFFFFF` |
| Azure | `azure` | default calendar color | `#3CABFF` | `#FFFFFF` |

Rules: persist canonical key to `gcal_event.color_id`, accept Google numeric IDs as input aliases, do NOT store raw hex.

### Preferred Color Service Shape

```csharp
public sealed record CalendarColorOption(string Key, string DisplayName, string Hex, string ContrastTextHex);
// ContrastTextHex is always "#FFFFFF" for all 12 colors.

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

- [x] **Task 1: Finalize the shared color metadata contract** (AC: 4.3.2, 4.3.3, 4.3.5, 4.3.8)
  - [x] Extend `Services/IColorMappingService.cs` with `PickerColors`, `GetDisplayName`, `NormalizeColorKey`

- [x] **Task 2: Carry canonical color through display/query path** (AC: 4.3.2, 4.3.3, 4.3.8)
  - [x] Ensure `CalendarEventDisplayModel` includes current color key and display name

- [x] **Task 3: Extend edit view model with picker state** (AC: 4.3.1, 4.3.4, 4.3.5, 4.3.8)
  - [x] Add `EditColorId`, `EditColorName`, `EditColorHex`, `AvailableColors`, `SelectColorCommand`

- [x] **Task 4: Replace Story 4.1 color stub with real picker** (AC: 4.3.1, 4.3.2, 4.3.3, 4.3.4, 4.3.8)
  - [x] Replace disabled placeholder with interactive `Flyout` containing 3×3 swatch grid

- [x] **Task 5: Persist color edits via pending_event upsert** (AC: 4.3.4, 4.3.5, 4.3.8)
  - [x] Reuse `IPendingEventRepository.UpsertAsync` from Story 4.1; if no pending row exists, create one copying all `gcal_event` fields first; set `color_id` = selected canonical key; update `updated_at`
  - [x] Do NOT call `IGcalEventRepository.UpdateAsync` — `gcal_event` is not modified in this story

- [x] **Task 6: ~~Snapshot version history~~ (removed)** (AC: 4.3.7 — no snapshot needed)
  - [x] Confirm no `gcal_event_version` write in this story; version history occurs on Push to GCal (Story 4.4)

- [x] **Task 7: Refresh active calendar shell after save** (AC: 4.3.6)
  - [x] Reuse `EventUpdatedMessage` from Story 4.1

- [x] **Task 8: Automated coverage** (AC: 4.3.2–4.3.8)
  - [x] Color service tests: picker options, normalization, display-name lookup, Azure fallback
  - [x] ViewModel tests: immediate save, dismiss-without-save, no-op same-color selection
  - [x] Repository/integration tests: color-change persistence and history snapshot

- [ ] **Task 9: Validation**
  - [x] `dotnet build -p:Platform=x64`
  - [x] `dotnet test GoogleCalendarManagement.Tests/`
  - [ ] Manual: open edit mode → activate color field → picker shows 9 options in 2×6 grid → select → closes and updates immediately → calendar repaints

## Dev Agent Record

### Agent Model Used

GPT-5 Codex

### Debug Log References

- `dotnet build -p:Platform=x64`
- `dotnet test GoogleCalendarManagement.Tests/ -p:Platform=x64`

### Completion Notes List

- Added a canonical color picker contract with ordered picker metadata, canonical key normalization, and Azure fallback for null, unknown, and legacy numeric IDs.
- Extended `CalendarEventDisplayModel` and `CalendarQueryService` so loaded events carry a canonical `ColorKey` alongside display name and hex, allowing the picker to open with the correct selection and refresh views consistently.
- Extended `EventDetailsPanelViewModel` with picker state and an immediate-save `SelectColorCommand` that previews the new color at 60% opacity, persists through `pending_event`, and refreshes the selected event through `EventUpdatedMessage`.
- Replaced the Story 4.1 "Coming soon" color stub with an anchored `Flyout` picker that renders 12 swatches in the AC-required 2×6 layout, exposes accessible names, and visibly highlights the current selection.
- Added automated coverage for picker ordering, canonical normalization, immediate color save, same-color no-op behavior, and persistence without touching live `gcal_event` rows or `gcal_event_version`.
- Manual UI validation is still pending in this headless session, so the story remains `in-progress` until the final picker walkthrough is completed.

### File List

- `GoogleCalendarManagement.Tests/Integration/CalendarQueryServiceTests.cs`
- `GoogleCalendarManagement.Tests/Integration/PendingEventRepositoryTests.cs`
- `GoogleCalendarManagement.Tests/Unit/Services/ColorMappingServiceTests.cs`
- `GoogleCalendarManagement.Tests/Unit/ViewModels/EventDetailsPanelViewModelTests.cs`
- `Models/CalendarEventDisplayModel.cs`
- `Services/CalendarQueryService.cs`
- `Services/ColorMappingService.cs`
- `Services/IColorMappingService.cs`
- `ViewModels/EventDetailsPanelViewModel.cs`
- `Views/EventDetailsPanelControl.xaml.cs`

## Change Log

- 2026-04-26: Implemented Story 4.3 color picker contract, immediate pending-event color persistence, anchored flyout picker UI, and automated coverage; manual UI validation still pending.

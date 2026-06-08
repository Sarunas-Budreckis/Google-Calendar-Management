# Deferred Work

## Deferred from: code review of 3-1-build-year-month-week-day-calendar-views (2026-03-31)

- **F11: `GetWeekRange` duplicated across MainViewModel + four view controls** — DRY violation; extract shared static helper to prevent silent divergence if week-start logic changes. [`ViewModels/MainViewModel.cs`, `Views/WeekViewControl.xaml.cs`]
- **F12: `InitializeAsync` non-atomic `??=` on `_initializationTask`** — hypothetical race; unreachable in practice since WinUI 3 `OnLaunched` runs single-threaded with one call site. [`ViewModels/MainViewModel.cs`]
- **F24: TestData JSON fixtures absent from story diff** — `sample_gcal_events_month.json` and `sample_gcal_events_week.json` exist on disk but were not committed within the reviewed commit range; verify they were committed and contain the correct data described in the story spec.

## Deferred from: code review of 3-10-virtualize-week-view-event-rendering (2026-04-06)

- **`activeSegments` overlap depth unbounded** — Overlap depth grows without cap; deeply-nested overlapping events can push event blocks outside the visible column (collapsed to 1 px width). Pre-existing algorithm carried from old imperative rendering. [`Services/WeekTimedEventProjectionBuilder.cs`]
- **`GetDisplayStart`/`GetDisplayEnd` incorrect for events spanning 3+ calendar days** — For events with `StartLocal.Date != EndLocal.Date`, all segments show the original start/end times rather than per-segment visible times; middle-day segments display misleading labels. Rare in practice (multi-day timed events, not all-day). [`Services/WeekTimedEventProjectionBuilder.cs:125-137`]
- **`currentEvents` `IEnumerable` enumerated 7× per `Rebuild`** — The `Build` method iterates the source once per day (7×); callers currently pass a materialized list so no observable issue, but a LINQ chain or live collection would cause 7 full traversals. [`Services/WeekTimedEventProjectionBuilder.cs`]
- **`ConfigureTimedEventBorder` accesses `Children[0]`/`[1]` without bounds guard** — Assumes `DataTemplate` always produces exactly a `Grid` with 2 children; deviating template changes will throw `IndexOutOfRangeException`. [`Views/WeekViewControl.xaml.cs`]
- **Tapping `WeekHeaderGrid` background clears event selection** — `WeekGrid_Tapped` is also attached to `WeekHeaderGrid`; tapping a day header area without a handled child fires `ClearSelection()` unexpectedly. Pre-existing behavior. [`Views/WeekViewControl.xaml.cs`]
- **`Loaded` handler can double-subscribe `PropertyChanged` on repeated navigation** — `ViewModel.PropertyChanged += ViewModel_PropertyChanged` in `WeekViewControl_Loaded` has no guard; if `Loaded` fires twice without an intervening `Unloaded`, event handlers fire twice per change. Pre-existing pattern. [`Views/WeekViewControl.xaml.cs`]

## Deferred from: code review of 3-11-export-events-to-ics (2026-04-10)

- **Zero-duration timed events produce `DTSTART == DTEND`** — Violates RFC 5545 §3.6.1; Google Calendar never produces zero-duration events so this is unreachable in practice. [`Services/IcsExporter.cs`]
- **Bare `\r` in field values silently deleted rather than escaped** — Mac line-ending edge case; Google Calendar data does not produce bare CR. [`Services/IcsExporter.cs`]
- **`ExportToFileAsync` has no `from <= to` guard at service layer** — Dialog enforces this constraint before calling the service; defensive only. [`Services/IcsExportService.cs`]
- **`DatePicker` pre-fill not guarded against extreme `DateOnly` values** — Defaults always resolve to today or stored event dates, so `MinValue`/`MaxValue` can't be produced. [`Views/MainPage.xaml.cs`]
- **`File.Move` non-atomic across filesystems/network shares** — Mitigated because temp file is placed in the same directory as the destination (same volume). [`Services/IcsExportService.cs`]

## Deferred from: code review of 3-12-import-events-from-ics (2026-04-10)

- **RRULE + malformed DTSTART miscounts as invalid instead of skipped-recurring** — If a VEVENT has both an RRULE and an unparseable DTSTART, early-return in date parsing increments `invalidEventCount` before the post-loop RRULE check fires; the summary shows it as invalid rather than recurring-skipped. Rare edge case; no real-world ICS files exhibit this combination. [`Services/IcsParser.cs:122-139`]
- **`FindValueSeparatorIndex` treats `\:` as escaped colon** — RFC 5545 does not define `\:` as an escape; the guard `line[i-1] != '\\'` is technically incorrect but harmless — `\:` never appears in valid ICS property descriptor lines. [`Services/IcsParser.cs:235-246`]

## Deferred from: code review of 3-9-enhance-year-view-with-event-indicators-and-all-day-previews (2026-04-06)

- **`SpanDays` field populated but unused** — `YearViewPreviewBarDisplayModel.SpanDays` is assigned from `AllDayEventSpan.SpanDays` but no rendering path reads it; if future code uses it, the value reflects the full calendar span of the event, not the clipped visible span. [`Models/YearViewDayDisplayModel.cs:14`]
- **Test `Build_CollapsesMultiDayAssignmentsIntoSingleWeekSegments` validates unused output** — The test asserts `projection.MultiDaySegmentsByWeekStart` but the view uses `BuildWeekSegments` instead; test provides no coverage of the actual rendered output. [`GoogleCalendarManagement.Tests/Unit/Services/YearViewDayProjectionBuilderTests.cs:118`]
- **No test for carry-forward starting before the visible date range** — All tests seed events whose `StartDay` is within `visibleDates`; there is no coverage for a multi-day event that starts before Jan 1 and extends into the visible year. [`GoogleCalendarManagement.Tests/Unit/Services/YearViewDayProjectionBuilderTests.cs`]
- **Zero-duration all-day events silently promoted to single-day** — `BuildAllDaySpans` treats `EndLocal == StartLocal` as SpanDays=1; a malformed/API-edge event gets shown as a valid 1-day bar. [`Services/YearViewDayProjectionBuilder.cs:163-167`]
- **Inverted-date all-day events silently clamped without logging** — When `EndLocal < StartLocal`, the guard clamps `endDay = startDay` silently; a log warning would help diagnose corrupted event data. [`Services/YearViewDayProjectionBuilder.cs:168-170`]

## Deferred from: code review of delete-ux-auto-stage-candidate-undo-toast (2026-05-13)

- **`DeleteUneditedDraftAsync` has no undo** — Called in multiple places to silently discard unedited new drafts with no confirmation or recovery. Inconsistent with the new candidate-delete undo toast. [`ViewModels/EventDetailsPanelViewModel.cs` ~line 1902]
- **`DeleteEventByIdAsync` still shows a confirmation dialog for Pending events** — Programmatic delete path has a different UX than the user-initiated `DeleteEventAsync`. [`ViewModels/EventDetailsPanelViewModel.cs` ~line 1812]
- **`RevertPendingChangesForEventAsync` silently deletes pending edits** — No undo offered when reverting a pending edit via the three-choice dialog. Pre-existing behaviour. [`ViewModels/EventDetailsPanelViewModel.cs` ~line 1880]

## Deferred from: code review of 7-4-toggl-phone (2026-06-05)

- **Duplicate migration timestamp `20260604130000`** — `AddTogglPhoneRule` and `AddCiv5SessionPoint` share the same timestamp; alphabetical ordering happens to be correct, but colliding timestamps are fragile; consider renaming `AddTogglPhoneRule` to a distinct timestamp (e.g., `20260604131000`). [`Data/Migrations/`]
- **`TogglPhoneDrilldownControl` doesn't use shared `VerticalDotTimelineControl`** — Story note says dot timeline should be reusable (shared with Spotify 7.10); phone drilldown implements its own inline 480px canvas instead. Unify when Spotify drilldown (7.10) is reviewed. [`Views/TogglPhoneDrilldownControl.xaml.cs`]
- **Tooltip duration fallback `EndTime.Value - StartTime` is `DateTimeKind`-unsafe** — Used when `DurationSeconds` is null; subtracts DateTime values that may have different Kinds (e.g., StartTime=Utc, EndTime=Unspecified from SQLite), producing incorrect tooltip durations. [`ViewModels/TogglPhoneEntryViewModel.cs`]
- **"Manage Rules" button (7.5) bundled in 7.4 drilldown** — 7.4 and 7.5 were implemented together; "Manage Rules" is outside 7.4 AC but intentional per completion notes. Reviewed as part of 7.5. [`Views/TogglPhoneDrilldownControl.xaml`]

## Deferred from: code review of 7-5-toggl-phone-date-range-rules (2026-06-05)

- **`MaxDurationMinutesText` setter silently ignores invalid input** — Non-numeric, zero, or negative values leave the bound `MaxDurationMinutes` unchanged with no user feedback; displayed text diverges from saved value. [`ViewModels/TogglPhoneRulesViewModel.cs`]
- **No validation for inverted date range (`DateFrom > DateTo`)** — A rule with DateFrom after DateTo is accepted and saved; it will silently never match any entry. [`ViewModels/TogglPhoneRulesViewModel.cs`]
- **`DateTimeKind.Unspecified` in `MatchesAnyRule` treated as local** — Consistent with rest of codebase; flag for future hardening. [`Services/TogglPhoneClassificationService.cs`]
- **DST boundary may undercount entries in `GetPhoneEntryCountsForRangeAsync`** — UTC-to-local conversion near midnight on DST transition days can shift an entry's date; post-filter may drop edge entries. [`Services/TogglPhoneRepository.cs`]
- **`DeactivateAsync` split-brain UI state on `RefreshAsync` failure** — `IsActive = false` is set before `RefreshAsync`; if refresh throws, the item shows deactivated while the full list is stale. [`ViewModels/TogglPhoneRulesViewModel.cs`]
- **`UpdateRuleAsync` with externally-deleted row throws `DbUpdateConcurrencyException` with no UX** — Unlikely in single-user desktop app. [`Services/TogglPhoneRuleRepository.cs`]
- **Null-duration (running timer) entries match open-ended rules on description alone** — May or may not be intended; spec silent on running entries. [`Services/TogglPhoneClassificationService.cs`]

## Deferred from: code review of 7-11-civilization-5-saves (2026-06-05)

- **DST midnight boundary causes ±1h UTC range in `GetPointsForDateAsync`** — `DateTime.SpecifyKind + ToUniversalTime` on local midnight is off by 1h during DST transitions; pre-existing pattern consistent with other data-source repositories. [`Services/Civ5SessionRepository.cs:GetPointsForDateAsync`]
- **`CollectCandidates` blocks UI thread** — synchronous `Directory.EnumerateFiles` runs on the dispatcher thread after the first `await`; acceptable for typical save volumes but would freeze UI on very large save collections. [`Services/Civ5SaveScannerService.cs:CollectCandidates`]

## Deferred from: code review of 7-10-spotify-stats-fm (2026-06-05)

- **`UpsertStreamsAsync` N+1 database queries** — One `FirstOrDefaultAsync` per stream item (up to 500 per page); acceptable for current volumes but will degrade with large history imports. [`Services/SpotifyImportService.cs:UpsertStreamsAsync`]
- **`ParseEndTime` has no null/format guard** — `DateTimeOffset.Parse` throws `FormatException`/`ArgumentNullException` on malformed API data; not caught by the import exception filter, aborting the whole import on a single bad record. Low risk for the unofficial API. [`Services/SpotifyImportService.cs:ParseEndTime`]
- **DST boundary edge in `ToUtcRange`** — Spring-forward midnight may produce a UTC range off by one hour; pre-existing pattern used across all data sources. [`Services/SpotifyStreamRepository.cs:ToUtcRange`]
- **All timeline dots placed at same X position** — Multiple tracks within the same clock minute overlap completely with no visual indicator of stacking. [`Views/VerticalDotTimelineControl.xaml.cs:SetItems`]
- **`EnsureDataSourceAsync` TOCTOU race on first concurrent import** — Unlikely in a single-user desktop app; both calls could insert a duplicate data-source row if they race. [`Services/SpotifyImportService.cs:EnsureDataSourceAsync`]
- **Token encryption write path not verified in filtered diff** — Verify `SetConfigValueAsync(StatsFmTokenConfigKey, token, encrypt: true)` is used in `SettingsViewModel` save handler. [`ViewModels/SettingsViewModel.cs`]

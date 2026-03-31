# Deferred Work

## Deferred from: code review of 3-1-build-year-month-week-day-calendar-views (2026-03-31)

- **F11: `GetWeekRange` duplicated across MainViewModel + four view controls** — DRY violation; extract shared static helper to prevent silent divergence if week-start logic changes. [`ViewModels/MainViewModel.cs`, `Views/WeekViewControl.xaml.cs`]
- **F12: `InitializeAsync` non-atomic `??=` on `_initializationTask`** — hypothetical race; unreachable in practice since WinUI 3 `OnLaunched` runs single-threaded with one call site. [`ViewModels/MainViewModel.cs`]
- **F24: TestData JSON fixtures absent from story diff** — `sample_gcal_events_month.json` and `sample_gcal_events_week.json` exist on disk but were not committed within the reviewed commit range; verify they were committed and contain the correct data described in the story spec.

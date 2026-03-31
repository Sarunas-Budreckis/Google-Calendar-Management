using System.Globalization;
using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task SwitchViewModeCommand_MonthView_LoadsWholeMonth_AndUpdatesBreadcrumb()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Month);

        queryService.LastFrom.Should().Be(new DateOnly(2026, 01, 01));
        queryService.LastTo.Should().Be(new DateOnly(2026, 01, 31));
        viewModel.BreadcrumbLabel.Should().Be("January 2026");
    }

    [Fact]
    public async Task SwitchViewModeCommand_WeekView_StartsOnMonday()
    {
        using var cultureScope = new CultureScope("en-US");
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 03, 26)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.SwitchViewModeCommand.ExecuteAsync(ViewMode.Week);

        queryService.LastFrom.Should().Be(new DateOnly(2026, 03, 23));
        queryService.LastTo.Should().Be(new DateOnly(2026, 03, 29));
        viewModel.BreadcrumbLabel.Should().Be("Mar 23\u201329, 2026");
    }

    [Fact]
    public async Task NavigatePreviousCommand_MonthView_WrapsJanuaryToPreviousDecember()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.NavigatePreviousCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2025, 12, 15));
        queryService.LastFrom.Should().Be(new DateOnly(2025, 12, 01));
        queryService.LastTo.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public async Task NavigateTodayCommand_UsesTimeProviderDate()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Day, new DateOnly(2026, 01, 15)));
        var viewModel = CreateViewModel(
            queryService,
            navigationStateService,
            new FixedTimeProvider(new DateTimeOffset(2026, 04, 02, 12, 0, 0, TimeSpan.Zero)));

        await viewModel.InitializeAsync();
        await viewModel.NavigateTodayCommand.ExecuteAsync(null);

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 04, 02));
        queryService.LastFrom.Should().Be(new DateOnly(2026, 04, 02));
        queryService.LastTo.Should().Be(new DateOnly(2026, 04, 02));
    }

    [Fact]
    public async Task InitializeAsync_LoadsSavedYearState_AndBuildsYearBreadcrumb()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Year, new DateOnly(2026, 06, 15)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();

        viewModel.CurrentViewMode.Should().Be(ViewMode.Year);
        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 06, 15));
        viewModel.BreadcrumbLabel.Should().Be("2026");
        queryService.LastFrom.Should().Be(new DateOnly(2026, 01, 01));
        queryService.LastTo.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task JumpToDateCommand_SetsCurrentDateAndLoadsCorrectRange()
    {
        var queryService = new RecordingCalendarQueryService();
        var navigationStateService = new StubNavigationStateService(
            new NavigationState(ViewMode.Month, new DateOnly(2026, 01, 01)));
        var viewModel = CreateViewModel(queryService, navigationStateService);

        await viewModel.InitializeAsync();
        await viewModel.JumpToDateCommand.ExecuteAsync(new DateOnly(2026, 06, 15));

        viewModel.CurrentDate.Should().Be(new DateOnly(2026, 06, 15));
        viewModel.CurrentViewMode.Should().Be(ViewMode.Month);
        queryService.LastFrom.Should().Be(new DateOnly(2026, 06, 01));
        queryService.LastTo.Should().Be(new DateOnly(2026, 06, 30));
    }

    private static MainViewModel CreateViewModel(
        RecordingCalendarQueryService queryService,
        StubNavigationStateService navigationStateService,
        TimeProvider? timeProvider = null)
    {
        return new MainViewModel(
            queryService,
            navigationStateService,
            NullLogger<MainViewModel>.Instance,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 03, 30, 12, 0, 0, TimeSpan.Zero)));
    }

    private sealed class RecordingCalendarQueryService : ICalendarQueryService
    {
        public DateOnly? LastFrom { get; private set; }
        public DateOnly? LastTo { get; private set; }

        public Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default)
        {
            return Task.FromResult<CalendarEventDisplayModel?>(null);
        }

        public Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            LastFrom = from;
            LastTo = to;
            return Task.FromResult<IList<CalendarEventDisplayModel>>([]);
        }
    }

    private sealed class StubNavigationStateService : INavigationStateService
    {
        private NavigationState _state;

        public StubNavigationStateService(NavigationState state)
        {
            _state = state;
        }

        public Task<NavigationState> LoadAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_state);
        }

        public Task SaveAsync(NavigationState state, CancellationToken ct = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}

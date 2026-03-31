using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICalendarQueryService _calendarQueryService;
    private readonly INavigationStateService _navigationStateService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly TimeProvider _timeProvider;
    private Task? _initializationTask;
    private ViewMode _currentViewMode;
    private DateOnly _currentDate;
    private string _breadcrumbLabel = string.Empty;
    private IList<CalendarEventDisplayModel> _currentEvents = [];
    private bool _isLoading;

    public MainViewModel(
        ICalendarQueryService calendarQueryService,
        INavigationStateService navigationStateService,
        ILogger<MainViewModel> logger,
        TimeProvider? timeProvider = null)
    {
        _calendarQueryService = calendarQueryService;
        _navigationStateService = navigationStateService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;

        SwitchViewModeCommand = new AsyncRelayCommand<ViewMode>(SwitchViewModeAsync);
        NavigatePreviousCommand = new AsyncRelayCommand(NavigatePreviousAsync);
        NavigateNextCommand = new AsyncRelayCommand(NavigateNextAsync);
        NavigateTodayCommand = new AsyncRelayCommand(NavigateTodayAsync);
        JumpToDateCommand = new AsyncRelayCommand<DateOnly>(JumpToDateAsync);
    }

    public ViewMode CurrentViewMode
    {
        get => _currentViewMode;
        private set => SetProperty(ref _currentViewMode, value);
    }

    public DateOnly CurrentDate
    {
        get => _currentDate;
        private set => SetProperty(ref _currentDate, value);
    }

    public string BreadcrumbLabel
    {
        get => _breadcrumbLabel;
        private set => SetProperty(ref _breadcrumbLabel, value);
    }

    public IList<CalendarEventDisplayModel> CurrentEvents
    {
        get => _currentEvents;
        private set
        {
            if (SetProperty(ref _currentEvents, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }
    }

    public bool ShowEmptyState => !IsLoading && CurrentEvents.Count == 0;

    public Visibility EmptyStateVisibility => ShowEmptyState ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand<ViewMode> SwitchViewModeCommand { get; }

    public IAsyncRelayCommand NavigatePreviousCommand { get; }

    public IAsyncRelayCommand NavigateNextCommand { get; }

    public IAsyncRelayCommand NavigateTodayCommand { get; }

    public IAsyncRelayCommand<DateOnly> JumpToDateCommand { get; }

    public Task InitializeAsync()
    {
        return _initializationTask ??= InitializeCoreAsync();
    }

    private async Task SwitchViewModeAsync(ViewMode mode)
    {
        CurrentViewMode = mode;
        await RefreshAsync();
    }

    private async Task NavigatePreviousAsync()
    {
        CurrentDate = CurrentViewMode switch
        {
            ViewMode.Year => CurrentDate.AddYears(-1),
            ViewMode.Month => CurrentDate.AddMonths(-1),
            ViewMode.Week => CurrentDate.AddDays(-7),
            ViewMode.Day => CurrentDate.AddDays(-1),
            _ => CurrentDate
        };

        await RefreshAsync();
    }

    private async Task NavigateNextAsync()
    {
        CurrentDate = CurrentViewMode switch
        {
            ViewMode.Year => CurrentDate.AddYears(1),
            ViewMode.Month => CurrentDate.AddMonths(1),
            ViewMode.Week => CurrentDate.AddDays(7),
            ViewMode.Day => CurrentDate.AddDays(1),
            _ => CurrentDate
        };

        await RefreshAsync();
    }

    private async Task NavigateTodayAsync()
    {
        CurrentDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        await RefreshAsync();
    }

    private async Task JumpToDateAsync(DateOnly date)
    {
        CurrentDate = date;
        await RefreshAsync();
    }

    private async Task InitializeCoreAsync()
    {
        var state = await _navigationStateService.LoadAsync();
        CurrentViewMode = state.ViewMode;
        CurrentDate = state.CurrentDate;
        await RefreshAsync();
    }

    private async Task RefreshAsync(CancellationToken ct = default)
    {
        IsLoading = true;

        try
        {
            var (from, to) = GetDateRange(CurrentViewMode, CurrentDate);
            CurrentEvents = await _calendarQueryService.GetEventsForRangeAsync(from, to, ct);
            BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
            await _navigationStateService.SaveAsync(new NavigationState(CurrentViewMode, CurrentDate), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to refresh the calendar view for {ViewMode} on {Date}.", CurrentViewMode, CurrentDate);
            CurrentEvents = [];
            BreadcrumbLabel = BuildBreadcrumb(CurrentViewMode, CurrentDate);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static (DateOnly From, DateOnly To) GetDateRange(ViewMode viewMode, DateOnly date)
    {
        return viewMode switch
        {
            ViewMode.Year => (new DateOnly(date.Year, 1, 1), new DateOnly(date.Year, 12, 31)),
            ViewMode.Month => (new DateOnly(date.Year, date.Month, 1), new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month))),
            ViewMode.Week => GetWeekRange(date),
            ViewMode.Day => (date, date),
            _ => (date, date)
        };
    }

    private static string BuildBreadcrumb(ViewMode viewMode, DateOnly date)
    {
        var culture = CultureInfo.CurrentCulture;

        return viewMode switch
        {
            ViewMode.Year => date.Year.ToString(culture),
            ViewMode.Month => date.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture),
            ViewMode.Week => BuildWeekBreadcrumb(date, culture),
            ViewMode.Day => date.ToDateTime(TimeOnly.MinValue).ToString("dddd, dd MMMM yyyy", culture),
            _ => string.Empty
        };
    }

    private static (DateOnly From, DateOnly To) GetWeekRange(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var monday = date.AddDays(-daysFromMonday);
        return (monday, monday.AddDays(6));
    }

    private static string BuildWeekBreadcrumb(DateOnly date, CultureInfo culture)
    {
        var (from, to) = GetWeekRange(date);
        var fromDate = from.ToDateTime(TimeOnly.MinValue);
        var toDate = to.ToDateTime(TimeOnly.MinValue);

        if (from.Year != to.Year)
        {
            return $"{fromDate.ToString("MMM d, yyyy", culture)}-{toDate.ToString("MMM d, yyyy", culture)}";
        }

        if (from.Month != to.Month)
        {
            return $"{fromDate.ToString("MMM d", culture)}-{toDate.ToString("MMM d", culture)}, {to.Year.ToString(culture)}";
        }

        return $"{fromDate.ToString("MMM d", culture)}-{to.Day.ToString(culture)}, {to.Year.ToString(culture)}";
    }
}

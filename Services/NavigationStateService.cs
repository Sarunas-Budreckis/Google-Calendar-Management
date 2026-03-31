using GoogleCalendarManagement.Models;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class NavigationStateService : INavigationStateService
{
    private const string CurrentViewModeKey = "current_view_mode";
    private const string CurrentViewDateKey = "current_view_date";

    private readonly ISystemStateRepository _systemStateRepository;
    private readonly ILogger<NavigationStateService> _logger;
    private readonly TimeProvider _timeProvider;

    public NavigationStateService(
        ISystemStateRepository systemStateRepository,
        ILogger<NavigationStateService> logger,
        TimeProvider? timeProvider = null)
    {
        _systemStateRepository = systemStateRepository;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<NavigationState> LoadAsync(CancellationToken ct = default)
    {
        try
        {
            var viewModeText = await _systemStateRepository.GetAsync(CurrentViewModeKey, ct);
            var currentDateText = await _systemStateRepository.GetAsync(CurrentViewDateKey, ct);

            if (Enum.TryParse<ViewMode>(viewModeText, ignoreCase: true, out var viewMode)
                && DateOnly.TryParse(currentDateText, out var currentDate))
            {
                return new NavigationState(viewMode, currentDate);
            }

            if (!string.IsNullOrWhiteSpace(viewModeText) || !string.IsNullOrWhiteSpace(currentDateText))
            {
                _logger.LogWarning(
                    "Navigation state was invalid. ViewMode='{ViewMode}', Date='{Date}'. Falling back to defaults.",
                    viewModeText,
                    currentDateText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load navigation state. Falling back to defaults.");
        }

        return GetDefaultState();
    }

    public async Task SaveAsync(NavigationState state, CancellationToken ct = default)
    {
        await _systemStateRepository.SetAsync(CurrentViewModeKey, state.ViewMode.ToString(), ct);
        await _systemStateRepository.SetAsync(CurrentViewDateKey, state.CurrentDate.ToString("yyyy-MM-dd"), ct);
    }

    private NavigationState GetDefaultState()
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        return new NavigationState(ViewMode.Year, today);
    }
}

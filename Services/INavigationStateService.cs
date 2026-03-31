using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public interface INavigationStateService
{
    Task<NavigationState> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(NavigationState state, CancellationToken ct = default);
}

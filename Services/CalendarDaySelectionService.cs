using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarDaySelectionService : ICalendarDaySelectionService
{
    private readonly INavigationStateService _navigationStateService;

    public CalendarDaySelectionService(INavigationStateService navigationStateService)
    {
        _navigationStateService = navigationStateService;
        var state = _navigationStateService.LoadAsync().GetAwaiter().GetResult();
        SelectedDay = state.SelectedDay;
        ManuallySelectedDay = state.SelectedDay;
    }

    public DateOnly? SelectedDay { get; private set; }

    public DateOnly? ManuallySelectedDay { get; private set; }

    public void SelectDay(DateOnly date)
    {
        if (SelectedDay == date)
        {
            ClearSelection();
            return;
        }

        ManuallySelectedDay = date;
        SetSelectedDay(date, persistManualSelection: true);
    }

    public void AutoSelectDay(DateOnly date)
    {
        SetSelectedDay(date, persistManualSelection: false);
    }

    public void RestoreManualSelection()
    {
        SetSelectedDay(ManuallySelectedDay, persistManualSelection: false);
    }

    public void ClearSelection()
    {
        ManuallySelectedDay = null;
        SetSelectedDay(null, persistManualSelection: true);
    }

    private void SetSelectedDay(DateOnly? selectedDay, bool persistManualSelection)
    {
        var changed = SelectedDay != selectedDay;
        SelectedDay = selectedDay;

        if (persistManualSelection)
        {
            PersistManualSelection();
        }

        if (changed)
        {
            WeakReferenceMessenger.Default.Send(new DaySelectedMessage(selectedDay));
        }
    }

    private void PersistManualSelection()
    {
        var currentState = _navigationStateService.LoadAsync().GetAwaiter().GetResult();
        var updatedState = currentState with { SelectedDay = ManuallySelectedDay };
        _navigationStateService.SaveAsync(updatedState).GetAwaiter().GetResult();
    }
}

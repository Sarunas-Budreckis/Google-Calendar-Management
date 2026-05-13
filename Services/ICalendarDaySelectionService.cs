namespace GoogleCalendarManagement.Services;

public interface ICalendarDaySelectionService
{
    DateOnly? SelectedDay { get; }

    DateOnly? ManuallySelectedDay { get; }

    void SelectDay(DateOnly date);

    void AutoSelectDay(DateOnly date);

    void RestoreManualSelection();

    void ClearSelection();
}

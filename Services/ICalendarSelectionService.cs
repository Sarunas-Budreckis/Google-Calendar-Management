namespace GoogleCalendarManagement.Services;

public interface ICalendarSelectionService
{
    string? SelectedGcalEventId { get; }

    void Select(string gcalEventId);

    void ClearSelection();
}

using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public interface ICalendarSelectionService
{
    string? SelectedEventId { get; }

    CalendarEventSourceKind? SelectedSourceKind { get; }

    void Select(string eventId, CalendarEventSourceKind sourceKind, bool openInEditMode = false);

    void ClearSelection();
}

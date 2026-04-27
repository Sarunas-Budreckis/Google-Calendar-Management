using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarSelectionService : ICalendarSelectionService
{
    public string? SelectedEventId { get; private set; }

    public CalendarEventSourceKind? SelectedSourceKind { get; private set; }

    public void Select(string eventId, CalendarEventSourceKind sourceKind, bool openInEditMode = false)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("Event ID must be a non-empty, non-whitespace string.", nameof(eventId));
        }

        if (string.Equals(SelectedEventId, eventId, StringComparison.Ordinal) &&
            SelectedSourceKind == sourceKind)
        {
            return;
        }

        SelectedEventId = eventId;
        SelectedSourceKind = sourceKind;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(eventId, sourceKind, openInEditMode));
    }

    public void ClearSelection()
    {
        if (SelectedEventId is null)
        {
            return;
        }

        SelectedEventId = null;
        SelectedSourceKind = null;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
    }
}

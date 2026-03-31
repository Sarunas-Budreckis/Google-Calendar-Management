using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarSelectionService : ICalendarSelectionService
{
    public string? SelectedGcalEventId { get; private set; }

    public void Select(string gcalEventId)
    {
        if (string.IsNullOrWhiteSpace(gcalEventId))
        {
            ClearSelection();
            return;
        }

        if (string.Equals(SelectedGcalEventId, gcalEventId, StringComparison.Ordinal))
        {
            return;
        }

        SelectedGcalEventId = gcalEventId;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(gcalEventId));
    }

    public void ClearSelection()
    {
        if (SelectedGcalEventId is null)
        {
            return;
        }

        SelectedGcalEventId = null;
        WeakReferenceMessenger.Default.Send(new EventSelectedMessage(null));
    }
}

namespace GoogleCalendarManagement.Models;

public sealed record EventPickerResult(
    IReadOnlyList<EventPickerItem> ConcurrentEvents,
    IReadOnlyList<EventPickerItem> OtherEvents);

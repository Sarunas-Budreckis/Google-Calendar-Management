namespace GoogleCalendarManagement.Models;

public sealed record EventPickerItem(
    string EventId,
    string Summary,
    DateTime StartLocal,
    DateTime EndLocal,
    string? ColorId,
    string ColorHex,
    bool IsConcurrent)
{
    public string TimeRangeText =>
        StartLocal.ToString("MMM d, ddd HH:mm") + " – " + EndLocal.ToString("HH:mm");
}

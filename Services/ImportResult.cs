namespace GoogleCalendarManagement.Services;

public sealed record ImportResult(
    bool Success,
    int ImportedEventCount,
    int NewEventCount,
    int UpdatedEventCount,
    int SkippedInvalidEventCount,
    int SkippedRecurringEventCount,
    string? ErrorMessage)
{
    public int SkippedEventCount => SkippedInvalidEventCount + SkippedRecurringEventCount;
}

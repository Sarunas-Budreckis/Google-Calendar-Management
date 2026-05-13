namespace GoogleCalendarManagement.Services;

public enum GoogleCalendarWriteFailureKind
{
    None = 0,
    Unknown = 1,
    Authentication = 2,
    Network = 3,
    PreconditionFailed = 4
}

public sealed record GoogleCalendarDeleteResult(
    bool Success,
    string? ErrorMessage = null,
    string? ErrorDetails = null)
{
    public static GoogleCalendarDeleteResult Ok() => new(true);

    public static GoogleCalendarDeleteResult Failure(string message, string? errorDetails = null)
        => new(false, message, errorDetails);
}

public sealed record GoogleCalendarWriteResult(
    bool Success,
    GcalEventDto? Event,
    string? ErrorMessage,
    GoogleCalendarWriteFailureKind FailureKind = GoogleCalendarWriteFailureKind.None,
    string? ErrorDetails = null)
{
    public static GoogleCalendarWriteResult Ok(GcalEventDto eventDto)
        => new(true, eventDto, null);

    public static GoogleCalendarWriteResult Failure(
        string message,
        GoogleCalendarWriteFailureKind failureKind = GoogleCalendarWriteFailureKind.Unknown,
        string? errorDetails = null)
        => new(false, null, message, failureKind, errorDetails);
}

namespace GoogleCalendarManagement.Services;

public sealed class TogglApiException : Exception
{
    public TogglApiException(string message)
        : base(message)
    {
    }

    public TogglApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

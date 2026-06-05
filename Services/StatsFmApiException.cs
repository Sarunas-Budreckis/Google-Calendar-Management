namespace GoogleCalendarManagement.Services;

public sealed class StatsFmApiException : Exception
{
    public StatsFmApiException(string message) : base(message) { }
}

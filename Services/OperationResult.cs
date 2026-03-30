namespace GoogleCalendarManagement.Services;

public sealed record OperationResult<T>(bool Success, T? Data, string? ErrorMessage)
{
    public static OperationResult<T> Ok(T data) => new(true, data, null);

    public static OperationResult<T> Failure(string message) => new(false, default, message);
}

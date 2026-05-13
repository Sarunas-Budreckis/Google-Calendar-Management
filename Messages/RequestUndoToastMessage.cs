namespace GoogleCalendarManagement.Messages;

public sealed record RequestUndoToastMessage(
    string Message,
    Func<CancellationToken, Task> OnUndo);

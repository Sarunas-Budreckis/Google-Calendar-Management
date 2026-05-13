namespace GoogleCalendarManagement.Messages;

public sealed record DataSourceImportCompletedMessage(
    int DataSourceId,
    string SourceKey,
    bool Success);

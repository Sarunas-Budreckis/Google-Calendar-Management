namespace GoogleCalendarManagement.Services;

public sealed record OutlookImportResult(
    bool Success,
    int NewRecords,
    int UpdatedRecords,
    string? ErrorMessage)
{
    public int RecordsFetched => NewRecords + UpdatedRecords;
}

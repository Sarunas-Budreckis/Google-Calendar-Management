namespace GoogleCalendarManagement.Services;

public sealed record TogglTransitImportResult(bool Success, int NewRecords, int UpdatedRecords, string? ErrorMessage)
{
    public int RecordsFetched => NewRecords + UpdatedRecords;
}

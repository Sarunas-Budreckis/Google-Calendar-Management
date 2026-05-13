namespace GoogleCalendarManagement.Data.Entities;

public class DataSourceImportLog
{
    public int ImportLogId { get; set; }
    public int DataSourceId { get; set; }
    public DateOnly CoveredStartDate { get; set; }
    public DateOnly CoveredEndDate { get; set; }
    public DateTime ImportedAt { get; set; }
    public int? RecordsFetched { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public DataSource DataSource { get; set; } = null!;
}

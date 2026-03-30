namespace GoogleCalendarManagement.Data.Entities;

public class DataSourceRefresh
{
    public int RefreshId { get; set; }
    public string SourceName { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastRefreshedAt { get; set; }
    public int? RecordsFetched { get; set; }
    public bool? Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SyncToken { get; set; }
}

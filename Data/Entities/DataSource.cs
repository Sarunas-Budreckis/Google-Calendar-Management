namespace GoogleCalendarManagement.Data.Entities;

public class DataSource
{
    public int DataSourceId { get; set; }
    public string SourceKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public bool SupportsNoDataHint { get; set; }
    public string? ColorHex { get; set; }
    public DateTime CreatedAt { get; set; }
}

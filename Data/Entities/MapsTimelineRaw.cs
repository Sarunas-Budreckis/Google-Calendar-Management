namespace GoogleCalendarManagement.Data.Entities;

public class MapsTimelineRaw
{
    public int Id { get; set; }
    public DateTime ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public DateOnly? CoveredDateMin { get; set; }
    public DateOnly? CoveredDateMax { get; set; }
    public string RawJson { get; set; } = "";
}

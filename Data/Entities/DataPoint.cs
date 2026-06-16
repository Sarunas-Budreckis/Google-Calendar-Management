namespace GoogleCalendarManagement.Data.Entities;

public class DataPoint
{
    public int DataPointId { get; set; }
    public string SourceKey { get; set; } = "";
    public string SourceRef { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Link> Links { get; set; } = new List<Link>();
}

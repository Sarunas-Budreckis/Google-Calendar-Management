namespace GoogleCalendarManagement.Data.Entities;

public class CallLogEntry
{
    public int Id { get; set; }
    public int ImportId { get; set; }
    public string CallType { get; set; } = "";
    public DateTime Date { get; set; }
    public int DurationSeconds { get; set; }
    public string? Number { get; set; }
    public string? Contact { get; set; }
    public string? Location { get; set; }
    public string Service { get; set; } = "";
    public string? LinkedEventId { get; set; }
    public string? LinkedEventType { get; set; }

    public CallLogImport Import { get; set; } = null!;
}

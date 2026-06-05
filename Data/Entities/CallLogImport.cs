namespace GoogleCalendarManagement.Data.Entities;

public class CallLogImport
{
    public int Id { get; set; }
    public DateTime ImportedAt { get; set; }
    public string FileName { get; set; } = "";
    public int RecordCount { get; set; }
    public DateOnly DateMin { get; set; }
    public DateOnly DateMax { get; set; }

    public ICollection<CallLogEntry> Entries { get; set; } = [];
}

namespace GoogleCalendarManagement.Data.Entities;

public class AuditLog
{
    public int LogId { get; set; }
    public DateTime Timestamp { get; set; }
    public string OperationType { get; set; } = "";
    public string? OperationDetails { get; set; }
    public string? AffectedDates { get; set; }
    public string? AffectedEvents { get; set; }
    public bool? UserAction { get; set; }
    public bool? Success { get; set; }
    public string? ErrorMessage { get; set; }
}

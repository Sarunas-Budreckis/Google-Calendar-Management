namespace GoogleCalendarManagement.Data.Entities;

public class TogglSleepQuality
{
    public DateOnly Date { get; set; }
    public int? Quality { get; set; }
    public DateTime UpdatedAt { get; set; }
}

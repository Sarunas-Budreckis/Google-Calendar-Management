namespace GoogleCalendarManagement.Data.Entities;

public class SystemState
{
    public int StateId { get; set; }
    public string? StateName { get; set; }
    public string? StateValue { get; set; }
    public DateTime UpdatedAt { get; set; }
}

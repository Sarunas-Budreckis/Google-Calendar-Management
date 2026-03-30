namespace GoogleCalendarManagement.Data.Entities;

public class Config
{
    public string ConfigKey { get; set; } = "";
    public string? ConfigValue { get; set; }
    public string? ConfigType { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

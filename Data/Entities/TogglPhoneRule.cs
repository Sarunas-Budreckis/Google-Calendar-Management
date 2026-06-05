namespace GoogleCalendarManagement.Data.Entities;

public class TogglPhoneRule
{
    public int Id { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string DescriptionPattern { get; set; } = "";
    public int? MaxDurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

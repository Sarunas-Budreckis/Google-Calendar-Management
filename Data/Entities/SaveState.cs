namespace GoogleCalendarManagement.Data.Entities;

public class SaveState
{
    public int SaveId { get; set; }
    public string SaveName { get; set; } = "";
    public string? SaveDescription { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SnapshotData { get; set; }
}

namespace GoogleCalendarManagement.Data.Entities;

public class ComfyUIFolder
{
    public int Id { get; set; }
    public string FolderPath { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; }
}

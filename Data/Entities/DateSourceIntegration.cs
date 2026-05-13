namespace GoogleCalendarManagement.Data.Entities;

public class DateSourceIntegration
{
    public int IntegrationId { get; set; }
    public DateOnly Date { get; set; }
    public int DataSourceId { get; set; }
    public bool Integrated { get; set; }
    public DateTime? IntegratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DataSource DataSource { get; set; } = null!;
}

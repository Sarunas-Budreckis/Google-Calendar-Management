namespace GoogleCalendarManagement.Data.Entities;

/// <summary>
/// THROWAWAY POCO SHIM (Story 8.2). The date_source_integration table was DROPPED in the
/// UnifyEventTable migration — the manual per-day "integrated?" checkbox is superseded by
/// computed coverage (Story 8.10). This class is no longer an EF entity, is NOT mapped, and
/// has no DbSet. It survives only so DataSourceRepository's now-stubbed integration methods
/// keep compiling until Story 8.10 removes them. Do NOT add new usages.
/// </summary>
public class DateSourceIntegration
{
    public int IntegrationId { get; set; }
    public DateOnly Date { get; set; }
    public int DataSourceId { get; set; }
    public bool Integrated { get; set; }
    public DateTime? IntegratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

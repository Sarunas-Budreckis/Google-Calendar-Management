namespace GoogleCalendarManagement.Data.Entities;

public class RecurringEventSeries
{
    public string SeriesId { get; set; } = "";
    public string CalendarId { get; set; } = "primary";
    public string Recurrence { get; set; } = "";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? ColorId { get; set; }
    public bool? IsAllDay { get; set; }
    public DateTime? SeriesStartDatetime { get; set; }
    public DateTime? SeriesEndDatetime { get; set; }
    public string? GcalEtag { get; set; }
    public DateTime? GcalUpdatedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

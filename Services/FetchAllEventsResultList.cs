namespace GoogleCalendarManagement.Services;

internal sealed class FetchAllEventsResultList : List<GcalEventDto>
{
    public bool WasCancelled { get; init; }
}

namespace GoogleCalendarManagement.Services;

public interface IGraphApiClient
{
    Task<IReadOnlyList<GraphEventDto>> GetCalendarViewAsync(
        string accessToken,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);
}

public sealed class GraphEventDto
{
    public string Id { get; set; } = "";
    public string Subject { get; set; } = "";
    public GraphDateTimeDto? Start { get; set; }
    public GraphDateTimeDto? End { get; set; }
    public bool IsAllDay { get; set; }
    public GraphOrganizerDto? Organizer { get; set; }
    public GraphLocationDto? Location { get; set; }
    public string? BodyPreview { get; set; }
    public string Type { get; set; } = "";
    public string? SeriesMasterId { get; set; }
}

public sealed class GraphDateTimeDto
{
    public string DateTime { get; set; } = "";
    public string TimeZone { get; set; } = "";
}

public sealed class GraphOrganizerDto
{
    public GraphEmailAddressDto? EmailAddress { get; set; }
}

public sealed class GraphEmailAddressDto
{
    public string? Name { get; set; }
    public string? Address { get; set; }
}

public sealed class GraphLocationDto
{
    public string? DisplayName { get; set; }
}

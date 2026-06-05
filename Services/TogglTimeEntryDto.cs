using System.Text.Json.Serialization;

namespace GoogleCalendarManagement.Services;

public sealed record TogglTimeEntryDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("start")] string? Start,
    [property: JsonPropertyName("stop")] string? Stop,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("project_id")] long? ProjectId,
    [property: JsonPropertyName("project_name")] string? ProjectName,
    [property: JsonPropertyName("tags")] string[]? Tags);

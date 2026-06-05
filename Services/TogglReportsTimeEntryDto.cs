using System.Text.Json.Serialization;

namespace GoogleCalendarManagement.Services;

public sealed record TogglReportsTimeEntryDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("start")] string? Start,
    [property: JsonPropertyName("stop")] string? Stop,
    [property: JsonPropertyName("dur")] long Dur,
    [property: JsonPropertyName("pid")] long? Pid,
    [property: JsonPropertyName("project")] string? Project,
    [property: JsonPropertyName("tags")] string[]? Tags);

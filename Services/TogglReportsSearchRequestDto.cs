using System.Text.Json.Serialization;

namespace GoogleCalendarManagement.Services;

public sealed record TogglReportsSearchRequestDto(
    [property: JsonPropertyName("start_date")] string StartDate,
    [property: JsonPropertyName("end_date")] string EndDate,
    [property: JsonPropertyName("order_by")] string OrderBy,
    [property: JsonPropertyName("order_dir")] string OrderDir,
    [property: JsonPropertyName("first_row_number"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? FirstRowNumber);

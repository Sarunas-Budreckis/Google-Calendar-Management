using System.Text.Json.Serialization;

namespace GoogleCalendarManagement.Services;

public sealed record StatsFmMeDto(
    [property: JsonPropertyName("item")] StatsFmMeItemDto Item);

public sealed record StatsFmMeItemDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName);

public sealed record StatsFmStreamsResponseDto(
    [property: JsonPropertyName("items")] List<StatsFmStreamItemDto> Items);

public sealed record StatsFmStreamItemDto(
    [property: JsonPropertyName("playedMs")] int PlayedMs,
    [property: JsonPropertyName("endTime")] string EndTime,
    [property: JsonPropertyName("track")] StatsFmStreamTrackDto Track);

public sealed record StatsFmStreamTrackDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("durationMs")] int DurationMs,
    [property: JsonPropertyName("artists")] List<StatsFmArtistDto>? Artists,
    [property: JsonPropertyName("albums")] List<StatsFmAlbumDto>? Albums);

public sealed record StatsFmArtistDto(
    [property: JsonPropertyName("name")] string Name);

public sealed record StatsFmAlbumDto(
    [property: JsonPropertyName("name")] string Name);

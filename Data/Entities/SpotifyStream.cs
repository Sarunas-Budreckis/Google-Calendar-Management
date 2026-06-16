namespace GoogleCalendarManagement.Data.Entities;

public class SpotifyStream
{
    public int Id { get; set; }
    public string NaturalKey { get; set; } = "";
    public DateTime PlayedAt { get; set; }
    public string TrackName { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string? AlbumName { get; set; }
    public int DurationMs { get; set; }
    public int MsPlayed { get; set; }
}

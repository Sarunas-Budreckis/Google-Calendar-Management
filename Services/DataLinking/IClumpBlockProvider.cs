namespace GoogleCalendarManagement.Services.DataLinking;

public interface IClumpBlockProvider
{
    string SourceKey { get; }

    Task<IReadOnlyList<ClumpBlockResult>> GetClumpsAndBlocksAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

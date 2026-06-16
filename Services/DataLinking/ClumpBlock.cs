namespace GoogleCalendarManagement.Services.DataLinking;

public sealed record ClumpDataPoint(
    long DataPointId,
    string SourceKey,
    string SourceRef,
    DateTime StartUtc,
    DateTime EndUtc);

public sealed record Clump(
    IReadOnlyList<ClumpDataPoint> DataPoints,
    DateTime ClumpStartUtc,
    DateTime ClumpEndUtc);

public sealed record Block(DateTime BlockStartUtc, DateTime BlockEndUtc);

public sealed record ClumpBlockResult(Clump Clump, IReadOnlyList<Block> Blocks);

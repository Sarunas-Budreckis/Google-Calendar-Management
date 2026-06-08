using FluentAssertions;
using GoogleCalendarManagement.Data.Configurations;
using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ComfyUIFolderScannerServiceTests
{
    // ---------------------------------------------------------------------------
    // Deduplication logic
    // ---------------------------------------------------------------------------

    [Fact]
    public void Dedup_CandidateAlreadyExists_IsSkipped()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.ModifiedEventType)
        };

        var candidates = new List<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.ModifiedEventType),
            (t.AddMinutes(5), ComfyUIScanPointConfiguration.ModifiedEventType)
        };

        var result = candidates
            .Where(c => !existing.Contains(c))
            .ToList();

        result.Should().HaveCount(1);
        result[0].Timestamp.Should().Be(t.AddMinutes(5));
    }

    [Fact]
    public void Dedup_AllCandidatesAlreadyExist_ReturnsEmpty()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.ModifiedEventType),
            (t.AddMinutes(10), ComfyUIScanPointConfiguration.CreatedEventType)
        };

        var candidates = new List<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.ModifiedEventType),
            (t.AddMinutes(10), ComfyUIScanPointConfiguration.CreatedEventType)
        };

        var result = candidates
            .Where(c => !existing.Contains(c))
            .ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void Dedup_NoCandidatesExist_AllAreKept()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime Timestamp, string EventType)>();

        var candidates = new List<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.CreatedEventType),
            (t, ComfyUIScanPointConfiguration.ModifiedEventType)
        };

        var result = candidates
            .Where(c => !existing.Contains(c))
            .ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Dedup_SameTimestampDifferentEventType_BothKept()
    {
        var t = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = new HashSet<(DateTime Timestamp, string EventType)>();

        var candidates = new List<(DateTime Timestamp, string EventType)>
        {
            (t, ComfyUIScanPointConfiguration.CreatedEventType),
            (t, ComfyUIScanPointConfiguration.ModifiedEventType)
        };

        var result = candidates
            .Where(c => !existing.Contains(c))
            .ToList();

        result.Should().HaveCount(2);
    }

    // ---------------------------------------------------------------------------
    // Entity construction
    // ---------------------------------------------------------------------------

    [Fact]
    public void ScanPoint_TimestampAndEventTypeStoredCorrectly()
    {
        var timestamp = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var scannedAt = new DateTime(2025, 6, 5, 9, 0, 0, DateTimeKind.Utc);

        var point = new ComfyUIScanPoint
        {
            ScannedAt = scannedAt,
            Timestamp = timestamp,
            EventType = ComfyUIScanPointConfiguration.CreatedEventType
        };

        point.Timestamp.Should().Be(timestamp);
        point.EventType.Should().Be(ComfyUIScanPointConfiguration.CreatedEventType);
        point.ScannedAt.Should().Be(scannedAt);
        point.LinkedEventId.Should().BeNull();
        point.LinkedEventType.Should().BeNull();
    }
}

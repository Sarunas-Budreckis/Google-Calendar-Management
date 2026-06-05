using FluentAssertions;
using GoogleCalendarManagement.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class MapsTimelineParserTests
{
    private static MapsTimelineParser CreateParser() =>
        new(NullLogger<MapsTimelineParser>.Instance);

    // -------------------------------------------------------------------------
    // Old format (timelineObjects)
    // -------------------------------------------------------------------------

    private const string OldFormatJson = """
        {
          "timelineObjects": [
            {
              "placeVisit": {
                "location": { "name": "Home" },
                "duration": {
                  "startTimestamp": "2024-03-15T08:00:00Z",
                  "endTimestamp": "2024-03-15T09:30:00Z"
                }
              }
            },
            {
              "activitySegment": {
                "duration": {
                  "startTimestamp": "2024-03-15T09:30:00Z",
                  "endTimestamp": "2024-03-15T10:00:00Z"
                },
                "activityType": "IN_VEHICLE"
              }
            },
            {
              "placeVisit": {
                "location": { "name": "Office" },
                "duration": {
                  "startTimestamp": "2024-03-16T09:00:00Z",
                  "endTimestamp": "2024-03-16T17:00:00Z"
                }
              }
            }
          ]
        }
        """;

    [Fact]
    public void ExtractDateRange_OldFormat_ReturnsCorrectRange()
    {
        var parser = CreateParser();

        var (min, max) = parser.ExtractDateRange(OldFormatJson);

        min.Should().NotBeNull();
        max.Should().NotBeNull();
        min!.Value.Should().BeOnOrBefore(new DateOnly(2024, 3, 15));
        max!.Value.Should().BeOnOrAfter(new DateOnly(2024, 3, 16));
    }

    [Fact]
    public void GetSegmentsForDate_OldFormat_ReturnsBothTypesForDate()
    {
        var parser = CreateParser();
        var date = new DateOnly(2024, 3, 15);

        var segments = parser.GetSegmentsForDate(OldFormatJson, date);

        segments.Should().HaveCount(2);
        segments.Should().Contain(s => s.IsVisit && s.LocationName == "Home");
        segments.Should().Contain(s => !s.IsVisit && s.ActivityType == "IN_VEHICLE");
    }

    [Fact]
    public void GetSegmentsForDate_OldFormat_ExcludesOtherDates()
    {
        var parser = CreateParser();
        var date = new DateOnly(2024, 3, 17);

        var segments = parser.GetSegmentsForDate(OldFormatJson, date);

        segments.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // New format (semanticSegments)
    // -------------------------------------------------------------------------

    private const string NewFormatJson = """
        {
          "semanticSegments": [
            {
              "startTime": "2024-05-10T07:00:00.000Z",
              "endTime": "2024-05-10T08:30:00.000Z",
              "visit": {
                "topCandidate": { "semanticType": "HOME", "probability": 0.95 }
              }
            },
            {
              "startTime": "2024-05-10T08:30:00.000Z",
              "endTime": "2024-05-10T09:00:00.000Z",
              "activity": {
                "topCandidate": { "type": "WALKING", "probability": 0.8 },
                "distanceMeters": 500
              }
            },
            {
              "startTime": "2024-05-11T10:00:00.000Z",
              "endTime": "2024-05-11T11:00:00.000Z",
              "visit": {
                "topCandidate": { "semanticType": "WORK", "probability": 0.9 }
              }
            }
          ]
        }
        """;

    [Fact]
    public void ExtractDateRange_NewFormat_ReturnsCorrectRange()
    {
        var parser = CreateParser();

        var (min, max) = parser.ExtractDateRange(NewFormatJson);

        min.Should().NotBeNull();
        max.Should().NotBeNull();
        min!.Value.Should().BeOnOrBefore(new DateOnly(2024, 5, 10));
        max!.Value.Should().BeOnOrAfter(new DateOnly(2024, 5, 11));
    }

    [Fact]
    public void GetSegmentsForDate_NewFormat_ReturnsBothTypesForDate()
    {
        var parser = CreateParser();
        var date = new DateOnly(2024, 5, 10);

        var segments = parser.GetSegmentsForDate(NewFormatJson, date);

        segments.Should().HaveCount(2);
        segments.Should().Contain(s => s.IsVisit);
        segments.Should().Contain(s => !s.IsVisit && s.ActivityType == "WALKING");
    }

    [Fact]
    public void GetSegmentsForDate_NewFormat_ExcludesOtherDates()
    {
        var parser = CreateParser();
        var date = new DateOnly(2024, 5, 12);

        var segments = parser.GetSegmentsForDate(NewFormatJson, date);

        segments.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void ExtractDateRange_EmptyJson_ReturnsNulls()
    {
        var parser = CreateParser();

        var (min, max) = parser.ExtractDateRange("{}");

        min.Should().BeNull();
        max.Should().BeNull();
    }

    [Fact]
    public void ExtractDateRange_EmptyTimelineObjects_ReturnsNulls()
    {
        var parser = CreateParser();

        var (min, max) = parser.ExtractDateRange("""{ "timelineObjects": [] }""");

        min.Should().BeNull();
        max.Should().BeNull();
    }

    [Fact]
    public void GetSegmentsForDate_InvalidJson_ReturnsEmpty()
    {
        var parser = CreateParser();

        var segments = parser.GetSegmentsForDate("not valid json !!!", new DateOnly(2024, 1, 1));

        segments.Should().BeEmpty();
    }

    [Fact]
    public void GetSegmentsForDate_OldFormat_PlaceVisitWithoutName_ReturnsNullLocationName()
    {
        var parser = CreateParser();
        const string json = """
            {
              "timelineObjects": [
                {
                  "placeVisit": {
                    "duration": {
                      "startTimestamp": "2024-06-01T10:00:00Z",
                      "endTimestamp": "2024-06-01T11:00:00Z"
                    }
                  }
                }
              ]
            }
            """;

        var segments = parser.GetSegmentsForDate(json, new DateOnly(2024, 6, 1));

        segments.Should().ContainSingle();
        segments[0].LocationName.Should().BeNull();
        segments[0].IsVisit.Should().BeTrue();
    }
}

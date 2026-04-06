using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class IcsExporterTests
{
    [Fact]
    public void GenerateIcs_TimedEvent_WritesUtcDateTimeFields()
    {
        var calendarEvent = new GcalEvent
        {
            GcalEventId = "evt-1",
            Summary = "Planning Session",
            Description = "Discuss roadmap",
            StartDatetime = new DateTime(2026, 04, 05, 14, 30, 00, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 05, 15, 45, 00, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 01, 09, 10, 11, DateTimeKind.Utc)
        };

        var ics = IcsExporter.GenerateIcs(
            [calendarEvent],
            new DateTime(2026, 04, 05, 16, 00, 00, DateTimeKind.Utc));

        ics.Should().Contain("BEGIN:VCALENDAR\r\n");
        ics.Should().Contain("PRODID:-//Google Calendar Management//EN\r\n");
        ics.Should().Contain("UID:evt-1\r\n");
        ics.Should().Contain("SUMMARY:Planning Session\r\n");
        ics.Should().Contain("DESCRIPTION:Discuss roadmap\r\n");
        ics.Should().Contain("DTSTART:20260405T143000Z\r\n");
        ics.Should().Contain("DTEND:20260405T154500Z\r\n");
        ics.Should().Contain("DTSTAMP:20260405T160000Z\r\n");
        ics.Should().Contain("LAST-MODIFIED:20260401T091011Z\r\n");
        ics.Should().Contain("END:VEVENT\r\n");
        ics.Should().EndWith("END:VCALENDAR\r\n");
    }

    [Fact]
    public void GenerateIcs_AllDayEvent_UsesDateFormat_AndOmitsEmptyDescription()
    {
        var calendarEvent = new GcalEvent
        {
            GcalEventId = "all-day",
            Summary = "Holiday",
            Description = null,
            IsAllDay = true,
            StartDatetime = new DateTime(2026, 12, 24, 00, 00, 00, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 12, 25, 00, 00, 00, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 12, 01, 08, 00, 00, DateTimeKind.Utc)
        };

        var ics = IcsExporter.GenerateIcs(
            [calendarEvent],
            new DateTime(2026, 12, 02, 10, 30, 00, DateTimeKind.Utc));

        ics.Should().Contain("DTSTART;VALUE=DATE:20261224\r\n");
        ics.Should().Contain("DTEND;VALUE=DATE:20261225\r\n");
        ics.Should().NotContain("DESCRIPTION:");
        ics.Should().NotContain("DTSTART:20261224T");
    }

    [Fact]
    public void GenerateIcs_EscapesSpecialCharacters_AndNormalizesNewLines()
    {
        var calendarEvent = new GcalEvent
        {
            GcalEventId = @"evt\,semi;slash",
            Summary = @"Comma, Semi; Slash\",
            Description = "Line 1\r\nLine 2, Details; More\\Info",
            StartDatetime = new DateTime(2026, 04, 05, 14, 30, 00, DateTimeKind.Utc),
            EndDatetime = new DateTime(2026, 04, 05, 15, 45, 00, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 04, 01, 09, 10, 11, DateTimeKind.Utc)
        };

        var ics = IcsExporter.GenerateIcs([calendarEvent]);

        ics.Should().Contain(@"UID:evt\\\,semi\;slash");
        ics.Should().Contain(@"SUMMARY:Comma\, Semi\; Slash\\");
        ics.Should().Contain(@"DESCRIPTION:Line 1\nLine 2\, Details\; More\\Info");
    }
}

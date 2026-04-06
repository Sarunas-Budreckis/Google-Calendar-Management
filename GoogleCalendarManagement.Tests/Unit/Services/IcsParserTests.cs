using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class IcsParserTests
{
    [Fact]
    public void ParseIcs_TimedEvent_ParsesRequiredFields()
    {
        const string ics = """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:evt-1
SUMMARY:Planning Session
DESCRIPTION:Discuss roadmap
DTSTART:20260405T143000Z
DTEND:20260405T154500Z
END:VEVENT
END:VCALENDAR
""";

        var result = IcsParser.ParseIcs(ics);

        result.IsValidCalendar.Should().BeTrue();
        result.InvalidEventCount.Should().Be(0);
        result.SkippedRecurringEventCount.Should().Be(0);
        result.Events.Should().ContainSingle();
        result.Events[0].Uid.Should().Be("evt-1");
        result.Events[0].Summary.Should().Be("Planning Session");
        result.Events[0].Description.Should().Be("Discuss roadmap");
        result.Events[0].StartUtc.Should().Be(new DateTime(2026, 04, 05, 14, 30, 00, DateTimeKind.Utc));
        result.Events[0].EndUtc.Should().Be(new DateTime(2026, 04, 05, 15, 45, 00, DateTimeKind.Utc));
        result.Events[0].IsAllDay.Should().BeFalse();
    }

    [Fact]
    public void ParseIcs_AllDayEvent_UsesDateValues_AndNullDescriptionWhenMissing()
    {
        const string ics = """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:all-day
SUMMARY:Holiday
DTSTART;VALUE=DATE:20261224
DTEND;VALUE=DATE:20261225
END:VEVENT
END:VCALENDAR
""";

        var result = IcsParser.ParseIcs(ics);

        result.IsValidCalendar.Should().BeTrue();
        result.Events.Should().ContainSingle();
        result.Events[0].Description.Should().BeNull();
        result.Events[0].IsAllDay.Should().BeTrue();
        result.Events[0].StartUtc.Should().Be(new DateTime(2026, 12, 24, 00, 00, 00, DateTimeKind.Utc));
        result.Events[0].EndUtc.Should().Be(new DateTime(2026, 12, 25, 00, 00, 00, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseIcs_SkipsRecurringAndInvalidEvents_AndCountsThem()
    {
        const string ics = """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:recurring-1
SUMMARY:Daily Standup
DTSTART:20260405T090000Z
DTEND:20260405T093000Z
RRULE:FREQ=DAILY
END:VEVENT
BEGIN:VEVENT
UID:
SUMMARY:Missing Uid
DTSTART:20260405T100000Z
DTEND:20260405T103000Z
END:VEVENT
BEGIN:VEVENT
UID:valid-1
SUMMARY:Valid Event
DTSTART:20260405T110000Z
DTEND:20260405T113000Z
END:VEVENT
END:VCALENDAR
""";

        var result = IcsParser.ParseIcs(ics);

        result.IsValidCalendar.Should().BeTrue();
        result.SkippedRecurringEventCount.Should().Be(1);
        result.InvalidEventCount.Should().Be(1);
        result.Events.Should().ContainSingle();
        result.Events[0].Uid.Should().Be("valid-1");
    }

    [Fact]
    public void ParseIcs_UnfoldsContinuationLines_AndUnescapesText()
    {
        const string ics = """
BEGIN:VCALENDAR
VERSION:2.0
BEGIN:VEVENT
UID:evt-2
SUMMARY:Quarterly review and status
  update
DESCRIPTION:Line 1\nLine 2\, Details\; More\\Info
DTSTART:20260405T143000Z
DTEND:20260405T154500Z
END:VEVENT
END:VCALENDAR
""";

        var result = IcsParser.ParseIcs(ics);

        result.Events.Should().ContainSingle();
        result.Events[0].Summary.Should().Be("Quarterly review and status update");
        result.Events[0].Description.Should().Be("Line 1\nLine 2, Details; More\\Info");
    }

    [Fact]
    public void ParseIcs_InvalidCalendar_ReturnsInvalidResultWithoutThrowing()
    {
        const string ics = """
BEGIN:VEVENT
UID:evt-1
SUMMARY:Missing Calendar
DTSTART:20260405T143000Z
DTEND:20260405T154500Z
END:VEVENT
""";

        var result = IcsParser.ParseIcs(ics);

        result.IsValidCalendar.Should().BeFalse();
        result.Events.Should().BeEmpty();
        result.InvalidEventCount.Should().Be(0);
        result.SkippedRecurringEventCount.Should().Be(0);
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}

using System.Globalization;
using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class WeekTimedEventProjectionBuilderTests
{
    [Fact]
    public void Build_PlacesTimedEventInExpectedColumnAndVerticalSlot()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var events = new[]
        {
            CreateTimedEvent("evt-1", "Planning", weekStart, new TimeOnly(9, 30), new TimeOnly(10, 15), "#336699")
        };

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().ContainSingle();
        var item = items[0];
        item.GcalEventId.Should().Be("evt-1");
        item.Title.Should().Be("Planning");
        item.ColorHex.Should().Be("#336699");
        item.DayOffset.Should().Be(0);
        item.GridRow.Should().Be(9);
        item.GridRowSpan.Should().Be(2);
        item.Left.Should().BeApproximately(88, 0.001);
        item.Top.Should().BeApproximately(684, 0.001);
        item.Width.Should().BeApproximately(112, 0.001);
        item.Height.Should().BeApproximately(51, 0.001);
        item.PrimaryText.Should().Be("Planning");
        item.SecondaryText.Should().Be("9:30 AM - 10:15 AM");
        item.IsCompact.Should().BeFalse();
    }

    [Fact]
    public void Build_CompactsShortEventsIntoSingleSummaryLine()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var events = new[]
        {
            CreateTimedEvent("evt-compact", "Standup", weekStart.AddDays(2), new TimeOnly(9, 0), new TimeOnly(9, 30), "#AA5500")
        };

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().ContainSingle();
        var item = items[0];
        item.DayOffset.Should().Be(2);
        item.IsCompact.Should().BeTrue();
        item.PrimaryText.Should().Be("Standup, 9:00 AM");
        item.SecondaryText.Should().BeNull();
        item.MaxTitleLines.Should().Be(1);
        item.Left.Should().BeApproximately(328, 0.001);
        item.Height.Should().BeApproximately(33, 0.001);
        item.CompactTopPadding.Should().BeApproximately(6, 0.001);
    }

    [Fact]
    public void Build_OffsetsAndOutlinesOverlappingEvents()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var events = new[]
        {
            CreateTimedEvent("evt-1", "One", weekStart.AddDays(1), new TimeOnly(9, 0), new TimeOnly(10, 30), "#111111"),
            CreateTimedEvent("evt-2", "Two", weekStart.AddDays(1), new TimeOnly(9, 15), new TimeOnly(10, 0), "#222222")
        };

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().HaveCount(2);
        var first = items[0];
        var second = items[1];

        first.UseOverlapOutline.Should().BeFalse();
        first.Left.Should().BeApproximately(208, 0.001);
        first.Width.Should().BeApproximately(112, 0.001);

        second.UseOverlapOutline.Should().BeTrue();
        second.Left.Should().BeApproximately(218, 0.001);
        second.Width.Should().BeApproximately(102, 0.001);
    }

    [Fact]
    public void Build_PreservesLargeWeeklyEventSets()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var events = Enumerable.Range(0, 210)
            .Select(index =>
            {
                var dayOffset = index % 7;
                var startHour = 8 + ((index / 7) % 10);
                var startMinute = (index % 2) * 30;
                var start = new TimeOnly(startHour, startMinute);
                var end = start.AddMinutes(30);
                return CreateTimedEvent($"evt-{index}", $"Event {index}", weekStart.AddDays(dayOffset), start, end, "#0088CC");
            })
            .ToArray();

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().HaveCount(210);
        items.Select(static item => item.GcalEventId).Should().OnlyHaveUniqueItems();
        items.Should().OnlyContain(static item => item.Width > 0);
    }

    [Fact]
    public void Build_ClipsCrossMidnightEventsPerDayBeforeComputingVerticalPlacement()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var start = new DateTime(2026, 04, 06, 23, 30, 0);
        var end = new DateTime(2026, 04, 07, 1, 15, 0);
        var events = new[]
        {
            CreateTimedEvent("evt-overnight", "Overnight", start, end, "#1144AA")
        };

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().HaveCount(2);

        var mondaySegment = items.Single(item => item.DayOffset == 0);
        mondaySegment.Top.Should().BeApproximately(1692, 0.001);
        mondaySegment.Height.Should().BeApproximately(33, 0.001);
        mondaySegment.IsCompact.Should().BeTrue();
        mondaySegment.PrimaryText.Should().Be("Overnight, 11:30 PM");

        var tuesdaySegment = items.Single(item => item.DayOffset == 1);
        tuesdaySegment.Top.Should().BeApproximately(0, 0.001);
        tuesdaySegment.GridRow.Should().Be(0);
        tuesdaySegment.GridRowSpan.Should().Be(2);
        tuesdaySegment.Height.Should().BeApproximately(87, 0.001);
        tuesdaySegment.IsCompact.Should().BeFalse();
        tuesdaySegment.SecondaryText.Should().Be("12:00 AM - 1:15 AM");
    }

    [Fact]
    public void Build_UsesExactClippedStartTimeForVerticalPlacement()
    {
        using var cultureScope = new CultureScope("en-US");
        var weekStart = new DateOnly(2026, 04, 06);
        var start = new DateTime(2026, 04, 06, 9, 30, 30);
        var end = new DateTime(2026, 04, 06, 10, 0, 30);
        var events = new[]
        {
            CreateTimedEvent("evt-seconds", "Precise", start, end, "#226622")
        };

        var items = WeekTimedEventProjectionBuilder.Build(weekStart, events, 120, CultureInfo.CurrentCulture);

        items.Should().ContainSingle();
        var item = items[0];
        item.Top.Should().BeApproximately(684.6, 0.001);
        item.GridRow.Should().Be(9);
        item.GridRowSpan.Should().Be(2);
        item.Height.Should().BeApproximately(33, 0.001);
    }

    private static CalendarEventDisplayModel CreateTimedEvent(
        string id,
        string title,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        string colorHex)
    {
        var start = date.ToDateTime(startTime);
        var end = date.ToDateTime(endTime);

        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            false,
            colorHex,
            "Azure",
            false,
            null,
            null);
    }

    private static CalendarEventDisplayModel CreateTimedEvent(
        string id,
        string title,
        DateTime start,
        DateTime end,
        string colorHex)
    {
        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            false,
            colorHex,
            "Azure",
            false,
            null,
            null);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}

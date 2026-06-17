using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

public sealed class OutlookDrilldownViewModelTests
{
    private readonly DateOnly _date = new(2026, 6, 10);
    private readonly Mock<IOutlookEventRepository> _repository = new();
    private readonly Mock<IRuleEngineService> _ruleEngine = new();

    [Fact]
    public async Task ToggleSuppressAsync_RerunsOutlookRulesBeforeRefresh()
    {
        var events = new List<OutlookEvent> { MakeEvent("oe-1") };
        _repository
            .Setup(r => r.GetEventsForDateAsync(_date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);
        var vm = new OutlookDrilldownViewModel(_repository.Object, _ruleEngine.Object);
        await vm.LoadAsync(_date);

        await vm.ToggleSuppressAsync("oe-1", suppress: true);

        _repository.Verify(r => r.SetSuppressedAsync("oe-1", true, It.IsAny<CancellationToken>()), Times.Once);
        _ruleEngine.Verify(r => r.RunForImportAsync(OutlookImportService.SourceKey, It.IsAny<CancellationToken>()), Times.Once);
        vm.Items.Should().ContainSingle();
    }

    private static OutlookEvent MakeEvent(string id)
    {
        var start = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
        return new OutlookEvent
        {
            OutlookEventId = id,
            Subject = "Project Sync",
            StartDatetime = start,
            EndDatetime = start.AddHours(1),
            LastSyncedAt = DateTime.UtcNow
        };
    }
}

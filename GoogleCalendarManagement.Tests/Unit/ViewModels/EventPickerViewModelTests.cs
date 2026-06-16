using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class EventPickerViewModelTests
{
    private static readonly DateTimeOffset RangeStart = new(2026, 6, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RangeEnd = new(2026, 6, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IEventPickerService> _pickerService = new();
    private readonly Mock<ILinkService> _linkService = new();

    public EventPickerViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPickerResult([], []));
    }

    private EventPickerViewModel CreateVm(IReadOnlyList<int>? dataPointIds = null) =>
        new(_pickerService.Object, _linkService.Object, RangeStart, RangeEnd, dataPointIds ?? [1], dispatcherQueue: null);

    [Fact]
    public async Task ConfirmLinkCommand_CanExecute_FalseInitially_TrueAfterSelectedItemSet()
    {
        var vm = CreateVm();
        await vm.LoadAsync(null);

        vm.ConfirmLinkCommand.CanExecute(null).Should().BeFalse();

        vm.SelectedItem = new EventPickerItem("evt-1", "Summary", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", false);

        vm.ConfirmLinkCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_TrueWhenServiceReturnsEmptyLists()
    {
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPickerResult([], []));

        var vm = CreateVm();
        await vm.LoadAsync(null);

        vm.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_FalseWhenServiceReturnsItems()
    {
        var item = new EventPickerItem("evt-1", "Event", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", true);
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPickerResult([item], []));

        var vm = CreateVm();
        await vm.LoadAsync(null);

        vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task SearchResults_AreNotOverwrittenBySlowInitialLoad()
    {
        var initialItem = new EventPickerItem("evt-initial", "Initial", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", true);
        var searchItem = new EventPickerItem("evt-search", "Search", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", true);
        var initialLoad = new TaskCompletionSource<EventPickerResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var searchLoad = new TaskCompletionSource<EventPickerResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()))
            .Returns(initialLoad.Task);
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), "search", It.IsAny<CancellationToken>()))
            .Returns(searchLoad.Task);

        var vm = CreateVm();
        vm.SearchText = "search";

        await Task.Delay(350);
        searchLoad.SetResult(new EventPickerResult([searchItem], []));
        await Task.Delay(50);

        vm.ConcurrentEvents.Should().ContainSingle(e => e.EventId == "evt-search");

        initialLoad.SetResult(new EventPickerResult([initialItem], []));
        await Task.Delay(50);

        vm.ConcurrentEvents.Should().ContainSingle(e => e.EventId == "evt-search");
    }

    [Fact]
    public async Task LoadAsync_SetsErrorMessage_WhenPickerServiceThrows()
    {
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Picker failed"));

        var vm = CreateVm();

        await vm.LoadAsync(null);

        vm.ErrorMessage.Should().Be("Picker failed");
        vm.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmLinkCommand_CallsLinkAsync_WithCorrectArgs_ForSingleDataPoint()
    {
        var item = new EventPickerItem("evt-abc", "Event", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", true);
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPickerResult([item], []));

        _linkService
            .Setup(s => s.LinkAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("action-group-1");

        var vm = CreateVm(dataPointIds: [42]);
        await vm.LoadAsync(null);
        vm.SelectedItem = item;

        await vm.ConfirmLinkCommand.ExecuteAsync(null);

        _linkService.Verify(s => s.LinkAsync(42, "evt-abc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmLinkCommand_SetsErrorMessage_WhenLinkServiceThrows()
    {
        var item = new EventPickerItem("evt-err", "Event", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, "#22874A", false);
        _pickerService
            .Setup(s => s.GetCandidatesAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EventPickerResult([], [item]));

        _linkService
            .Setup(s => s.LinkAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Link failed"));

        var vm = CreateVm();
        await vm.LoadAsync(null);
        vm.SelectedItem = item;

        await vm.ConfirmLinkCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Link failed");
        vm.HasError.Should().BeTrue();
    }
}

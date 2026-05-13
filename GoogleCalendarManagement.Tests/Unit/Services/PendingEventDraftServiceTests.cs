using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class PendingEventDraftServiceTests
{
    [Fact]
    public async Task CreateDraftAsync_WithoutSummary_CreatesUntitledDraft()
    {
        var repositoryMock = new Mock<IPendingEventRepository>();
        PendingEvent? storedDraft = null;
        repositoryMock
            .Setup(repository => repository.UpsertAsync(It.IsAny<PendingEvent>(), It.IsAny<CancellationToken>()))
            .Callback<PendingEvent, CancellationToken>((pendingEvent, _) => storedDraft = pendingEvent)
            .Returns(Task.CompletedTask);
        var service = new PendingEventDraftService(
            repositoryMock.Object,
            new FixedTimeProvider(new DateTimeOffset(new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc))));

        var draft = await service.CreateDraftAsync(
            new DateTime(2026, 5, 13, 9, 0, 0, DateTimeKind.Local),
            new DateTime(2026, 5, 13, 10, 0, 0, DateTimeKind.Local));

        draft.Summary.Should().BeNull();
        storedDraft.Should().NotBeNull();
        storedDraft!.Summary.Should().BeNull();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}

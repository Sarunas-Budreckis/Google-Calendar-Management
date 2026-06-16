using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Tests.Integration;

public sealed class LinkServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public LinkServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();

        _contextFactory = new TestDbContextFactory(options);
    }

    public void Dispose() => _connection.Dispose();

    private ILinkService CreateService() => new LinkService(_contextFactory);

    [Fact]
    public async Task LinkAsync_CreatesLinkRow_WithCorrectFields()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();

        var groupId = await service.LinkAsync(dpId, "evt-1");

        groupId.Should().NotBeNullOrWhiteSpace();
        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("linked");
        row.Origin.Should().Be("manual");
        row.EventId.Should().Be("evt-1");
        row.ActionGroupId.Should().Be(groupId);
        row.RuleId.Should().BeNull();
    }

    [Fact]
    public async Task IgnoreAsync_CreatesIgnoreRow_WithNullEventId()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        await service.IgnoreAsync(dpId);

        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("ignored");
        row.Origin.Should().Be("manual");
        row.EventId.Should().BeNull();
    }

    [Fact]
    public async Task UnlinkAsync_RemovesRow_WhenRowExists()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();
        await service.LinkAsync(dpId, "evt-1");

        await service.UnlinkAsync(dpId);

        (await GetLinkRowAsync(dpId)).Should().BeNull();
    }

    [Fact]
    public async Task UnlinkAsync_IsNoOp_WhenRowAbsent()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        var act = async () => await service.UnlinkAsync(dpId);

        await act.Should().NotThrowAsync();
        (await GetLinkRowAsync(dpId)).Should().BeNull();
    }

    [Fact]
    public async Task LinkClumpAsync_WritesNRows_UnderSameActionGroupId()
    {
        var dp1 = await SeedDataPointAsync("spotify_stream");
        var dp2 = await SeedDataPointAsync("spotify_stream");
        var dp3 = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();

        var groupId = await service.LinkClumpAsync(new[] { dp1, dp2, dp3 }, "evt-1");

        var rows = await service.GetLinksByActionGroupAsync(groupId);
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(r => r.ActionGroupId == groupId);
        rows.Should().OnlyContain(r => r.State == "linked" && r.EventId == "evt-1");
    }

    [Fact]
    public async Task LinkClumpAsync_DuplicateDataPointIds_WritesOneRowAndUndoSnapshot()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();

        var groupId = await service.LinkClumpAsync(new[] { dpId, dpId }, "evt-1");

        (await service.GetLinksByActionGroupAsync(groupId)).Should().ContainSingle();
        await service.UndoActionGroupAsync(groupId);
        (await GetLinkRowAsync(dpId)).Should().BeNull();
    }

    [Fact]
    public async Task LinkClumpAsync_Throws_WhenInputEmpty()
    {
        await SeedEventAsync("evt-1");
        var service = CreateService();

        var act = async () => await service.LinkClumpAsync(Array.Empty<int>(), "evt-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LinkClumpAsync_Throws_WhenInputNull()
    {
        await SeedEventAsync("evt-1");
        var service = CreateService();

        var act = async () => await service.LinkClumpAsync(null!, "evt-1");

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UndoActionGroupAsync_RestoresPreviousState_WhenWasUnlinked()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();
        var groupId = await service.LinkAsync(dpId, "evt-1");

        await service.UndoActionGroupAsync(groupId);

        (await GetLinkRowAsync(dpId)).Should().BeNull();
    }

    [Fact]
    public async Task UndoActionGroupAsync_RestoresPreviousState_WhenWasLinkedToOtherEvent()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-A");
        await SeedEventAsync("evt-B");
        var service = CreateService();
        await service.LinkAsync(dpId, "evt-A");

        var groupId = await service.LinkAsync(dpId, "evt-B");
        await service.UndoActionGroupAsync(groupId);

        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("linked");
        row.EventId.Should().Be("evt-A");
    }

    [Fact]
    public async Task UndoActionGroupAsync_IsNoOp_WhenActionGroupSuperseded()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-A");
        await SeedEventAsync("evt-B");
        var service = CreateService();
        var supersededGroupId = await service.LinkAsync(dpId, "evt-A");
        await service.LinkAsync(dpId, "evt-B");

        await service.UndoActionGroupAsync(supersededGroupId);

        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.EventId.Should().Be("evt-B");
        row.State.Should().Be("linked");
    }

    [Fact]
    public async Task UndoActionGroupAsync_IsNoOp_WhenGroupIdUnknown()
    {
        var service = CreateService();

        var act = async () => await service.UndoActionGroupAsync(Guid.NewGuid().ToString("N"));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CascadeDelete_DeletesLinkRow_WhenDataPointDeleted()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();
        await service.LinkAsync(dpId, "evt-1");

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var dp = await context.DataPoints.SingleAsync(d => d.DataPointId == dpId);
            context.DataPoints.Remove(dp);
            await context.SaveChangesAsync();
        }

        (await GetLinkRowAsync(dpId)).Should().BeNull();
    }

    [Fact]
    public async Task EventDelete_Throws_WhenLinkReferencesEvent()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();
        await service.LinkAsync(dpId, "evt-1");

        await using var context = await _contextFactory.CreateDbContextAsync();
        var eventRow = await context.Events.SingleAsync(e => e.EventId == "evt-1");
        context.Events.Remove(eventRow);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task WriteAutoLinkAsync_DoesNotOverwrite_ManualRow()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        await SeedEventAsync("evt-2");
        var service = CreateService();
        await service.LinkAsync(dpId, "evt-1");

        var act = async () => await service.WriteAutoLinkAsync(dpId, "evt-2", "rule-x");

        await act.Should().ThrowAsync<InvalidOperationException>();
        var row = await GetLinkRowAsync(dpId);
        row!.Origin.Should().Be("manual");
        row.EventId.Should().Be("evt-1");
    }

    [Fact]
    public async Task WriteAutoLinkAsync_Overwrites_ExistingAutoRow()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        await SeedEventAsync("evt-2");
        var service = CreateService();
        await service.WriteAutoLinkAsync(dpId, "evt-1", "rule-x");

        await service.WriteAutoLinkAsync(dpId, "evt-2", "rule-y");

        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.Origin.Should().Be("auto_rule");
        row.EventId.Should().Be("evt-2");
        row.RuleId.Should().Be("rule-y");
    }

    [Fact]
    public async Task WriteAutoIgnoreAsync_CreatesIgnoreRow_WithRuleId()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        await service.WriteAutoIgnoreAsync(dpId, "rule-ignore");

        var row = await GetLinkRowAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("ignored");
        row.Origin.Should().Be("auto_rule");
        row.EventId.Should().BeNull();
        row.RuleId.Should().Be("rule-ignore");
    }

    [Fact]
    public async Task WriteAutoIgnoreAsync_Throws_WhenRuleIdMissing()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        var act = async () => await service.WriteAutoIgnoreAsync(dpId, "");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task IgnoreClumpAsync_AndUnlinkClumpAsync_UpdateAllRows()
    {
        var dp1 = await SeedDataPointAsync("spotify_stream");
        var dp2 = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        var ignoreGroupId = await service.IgnoreClumpAsync(new[] { dp1, dp2 });
        var ignoredRows = await service.GetLinksByActionGroupAsync(ignoreGroupId);
        ignoredRows.Should().HaveCount(2);
        ignoredRows.Should().OnlyContain(r => r.State == "ignored" && r.EventId == null);

        await service.UnlinkClumpAsync(new[] { dp1, dp2 });

        (await GetLinkRowAsync(dp1)).Should().BeNull();
        (await GetLinkRowAsync(dp2)).Should().BeNull();
    }

    [Fact]
    public async Task GetLinkAsync_AndGetLinksByEventAsync_ReturnExpectedRows()
    {
        var dp1 = await SeedDataPointAsync("spotify_stream");
        var dp2 = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        await SeedEventAsync("evt-2");
        var service = CreateService();
        await service.LinkAsync(dp1, "evt-1");
        await service.LinkAsync(dp2, "evt-2");

        var link = await service.GetLinkAsync(dp1);
        var eventLinks = await service.GetLinksByEventAsync("evt-1");

        link.Should().NotBeNull();
        link!.DataPointId.Should().Be(dp1);
        eventLinks.Should().ContainSingle();
        eventLinks[0].DataPointId.Should().Be(dp1);
    }

    [Fact]
    public async Task UniqueConstraint_Enforced_AtDbLevel()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var now = DateTime.UtcNow;

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Links.Add(new Link
        {
            DataPointId = dpId, EventId = "evt-1", State = "linked", Origin = "manual",
            ActionGroupId = "g1", CreatedAt = now, UpdatedAt = now
        });
        context.Links.Add(new Link
        {
            DataPointId = dpId, EventId = "evt-1", State = "linked", Origin = "manual",
            ActionGroupId = "g2", CreatedAt = now, UpdatedAt = now
        });

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData("bad_state", "manual")]
    [InlineData("linked", "bad_origin")]
    public async Task StateAndOriginConstraints_Enforced_AtDbLevel(string state, string origin)
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var now = DateTime.UtcNow;

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Links.Add(new Link
        {
            DataPointId = dpId,
            EventId = "evt-1",
            State = state,
            Origin = origin,
            ActionGroupId = "g1",
            CreatedAt = now,
            UpdatedAt = now
        });

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task UndoClump_RestoresAllNDatapoints_InOneStep()
    {
        var dp1 = await SeedDataPointAsync("spotify_stream");
        var dp2 = await SeedDataPointAsync("spotify_stream");
        var dp3 = await SeedDataPointAsync("spotify_stream");
        await SeedEventAsync("evt-1");
        var service = CreateService();
        var groupId = await service.LinkClumpAsync(new[] { dp1, dp2, dp3 }, "evt-1");

        await service.UndoActionGroupAsync(groupId);

        (await GetLinkRowAsync(dp1)).Should().BeNull();
        (await GetLinkRowAsync(dp2)).Should().BeNull();
        (await GetLinkRowAsync(dp3)).Should().BeNull();
    }

    [Fact]
    public async Task LinkAsync_Throws_WhenEventIdMissing()
    {
        var dpId = await SeedDataPointAsync("spotify_stream");
        var service = CreateService();

        var act = async () => await service.LinkAsync(dpId, "");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- Seed helpers ---

    private async Task<int> SeedDataPointAsync(string sourceKey)
    {
        var now = DateTime.UtcNow;
        var dp = new DataPoint
        {
            SourceKey = sourceKey,
            SourceRef = Guid.NewGuid().ToString("N"),
            StartUtc = now,
            EndUtc = now.AddMinutes(30),
            CreatedAt = now
        };

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.DataPoints.Add(dp);
        await context.SaveChangesAsync();
        return dp.DataPointId;
    }

    private async Task SeedEventAsync(string eventId)
    {
        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Events.Add(new Event
        {
            EventId = eventId,
            Lifecycle = "approved",
            Publish = "local_only",
            HasUnpublishedChanges = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private async Task<Link?> GetLinkRowAsync(int dataPointId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Links.AsNoTracking()
            .SingleOrDefaultAsync(l => l.DataPointId == dataPointId);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;

        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options)
        {
            _options = options;
        }

        public CalendarDbContext CreateDbContext() => new(_options);

        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}

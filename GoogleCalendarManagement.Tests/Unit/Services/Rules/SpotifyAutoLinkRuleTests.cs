using FluentAssertions;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.Services.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GoogleCalendarManagement.Tests.Unit.Services.Rules;

/// <summary>
/// Story 8.15 — <see cref="SpotifyAutoLinkRule"/>. Covers the three coverage cases (single / multi /
/// zero), source filtering, lifecycle filtering, and the end-to-end reversal + manual-sacred
/// guarantees through the real <see cref="RuleEngineService"/>.
/// </summary>
public sealed class SpotifyAutoLinkRuleTests : IDisposable
{
    private const string Spotify = "spotify";

    private static readonly DateTime BaseUtc =
        DateTime.SpecifyKind(new DateTime(2026, 6, 10, 12, 0, 0), DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public SpotifyAutoLinkRuleTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(_connection).Options;
        using var context = new CalendarDbContext(options);
        context.Database.EnsureCreated();
        _contextFactory = new TestDbContextFactory(options);
    }

    public void Dispose() => _connection.Dispose();

    // --- Pure rule tests (ProposeOpsAsync decision logic) ---

    [Fact] // AC #1
    public async Task SingleCover_ProposesLinkToThatEvent()
    {
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var dp = Eligible(1, BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { dp }, default);

        ops.Should().ContainSingle();
        ops[0].Kind.Should().Be(ProposedOpKind.Link);
        ops[0].DataPointId.Should().Be(1);
        ops[0].EventId.Should().Be("evt-1");
        ops[0].RuleId.Should().Be(SpotifyAutoLinkRule.Id);
    }

    [Fact] // AC #2
    public async Task MultiCover_ProposesNothing()
    {
        await SeedEventAsync("evt-A", BaseUtc, BaseUtc.AddMinutes(30));
        await SeedEventAsync("evt-B", BaseUtc, BaseUtc.AddMinutes(30));
        var dp = Eligible(1, BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { dp }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // AC #3
    public async Task ZeroCover_ProposesNothing()
    {
        await SeedEventAsync("evt-far", BaseUtc.AddHours(5), BaseUtc.AddHours(6));
        var dp = Eligible(1, BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { dp }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // AC #1 — only approved events count toward coverage
    public async Task CandidateEvent_DoesNotCountAsCover()
    {
        await SeedEventAsync("cand-1", BaseUtc, BaseUtc.AddMinutes(30), lifecycle: "candidate");
        var dp = Eligible(1, BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { dp }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // AC #1 — soft-deleted events do not count
    public async Task DeletedEvent_DoesNotCountAsCover()
    {
        await SeedEventAsync("evt-del", BaseUtc, BaseUtc.AddMinutes(30), isDeleted: true);
        var dp = Eligible(1, BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { dp }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // The rule only acts on its own source's datapoints
    public async Task IgnoresNonSpotifyEligibleDatapoints()
    {
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var togglDp = new EligibleDataPoint(9, "toggl", BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { togglDp }, default);

        ops.Should().BeEmpty();
    }

    // --- End-to-end through the real engine (AC #1, #4) ---

    [Fact] // AC #1 — single cover writes an auto link
    public async Task Engine_SingleCover_WritesAutoLink()
    {
        var dpId = await SeedDataPointAsync(BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));
        await SeedEventAsync("evt-1", BaseUtc, BaseUtc.AddMinutes(30));
        var engine = CreateEngine();

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("linked");
        row.Origin.Should().Be("auto_rule");
        row.RuleId.Should().Be(SpotifyAutoLinkRule.Id);
        row.EventId.Should().Be("evt-1");
    }

    [Fact] // AC #4 — manual links are never overridden (engine excludes them from eligible)
    public async Task Engine_ManualLink_IsNeverOverridden()
    {
        var dpId = await SeedDataPointAsync(BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));
        await SeedEventAsync("evt-approved", BaseUtc, BaseUtc.AddMinutes(30));
        await SeedEventAsync("evt-manual", BaseUtc, BaseUtc.AddMinutes(30));
        var links = new LinkService(_contextFactory);
        await links.LinkAsync(dpId, "evt-manual"); // manual decision (a different event than the rule would pick)

        await CreateEngine().RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row!.Origin.Should().Be("manual");
        row.EventId.Should().Be("evt-manual");
        row.RuleId.Should().BeNull();
    }

    [Fact] // AC #4 — moving the covering event reverses the stale auto link
    public async Task Engine_Reversal_RemovesAutoLink_WhenCoveringEventMovesAway()
    {
        var dpId = await SeedDataPointAsync(BaseUtc.AddMinutes(5), BaseUtc.AddMinutes(8));
        var oldStart = BaseUtc;
        var oldEnd = BaseUtc.AddMinutes(30);
        await SeedEventAsync("evt-1", oldStart, oldEnd);
        var engine = CreateEngine();

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        (await GetLinkAsync(dpId))!.Origin.Should().Be("auto_rule");

        var newStart = BaseUtc.AddDays(1);
        await MoveEventAsync("evt-1", newStart, newStart.AddMinutes(30));
        await engine.RunForEventEditTimeAsync("evt-1", oldStart, oldEnd);

        (await GetLinkAsync(dpId)).Should().BeNull();
    }

    // --- Helpers ---

    private static RuleScope AnyScope =>
        new(DateOnly.FromDateTime(BaseUtc), DateOnly.FromDateTime(BaseUtc));

    private static RuleScope ScopeFor(DateTime utc)
    {
        var day = DateOnly.FromDateTime(utc.ToLocalTime());
        return new RuleScope(day, day);
    }

    private static EligibleDataPoint Eligible(int id, DateTime startUtc, DateTime endUtc) =>
        new(id, Spotify, startUtc, endUtc);

    private SpotifyAutoLinkRule Rule() => new(_contextFactory);

    private RuleEngineService CreateEngine()
    {
        var eventRepository = new EventRepository(_contextFactory);
        var identity = new EventIdentityService(eventRepository);
        return new RuleEngineService(
            _contextFactory, eventRepository, identity, new LinkService(_contextFactory),
            new ILinkRule[] { new SpotifyAutoLinkRule(_contextFactory) },
            NullLogger<RuleEngineService>.Instance);
    }

    private async Task<int> SeedDataPointAsync(DateTime startUtc, DateTime endUtc)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var dp = new DataPoint
        {
            SourceKey = Spotify,
            SourceRef = Guid.NewGuid().ToString("N"),
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAt = DateTime.UtcNow
        };
        context.DataPoints.Add(dp);
        await context.SaveChangesAsync();
        return dp.DataPointId;
    }

    private async Task SeedEventAsync(
        string eventId, DateTime startUtc, DateTime endUtc,
        string lifecycle = "approved", bool isDeleted = false)
    {
        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Events.Add(new Event
        {
            EventId = eventId,
            Lifecycle = lifecycle,
            Publish = "local_only",
            StartDatetime = startUtc,
            EndDatetime = endUtc,
            IsDeleted = isDeleted,
            HasUnpublishedChanges = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private async Task MoveEventAsync(string eventId, DateTime startUtc, DateTime endUtc)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ev = await context.Events.SingleAsync(e => e.EventId == eventId);
        ev.StartDatetime = startUtc;
        ev.EndDatetime = endUtc;
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task<Link?> GetLinkAsync(int dataPointId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Links.AsNoTracking().SingleOrDefaultAsync(l => l.DataPointId == dataPointId);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<CalendarDbContext>
    {
        private readonly DbContextOptions<CalendarDbContext> _options;
        public TestDbContextFactory(DbContextOptions<CalendarDbContext> options) => _options = options;
        public CalendarDbContext CreateDbContext() => new(_options);
        public Task<CalendarDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(CreateDbContext());
    }
}

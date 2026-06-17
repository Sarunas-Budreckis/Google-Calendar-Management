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
/// Story 8.15 — <see cref="OutlookGenerateCandidateRule"/>. Covers unsuppressed→candidate,
/// suppressed→ignore, idempotency, the suppression toggle (both directions), unresolvable refs, and
/// end-to-end candidate minting through the real <see cref="RuleEngineService"/>.
/// </summary>
public sealed class OutlookGenerateCandidateRuleTests : IDisposable
{
    private const string Outlook = "outlook";

    private static readonly DateTime BaseUtc =
        DateTime.SpecifyKind(new DateTime(2026, 6, 10, 9, 0, 0), DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly TestDbContextFactory _contextFactory;

    public OutlookGenerateCandidateRuleTests()
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

    [Fact] // AC #5
    public async Task Unsuppressed_NoPriorLink_ProposesGenerateCandidate()
    {
        var start = BaseUtc;
        var end = BaseUtc.AddHours(1);
        var dpId = await SeedOutlookAsync("oe-1", "Standup", start, end, isSuppressed: false);

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, start, end) }, default);

        ops.Should().ContainSingle();
        var op = ops[0];
        op.Kind.Should().Be(ProposedOpKind.GenerateCandidate);
        op.DataPointId.Should().Be(dpId);
        op.RuleId.Should().Be(OutlookGenerateCandidateRule.Id);
        op.GeneratedEventSummary.Should().Be("Standup");
        op.GeneratedEventStart.Should().Be(start);
        op.GeneratedEventEnd.Should().Be(end);
        op.GeneratedEventSourceSystem.Should().Be(Outlook);
    }

    [Fact] // AC #6
    public async Task Suppressed_NoPriorLink_ProposesIgnore()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Lunch", BaseUtc, BaseUtc.AddHours(1), isSuppressed: true);

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().ContainSingle();
        ops[0].Kind.Should().Be(ProposedOpKind.Ignore);
        ops[0].DataPointId.Should().Be(dpId);
        ops[0].RuleId.Should().Be(OutlookGenerateCandidateRule.Id);
    }

    [Fact] // AC #7 — already linked candidate → idempotent no-op
    public async Task Unsuppressed_AlreadyLinked_ProposesNothing()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Standup", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        await SeedAutoLinkAsync(
            dpId, state: "linked", eventId: "cand-existing",
            summary: "Standup", startUtc: BaseUtc, endUtc: BaseUtc.AddHours(1));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // AC #5 — re-imported raw fields should refresh an existing candidate
    public async Task Unsuppressed_AlreadyLinkedButCandidateStale_ProposesGenerateCandidate()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Standup Updated", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        await SeedAutoLinkAsync(
            dpId, state: "linked", eventId: "cand-existing",
            summary: "Standup", startUtc: BaseUtc, endUtc: BaseUtc.AddHours(1));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().ContainSingle();
        ops[0].Kind.Should().Be(ProposedOpKind.GenerateCandidate);
        ops[0].GeneratedEventSummary.Should().Be("Standup Updated");
    }

    [Fact] // AC #7 — already ignored → idempotent no-op
    public async Task Suppressed_AlreadyIgnored_ProposesNothing()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Lunch", BaseUtc, BaseUtc.AddHours(1), isSuppressed: true);
        await SeedAutoLinkAsync(dpId, state: "ignored", eventId: null);

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // AC #8 — IsSuppressed toggled false→true on a linked datapoint → flip to ignore
    public async Task ToggleToSuppressed_PreviouslyLinked_ProposesIgnore()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Standup", BaseUtc, BaseUtc.AddHours(1), isSuppressed: true);
        await SeedAutoLinkAsync(dpId, state: "linked", eventId: "cand-existing");

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().ContainSingle();
        ops[0].Kind.Should().Be(ProposedOpKind.Ignore);
    }

    [Fact] // AC #8 — IsSuppressed toggled true→false on an ignored datapoint → re-generate
    public async Task ToggleToUnsuppressed_PreviouslyIgnored_ProposesGenerateCandidate()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Standup", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        await SeedAutoLinkAsync(dpId, state: "ignored", eventId: null);

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().ContainSingle();
        ops[0].Kind.Should().Be(ProposedOpKind.GenerateCandidate);
    }

    [Fact] // empty subject gets a stable placeholder so the engine's required-summary guard passes
    public async Task EmptySubject_UsesPlaceholderSummary()
    {
        var dpId = await SeedOutlookAsync("oe-1", "", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().ContainSingle();
        ops[0].GeneratedEventSummary.Should().Be("(No subject)");
    }

    [Fact] // datapoint whose raw Outlook event is gone → leave for reconciliation
    public async Task UnresolvableSourceRef_ProposesNothing()
    {
        var dpId = await SeedDataPointOnlyAsync("missing-oe", BaseUtc, BaseUtc.AddHours(1));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { Eligible(dpId, BaseUtc, BaseUtc.AddHours(1)) }, default);

        ops.Should().BeEmpty();
    }

    [Fact] // the rule only acts on its own source's datapoints
    public async Task IgnoresNonOutlookEligibleDatapoints()
    {
        var spotifyDp = new EligibleDataPoint(99, "spotify", BaseUtc, BaseUtc.AddHours(1));

        var ops = await Rule().ProposeOpsAsync(AnyScope, new[] { spotifyDp }, default);

        ops.Should().BeEmpty();
    }

    // --- End-to-end through the real engine (AC #5, #6, #7) ---

    [Fact] // AC #5 — generate-candidate mints an outlook-sourced candidate and links it
    public async Task Engine_Unsuppressed_CreatesOutlookCandidateAndLink()
    {
        var start = BaseUtc;
        var end = BaseUtc.AddHours(1);
        var dpId = await SeedOutlookAsync("oe-1", "Project Sync", start, end, isSuppressed: false);

        await CreateEngine().RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("linked");
        row.Origin.Should().Be("auto_rule");
        row.RuleId.Should().Be(OutlookGenerateCandidateRule.Id);
        row.EventId.Should().NotBeNullOrWhiteSpace();

        var candidate = await GetEventAsync(row.EventId!);
        candidate.Should().NotBeNull();
        candidate!.Lifecycle.Should().Be("candidate");
        candidate.Publish.Should().Be("local_only");
        candidate.SourceSystem.Should().Be(Outlook);
        candidate.Summary.Should().Be("Project Sync");
        candidate.ColorId.Should().BeNull();
        candidate.StartDatetime.Should().Be(start);
        candidate.EndDatetime.Should().Be(end);
    }

    [Fact] // AC #7 — re-running produces no new candidate
    public async Task Engine_Idempotent_SecondRunCreatesNoNewCandidate()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Project Sync", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        var engine = CreateEngine();

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));
        var firstEventId = (await GetLinkAsync(dpId))!.EventId;

        await engine.RunPipelineAsync(ScopeFor(BaseUtc));

        (await GetLinkAsync(dpId))!.EventId.Should().Be(firstEventId);
        (await CountCandidatesAsync()).Should().Be(1);
    }

    [Fact] // AC #5 — re-imported Outlook fields refresh the existing generated candidate
    public async Task Engine_ReimportedOutlookEvent_RefreshesExistingCandidate()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Project Sync", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        var engine = CreateEngine();

        await engine.RunForImportAsync(Outlook);
        var candidateId = (await GetLinkAsync(dpId))!.EventId!;

        var newStart = BaseUtc.AddHours(2);
        await UpdateOutlookAsync("oe-1", "Project Sync Updated", newStart, newStart.AddHours(2), isSuppressed: false);
        await UpdateDataPointAsync(dpId, newStart, newStart.AddHours(2));

        await engine.RunForImportAsync(Outlook);

        (await CountCandidatesAsync()).Should().Be(1);
        (await GetLinkAsync(dpId))!.EventId.Should().Be(candidateId);
        var candidate = await GetEventAsync(candidateId);
        candidate!.Summary.Should().Be("Project Sync Updated");
        candidate.StartDatetime.Should().Be(newStart);
        candidate.EndDatetime.Should().Be(newStart.AddHours(2));
    }

    [Fact] // AC #6, #10 — suppressed datapoint is ignored (link row retained, no event)
    public async Task Engine_Suppressed_WritesIgnoredRowWithNoEvent()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Lunch", BaseUtc, BaseUtc.AddHours(1), isSuppressed: true);

        await CreateEngine().RunPipelineAsync(ScopeFor(BaseUtc));

        var row = await GetLinkAsync(dpId);
        row.Should().NotBeNull();
        row!.State.Should().Be("ignored");
        row.Origin.Should().Be("auto_rule");
        row.RuleId.Should().Be(OutlookGenerateCandidateRule.Id);
        row.EventId.Should().BeNull();
        (await CountCandidatesAsync()).Should().Be(0);
    }

    [Fact] // AC #8 — linked→ignored transition removes the generated candidate
    public async Task Engine_ToggleToSuppressed_RemovesGeneratedCandidate()
    {
        var dpId = await SeedOutlookAsync("oe-1", "Project Sync", BaseUtc, BaseUtc.AddHours(1), isSuppressed: false);
        var engine = CreateEngine();
        await engine.RunForImportAsync(Outlook);
        var candidateId = (await GetLinkAsync(dpId))!.EventId!;

        await UpdateOutlookAsync("oe-1", "Project Sync", BaseUtc, BaseUtc.AddHours(1), isSuppressed: true);
        await engine.RunForImportAsync(Outlook);

        var row = await GetLinkAsync(dpId);
        row!.State.Should().Be("ignored");
        row.EventId.Should().BeNull();
        (await GetEventAsync(candidateId)).Should().BeNull();
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
        new(id, Outlook, startUtc, endUtc);

    private OutlookGenerateCandidateRule Rule() => new(_contextFactory);

    private RuleEngineService CreateEngine()
    {
        var eventRepository = new EventRepository(_contextFactory);
        var identity = new EventIdentityService(eventRepository);
        return new RuleEngineService(
            _contextFactory, eventRepository, identity, new LinkService(_contextFactory),
            new ILinkRule[] { new OutlookGenerateCandidateRule(_contextFactory) },
            NullLogger<RuleEngineService>.Instance);
    }

    private async Task<int> SeedOutlookAsync(
        string outlookEventId, string subject, DateTime startUtc, DateTime endUtc, bool isSuppressed)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.OutlookEvents.Add(new OutlookEvent
        {
            OutlookEventId = outlookEventId,
            Subject = subject,
            StartDatetime = startUtc,
            EndDatetime = endUtc,
            LastSyncedAt = DateTime.UtcNow,
            IsSuppressed = isSuppressed
        });
        var dp = new DataPoint
        {
            SourceKey = Outlook,
            SourceRef = outlookEventId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAt = DateTime.UtcNow
        };
        context.DataPoints.Add(dp);
        await context.SaveChangesAsync();
        return dp.DataPointId;
    }

    private async Task<int> SeedDataPointOnlyAsync(string sourceRef, DateTime startUtc, DateTime endUtc)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var dp = new DataPoint
        {
            SourceKey = Outlook,
            SourceRef = sourceRef,
            StartUtc = startUtc,
            EndUtc = endUtc,
            CreatedAt = DateTime.UtcNow
        };
        context.DataPoints.Add(dp);
        await context.SaveChangesAsync();
        return dp.DataPointId;
    }

    private async Task SeedAutoLinkAsync(
        int dataPointId,
        string state,
        string? eventId,
        string? summary = null,
        DateTime? startUtc = null,
        DateTime? endUtc = null)
    {
        var now = DateTime.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync();
        if (eventId is not null)
        {
            // The link.event_id → event FK requires the referenced row to exist.
            context.Events.Add(new Event
            {
                EventId = eventId,
                Lifecycle = "candidate",
                Publish = "local_only",
                SourceSystem = Outlook,
                Summary = summary,
                StartDatetime = startUtc ?? BaseUtc,
                EndDatetime = endUtc ?? BaseUtc.AddHours(1),
                HasUnpublishedChanges = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        context.Links.Add(new Link
        {
            DataPointId = dataPointId,
            EventId = eventId,
            State = state,
            Origin = "auto_rule",
            RuleId = OutlookGenerateCandidateRule.Id,
            ActionGroupId = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private async Task UpdateOutlookAsync(
        string outlookEventId, string subject, DateTime startUtc, DateTime endUtc, bool isSuppressed)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var ev = await context.OutlookEvents.SingleAsync(e => e.OutlookEventId == outlookEventId);
        ev.Subject = subject;
        ev.StartDatetime = startUtc;
        ev.EndDatetime = endUtc;
        ev.IsSuppressed = isSuppressed;
        ev.LastSyncedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    private async Task UpdateDataPointAsync(int dataPointId, DateTime startUtc, DateTime endUtc)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var dp = await context.DataPoints.SingleAsync(e => e.DataPointId == dataPointId);
        dp.StartUtc = startUtc;
        dp.EndUtc = endUtc;
        await context.SaveChangesAsync();
    }

    private async Task<Link?> GetLinkAsync(int dataPointId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Links.AsNoTracking().SingleOrDefaultAsync(l => l.DataPointId == dataPointId);
    }

    private async Task<Event?> GetEventAsync(string eventId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
    }

    private async Task<int> CountCandidatesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Events.CountAsync(e => e.Lifecycle == "candidate");
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

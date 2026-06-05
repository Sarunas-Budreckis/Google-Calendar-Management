using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneClassificationService : ITogglPhoneClassificationService
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ITogglPhoneRuleRepository _ruleRepository;

    public TogglPhoneClassificationService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ITogglPhoneRuleRepository ruleRepository)
    {
        _contextFactory = contextFactory;
        _ruleRepository = ruleRepository;
    }

    public async Task ClassifyAllAsync(CancellationToken ct = default)
    {
        var activeRules = await _ruleRepository.GetActiveRulesAsync(ct);
        if (activeRules.Count == 0)
        {
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Reset all existing toggl_phone tags first (re-classification is idempotent)
        var existingPhoneEntries = await context.TogglEntries
            .Where(e => e.TogglDataType == TogglDataType.TogglPhone)
            .ToListAsync(ct);

        foreach (var entry in existingPhoneEntries)
        {
            entry.TogglDataType = null;
        }

        // Fetch all untyped entries as candidates (non-sleep, non-transit, and currently null)
        var candidates = await context.TogglEntries
            .Where(e => e.TogglDataType == null || e.TogglDataType == TogglDataType.TogglPhone)
            .ToListAsync(ct);

        foreach (var entry in candidates)
        {
            if (MatchesAnyRule(entry, activeRules))
            {
                entry.TogglDataType = TogglDataType.TogglPhone;
            }
            else
            {
                entry.TogglDataType = null;
            }
        }

        await context.SaveChangesAsync(ct);
    }

    private static bool MatchesAnyRule(TogglEntry entry, IReadOnlyList<TogglPhoneRule> rules)
    {
        var entryDate = DateOnly.FromDateTime(
            entry.StartTime.Kind == DateTimeKind.Utc
                ? entry.StartTime.ToLocalTime()
                : entry.StartTime);

        var durationMinutes = entry.DurationSeconds.HasValue
            ? entry.DurationSeconds.Value / 60.0
            : double.MaxValue;

        foreach (var rule in rules)
        {
            if (rule.DateFrom.HasValue && entryDate < rule.DateFrom.Value)
            {
                continue;
            }

            if (rule.DateTo.HasValue && entryDate > rule.DateTo.Value)
            {
                continue;
            }

            if (!string.Equals(entry.Description, rule.DescriptionPattern, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (rule.MaxDurationMinutes.HasValue && durationMinutes > rule.MaxDurationMinutes.Value)
            {
                continue;
            }

            return true;
        }

        return false;
    }
}

using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class TogglPhoneRuleRepository : ITogglPhoneRuleRepository
{
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;

    public TogglPhoneRuleRepository(IDbContextFactory<CalendarDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<TogglPhoneRule>> GetAllRulesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.TogglPhoneRules
            .AsNoTracking()
            .OrderBy(r => r.DateFrom == null ? 0 : 1)
            .ThenBy(r => r.DateFrom)
            .ThenBy(r => r.DescriptionPattern)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TogglPhoneRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.TogglPhoneRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .ToListAsync(ct);
    }

    public async Task AddRuleAsync(TogglPhoneRule rule, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.TogglPhoneRules.Add(rule);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateRuleAsync(TogglPhoneRule rule, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.TogglPhoneRules.Update(rule);
        await context.SaveChangesAsync(ct);
    }

    public async Task DeactivateRuleAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var rule = await context.TogglPhoneRules.FindAsync([id], ct);
        if (rule is null)
        {
            return;
        }

        rule.IsActive = false;
        await context.SaveChangesAsync(ct);
    }
}

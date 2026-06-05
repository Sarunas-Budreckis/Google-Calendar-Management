using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface ITogglPhoneRuleRepository
{
    Task<IReadOnlyList<TogglPhoneRule>> GetAllRulesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TogglPhoneRule>> GetActiveRulesAsync(CancellationToken ct = default);
    Task AddRuleAsync(TogglPhoneRule rule, CancellationToken ct = default);
    Task UpdateRuleAsync(TogglPhoneRule rule, CancellationToken ct = default);
    Task DeactivateRuleAsync(int id, CancellationToken ct = default);
}

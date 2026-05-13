using System.Security.Cryptography;
using System.Text;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class ConfigRepository : IConfigRepository
{
    private const string SecretConfigType = "secret";

    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<ConfigRepository> _logger;

    public ConfigRepository(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ILogger<ConfigRepository> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<string?> GetConfigValueAsync(string key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var config = await context.Configs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.ConfigKey == key, ct);

        if (config?.ConfigValue is null)
        {
            return null;
        }

        if (!string.Equals(config.ConfigType, SecretConfigType, StringComparison.OrdinalIgnoreCase))
        {
            return config.ConfigValue;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(config.ConfigValue);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogError(ex, "Unable to decrypt secret config value for key {ConfigKey}.", key);
            return null;
        }
    }

    public async Task SetConfigValueAsync(
        string key,
        string? value,
        string? configType = null,
        string? description = null,
        bool encrypt = false,
        CancellationToken ct = default)
    {
        var storedValue = value;
        var storedType = configType;
        if (encrypt && value is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var encryptedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            storedValue = Convert.ToBase64String(encryptedBytes);
            storedType = SecretConfigType;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.Configs.SingleOrDefaultAsync(c => c.ConfigKey == key, ct);
        if (existing is null)
        {
            context.Configs.Add(new Config
            {
                ConfigKey = key,
                ConfigValue = storedValue,
                ConfigType = storedType,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ConfigValue = storedValue;
            existing.ConfigType = storedType;
            existing.Description = description ?? existing.Description;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(ct);
    }
}

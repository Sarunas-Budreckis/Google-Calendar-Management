using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class DpapiTokenStorageService : ITokenStorageService
{
    public const string TokenConfigKey = "GcalTokenResponse";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<DpapiTokenStorageService> _logger;

    public DpapiTokenStorageService(
        IDbContextFactory<CalendarDbContext> contextFactory,
        ILogger<DpapiTokenStorageService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task StoreTokenAsync(TokenResponse tokenResponse)
    {
        var json = JsonSerializer.Serialize(tokenResponse, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var ciphertext = Convert.ToBase64String(encryptedBytes);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Configs.SingleOrDefaultAsync(config => config.ConfigKey == TokenConfigKey);

        if (existing is null)
        {
            context.Configs.Add(new Config
            {
                ConfigKey = TokenConfigKey,
                ConfigValue = ciphertext,
                ConfigType = "secret",
                Description = "Encrypted Google OAuth token response",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.ConfigValue = ciphertext;
            existing.ConfigType = "secret";
            existing.Description = "Encrypted Google OAuth token response";
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task<TokenResponse?> LoadTokenAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tokenConfig = await context.Configs.AsNoTracking()
            .SingleOrDefaultAsync(config => config.ConfigKey == TokenConfigKey);

        if (tokenConfig?.ConfigValue is null)
        {
            return null;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(tokenConfig.ConfigValue);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decryptedBytes);
            return JsonSerializer.Deserialize<TokenResponse>(json, SerializerOptions);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Unable to decrypt Google Calendar token for the current user.");
            return null;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Stored Google Calendar token is not valid Base64.");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Stored Google Calendar token could not be deserialized.");
            return null;
        }
    }

    public async Task ClearTokenAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Configs.SingleOrDefaultAsync(config => config.ConfigKey == TokenConfigKey);
        if (existing is null)
        {
            return;
        }

        context.Configs.Remove(existing);
        await context.SaveChangesAsync();
    }
}

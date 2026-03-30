using Google.Apis.Auth.OAuth2.Responses;

namespace GoogleCalendarManagement.Services;

public interface ITokenStorageService
{
    Task StoreTokenAsync(TokenResponse tokenResponse);

    Task<TokenResponse?> LoadTokenAsync();

    Task ClearTokenAsync();
}

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;

namespace GoogleCalendarManagement.Services;

public interface IGoogleAuthorizationBroker
{
    Task<UserCredential> AuthorizeAsync(
        ClientSecrets clientSecrets,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken);

    UserCredential CreateUserCredential(
        GoogleAuthorizationCodeFlow flow,
        string userId,
        Google.Apis.Auth.OAuth2.Responses.TokenResponse tokenResponse);
}

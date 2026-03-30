using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;

namespace GoogleCalendarManagement.Services;

public sealed class GoogleAuthorizationBrokerAdapter : IGoogleAuthorizationBroker
{
    public Task<UserCredential> AuthorizeAsync(
        ClientSecrets clientSecrets,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken)
    {
        return GoogleWebAuthorizationBroker.AuthorizeAsync(
            clientSecrets,
            scopes,
            "user",
            cancellationToken,
            new NullDataStore(),
            new LocalServerCodeReceiver());
    }

    public UserCredential CreateUserCredential(
        GoogleAuthorizationCodeFlow flow,
        string userId,
        Google.Apis.Auth.OAuth2.Responses.TokenResponse tokenResponse)
    {
        return new UserCredential(flow, userId, tokenResponse);
    }
}

namespace GoogleCalendarManagement.Services;

public sealed class GoogleCalendarOptions
{
    public GoogleCalendarOptions(string appDataDirectory)
    {
        AppDataDirectory = appDataDirectory;
        // OAuth client secrets are intentionally loaded from LocalAppData so they stay outside the repo and git history.
        CredentialsDirectoryPath = Path.Combine(appDataDirectory, "credentials");
        ClientSecretPath = Path.Combine(CredentialsDirectoryPath, "client_secret.json");
    }

    public string AppDataDirectory { get; }

    public string CredentialsDirectoryPath { get; }

    public string ClientSecretPath { get; }
}

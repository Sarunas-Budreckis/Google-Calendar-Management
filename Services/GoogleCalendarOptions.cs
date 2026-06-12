namespace GoogleCalendarManagement.Services;

public sealed class GoogleCalendarOptions
{
    public GoogleCalendarOptions(string projectRoot)
    {
        ProjectRoot = projectRoot;
        CredentialsDirectoryPath = Path.Combine(projectRoot, "credentials");
        ClientSecretPath = Path.Combine(CredentialsDirectoryPath, "client_secret.json");
    }

    public string ProjectRoot { get; }

    public string CredentialsDirectoryPath { get; }

    public string ClientSecretPath { get; }
}

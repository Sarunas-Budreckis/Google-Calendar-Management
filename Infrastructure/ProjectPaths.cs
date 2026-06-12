namespace GoogleCalendarManagement.Infrastructure;

internal static class ProjectPaths
{
    private static string? _cachedRoot;

    public static string GetProjectRoot()
    {
        if (_cachedRoot is not null)
            return _cachedRoot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                _cachedRoot = dir.FullName;
                return _cachedRoot;
            }
            dir = dir.Parent;
        }

        _cachedRoot = AppContext.BaseDirectory;
        return _cachedRoot;
    }
}

# Google Calendar Management

A WinUI 3 desktop application for managing Google Calendar events on Windows.

## Prerequisites

- **Visual Studio 2022** (version 17.9 or later)
  - Workload: **.NET desktop development**
  - Workload: **Windows application development** (includes Windows App SDK components)
- **.NET 9 SDK** (9.0.x) — https://dotnet.microsoft.com/download/dotnet/9.0
- **Windows App SDK 1.8.x** (bundled with the Visual Studio workload above)
- **Windows 10 version 1809** (build 17763) or later

## Getting Started

1. **Clone the repository**
   ```
   git clone <repository-url>
   cd GoogleCalendarManagement
   ```

2. **Open the solution in Visual Studio 2022**
   ```
   GoogleCalendarManagement.sln
   ```

3. **Restore NuGet packages**
   - Packages restore automatically when you open the solution.
   - Or manually: `dotnet restore`

4. **Build the project**
   ```
   dotnet build -p:Platform=x64
   ```

5. **Configure Google Calendar OAuth credentials**
   - Open Google Cloud Console and select or create a project.
   - Enable the **Google Calendar API** for that project.
   - Go to **APIs & Services → Credentials**.
   - Create an **OAuth client ID** for a **Desktop app**.
   - Download the client secret JSON file from Google.
   - Create this folder at the project root if it does not already exist:
     `credentials\`
   - Copy the downloaded file into that folder and rename it to:
     `client_secret.json`
   - The final runtime path must be:
     `[project-root]\credentials\client_secret.json`
   - This file must not be committed to the repository (it is gitignored).
   - The `credentials` folder and `client_secret.json` are intentionally ignored by git and are treated as local-only machine secrets.

6. **Run the application**
   - In Visual Studio: press **F5** (Debug → Start Debugging)
   - The application window opens and runs the database migration service on startup.
   - On first launch, the SQLite database is created at:
     `[project-root]\database\calendar.db`
   - Backups are written to `[project-root]\database\backups\` on each migration run.
   - Logs are written to `[project-root]\logs\`.
   - If `client_secret.json` is missing, the app starts in a disconnected state and logs a warning instead of crashing.

## Build Commands

| Purpose | Command |
|---|---|
| Build (Debug) | `dotnet build -p:Platform=x64 -c Debug` |
| Build (Release) | `dotnet build -p:Platform=x64 -c Release` |
| Run tests | `dotnet test -p:Platform=x64` |
| Publish self-contained (win-x64) | `dotnet publish -p:PublishProfile=win-x64 -c Release` |

The published executable is written to:
`bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\GoogleCalendarManagement.exe`

## Project Structure

```
GoogleCalendarManagement/
├── GoogleCalendarManagement.sln
├── GoogleCalendarManagement.csproj
├── App.xaml / App.xaml.cs          # Application entry point, DI setup
├── Properties/
│   └── PublishProfiles/
│       ├── win-x64.pubxml          # Self-contained publish (x64)
│       ├── win-x86.pubxml
│       └── win-arm64.pubxml
├── Data/                           # EF Core DbContext, entities, migrations
├── Services/                       # MigrationService, LoggingService, ErrorHandlingService
└── GoogleCalendarManagement.Tests/ # xUnit test project
```

## Troubleshooting

### Build fails: "Platform mismatch" or "MSIX requires a specific platform"
Always specify the platform when building the main project:
```
dotnet build -p:Platform=x64
```
Always include `-p:Platform=x64` for tests as well. Without it, the test runner may select the wrong architecture bin path and fail to locate the compiled assembly.

### Hot reload limitations
- **XAML changes** reload live during a Debug session.
- **C# code changes** require stopping and restarting the debugger (hot reload does not apply to C# in WinUI 3).

### ContentDialog throws NullReferenceException (XamlRoot is null)
The window must be activated (`window.Activate()`) before showing any `ContentDialog`. Ensure `dialog.XamlRoot` is set to `window.Content.XamlRoot` after the window is active.

### EF Core migrations
To add a new migration, specify the platform:
```
dotnet ef migrations add <MigrationName> -p:Platform=x64
```

### Published executable fails to find the database
`ProjectPaths.GetProjectRoot()` walks up from `AppContext.BaseDirectory` looking for a directory containing a `*.csproj` file. In a published build (no `.csproj` present), it falls back to `AppContext.BaseDirectory`. Ensure `database/`, `logs/`, and `credentials/` exist relative to the executable in that case.

### Google Calendar connect says `client_secret.json` is missing
The OAuth client secret is not loaded from the repository. It must exist at:
`[project-root]\credentials\client_secret.json`

If your downloaded file has a longer Google-generated name, copy it into that folder and rename it to `client_secret.json`.

### Migrating from the old AppData location
Prior to 2026-06-08, runtime files lived under `%LOCALAPPDATA%\GoogleCalendarManagement\`. To migrate:
1. Copy `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db` → `[project-root]\database\calendar.db`
2. Copy `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\` → `[project-root]\credentials\`
3. The old AppData folder can be deleted afterward.

### Trimming issues in published build
All publish profiles use `PublishTrimmed=false`. EF Core migrations and the WinUI 3 XAML loader use reflection incompatible with IL trimming. Do not enable trimming.

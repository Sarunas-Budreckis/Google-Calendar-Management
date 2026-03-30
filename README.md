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

5. **Run the application**
   - In Visual Studio: press **F5** (Debug → Start Debugging)
   - The application window opens and runs the database migration service on startup.
   - On first launch, the SQLite database is created at:
     `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db`

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
All file paths must use `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`. Hard-coded paths like `C:\Users\...` break the published build on other machines.

### Trimming issues in published build
All publish profiles use `PublishTrimmed=false`. EF Core migrations and the WinUI 3 XAML loader use reflection incompatible with IL trimming. Do not enable trimming.

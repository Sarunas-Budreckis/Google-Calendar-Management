# Story 1.5: Set Up Development Environment and Deployment Pipeline

Status: Approved

## Story

As a **developer**,
I want **a documented development environment setup and optimized build and publish configuration**,
So that **I can build, test, and deploy the application reliably on any compatible Windows machine**.

## Acceptance Criteria

**Given** the project repository
**When** I follow the README setup instructions
**Then** I can build and run the application locally

**And** development environment is documented:
- README.md created at repository root with prerequisites (VS 2022 17.9+, .NET 9 SDK 9.0.x, Windows App SDK 1.8.x)
- Step-by-step setup instructions covering clone, NuGet restore, build, and run
- Common troubleshooting issues documented (MSIX build quirks, Platform=x64 requirement, hot reload limitations)
- Required Visual Studio workloads listed (.NET desktop development, Windows App SDK components)

**And** build configuration is optimized in `GoogleCalendarManagement.csproj`:
- `<Version>1.0.0</Version>` managed in project file (source of truth for app version)
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` enabled for both configurations
- Debug configuration: symbols enabled, no optimization (`<Optimize>false</Optimize>`)
- Release configuration: fully optimized (`<Optimize>true</Optimize>`, `<DebugType>none</DebugType>`)

**And** self-contained publish is functional for win-x64:
- Existing publish profiles at `Properties/PublishProfiles/win-x64.pubxml` (SelfContained=true, PublishTrimmed in Release) produce a working executable
- `dotnet publish -p:PublishProfile=win-x64 -c Release` completes without errors
- Published `.exe` launches on a clean Windows 10/11 machine without a pre-installed .NET runtime
- Database migrations (`MigrationService.RunStartupAsync()`) execute correctly in the published build
- No hard-coded paths — all paths use `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` or equivalent

**And** the deployment artifact is validated:
- All required DLL dependencies are present in publish output folder
- Application creates `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db` on first launch
- No unhandled exceptions on first run of published build

## Tasks / Subtasks

- [ ] Create `README.md` at repository root (AC: 1)
  - [ ] Write prerequisites section: VS 2022 (17.9+), .NET 9 SDK, Windows App SDK 1.8.x, Windows 10 1809+
  - [ ] Write step-by-step setup: clone → open `.sln` → restore NuGet → build (`Platform=x64`) → run
  - [ ] Document build commands: `dotnet build -p:Platform=x64`, `dotnet test`, `dotnet publish -p:PublishProfile=win-x64 -c Release`
  - [ ] Document required VS workloads: ".NET desktop development" + "Windows application development"
  - [ ] Add troubleshooting section: MSIX packaging quirks, `Platform` requirement, hot reload XAML-only limitation, `ContentDialog` XamlRoot requirement

- [ ] Add version number and build configuration properties to `GoogleCalendarManagement.csproj` (AC: 2)
  - [ ] Add `<Version>1.0.0</Version>` to the main `<PropertyGroup>`
  - [ ] Add `<AssemblyVersion>1.0.0.0</AssemblyVersion>` and `<FileVersion>1.0.0.0</FileVersion>`
  - [ ] Add Debug-specific `<PropertyGroup Condition="'$(Configuration)' == 'Debug'">` with `<Optimize>false</Optimize>` and `<DebugType>full</DebugType>`
  - [ ] Add Release-specific `<PropertyGroup Condition="'$(Configuration)' == 'Release'">` with `<Optimize>true</Optimize>` and `<DebugType>none</DebugType>`
  - [ ] Add `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` to main `<PropertyGroup>`
  - [ ] Build Debug — verify no errors, verify PDB symbols present in `bin/Debug/`
  - [ ] Build Release — verify no errors, verify no PDB in `bin/Release/`

- [ ] Validate publish profiles and self-contained publish (AC: 3)
  - [ ] Review `Properties/PublishProfiles/win-x64.pubxml` — confirm `SelfContained=true` and `PublishTrimmed=true` (Release) are correct
  - [ ] Run `dotnet publish -p:PublishProfile=win-x64 -c Release` — confirm it succeeds
  - [ ] Verify `bin/Release/net9.0-windows10.0.19041.0/win-x64/publish/` contains the `.exe` and all dependency DLLs
  - [ ] Launch published `.exe` locally — confirm main window opens and no startup exceptions
  - [ ] Confirm `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db` is created on first launch
  - [ ] Confirm `MigrationService` runs successfully in published build (check via logs or absence of error dialog)

- [ ] Final validation (All ACs)
  - [ ] Build Debug — no errors, symbols present
  - [ ] Build Release — no errors, no symbols
  - [ ] Publish win-x64 — succeeds end-to-end
  - [ ] Published `.exe` launches on local machine — database created, no error dialogs
  - [ ] Run full test suite — all tests pass
  - [ ] Review README for accuracy against actual project structure

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- .NET 9.0.12 (net9.0-windows10.0.19041.0 target framework)
- Windows App SDK 1.8.x with WinUI 3 (`UseWinUI=true`)
- MSIX tooling enabled but `WindowsPackageType=None` — app runs unpackaged, enabling `dotnet run` and simpler self-contained publish
- Publish profiles at `Properties/PublishProfiles/win-{arch}.pubxml` for x64, x86, ARM64

**Critical Architecture Decisions:**
- **Version in .csproj, not Assembly:** Use `<Version>` in the `.csproj` PropertyGroup as the single source of truth. This version flows into the published binary. Do not maintain version in `AssemblyInfo.cs` or elsewhere.
- **TreatWarningsAsErrors:** Adding this will fail the build on any currently suppressed warning. Audit existing warnings first with a clean build before enabling globally. If existing suppressions exist, address them rather than adding `<NoWarn>` entries.
- **Trimming Compatibility:** `PublishTrimmed=true` is already in the Release publish profile. WinUI 3 and EF Core can have trimming issues. If `dotnet publish` with trimming produces a non-functional app, set `<PublishTrimmed>false</PublishTrimmed>` in the Release profile and document the reason.
- **No IHostedService:** MigrationService (Story 1.4) is wired via explicit `App.OnLaunched` call, not Generic Host. This remains unchanged — Story 1.5 only validates it works in the published artifact.

**Build Configuration Reference — `.csproj` additions:**
```xml
<PropertyGroup>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0.0</AssemblyVersion>
  <FileVersion>1.0.0.0</FileVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <Optimize>false</Optimize>
  <DebugType>full</DebugType>
  <DebugSymbols>true</DebugSymbols>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <Optimize>true</Optimize>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

**Publish Command (CLI):**
```bash
dotnet publish GoogleCalendarManagement.csproj -p:PublishProfile=win-x64 -c Release
```

**Build Command (requires Platform specification):**
```bash
dotnet build -p:Platform=x64 -c Release
dotnet build -p:Platform=x64 -c Debug
```

**README.md Outline:**
```
# Google Calendar Management

## Prerequisites
- Visual Studio 2022 (17.9+) with "Windows application development" workload
- .NET 9 SDK (9.0.x) — https://dotnet.microsoft.com/download/dotnet/9.0
- Windows App SDK 1.8.x (bundled with VS 2022 workload)
- Windows 10 version 1809 or later

## Getting Started
1. Clone repository
2. Open `GoogleCalendarManagement.sln` in Visual Studio 2022
3. Restore NuGet packages (automatic on open, or: Tools → NuGet Package Manager → Restore)
4. Build: Build → Build Solution (or `dotnet build -p:Platform=x64`)
5. Run: Debug → Start Debugging (F5)

## Build Commands
dotnet build -p:Platform=x64 -c Debug
dotnet build -p:Platform=x64 -c Release
dotnet test
dotnet publish -p:PublishProfile=win-x64 -c Release

## Troubleshooting
- Build fails with "Platform mismatch": always specify -p:Platform=x64 (or x86/ARM64)
- Hot reload: XAML changes reload live; C# changes require restart
- ContentDialog crash (XamlRoot null): window must be activated before showing dialogs
- EF Core migrations: run `dotnet ef migrations add <Name> -p:Platform=x64` to add migrations
```

### Project Structure After Story 1.5

```
GoogleCalendarManagement/                         # repository root
├── README.md                                     # New: developer setup guide
├── GoogleCalendarManagement.sln
├── GoogleCalendarManagement.csproj               # Updated: Version, TreatWarningsAsErrors, Debug/Release configs
├── Properties/
│   └── PublishProfiles/
│       ├── win-x64.pubxml                        # Validated: SelfContained=true
│       ├── win-x86.pubxml
│       └── win-arm64.pubxml
├── App.xaml.cs                                   # Unchanged from Story 1.4
├── Services/
│   ├── IMigrationService.cs                      # From Story 1.4
│   └── MigrationService.cs                       # From Story 1.4
└── Data/                                         # From Stories 1.2-1.3
GoogleCalendarManagement.Tests/                   # Unchanged from Story 1.4
```

### References

**Source Documents:**
- [Epic 1: Story 1.5 Definition](../epics.md#story-15-set-up-development-environment-and-deployment-pipeline)
- [Epic 1 Tech Spec: AC-1.5](../tech-spec-epic-1.md#ac-15-development-environment-story-15)
- [Epic 1 Tech Spec: Development Tools](../tech-spec-epic-1.md#development-tools-story-15)
- [Epic 1 Tech Spec: Story 1.5 Manual Tests](../tech-spec-epic-1.md#story-15-development-environment)

**Specific Technical Mandates:**
- **NFR-M1 (Code Quality):** Version in project file, TreatWarningsAsErrors, proper build configurations
- **NFR-M4 (Documentation):** README with setup instructions, workload requirements, and troubleshooting

**Prerequisites:**
- Story 1.1 complete — WinUI 3 project structure, .csproj, publish profiles in place
- Story 1.2 complete — EF Core configured, WAL mode set
- Story 1.3 complete — Core schema tables, entities in place
- Story 1.4 complete — MigrationService wired in `App.OnLaunched`; must work in published build

### Common Troubleshooting

- If `TreatWarningsAsErrors` causes build failures: run `dotnet build -p:Platform=x64 2>&1` first to list all warnings, address each before enabling the flag
- If `PublishTrimmed=true` causes runtime failures (trimmed-away reflection dependencies in EF Core or WinUI 3): set `<PublishTrimmed>false</PublishTrimmed>` in the publish profile and document why
- If published `.exe` fails to find `calendar.db` location: verify `MigrationService` uses `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` — no hard-coded paths
- If database migrations fail in published build: ensure `dotnet ef` migrations are embedded — EF Core design-time tools are `PrivateAssets=all` and not needed at runtime
- If `ContentDialog` fails in published build: `WindowsPackageType=None` keeps the app unpackaged — ensure this property is set in `.csproj`

### Testing Strategy

**Story 1.5 Testing Scope:**
- No automated unit or integration tests for this story — deliverables are documentation, build configuration, and publish validation
- All acceptance criteria validated via manual build/publish/launch steps
- Existing test suite (Stories 1.2-1.4) must continue to pass after `.csproj` changes
- If `TreatWarningsAsErrors` breaks the test project build, address in `GoogleCalendarManagement.Tests.csproj` separately

**Manual Validation Checklist:**
1. `dotnet build -p:Platform=x64 -c Debug` — succeeds, PDB present in `bin/Debug/`
2. `dotnet build -p:Platform=x64 -c Release` — succeeds, no PDB in `bin/Release/`
3. `dotnet test` — all existing tests pass
4. `dotnet publish -p:PublishProfile=win-x64 -c Release` — succeeds, `.exe` in publish folder
5. Launch published `.exe` — window opens, no error dialog, `calendar.db` created in AppData

### Change Log

**Version 1.0 - Initial Draft (2026-03-27)**
- Created from Epic 1, Story 1.5 definition in epics.md and AC-1.5 in tech-spec-epic-1.md
- Dev Notes include actual `.csproj` additions for version, TreatWarningsAsErrors, Debug/Release configs
- Noted existing publish profiles in `Properties/PublishProfiles/` — validation focus, not creation
- Trimming caveat added: WinUI 3 / EF Core trim compatibility must be tested before declaring AC-3 done
- No automated tests for this story — all validation is manual build/publish/launch

## Dev Agent Record

### Context Reference

- [Story Context XML](1-5-set-up-development-environment-and-deployment-pipeline.context.xml) - Generated 2026-03-27

*(Completion notes populated when implementation begins)*

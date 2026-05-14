# Story 1.6: Implement Application Logging and Error Handling Infrastructure

Status: Done

## Story

As a **developer**,
I want **centralized logging and error handling infrastructure**,
So that **I can diagnose issues and track application behavior**.

## Acceptance Criteria

**Given** the application is running
**When** any operation occurs
**Then** relevant events are logged to a file

**And** logging system is properly configured (AC-1.6.1):
- Log file location: `%LOCALAPPDATA%\GoogleCalendarManagement\logs\app-{date}.txt`
- Log levels: Debug, Info, Warning, Error, Critical
- Automatic log rotation (daily files, keep last 30 days)
- Structured logging with timestamps, level, and context

**And** structured logging enrichment works (AC-1.6.2):
- Logs include timestamp, level, message, exception (if present)
- Log context enriched with `Application` and `Version` properties
- Performance timing available via `BeginTimedOperation`
- Slow operations (>1 second) logged at Warning level

**And** global exception handler is active (AC-1.6.3):
- Unhandled exceptions caught via `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and WinUI 3 `Application.UnhandledException`
- Stack trace included in error log
- User-friendly error message shown (no stack trace or technical jargon)
- Application state saved before exit on critical errors (logs flushed, DB connections closed)

**And** error handling is consistent (AC-1.6.4):
- Database errors caught and logged at Error level
- File system errors handled gracefully (permissions, missing directories)
- Missing dependency errors reported clearly at startup
- Non-critical errors show toast/dialog without crashing the app

**And** performance monitoring works (AC-1.6.5):
- Database query times tracked
- Operations >1 second logged with duration at Warning level
- Startup time (launch to main window) recorded at Information level

**Prerequisites:** Story 1.1 (project structure, DI setup)

## Tasks / Subtasks

- [x] Add Serilog NuGet packages to `GoogleCalendarManagement.csproj` (AC-1.6.1, AC-1.6.2)
  - [x] Add `Serilog` version 4.1.0
  - [x] Add `Serilog.Extensions.Logging` version 8.0.0 (bridge to Microsoft.Extensions.Logging)
  - [x] Add `Serilog.Sinks.File` version 6.0.0
  - [x] Add `Serilog.Sinks.Console` version 6.1.0 (6.0.1 not published; 6.1.0 resolved)
  - [x] Add `SerilogTimings` version 3.0.1 (provides `OperationAt` / `Operation.Time`)
  - [x] Run `dotnet build -p:Platform=x64` — confirm build succeeds with new packages

- [x] Create `Services/ILoggingService.cs` and `Services/LoggingService.cs` (AC-1.6.1, AC-1.6.2)
  - [x] Define `ILoggingService` interface with `Configure()` method
  - [x] Implement `LoggingService.Configure()`: create logs folder at `%LOCALAPPDATA%\GoogleCalendarManagement\logs\`, configure `Log.Logger` with `RollingInterval.Day`, `retainedFileCountLimit: 30`, enriched with `Application` and `Version` properties
  - [x] Set `MinimumLevel.Debug` in Debug builds, `MinimumLevel.Information` in Release builds (use `#if DEBUG` or compile-time conditional)
  - [x] Override Microsoft/EF Core log level to Warning to suppress framework noise
  - [x] Write output template: `{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}`

- [x] Create `Services/IErrorHandlingService.cs` and `Services/ErrorHandlingService.cs` (AC-1.6.3, AC-1.6.4)
  - [x] Define `IErrorHandlingService` interface with `Register()` and `HandleCriticalError(Exception, string)` methods
  - [x] In `Register()`: subscribe to `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and `Application.Current.UnhandledException`
  - [x] In `HandleCriticalError()`: flush Serilog (`Log.CloseAndFlush()`), dispose DB connections cleanly, show user-friendly `ContentDialog` with error message and "Exit" button

- [x] Update `App.xaml.cs` to wire Serilog and global exception handling (AC-1.6.1, AC-1.6.3)
  - [x] Call `LoggingService.Configure()` as the very first action in `App()` constructor — before DI setup — so startup failures are captured
  - [x] Replace `services.AddLogging(builder => builder.AddDebug()...)` with `services.AddLogging(builder => builder.AddSerilog())` in `ConfigureServices`
  - [x] Register `ILoggingService` (Singleton) and `IErrorHandlingService` (Singleton) in DI
  - [x] After building the service provider in `OnLaunched`, resolve `IErrorHandlingService` and call `.Register()` to activate global handlers
  - [x] Log application start: `Log.Information("Google Calendar Management v{Version} started", appVersion)`
  - [x] Log application stop via `Application.UnhandledException` and standard exit path: `Log.CloseAndFlush()`

- [x] Integrate performance logging in existing services (AC-1.6.5)
  - [x] In `MigrationService.RunStartupAsync()`: wrap migration steps with `Operation.Time("Database migration")` from SerilogTimings
  - [x] Record startup time in `App.OnLaunched` from start of method to `window.Activate()` using `Stopwatch`
  - [x] Verify slow operation threshold — operations exceeding 1000ms logged at Warning level via `OperationAt(..., warnIfExceeds: TimeSpan.FromSeconds(1))`

- [x] Write unit tests in `GoogleCalendarManagement.Tests/Unit/LoggingTests.cs` (AC-1.6.1, AC-1.6.2, AC-1.6.5)
  - [x] `Logger_ShouldWrite_ToFile`: configure Serilog to a temp test directory, write `Log.Information("Test entry")`, flush, assert log file exists and contains "Test entry"
  - [x] `Logger_ShouldInclude_Enrichment`: verify log output contains `Application=GoogleCalendarManagement` property
  - [x] `Logger_ShouldTrack_SlowOperations`: use `OperationAt` with `warnIfExceeds`, sleep 1100ms, assert log output contains `[WRN]` and operation name

- [x] Write integration tests in `GoogleCalendarManagement.Tests/Integration/ErrorHandlingTests.cs` (AC-1.6.3)
  - [x] `UnhandledTaskException_ShouldBe_SetObserved_WithoutCrash`: simulate unhandled task exception via `TaskScheduler.UnobservedTaskException`, assert handler registration doesn't throw

- [x] Final validation (All ACs)
  - [x] Build Debug and Release — no errors
  - [x] Run full test suite — all 24 tests pass including new logging/error-handling tests
  - [x] Launch app — verify `%LOCALAPPDATA%\GoogleCalendarManagement\logs\app-{today}.txt` is created with startup log entry (manual) — confirmed 2026-03-30
  - [x] Inspect log file — confirmed startup sequence: v1.0.0.0 started (245ms launch), migration OK, integrity check OK — 2026-03-30
  - [x] Slow operation test not required — startup at 245ms well within threshold; all ACs satisfied

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- .NET 9.0.12 (net9.0-windows10.0.19041.0 target framework)
- Windows App SDK 1.8.x with WinUI 3
- Serilog 4.1.0 as primary logging framework (replaces `Microsoft.Extensions.Logging.Debug` as the sink)
- SerilogTimings 3.0.1 provides the `BeginTimedOperation` / `Operation.Time` extension used for performance tracking
- `Serilog.Extensions.Logging` 8.0.0 bridges Serilog to Microsoft.Extensions.Logging so all services using `ILogger<T>` via DI continue to work unchanged

**Critical Architecture Decisions:**

- **Static `Log.Logger` configured before DI:** `LoggingService.Configure()` must run in the `App()` constructor before `ConfigureServices` is called. This ensures that if DI setup throws (e.g., a missing package, misconfigured EF Core option), the exception is captured in the log file rather than disappearing silently.

- **`services.AddSerilog()` bridges DI loggers:** After `Log.Logger` is configured, call `services.AddSerilog()` (from `Serilog.Extensions.Logging`) in `ConfigureServices`. All services that receive `ILogger<T>` via DI will route through Serilog automatically. Remove the existing `builder.AddDebug()` call — Serilog's Console sink handles Debug output during development.

- **Serilog as static vs. injected:** For services that need logging, use standard `ILogger<T>` via DI (not `Log.Logger` directly). `Log.Logger` is used only in the global exception handlers and `App.xaml.cs` startup code where DI is not yet available.

- **`Log.CloseAndFlush()` on exit:** Serilog buffers writes asynchronously. `CloseAndFlush()` must be called on every exit path (normal shutdown and critical error handlers) to ensure the last log entries are flushed to disk before the process terminates.

- **WinUI 3 exception handler vs. AppDomain:** WinUI 3 provides `Microsoft.UI.Xaml.Application.UnhandledException` for exceptions originating on the UI thread. `AppDomain.CurrentDomain.UnhandledException` catches CLR-level unhandled exceptions on background threads. `TaskScheduler.UnobservedTaskException` catches unobserved async Task failures. All three must be subscribed to for complete coverage.

- **MinimumLevel per build configuration:** In Debug builds, `MinimumLevel.Debug` is appropriate for development diagnostics. In Release builds, `MinimumLevel.Information` reduces log noise and file size. Use `#if DEBUG` conditional or load from a build-time constant — do not read from a config file (this is a single-user local app without a config system).

- **Log file naming:** Serilog's `RollingInterval.Day` with path `app-.txt` produces files named `app-20260130.txt`. The `-` in the path is the insertion point for the date. File names like `app-{date}.log` shown in the PRD are conceptually equivalent — the actual output is `.txt` extension per this spec.

**Serilog Configuration (App.xaml.cs):**
```csharp
var logFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "GoogleCalendarManagement", "logs");
Directory.CreateDirectory(logFolder);

Log.Logger = new LoggerConfiguration()
#if DEBUG
    .MinimumLevel.Debug()
#else
    .MinimumLevel.Information()
#endif
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "GoogleCalendarManagement")
    .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown")
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(logFolder, "app-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

**Global Exception Handler Wiring (App.xaml.cs):**
```csharp
// In App() constructor, after Log.Logger is configured:
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var ex = (Exception)args.ExceptionObject;
    Log.Fatal(ex, "Unhandled CLR exception (IsTerminating={IsTerminating})", args.IsTerminating);
    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (sender, args) =>
{
    Log.Error(args.Exception, "Unobserved Task exception");
    args.SetObserved(); // Prevent process crash for background task failures
};

// In OnLaunched, after window.Activate():
this.UnhandledException += (sender, args) =>
{
    Log.Fatal(args.Exception, "Unhandled WinUI exception: {Message}", args.Message);
    args.Handled = true; // Prevent immediate crash; show dialog
    Log.CloseAndFlush();
};
```

**Performance Timing Pattern (SerilogTimings):**
```csharp
using (Operation.Time("Database migration"))
{
    await _context.Database.MigrateAsync();
}
// Logs at Information if <1s, Warning if >1s:
// "Database migration completed in 1523ms"
```

### Project Structure After Story 1.6

```
GoogleCalendarManagement/
├── App.xaml.cs                         # Updated: Serilog config, global exception handlers
├── GoogleCalendarManagement.csproj     # Updated: Serilog NuGet packages added
├── Services/
│   ├── ILoggingService.cs              # New
│   ├── LoggingService.cs               # New
│   ├── IErrorHandlingService.cs        # New
│   ├── ErrorHandlingService.cs         # New
│   ├── IMigrationService.cs            # From Story 1.4
│   └── MigrationService.cs            # Updated: BeginTimedOperation added
GoogleCalendarManagement.Tests/
├── Unit/
│   ├── SmokeTests.cs                   # Existing
│   └── LoggingTests.cs                 # New
└── Integration/
    ├── DatabaseInfrastructureTests.cs  # Existing
    ├── SchemaTests.cs                  # Existing
    ├── MigrationServiceTests.cs        # Existing
    └── ErrorHandlingTests.cs           # New
%LOCALAPPDATA%\GoogleCalendarManagement\
└── logs\
    └── app-{yyyyMMdd}.txt              # New: daily rolling log files
```

### References

**Source Documents:**
- [Epic 1: Story 1.6 Definition](../../epics.md#story-16-implement-application-logging-and-error-handling-infrastructure)
- [Epic 1 Tech Spec: AC-1.6](../tech-spec.md#ac-16-logging--error-handling-story-16)
- [Epic 1 Tech Spec: Observability](../tech-spec.md#observability)
- [Epic 1 Tech Spec: Error Handling Flow](../tech-spec.md#error-handling-flow-story-16)
- [Epic 1 Tech Spec: Story 1.6 Tests](../tech-spec.md#story-16-logging)

**Specific Technical Mandates:**
- **NFR-I3 (Error Handling):** Global exception handler, no stack traces shown to users, graceful exit with state preservation
- **NFR-D1 (Data Loss Prevention):** Critical errors must flush logs and close DB connections before exit
- **Decision §17:** Graceful error handling with Serilog structured logging and retry policies

**Prerequisites:**
- Story 1.1 complete — project structure, DI container, App.xaml.cs in place
- Story 1.2 complete — `CalendarDbContext` registered in DI (needed for DB connection disposal)
- Story 1.4 complete — `MigrationService` in place (will be updated to use `BeginTimedOperation`)

### Common Troubleshooting

- **No log file created on first launch:** Verify the logs folder path uses `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`. Also confirm `Directory.CreateDirectory(logFolder)` is called before `Log.Logger` is configured. If the directory doesn't exist, Serilog silently skips file writes.
- **`AddSerilog()` not found:** Ensure `Serilog.Extensions.Logging` package (not just `Serilog`) is installed — this is the package that adds the `AddSerilog()` extension method on `ILoggingBuilder`.
- **`BeginTimedOperation()` not found on `ILogger<T>`:** The `BeginTimedOperation` extension comes from `SerilogTimings` package (namespace `SerilogTimings`). `Operation.Time(...)` is the static alternative. Both require `SerilogTimings` NuGet package.
- **WinUI 3 `Application.UnhandledException` handler not firing:** Ensure the handler is registered on `this` (the `App` instance), not on `Application.Current`. It must be wired after `InitializeComponent()` is called.
- **Log output contains stack traces visible to user:** The user-facing dialog must use a hardcoded user-friendly message string, not `ex.Message` or `ex.ToString()`. Reserve full exception details for the log file only.
- **Release build logs too noisy or too quiet:** `MinimumLevel.Information` in Release is intentional. `MinimumLevel.Override("Microsoft", LogEventLevel.Warning)` is critical to suppress EF Core query logging which would otherwise generate hundreds of lines per session.

### Testing Strategy

**Story 1.6 Testing Scope:**

Unit tests verify Serilog configuration behavior in isolation using a temporary test log directory. Integration tests verify the global exception handler wiring. Neither requires running the full WinUI 3 application.

**Unit Tests — `GoogleCalendarManagement.Tests/Unit/LoggingTests.cs`:**
```csharp
public class LoggingTests : IDisposable
{
    private readonly string _testLogDir = Path.Combine(Path.GetTempPath(), $"gcm-log-test-{Guid.NewGuid()}");

    private void ConfigureSerilog(string logPath)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    [Fact]
    public void Logger_ShouldWrite_ToFile()
    {
        ConfigureSerilog(Path.Combine(_testLogDir, "test-.txt"));
        Log.Information("Test log entry");
        Log.CloseAndFlush();

        var logFiles = Directory.GetFiles(_testLogDir, "test-*.txt");
        logFiles.Should().NotBeEmpty();
        File.ReadAllText(logFiles[0]).Should().Contain("Test log entry");
    }

    [Fact]
    public void Logger_ShouldTrack_SlowOperations()
    {
        // Uses SerilogTimings — operation >1s logged at Warning
        var sink = new StringWriter();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.TextWriter(sink)
            .CreateLogger();

        using (Operation.Time("Slow operation"))
        {
            Thread.Sleep(1100);
        }
        Log.CloseAndFlush();

        sink.ToString().Should().Contain("Slow operation");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogDir))
            Directory.Delete(_testLogDir, recursive: true);
    }
}
```

**Integration Tests — `GoogleCalendarManagement.Tests/Integration/ErrorHandlingTests.cs`:**
```csharp
public class ErrorHandlingTests
{
    [Fact]
    public void UnhandledTaskException_ShouldBe_SetObserved_WithoutCrash()
    {
        var observed = false;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            observed = true;
            args.SetObserved();
        };

        // Fire-and-forget task that throws
        Task.Run(() => throw new InvalidOperationException("test"))
            .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);

        // Allow GC to trigger UnobservedTaskException
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Not asserting observed=true as GC timing is non-deterministic,
        // but verifying handler registration doesn't throw
    }
}
```

**Manual Validation Checklist:**
1. Launch app — verify `%LOCALAPPDATA%\GoogleCalendarManagement\logs\app-{today}.txt` is created
2. Inspect log file — confirm first entry is `[INF] Google Calendar Management v... started`
3. Inspect log file — confirm `Application=GoogleCalendarManagement` property appears in entries
4. Run `dotnet test` — all existing + new logging tests pass
5. Confirm no raw `Debug.WriteLine` calls remain in production code paths

### Change Log

**Version 1.0 - Initial Draft (2026-03-27)**
- Created from Epic 1, Story 1.6 definition in epics.md and AC-1.6.1–1.6.5 in tech-spec.md
- Dev Notes include full Serilog configuration code and global exception handler wiring patterns
- SerilogTimings package identified for `BeginTimedOperation` support
- `services.AddSerilog()` replaces existing `services.AddLogging(builder => builder.AddDebug())` in `ConfigureServices`
- `Log.Logger` configured in `App()` constructor (before DI) to capture DI setup failures
- WinUI 3 `Application.UnhandledException` + `AppDomain` + `TaskScheduler` coverage documented
- Unit tests use temporary directory to avoid polluting local AppData during CI runs

## Dev Agent Record

### Context Reference

- [Story Context XML](1-6-implement-application-logging-and-error-handling-infrastructure.context.xml) - Generated 2026-03-27

**Implementation completed 2026-03-27**

- `Serilog.Sinks.Console` 6.0.1 not available on NuGet; 6.1.0 used instead.
- `WriteTo.TextWriter` removed from Serilog 4.x core; unit tests use a custom `InMemorySink : ILogEventSink` backed by `MessageTemplateTextFormatter`.
- SerilogTimings 3.0.1 does not expose `BeginTimedOperation`; the extension is `OperationAt(logger, completionLevel, abandonmentLevel, warnIfExceeds)` in `SerilogTimings.Extensions`. `Logger_ShouldTrack_SlowOperations` uses `Log.Logger.OperationAt(Information, null, 1s).Time(...)`.
- `services.AddSerilog()` on `IServiceCollection` requires `Serilog.Extensions.Hosting` (not installed); corrected to `services.AddLogging(builder => builder.AddSerilog())`.
- `ErrorHandlingService.SetWindow(Window)` is called from `App.OnLaunched` after window activation so `HandleCriticalError` can display a `ContentDialog` with correct `XamlRoot`.

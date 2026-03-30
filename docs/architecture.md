# Architecture - Google Calendar Management

**Project:** Google Calendar Management
**Author:** Sarunas Budreckis
**Date:** 2026-01-30
**Architecture Version:** 1.0

## Executive Summary

This architecture document defines the technical foundation for Google Calendar Management, a Windows desktop application built with .NET 9 and WinUI 3. The application transforms retroactive life tracking from a tedious chore into a beautiful, satisfying ritual through intelligent data consolidation and approval workflows.

**Architectural Approach:** Local-first desktop application with cloud API integrations
**Key Principles:**
- **Local-first architecture** - SQLite database, works offline, user owns data
- **Single-pane experience** - Unified calendar view replacing tab-juggling chaos
- **Intelligent automation** - Coalescing algorithms reduce cognitive load
- **Long-term sustainability** - Designed for decades of personal use
- **Extensible foundation** - Modular architecture enables future enhancements

**Technology Foundation:** .NET 9 + WinUI 3 + SQLite + Entity Framework Core 9
**API Integrations:** Google Calendar, Toggl Track, YouTube Data API, Microsoft Graph (Outlook + Excel)

## Project Initialization

**First Implementation Story:** Project setup and foundation

```bash
# Create WinUI 3 project with .NET 9
dotnet new winui3 -n GoogleCalendarManagement -f net9.0

# Or use Visual Studio 2022/2026:
# File → New → Project → WinUI 3 App → Select .NET 9.0
```

This establishes the base architecture with:
- WinUI 3 UI framework (Windows App SDK 1.8.3)
- .NET 9.0.12 runtime
- XAML-based declarative UI
- MVVM pattern support
- Modern Fluent Design System

## Decision Summary

| Category | Decision | Version | Affects Epics | Rationale | Source |
| -------- | -------- | ------- | ------------- | --------- | ------ |
| **Runtime** | .NET | 9.0.12 | All | Latest LTS with modern C# features | Key Decisions §1 |
| **UI Framework** | WinUI 3 (Windows App SDK) | 1.8.3 | Epic 3, 5, 6, 10 | Modern, future-proof, native CalendarView control | Key Decisions §1 |
| **Database** | SQLite | Latest | All | Single-file, portable, no server required | Key Decisions §2 |
| **ORM** | Entity Framework Core | 9.0.12 | All | Code-first migrations, LINQ, async support | Key Decisions §2 |
| **Google Calendar API** | Google.Apis.Calendar.v3 | 1.73.0.3993 | Epic 2, 6 | Official .NET client, OAuth 2.0, batch operations | Tech Stack §4.1 |
| **Toggl Track API** | Custom HttpClient | v9 API | Epic 4 | No official SDK, RESTful API | Tech Stack §4.2 |
| **YouTube API** | Google.Apis.YouTube.v3 | 1.73.0.x | Epic 4 | Video metadata only (watch history via Takeout) | Key Decisions §7 |
| **Microsoft Graph** | Microsoft.Graph | 5.101.0 | Epic 4, 7 | Outlook calendar + Excel cloud sync | Key Decisions §8, §12 |
| **Logging** | Serilog + ILogger | 4.x | All | Structured logging for debugging | Tech Stack §5.4 |
| **HTTP Resilience** | Polly | 9.0.x | Epic 2, 4 | Retry policies with exponential backoff | Key Decisions §17 |
| **JSON** | System.Text.Json | Built-in | All | High performance, modern, built-in | Tech Stack §5.2 |
| **Table Naming** | Singular | N/A | Epic 1 | .NET best practice, matches entity names | Key Decisions §3 |
| **Approval Workflow** | In-memory until publish | N/A | Epic 6 | Simpler state machine, clearer UX | Key Decisions §4 |
| **Version History** | Own history + ETags | N/A | Epic 8 | Google doesn't provide rollback | Key Decisions §5 |
| **Data Storage** | Separate source tables | N/A | Epic 1, 4 | Preserve everything, enable reprocessing | Key Decisions §6 |
| **Rounding Algorithm** | 8/15 rule (configurable) | N/A | Epic 5 | More accurate than simple rounding | Key Decisions §10 |
| **Coalescing** | Source-specific algorithms | N/A | Epic 5 | Phone (15-min gaps), YouTube (duration+30min) | Key Decisions §9 |
| **Date State Tracking** | Multi-dimensional flags | N/A | Epic 7 | Granular progress per data source | Key Decisions §11 |
| **Weekly Status** | Local + Excel bidirectional sync | N/A | Epic 7 | Automates existing workflow | Key Decisions §12 |
| **Save/Restore** | Google Calendar state only | N/A | Epic 8 | User-visible state, fast snapshots | Key Decisions §13 |
| **Event Notation** | Append timestamp on modify | N/A | Epic 6 | Audit trail visible in Google Calendar | Key Decisions §14 |
| **API Caching** | Aggressive with user refresh | N/A | Epic 2, 4 | Fast, offline-capable, predictable | Key Decisions §15 |
| **Filtering** | Store all + visible_as_event flag | N/A | Epic 4, 5 | Never lose data, flexible rules | Key Decisions §16 |
| **Error Handling** | Graceful with retry + logging | N/A | All | Resilient to transient failures | Key Decisions §17 |

## Project Structure

```
GoogleCalendarManagement/
├── GoogleCalendarManagement.sln
├── src/
│   ├── GoogleCalendarManagement/              # WinUI 3 Desktop App
│   │   ├── App.xaml                           # Application entry point
│   │   ├── App.xaml.cs
│   │   ├── Package.appxmanifest               # App manifest
│   │   ├── Views/                             # XAML Views
│   │   │   ├── MainWindow.xaml                # Main calendar view
│   │   │   ├── EventEditPanel.xaml            # Event editing inline panel
│   │   │   ├── ImportDialog.xaml              # Data import dialogs
│   │   │   ├── SaveRestoreDialog.xaml         # Save/restore UI
│   │   │   └── SettingsPage.xaml              # App settings
│   │   ├── ViewModels/                        # MVVM ViewModels
│   │   │   ├── MainViewModel.cs               # Calendar display logic
│   │   │   ├── EventViewModel.cs              # Event editing logic
│   │   │   ├── ImportViewModel.cs             # Import workflow logic
│   │   │   └── SettingsViewModel.cs
│   │   ├── Models/                            # UI models (different from DB entities)
│   │   │   ├── CalendarEvent.cs               # Display model for events
│   │   │   ├── DateState.cs                   # Date completion state
│   │   │   └── WeeklyStatus.cs
│   │   ├── Converters/                        # XAML value converters
│   │   │   ├── DateTimeConverter.cs
│   │   │   ├── ColorConverter.cs
│   │   │   └── BoolToVisibilityConverter.cs
│   │   ├── Resources/                         # App resources
│   │   │   ├── Styles.xaml                    # Custom styles
│   │   │   └── ColorDefinitions.xaml          # 9 mental state colors
│   │   └── Assets/                            # Images, icons
│   │
│   ├── GoogleCalendarManagement.Core/         # Business Logic Library (.NET 9)
│   │   ├── Services/                          # Core services
│   │   │   ├── GoogleCalendarService.cs       # Google Calendar API wrapper
│   │   │   ├── TogglService.cs                # Toggl Track API client
│   │   │   ├── YouTubeService.cs              # YouTube Data API client
│   │   │   ├── MicrosoftGraphService.cs       # Outlook + Excel integration
│   │   │   ├── CoalescingService.cs           # Phone/YouTube coalescing algorithms
│   │   │   ├── RoundingService.cs             # 8/15 rounding algorithm
│   │   │   ├── DateStateService.cs            # Date state tracking + contiguity
│   │   │   ├── WeeklyStatusService.cs         # ISO 8601 week calculation
│   │   │   ├── SaveRestoreService.cs          # Snapshot and rollback
│   │   │   └── ImportService.cs               # File import orchestration
│   │   ├── Managers/                          # High-level workflow managers
│   │   │   ├── ApprovalManager.cs             # Approval workflow
│   │   │   ├── PublishManager.cs              # Batch publishing
│   │   │   └── SyncManager.cs                 # API sync coordination
│   │   ├── Algorithms/                        # Core algorithms (testable)
│   │   │   ├── EightFifteenRounding.cs        # 8/15 rule implementation
│   │   │   ├── PhoneCoalescing.cs             # Phone activity sliding window
│   │   │   ├── YouTubeCoalescing.cs           # YouTube session coalescing
│   │   │   └── ContiguityCalculator.cs        # Contiguity edge calculation
│   │   ├── Parsers/                           # Data parsers
│   │   │   ├── YouTubeTakeoutParser.cs        # Google Takeout JSON parser
│   │   │   ├── CallLogCsvParser.cs            # iMazing CSV parser
│   │   │   └── IcsFileParser.cs               # Outlook .ics parser
│   │   ├── Models/                            # Domain models
│   │   │   └── (DTOs, configuration models)
│   │   └── Interfaces/                        # Service contracts
│   │       ├── IDataSourceService.cs          # Plugin interface for data sources
│   │       ├── ICoalescingAlgorithm.cs        # Algorithm interface
│   │       └── IGoogleCalendarService.cs
│   │
│   ├── GoogleCalendarManagement.Data/         # Data Access Layer (.NET 9)
│   │   ├── CalendarDbContext.cs               # EF Core DbContext
│   │   ├── Entities/                          # Database entities (14 tables)
│   │   │   ├── DateState.cs
│   │   │   ├── TrackedGap.cs
│   │   │   ├── GcalEvent.cs
│   │   │   ├── GcalEventVersion.cs
│   │   │   ├── TogglData.cs
│   │   │   ├── YouTubeData.cs
│   │   │   ├── CallLogData.cs
│   │   │   ├── GeneratedEventSource.cs
│   │   │   ├── AuditLog.cs
│   │   │   ├── SystemState.cs
│   │   │   ├── SaveState.cs
│   │   │   ├── Config.cs
│   │   │   ├── WeeklyState.cs
│   │   │   └── DataSourceRefresh.cs
│   │   ├── Configurations/                    # EF entity configurations
│   │   │   ├── DateStateConfiguration.cs
│   │   │   ├── GcalEventConfiguration.cs
│   │   │   └── (... one per entity)
│   │   ├── Repositories/                      # Repository pattern
│   │   │   ├── IRepository.cs                 # Generic repository interface
│   │   │   ├── Repository.cs                  # Generic implementation
│   │   │   ├── DateStateRepository.cs         # Specific repos if needed
│   │   │   └── GcalEventRepository.cs
│   │   └── Migrations/                        # EF Core migrations
│   │       └── (generated migration files)
│   │
│   └── GoogleCalendarManagement.Tests/        # Unit + Integration Tests
│       ├── Unit/
│       │   ├── Algorithms/
│       │   │   ├── EightFifteenRoundingTests.cs
│       │   │   ├── PhoneCoalescingTests.cs
│       │   │   └── YouTubeCoalescingTests.cs
│       │   ├── Services/
│       │   │   └── DateStateServiceTests.cs
│       │   └── Parsers/
│       │       └── YouTubeTakeoutParserTests.cs
│       ├── Integration/
│       │   ├── DatabaseTests.cs               # In-memory SQLite tests
│       │   └── ApiClientTests.cs              # Mock HTTP tests
│       └── TestData/
│           ├── sample_toggl.json
│           ├── sample_youtube.json
│           └── sample_calllog.csv
│
├── data/                                       # User data directory (gitignored)
│   ├── calendar.db                            # SQLite database
│   ├── calendar.db-wal                        # Write-ahead log
│   ├── calendar.db-shm                        # Shared memory
│   ├── outlook_imports/                       # .ics file imports
│   └── logs/                                  # API response logs (optional, deletable)
│       ├── toggl/
│       ├── gcal/
│       └── youtube/
│
├── docs/                                       # Documentation
│   ├── PRD.md
│   ├── epics.md
│   ├── architecture.md                        # This file
│   ├── _key-decisions.md
│   ├── _database-schemas.md
│   ├── _technology-stack.md
│   └── stories/                               # Individual story markdown files
│
└── README.md
```

**Architecture Notes:**

- **Separation of Concerns:** UI (WinUI 3) → Core (business logic) → Data (EF Core)
- **Future Extensibility:** Core and Data layers are .NET 9 class libraries, enabling future web/mobile UIs
- **Testability:** Algorithms and services isolated from UI for unit testing
- **Plugin Pattern:** `IDataSourceService` allows adding new data sources without changing core architecture

## Epic to Architecture Mapping

| Epic | Components | Database Tables | External APIs | Key Services |
|------|-----------|----------------|---------------|--------------|
| **Epic 1: Foundation & Core Infrastructure** | Core, Data libraries | All 14 tables | None | CalendarDbContext, Repository pattern |
| **Epic 2: Google Calendar Integration & Sync** | GoogleCalendarService, SyncManager | gcal_event, gcal_event_version, audit_log, data_source_refresh | Google Calendar API v3 | GoogleCalendarService, SaveRestoreService |
| **Epic 3: Calendar UI & Visual Display** | MainWindow.xaml, MainViewModel, EventEditPanel | gcal_event, date_state | None | CalendarView control (WinUI 3) |
| **Epic 4: Data Source Integrations** | TogglService, YouTubeService, MicrosoftGraphService, Parsers | toggl_data, youtube_data, call_log_data | Toggl v9 API, YouTube Data API v3, Microsoft Graph | TogglService, YouTubeService, MicrosoftGraphService, ImportService |
| **Epic 5: Data Processing & Coalescing** | CoalescingService, RoundingService, Algorithms | toggl_data, youtube_data, generated_event_source, config | None | EightFifteenRounding, PhoneCoalescing, YouTubeCoalescing |
| **Epic 6: Approval Workflow & Publishing** | ApprovalManager, PublishManager | gcal_event, audit_log | Google Calendar API v3 | PublishManager, ApprovalManager |
| **Epic 7: Date State & Progress Tracking** | DateStateService, WeeklyStatusService | date_state, tracked_gap, weekly_state, system_state | Microsoft Graph (Excel) | DateStateService, WeeklyStatusService, ContiguityCalculator |
| **Epic 8: Save/Restore & Version Management** | SaveRestoreService, SaveRestoreDialog | save_state, gcal_event_version, audit_log | Google Calendar API v3 | SaveRestoreService |
| **Epic 9: Import Workflows & Data Management** | ImportService, Parsers, ImportDialog | All source tables, data_source_refresh | All | ImportService, YouTubeTakeoutParser, CallLogCsvParser, IcsFileParser |
| **Epic 10: Polish & Production Readiness** | All UI components, Logging, Error handling | audit_log, config | All | Serilog, Polly retry policies, validation services |

**Implementation Sequence:**

1. **Epic 1** → Foundation: Database schema, EF Core setup, basic services
2. **Epic 2** → Google Calendar: OAuth, fetch events, sync infrastructure
3. **Epic 3** → Calendar UI: Display events, navigation, visual design
4. **Epic 4** → Data Sources: Integrate Toggl, calls, YouTube, Outlook
5. **Epic 5** → Processing: Implement 8/15 rounding, coalescing algorithms
6. **Epic 6** → Approval: Selection, batch approval, publish workflow
7. **Epic 7** → Progress: Date states, contiguity edge, weekly status
8. **Epic 8** → Save/Restore: Snapshots, rollback, version history
9. **Epic 9** → Import UX: Drag-drop, file parsing, import summaries
10. **Epic 10** → Polish: Error handling, logging, performance, UX refinement

## Technology Stack Details

### Core Technologies

**Runtime & Framework:**
- **.NET 9.0.12** (January 2026 patch) - [Download](.NET 9.0 - Supported OS versions)
- **Windows App SDK 1.8.3** (latest stable) - [Documentation](Latest Windows App SDK downloads)
- **C# 13** (latest language features)

**UI Framework:**
- **WinUI 3** (part of Windows App SDK)
- **XAML** for declarative UI
- **MVVM** pattern with CommunityToolkit.Mvvm
- **CalendarView** control for main display

**Data Persistence:**
- **SQLite** (embedded, file-based)
- **Entity Framework Core 9.0.12** - [NuGet](NuGet Gallery | Microsoft.EntityFrameworkCore 10.0.2)
- **Code-First** migrations

**NuGet Packages (Verified January 2026):**
```xml
<!-- Google APIs -->
<PackageReference Include="Google.Apis.Calendar.v3" Version="1.73.0.3993" />
<PackageReference Include="Google.Apis.YouTube.v3" Version="1.73.0.x" />
<PackageReference Include="Google.Apis.Auth" Version="1.73.0.x" />

<!-- Microsoft Graph -->
<PackageReference Include="Microsoft.Graph" Version="5.101.0" />
<PackageReference Include="Azure.Identity" Version="1.x" />

<!-- Entity Framework Core -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.12" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.12" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.12" />

<!-- HTTP & Resilience -->
<PackageReference Include="Microsoft.Extensions.Http" Version="9.0.x" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.x" />

<!-- Logging -->
<PackageReference Include="Serilog" Version="4.x" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.x" />
<PackageReference Include="Serilog.Sinks.File" Version="6.x" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.x" />

<!-- MVVM -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.x" />

<!-- Testing -->
<PackageReference Include="xUnit" Version="2.x" />
<PackageReference Include="Moq" Version="4.x" />
<PackageReference Include="FluentAssertions" Version="6.x" />
```

**Built-in .NET Features (No External Dependencies):**
- `System.Text.Json` - JSON serialization
- `System.Globalization.ISOWeek` - ISO 8601 week calculation
- `HttpClient` with `IHttpClientFactory` - HTTP communications
- Windows DPAPI - Credential encryption

### Integration Points

**External APIs:**

1. **Google Calendar API v3**
   - **Auth:** OAuth 2.0 with refresh tokens
   - **Quota:** 1M requests/day (free tier)
   - **Rate Limit:** 10 queries/second/user
   - **Operations:** List, Insert, Update, Delete events; Batch requests; Incremental sync
   - **Integration:** `GoogleCalendarService` in Core layer

2. **Toggl Track API v9**
   - **Auth:** API token (Basic auth)
   - **Base URL:** `https://api.track.toggl.com/api/v9`
   - **Rate Limit:** 1 request/second
   - **Operations:** Fetch time entries by date range
   - **Integration:** `TogglService` with custom HttpClient

3. **YouTube Data API v3**
   - **Auth:** API key (server key)
   - **Quota:** 10K units/day (search=100, videos.list=1)
   - **Operations:** Fetch video metadata (duration, channel, title) by video ID
   - **Note:** Watch history NOT available via API; use Google Takeout
   - **Integration:** `YouTubeService` for metadata only

4. **Microsoft Graph API**
   - **Auth:** OAuth 2.0 delegated permissions
   - **Scopes:** `Calendars.ReadWrite`, `Files.ReadWrite`, `offline_access`
   - **Refresh Token:** 90-day expiration
   - **Operations:**
     - Outlook Calendar: Fetch events
     - Excel Online: Update weekly status cells
   - **Integration:** `MicrosoftGraphService`

**Local File Integrations:**

1. **Google Takeout (YouTube History)**
   - **Format:** JSON file from https://takeout.google.com
   - **Parser:** `YouTubeTakeoutParser`
   - **Structure:** Array of `{title, titleUrl, time, subtitles}`

2. **iMazing Call Logs**
   - **Format:** CSV export from iMazing
   - **Parser:** `CallLogCsvParser`
   - **Columns:** Call type, Date, Duration, Number, Contact, Location, Service

3. **Outlook .ics Files (Fallback)**
   - **Format:** Standard iCalendar (.ics) format
   - **Parser:** `IcsFileParser`
   - **Use:** Fallback if Microsoft Graph OAuth unavailable

## Implementation Patterns

These patterns ensure consistent implementation across all AI agents. **CRITICAL:** All agents MUST follow these conventions exactly to prevent conflicts and ensure code compatibility.

### NAMING PATTERNS

**C# Naming Conventions:**
```csharp
// Classes: PascalCase
public class GoogleCalendarService { }
public class DateStateRepository { }

// Interfaces: IPascalCase
public interface IDataSourceService { }
public interface ICoalescingAlgorithm { }

// Methods: PascalCase (verbs)
public async Task<List<Event>> FetchEventsAsync()
public void CalculateContiguityEdge()

// Properties: PascalCase (nouns)
public DateTime StartDateTime { get; set; }
public bool IsPublished { get; set; }

// Private fields: _camelCase
private readonly ILogger<MainViewModel> _logger;
private readonly HttpClient _httpClient;

// Parameters/locals: camelCase
public void ProcessEvent(DateTime startTime, string summary)

// Constants: PascalCase
public const int DefaultMinEventDuration = 5;
```

**Database Naming (EF Core Entities):**
```csharp
// Table names: Singular, snake_case (configured via Fluent API)
// Entity class name: PascalCase
public class DateState { }  // → table: date_state
public class GcalEvent { }  // → table: gcal_event

// Column names: snake_case
[Column("published_to_gcal")]
public bool PublishedToGcal { get; set; }

[Column("created_at")]
public DateTime CreatedAt { get; set; }

// Boolean columns: Descriptive with context
published_to_gcal (not just "published")
visible_as_event (not just "visible")
complete_walkthrough_approval (not just "complete")

// Timestamp columns: Suffix with "_at"
created_at, updated_at, published_at, synced_at

// Foreign keys: Full reference name
published_gcal_event_id (not just "event_id")
named_event_gcal_id

// Index names: idx_{table}_{column(s)}
CREATE INDEX idx_gcal_event_date ON gcal_event(start_datetime, end_datetime);
CREATE INDEX idx_toggl_description ON toggl_data(description);
```

**XAML Naming:**
```xml
<!-- Controls: PascalCase with type suffix -->
<CalendarView x:Name="MainCalendarView" />
<Button x:Name="PublishButton" />
<TextBox x:Name="EventTitleTextBox" />

<!-- Resources: PascalCase with descriptive name -->
<Style x:Key="EventCardStyle" TargetType="Border" />
<SolidColorBrush x:Key="AzureColorBrush" Color="#0088CC" />
```

### STRUCTURE PATTERNS

**Project Organization:**
```
- UI logic → GoogleCalendarManagement/Views + ViewModels
- Business logic → GoogleCalendarManagement.Core/Services + Managers
- Data access → GoogleCalendarManagement.Data/Repositories
- Algorithms → GoogleCalendarManagement.Core/Algorithms (pure functions, testable)
- Tests → GoogleCalendarManagement.Tests/Unit or Integration
```

**File Location Rules:**
- **One class per file**, file name = class name
- **Tests co-located by namespace**, e.g., `Algorithms/PhoneCoalescing.cs` → `Tests/Unit/Algorithms/PhoneCoalescingTests.cs`
- **Entity configurations** in `Data/Configurations/`, one file per entity
- **XAML views** in `Views/`, code-behind in `Views/*.xaml.cs`

**Namespace Convention:**
```csharp
// UI Project
namespace GoogleCalendarManagement.Views;
namespace GoogleCalendarManagement.ViewModels;

// Core Project
namespace GoogleCalendarManagement.Core.Services;
namespace GoogleCalendarManagement.Core.Algorithms;
namespace GoogleCalendarManagement.Core.Managers;

// Data Project
namespace GoogleCalendarManagement.Data.Entities;
namespace GoogleCalendarManagement.Data.Repositories;
namespace GoogleCalendarManagement.Data.Configurations;
```

### FORMAT PATTERNS

**Async/Await Convention:**
```csharp
// ALL I/O operations MUST be async
// Suffix async methods with "Async"
public async Task<List<Event>> FetchEventsAsync(DateTime start, DateTime end)
{
    return await _httpClient.GetFromJsonAsync<List<Event>>($"/events?start={start}");
}

// Use ConfigureAwait(false) in library code (Core, Data)
var result = await dbContext.SaveChangesAsync().ConfigureAwait(false);

// UI code can omit ConfigureAwait (runs on UI thread anyway)
var events = await _service.FetchEventsAsync(start, end);
```

**DateTime Handling:**
```csharp
// ALWAYS use UTC for storage
entity.CreatedAt = DateTime.UtcNow;

// Convert to local only for display
var localTime = utcDateTime.ToLocalTime();

// ISO 8601 for API calls
var isoString = dateTime.ToString("o"); // "2026-01-30T14:30:00.0000000Z"

// SQLite stores as TEXT in ISO 8601 format
```

**Error Response Format:**
```csharp
// Standard result pattern for operations that can fail
public class OperationResult<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
    public Exception Exception { get; set; }
}

// Usage
var result = await _service.ImportCallLogsAsync(filePath);
if (!result.Success)
{
    _logger.LogError(result.Exception, "Import failed: {Error}", result.ErrorMessage);
    // Show user-friendly message
}
```

**API Response Caching:**
```csharp
// Pattern for all API services
public class GoogleCalendarService
{
    // 1. Check local cache first
    var cached = await _repository.GetEventsAsync(start, end);
    if (cached.Any() && !forceRefresh)
        return cached;

    // 2. Fetch from API
    var apiEvents = await FetchFromGoogleAsync(start, end);

    // 3. Store in database
    await _repository.BulkInsertAsync(apiEvents);

    // 4. Log to audit_log
    await _auditService.LogOperationAsync("sync", "Google Calendar", apiEvents.Count);

    // 5. Return
    return apiEvents;
}
```

### COMMUNICATION PATTERNS

**Logging Format (Serilog):**
```csharp
// Structured logging with properties
_logger.LogInformation("Fetching events from {StartDate} to {EndDate}", start, end);
_logger.LogWarning("API quota approaching limit: {Used}/{Total}", used, total);
_logger.LogError(ex, "Failed to publish events: {EventIds}", string.Join(",", eventIds));

// Levels:
// - Verbose: Algorithm details, loop iterations
// - Debug: Service calls, cache hits
// - Information: User actions, API calls, imports
// - Warning: Recoverable errors, quota warnings
// - Error: Exceptions, failed operations
// - Fatal: App crashes (rare)
```

**Event Notification (UI):**
```csharp
// Use WeakReferenceMessenger for decoupled communication
WeakReferenceMessenger.Default.Send(new EventPublishedMessage(eventIds));

// ViewModels subscribe
WeakReferenceMessenger.Default.Register<EventPublishedMessage>(this, (r, m) =>
{
    // Update UI
    RefreshCalendar();
});
```

### LIFECYCLE PATTERNS

**Service Registration (Dependency Injection):**
```csharp
// Startup.cs or App.xaml.cs
services.AddDbContext<CalendarDbContext>(options =>
    options.UseSqlite("Data Source=calendar.db"));

// Singleton: App-level state (one instance)
services.AddSingleton<ILogger, SerilogLogger>();

// Scoped: Per-operation (one instance per scope)
services.AddScoped<GoogleCalendarService>();
services.AddScoped<IRepository<GcalEvent>, Repository<GcalEvent>>();

// Transient: New instance every request
services.AddTransient<PhoneCoalescingAlgorithm>();

// HttpClient factory (REQUIRED for all HTTP services)
services.AddHttpClient<TogglService>()
    .AddTransientHttpErrorPolicy(p =>
        p.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

**DbContext Usage:**
```csharp
// ALWAYS dispose DbContext (use using or DI scoped)
using var context = new CalendarDbContext();

// Or via DI (preferred)
public class DateStateService
{
    private readonly CalendarDbContext _context;

    public DateStateService(CalendarDbContext context)
    {
        _context = context; // Scoped, disposed automatically
    }
}
```

### CONSISTENCY PATTERNS (Cross-Cutting)

**Configuration Access:**
```csharp
// ALL configurable values in database config table
var threshold = await _configService.GetIntAsync("eight_fifteen_threshold"); // 8
var phoneGap = await _configService.GetIntAsync("phone_coalesce_gap_minutes"); // 15

// NOT hardcoded in algorithm classes
```

**Audit Logging:**
```csharp
// ALL user actions and API operations MUST log to audit_log
await _auditService.LogAsync(new AuditEntry
{
    OperationType = "publish",
    OperationDetails = JsonSerializer.Serialize(new { EventCount = events.Count }),
    AffectedDates = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}",
    AffectedEvents = JsonSerializer.Serialize(eventIds),
    UserAction = true,
    Success = true
});
```

**User-Facing Error Messages:**
```csharp
// Pattern: Friendly message + optional technical details + action guidance
"Unable to connect to Google Calendar. Please check your internet connection and try again."
"Failed to import call logs. The CSV file may be in an incorrect format. Expected columns: Call type, Date, Duration, Number, Contact, Location, Service."
"YouTube API quota exceeded (9,800/10,000 units used). Please try again tomorrow or use a different API key."

// NOT: "HttpRequestException: No such host is known"
```

## Consistency Rules

### Naming Conventions

See Implementation Patterns § Naming Patterns above for complete conventions.

**Quick Reference:**
- **C# Classes**: PascalCase
- **Interfaces**: IPascalCase
- **Methods**: PascalCase verbs
- **Private fields**: _camelCase
- **Database tables**: singular_snake_case
- **Database columns**: snake_case
- **Timestamps**: suffix_with_at
- **Booleans**: descriptive (published_to_gcal, not just published)

### Code Organization

**Layer Separation:**
```
UI (WinUI 3) → Core (Business Logic) → Data (EF Core) → Database (SQLite)
```

**Dependency Rule:** Inner layers don't reference outer layers
- Data layer knows nothing about UI
- Core layer knows nothing about WinUI 3
- UI depends on Core, Core depends on Data

**Testing Strategy:**
- Algorithms: Pure functions, 100% unit test coverage
- Services: Integration tests with in-memory SQLite
- UI: Manual testing (WinUI 3 automated UI testing complex)

### Error Handling

**Retry Policy (Polly):**
```csharp
// ALL external API calls use retry with exponential backoff
services.AddHttpClient<GoogleCalendarService>()
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                _logger.LogWarning("Retry {RetryCount} after {Delay}s", retryCount, timespan.TotalSeconds);
            }));
```

**Exception Handling Layers:**
```csharp
// 1. Service layer: Catch, log, return OperationResult
public async Task<OperationResult<List<Event>>> FetchEventsAsync()
{
    try
    {
        var events = await _api.GetEventsAsync();
        return OperationResult<List<Event>>.Success(events);
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "HTTP error fetching events");
        return OperationResult<List<Event>>.Failure("Unable to connect to Google Calendar", ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error");
        return OperationResult<List<Event>>.Failure("An unexpected error occurred", ex);
    }
}

// 2. ViewModel layer: Check result, update UI
var result = await _service.FetchEventsAsync();
if (!result.Success)
{
    ErrorMessage = result.ErrorMessage; // Bind to UI
    ShowErrorDialog = true;
}
else
{
    Events = result.Data;
}

// 3. NEVER let exceptions bubble to UI thread unhandled
```

**Validation Before Operations:**
```csharp
// Validate BEFORE attempting operation
if (!File.Exists(filePath))
{
    return OperationResult.Failure("File not found");
}

if (events.Count == 0)
{
    return OperationResult.Failure("No events to publish");
}

// Pre-flight checks
if (!await _service.IsOnlineAsync())
{
    return OperationResult.Failure("No internet connection");
}
```

### Logging Strategy

**Serilog Configuration:**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // Suppress EF Core noise
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

**What to Log:**
```csharp
// Information: User actions, API operations, imports
_logger.LogInformation("User approved {Count} events for {Date}", count, date);
_logger.LogInformation("Imported {Count} call logs from {File}", count, filename);
_logger.LogInformation("Published {Count} events to Google Calendar", count);

// Warning: Recoverable issues, approaching limits
_logger.LogWarning("API quota at {Percent}%: {Used}/{Total}", percent, used, total);
_logger.LogWarning("Coalescing resulted in {Percent}% phone activity (threshold: 50%)", percent);

// Error: Exceptions, failed operations
_logger.LogError(ex, "Failed to fetch Toggl entries for {Date}", date);
_logger.LogError("Event validation failed: {Errors}", string.Join(", ", errors));

// Debug: Cache hits, detailed flow (development only)
_logger.LogDebug("Cache hit for date range {Start} to {End}", start, end);
_logger.LogDebug("Executing query: {Query}", query);
```

**Structured Logging:**
```csharp
// Use properties, not string interpolation
_logger.LogInformation("Published {EventCount} events in {Duration}ms", eventCount, duration);

// NOT: _logger.LogInformation($"Published {eventCount} events in {duration}ms");
// Properties enable querying logs
```

## Data Architecture

**Complete database schema documented in:** [_database-schemas.md](\_database-schemas.md)

### Database Overview

- **Type:** SQLite (embedded, file-based)
- **Location:** `{AppData}/GoogleCalendarManagement/calendar.db`
- **ORM:** Entity Framework Core 9.0.12
- **Migrations:** Code-first with EF Core migrations
- **Schema:** 14 tables (see full schema document)

### Core Data Model

```
┌─────────────────┐         ┌──────────────┐
│   date_state    │←────────│ tracked_gap  │
│  (date flags)   │         │ (gap ranges) │
└─────────────────┘         └──────────────┘
        │
        │ named_event_gcal_id
        ↓
┌──────────────────────┐
│     gcal_event       │
│  (Google Calendar)   │
└──────────────────────┘
        │
        ├───→ gcal_event_version (version history)
        ├───→ toggl_data (via published_gcal_event_id)
        ├───→ youtube_data (via published_gcal_event_id)
        ├───→ call_log_data (via published_gcal_event_id)
        └───→ generated_event_source (many-to-many for coalesced events)

┌──────────────────┐    ┌─────────────────┐    ┌──────────────┐
│  weekly_state    │    │   save_state    │    │  audit_log   │
│ (ISO 8601 weeks) │    │  (snapshots)    │    │ (all ops)    │
└──────────────────┘    └─────────────────┘    └──────────────┘

┌──────────────────┐    ┌──────────────────────┐
│  system_state    │    │ data_source_refresh  │
│  (app state)     │    │  (API cache track)   │
└──────────────────┘    └──────────────────────┘

┌──────────────────┐
│     config       │
│ (configuration)  │
└──────────────────┘
```

### Key Entity Relationships

**1. Source Data → Published Events:**
```
toggl_data.published_gcal_event_id → gcal_event.gcal_event_id
youtube_data.published_gcal_event_id → gcal_event.gcal_event_id
call_log_data.published_gcal_event_id → gcal_event.gcal_event_id
```

**2. Coalesced Events (Many-to-Many):**
```
generated_event_source links multiple source records to one gcal_event
Example: 15 phone Toggl entries → 1 "Phone Activity" calendar event
```

**3. Date State Tracking:**
```
date_state.date (PK) tracks per-date flags:
- call_log_data_published
- youtube_data_published
- toggl_data_published
- complete_walkthrough_approval
```

**4. Version History:**
```
gcal_event_version stores full snapshots of gcal_event changes
Used for rollback since Google Calendar doesn't provide version history
```

### Data Types & Conventions

```sql
-- Primary Keys
INTEGER PRIMARY KEY AUTOINCREMENT  (auto-generated IDs)
TEXT PRIMARY KEY                   (Google event IDs, dates)

-- DateTime fields
DATETIME  (stored as ISO 8601 TEXT: "2026-01-30T14:30:00Z")
DATE      (stored as TEXT: "2026-01-30")

-- Booleans
BOOLEAN   (stored as INTEGER: 0 = false, 1 = true)

-- Foreign Keys
TEXT NOT NULL, FOREIGN KEY(...) REFERENCES ...

-- Timestamps (automatic)
created_at DATETIME DEFAULT CURRENT_TIMESTAMP
updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
```

### Indexing Strategy

```sql
-- Date range queries (most common)
CREATE INDEX idx_gcal_event_date ON gcal_event(start_datetime, end_datetime);
CREATE INDEX idx_toggl_date ON toggl_data(start_time, end_time);
CREATE INDEX idx_youtube_date ON youtube_data(watch_start_time);

-- Foreign keys (automatic query optimization)
CREATE INDEX idx_version_event ON gcal_event_version(gcal_event_id, created_at DESC);

-- Lookup queries
CREATE INDEX idx_gcal_source ON gcal_event(source_system);
CREATE INDEX idx_toggl_description ON toggl_data(description);
```

### Data Integrity Rules

1. **Never Delete Source Data** - Mark with `visible_as_event = FALSE` instead
2. **Complete Version History** - Every gcal_event change creates gcal_event_version entry
3. **Audit All Operations** - Every user action and API call logs to audit_log
4. **Referential Integrity** - Foreign key constraints enforced (SQLite PRAGMA foreign_keys = ON)
5. **Transaction Safety** - WAL mode enabled for crash recovery

## API Contracts

### Internal Service Interfaces

**IDataSourceService** (Plugin Interface):
```csharp
public interface IDataSourceService
{
    string SourceName { get; } // "toggl", "youtube", "call_log", "outlook"

    Task<OperationResult<int>> ImportAsync(DateTime startDate, DateTime endDate);

    Task<OperationResult<List<CalendarEvent>>> GetPendingEventsAsync(DateTime date);

    Task<OperationResult> PublishAsync(List<string> eventIds);
}
```

**ICoalescingAlgorithm** (Algorithm Interface):
```csharp
public interface ICoalescingAlgorithm
{
    string AlgorithmName { get; }

    Task<List<CoalescedEvent>> CoalesceAsync(List<RawDataEntry> entries, CoalescingConfig config);
}
```

### Google Calendar API Contract

**Authentication:**
```csharp
// OAuth 2.0 Desktop Flow
UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    new ClientSecrets { ClientId = "...", ClientSecret = "..." },
    new[] { CalendarService.Scope.Calendar },
    "user",
    CancellationToken.None);
```

**Key Operations:**
```csharp
// 1. List Events (with incremental sync)
var request = service.Events.List("primary");
request.TimeMin = startDate;
request.TimeMax = endDate;
request.SyncToken = lastSyncToken; // Incremental sync
var events = await request.ExecuteAsync();

// 2. Insert Event
var newEvent = new Event
{
    Summary = "Event Title",
    Description = "Description\n\nPublished by Google Calendar Management on 2026-01-30 14:30:00",
    Start = new EventDateTime { DateTime = startTime },
    End = new EventDateTime { DateTime = endTime },
    ColorId = "9" // Custom color (1-11)
};
await service.Events.Insert(newEvent, "primary").ExecuteAsync();

// 3. Update Event (for rollback)
var existing = await service.Events.Get("primary", eventId).ExecuteAsync();
existing.Summary = "Updated";
var updateRequest = service.Events.Update(existing, "primary", eventId);
updateRequest.IfMatchETag = currentEtag; // Conflict detection
await updateRequest.ExecuteAsync();

// 4. Batch Operations (up to 50 requests)
var batch = new BatchRequest(service);
foreach (var evt in events)
{
    batch.Queue(service.Events.Insert(evt, "primary"),
        (content, error, i, message) => HandleBatchCallback(content, error));
}
await batch.ExecuteAsync();
```

### Toggl Track API Contract

**Authentication:**
```http
Authorization: Basic {base64(api_token:api_token)}
```

**Get Time Entries:**
```http
GET /api/v9/me/time_entries?start_date={iso8601}&end_date={iso8601}

Response 200:
[{
    "id": 1234567890,
    "workspace_id": 123456,
    "description": "Phone",
    "start": "2026-01-30T14:30:00+00:00",
    "stop": "2026-01-30T14:35:00+00:00",
    "duration": 300,
    "tags": ["personal"]
}]
```

### YouTube Data API Contract

**Get Video Metadata:**
```http
GET /youtube/v3/videos?part=snippet,contentDetails&id={videoId1},{videoId2}&key={apiKey}

Response 200:
{
    "items": [{
        "id": "VIDEO_ID",
        "snippet": {
            "title": "Video Title",
            "channelTitle": "Channel Name"
        },
        "contentDetails": {
            "duration": "PT15M30S"  // ISO 8601 duration
        }
    }]
}
```

### Microsoft Graph API Contract

**Outlook Calendar Events:**
```http
GET /v1.0/me/calendar/events?$filter=start/dateTime ge '{start}' and end/dateTime le '{end}'

Response 200:
{
    "value": [{
        "id": "EVENT_ID",
        "subject": "Meeting",
        "start": { "dateTime": "2026-01-30T14:00:00", "timeZone": "UTC" },
        "end": { "dateTime": "2026-01-30T15:00:00", "timeZone": "UTC" }
    }]
}
```

**Excel Cell Update:**
```http
PATCH /v1.0/me/drive/items/{fileId}/workbook/worksheets/Sheet1/range(address='B2')
Content-Type: application/json

{
    "values": [["Yes"]]
}
```

### Event Description Format (App-Published)

**Standard Format:**
```
{User-entered description content}

Published by Google Calendar Management on {ISO 8601 datetime}
```

**Example:**
```
Call with John Doe about project timeline.

Published by Google Calendar Management on 2026-01-30T14:30:00Z
```

**Coalesced YouTube Event:**
```
YouTube - Tech Channel, Music Channel, Educational Channel

Published by Google Calendar Management on 2026-01-30T14:30:00Z
```

## Security Architecture

### Credential Storage

**OAuth Tokens (Google, Microsoft):**
```csharp
// Encrypted storage using Windows DPAPI
UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    clientSecrets,
    scopes,
    "user",
    CancellationToken.None,
    new FileDataStore("GoogleCalendar.Auth")); // Auto-encrypted by library using DPAPI

// FileDataStore location: %APPDATA%/GoogleCalendar.Auth/
// Files encrypted at rest with Windows user account protection
```

**API Keys (Toggl, YouTube):**
```csharp
// Stored in appsettings.json during development (user-secrets)
// In production, encrypted in database config table
await _configService.SetEncryptedAsync("Toggl:ApiToken", token);
var token = await _configService.GetDecryptedAsync("Toggl:ApiToken");
```

**Database Encryption (Optional):**
```csharp
// SQLite supports encryption via SQLCipher (Tier 2+ enhancement)
// Tier 1: File system permissions only (Windows user account protection)
```

### Authentication Flows

**1. Google Calendar OAuth 2.0:**
```
1. User clicks "Connect Google Calendar"
2. Browser opens Google OAuth consent screen
3. User grants Calendar access
4. Callback with authorization code
5. Exchange for access token + refresh token
6. Store refresh token encrypted (FileDataStore with DPAPI)
7. Auto-refresh access token when expired (1 hour lifetime)
```

**2. Microsoft Graph OAuth 2.0:**
```
1. Azure AD app registration (one-time setup)
2. User clicks "Connect Outlook"
3. Browser opens Microsoft login (supports work/school accounts)
4. User grants Calendars.ReadWrite + Files.ReadWrite
5. Refresh token valid 90 days
6. App warns 7 days before expiration
7. User re-authenticates before expiration
```

**3. Toggl Track API Token:**
```
1. User gets API token from Toggl profile settings
2. User pastes token in app settings
3. App encrypts and stores in config table
4. Token used with Basic auth for all requests
```

### Data Privacy

**Local-Only Storage:**
- All personal data stored locally in SQLite database
- No telemetry, analytics, or usage tracking
- No cloud sync except explicit integrations (Google Calendar, Outlook, Excel)

**Network Communication:**
- All API calls over HTTPS/TLS 1.2+
- Certificate validation enforced
- No data sent to third parties except user-authorized services

**Audit Trail:**
- Complete audit_log for all operations
- User can review all API calls and data modifications
- Export audit log for compliance if needed

### Security Best Practices

**Code Security:**
- No SQL injection (parameterized queries via EF Core)
- No command injection (validated file paths)
- No XSS risk (desktop app, not web)
- Input validation on all user inputs

**Secrets Management:**
```csharp
// Development: dotnet user-secrets
dotnet user-secrets set "Google:Calendar:ClientId" "your-client-id"

// Production: Encrypted config table + Windows Credential Manager
```

## Performance Considerations

### Target Performance Metrics (NFRs)

**UI Responsiveness:**
- Calendar view renders in <1 second for month with 200+ events ✓
- Event editing appears instantly on click (<100ms) ✓
- Selection feedback immediate (<50ms) ✓
- Smooth 60 FPS animations for view transitions ✓

**Data Operations:**
- Import 500+ events completes in <5 seconds ✓
- Coalescing algorithms process week of data in <2 seconds ✓
- Database queries <100ms for typical operations ✓
- Google Calendar sync (fetch/publish) <10 seconds for 50 events ✓

**App Launch:**
- Cold start <2 seconds to usable UI ✓
- Database initialization <500ms ✓
- Google Calendar cache loaded in background (non-blocking) ✓

### Performance Strategies

**1. Database Optimization:**
```csharp
// Read-only queries with AsNoTracking()
var events = await _context.GcalEvents
    .Where(e => e.StartDatetime >= start && e.StartDatetime <= end)
    .AsNoTracking() // Skip change tracking overhead
    .ToListAsync();

// Batch inserts (500 events in one transaction)
using var transaction = await _context.Database.BeginTransactionAsync();
_context.GcalEvents.AddRange(events);
await _context.SaveChangesAsync();
await transaction.CommitAsync();

// Eager loading to avoid N+1 queries
var events = await _context.GcalEvents
    .Include(e => e.GeneratedEventSources)
    .ThenInclude(s => s.SourceData)
    .ToListAsync();
```

**2. Caching Strategy:**
```csharp
// In-memory cache for frequently accessed data (config, color definitions)
private static readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

// Cache API responses in database (see Key Decisions §15)
// Google Calendar: Incremental sync with sync tokens
// Toggl/YouTube: Full cache with user-triggered refresh
```

**3. Async/Await Throughout:**
```csharp
// ALL I/O operations async (database, API, file)
// Never block UI thread
public async Task LoadEventsAsync()
{
    IsLoading = true;
    try
    {
        Events = await _service.FetchEventsAsync(StartDate, EndDate);
    }
    finally
    {
        IsLoading = false;
    }
}
```

**4. Lazy Loading in UI:**
```csharp
// Load visible month only
// Fetch adjacent months on demand when user navigates
// Virtualize long lists (if event list view added in Tier 2+)
```

**5. Background Processing:**
```csharp
// Heavy operations on background thread
await Task.Run(() => _coalescingService.ProcessPhoneActivityAsync(entries));

// Progress updates on UI thread
Progress<int> progress = new Progress<int>(percent =>
{
    ProgressPercentage = percent; // UI updates
});
```

## Deployment Architecture

### Development Environment

**Local Development:**
- Developer machine: Windows 10/11
- Visual Studio 2022 or Rider
- SQLite database in project directory
- API credentials via user-secrets

### Production (Single User)

**Installation:**
- Self-contained .exe (all .NET runtime included)
- OR MSIX package for Windows Store distribution
- Installer creates AppData directory structure

**Application Files:**
```
C:\Program Files\GoogleCalendarManagement\
├── GoogleCalendarManagement.exe
├── GoogleCalendarManagement.dll
├── *.dll (dependencies)
└── Resources/

%APPDATA%\GoogleCalendarManagement\
├── calendar.db                     # User data
├── calendar.db-wal
├── calendar.db-shm
├── GoogleCalendar.Auth/            # OAuth tokens (encrypted)
├── MicrosoftGraph.Auth/
├── logs/                           # Application logs
│   ├── app-2026-01-30.txt
│   └── (optional) API response logs
└── data/
    ├── outlook_imports/            # Manual .ics imports
    └── youtube_takeout/            # Takeout JSON files
```

**Updates:**
- Tier 1: Manual installation of new versions
- Database migrations run automatically on app launch
- Backwards-compatible database schema
- Automatic backup before migrations

**Backup Strategy:**
```csharp
// User can manually backup database (copy calendar.db)
// Automatic backup before destructive operations (restore, migration)
// Export to CSV/JSON for external backup
```

## Development Environment

### Prerequisites

**Required Software:**
1. **Windows 10/11** (version 1809 or later)
2. **.NET 9 SDK** - [Download](.NET 9.0 Update - January 13, 2026)
   ```bash
   winget install Microsoft.DotNet.SDK.9
   ```
3. **Visual Studio 2022** (17.8+) or **Visual Studio 2026** with:
   - .NET desktop development workload
   - Windows App SDK components
4. **Git** for version control

**Optional Tools:**
- **SQLite Studio** or **DB Browser for SQLite** (database inspection)
- **Postman** or **REST Client** (API testing)

### Setup Commands

**1. Clone Repository:**
```bash
git clone <repository-url>
cd GoogleCalendarManagement
```

**2. Restore NuGet Packages:**
```bash
dotnet restore
```

**3. Configure User Secrets (Development):**
```bash
cd src/GoogleCalendarManagement
dotnet user-secrets init

# Google Calendar API
dotnet user-secrets set "Google:Calendar:ClientId" "your-client-id"
dotnet user-secrets set "Google:Calendar:ClientSecret" "your-client-secret"

# YouTube Data API
dotnet user-secrets set "Google:YouTube:ApiKey" "your-api-key"

# Toggl Track API
dotnet user-secrets set "Toggl:ApiToken" "your-api-token"

# Azure AD (Microsoft Graph)
dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
```

**4. Create Database:**
```bash
cd src/GoogleCalendarManagement.Data

# Add initial migration (if not exists)
dotnet ef migrations add InitialCreate

# Create database
dotnet ef database update

# Verify migration was successful
```

**5. Build Solution:**
```bash
cd ../.. # Back to solution root
dotnet build
```

**6. Run Application:**
```bash
cd src/GoogleCalendarManagement
dotnet run
```

**Or use Visual Studio:**
- Open `GoogleCalendarManagement.sln`
- Set `GoogleCalendarManagement` as startup project
- Press F5 to run

### Development Workflow

**Entity Framework Migrations:**
```bash
# Add new migration after schema changes
dotnet ef migrations add <MigrationName> --project src/GoogleCalendarManagement.Data

# Update database
dotnet ef database update --project src/GoogleCalendarManagement.Data

# Rollback migration
dotnet ef database update <PreviousMigrationName> --project src/GoogleCalendarManagement.Data

# Remove last migration (if not applied)
dotnet ef migrations remove --project src/GoogleCalendarManagement.Data
```

**Running Tests:**
```bash
dotnet test
```

**Build for Release:**
```bash
dotnet publish -c Release -r win-x64 --self-contained -o ./publish
```

## Architecture Decision Records (ADRs)

**Full decision rationale documented in:** [_key-decisions.md](\_key-decisions.md)

### ADR-001: .NET 9 + WinUI 3

**Decision:** Use .NET 9 with WinUI 3 for Windows desktop application

**Alternatives:** Electron (cross-platform web), WPF (mature .NET), .NET MAUI (cross-platform native)

**Rationale:**
- Native Windows performance (better than Electron)
- Future-proof (Microsoft's recommended path for Windows apps)
- Modern Fluent Design System built-in
- Perfect CalendarView control for our use case
- Extensible architecture (data layer can support future web/mobile UIs)

**Trade-offs:** Windows-only for Tier 1 (acceptable for single-user personal app)

---

### ADR-002: SQLite + Entity Framework Core

**Decision:** Local SQLite database with EF Core 9 ORM

**Alternatives:** JSON files, PostgreSQL/MySQL (client-server), cloud database

**Rationale:**
- Single-file portability (easy backup)
- No server overhead (desktop app doesn't need client-server)
- Cross-platform (works on Windows, Linux, Mac for future)
- EF Core: Code-first migrations, LINQ queries, excellent tooling
- Performance adequate for expected data volume (15K+ events)

**Trade-offs:** Schema changes require migrations (good practice anyway)

---

### ADR-003: Local-First Architecture

**Decision:** All data stored locally, API calls only for sync/import

**Alternatives:** Cloud-first (data in cloud DB), hybrid (local + cloud sync)

**Rationale:**
- User owns data (decades of personal history)
- Works offline for viewing and editing
- Privacy (no third-party cloud storage)
- Fast performance (no network latency)
- Independent from platform changes

**Trade-offs:** No automatic multi-device sync (Tier 2+ enhancement)

---

### ADR-004: Own Version History (Not Google's)

**Decision:** Maintain complete version history in local database

**Alternatives:** Rely on Google Calendar API (doesn't exist), ETags only

**Rationale:**
- **Google Calendar API does NOT provide version history**
- ETags only for optimistic concurrency control, not rollback
- User requirement: Save/restore functionality
- Full audit trail for decades of data

**Implementation:** `gcal_event_version` table stores full snapshots, rollback sends UPDATE requests to Google

**Trade-offs:** More local storage (acceptable), rollback creates new version on Google's side (acceptable, content restored)

---

### ADR-005: Source-Specific Coalescing Algorithms

**Decision:** Different coalescing logic for Phone (Toggl) vs YouTube

**Alternatives:** Generic coalescing algorithm, no coalescing (show all entries)

**Rationale:**
- Phone activity: Many tiny entries from iOS shortcuts → Sliding window with 15-min gaps
- YouTube: Multiple videos in one session → Sliding window with (duration + 30min) threshold
- Each source has unique data characteristics
- Configurable thresholds in database

**Trade-offs:** More complex logic (worth it for better UX), users must understand coalescing

---

### ADR-006: 8/15 Rounding Algorithm

**Decision:** Divide time into 15-minute blocks, keep blocks with ≥8 minutes activity

**Alternatives:** Simple rounding to nearest 15 minutes, precise times (14:37-15:12)

**Rationale:**
- Google Calendar displays 15-minute increments
- Simple rounding inflates/deflates durations inaccurately
- Precise times look odd in calendar view
- 8/15 rule preserves accuracy while aligning to 15-min grid

**Example:** 35-minute activity (14:37-15:12) → 45 minutes (14:30-15:15) after 8/15 rounding

**Trade-offs:** More complex than simple rounding (worth it for accuracy)

---

### ADR-007: Approval State In-Memory Until Publish

**Decision:** User approval lives in UI state, only persist to database after publishing to Google Calendar

**Alternatives:** Store `user_approved` boolean in database before publishing

**Rationale:**
- Simpler state machine: Database = published state
- Clearer UX: "Approve and Publish" is atomic operation
- No orphaned approvals if app closes before publishing
- One source of truth: `published_to_gcal = TRUE` means it's on Google Calendar

**Trade-offs:** If app crashes during publish, some events may be published but not recorded locally (mitigated by sync from Google on restart)

---

### ADR-008: Multi-Dimensional Date State Tracking

**Decision:** Track multiple independent state flags per date

**Alternatives:** Single "complete" boolean per date

**Rationale:**
- User wants granular progress tracking per data source
- "Did I import call logs for this week?" → Check `call_log_data_published`
- "Which data sources am I missing for this date?" → Compare all flags
- Flexible: User can fill data sources in any order

**Implementation:** `date_state` table with separate flags: `call_log_data_published`, `youtube_data_published`, `toggl_data_published`, `complete_walkthrough_approval`

**Trade-offs:** More columns in database (acceptable), more complex state management (mitigated by EF Core)

---

### ADR-009: YouTube Watch History via Google Takeout

**Decision:** Manual Google Takeout download + YouTube Data API for metadata

**Alternatives:** Build Chrome extension immediately, try to scrape MyActivity

**Rationale:**
- **YouTube Data API does NOT provide watch history access** (deprecated 2016)
- Google Takeout provides historical data (years back)
- YouTube Data API provides video metadata (duration, channel, title)
- Chrome extension deferred to Tier 2+ (not blocking MVP)

**Workflow:** User downloads Takeout JSON → App parses video IDs → Batch fetch metadata → Coalesce sessions

**Trade-offs:** Manual download process (acceptable for Tier 1), not real-time (future enhancement)

---

### ADR-010: Aggressive API Caching

**Decision:** Cache all API responses locally, user triggers refresh

**Alternatives:** Always query API (fresh but slow), periodic auto-refresh

**Rationale:**
- Fast app startup and navigation
- Works offline (view cached data)
- Reduces API quota usage
- Predictable behavior (user controls when to refresh)
- Preserves historical data even if deleted from source

**Implementation:** All API data stored in database, `data_source_refresh` table tracks last refresh, Google Calendar uses incremental sync tokens

**Trade-offs:** Data may be stale (acceptable, user can refresh)

---

### ADR-011: Singular Table Names

**Decision:** Database table names are singular: `gcal_event`, `date_state`

**Alternatives:** Plural table names (Rails convention): `gcal_events`, `date_states`

**Rationale:**
- .NET/EF Core best practice (matches entity class names)
- Clarity: `toggl_data` table contains multiple records, each is a "datum"
- Consistency with C# naming (classes are singular)

---

### ADR-012: Store All Data, Filter with Flags

**Decision:** Store ALL imported data, use `visible_as_event = FALSE` for filtered items

**Alternatives:** Don't import filtered data, delete after import, separate filtered table

**Rationale:**
- Complete audit trail (never lose data)
- User can change filtering rules later
- Data analysis possible (e.g., "How many short calls do I get?")
- Reversible decisions

**Example:** Calls <3 minutes → `visible_as_event = FALSE` but still stored

**Trade-offs:** More database rows (acceptable, still small data volume)

---

### ADR-013: Phase-Gated Conflict Resolution Strategy

**Decision:** Evolve conflict resolution strategy across three phases

**Alternatives:** Single conflict resolution strategy for all phases, always GoogleWins, always LocalWins

**Rationale:**
- **Tier 1 (Read-Only Viewer):** User cannot edit, GoogleWins is simplest and correct
- **Tier 2 (Editing Enabled):** User can edit both locally and in Google Calendar, MergeTimestamp uses recency
- **Tier 3 (Verification Priority):** User has completed walkthrough approval, verified events are canonical truth

**Phase Progression:**

| Phase | Strategy | When Applied |
|-------|----------|--------------|
| Tier 1 | GoogleWins | Always (user can't edit, Google is source of truth) |
| Tier 2 | MergeTimestamp | Compare `gcal_updated_at` vs `app_last_modified_at`, newer wins |
| Tier 3 | LocalWins | Verified events (`app_created=TRUE` + `complete_walkthrough_approval=TRUE`) take precedence |

**Implementation:**
- All phases use ETags for conflict detection (optimistic concurrency control)
- Conflict resolution logic branches based on current phase and verification status
- Version history preserved in `gcal_event_version` regardless of resolution

**Benefits:**
- Simple logic in Tier 1 (no edge cases)
- Fair resolution in Tier 2 (respects most recent change)
- Protects verified data in Tier 3 (user's canonical truth)
- Gradual complexity increase matches feature rollout

**Trade-offs:** Tier 3 requires understanding verification semantics (acceptable for power users)

---

### ADR-014: Separate `pending_event` Table

**Decision:** Store unpushed local edits in separate `pending_event` table (not in `gcal_event`)

**Alternatives:**
- Store in `gcal_event` with random/temporary IDs
- Single unified table with `sync_status` column

**Rationale:**

**Why Separate Table:**
1. **Cleaner Semantics:** `gcal_event` = events that exist in Google Calendar, `pending_event` = local-only drafts
2. **Simpler Queries:** Filter by table instead of complex WHERE clauses
3. **No Pollution:** `gcal_event_version` doesn't get cluttered with unpublished changes
4. **Clear UI Logic:** Translucent events = query pending table, opaque = query gcal table
5. **Easier Validation:** Can enforce constraints differently (pending events don't have ETags)

**ID Strategy:**
- `pending_event` uses random GUID: `pending_{8-char-hex}` (e.g., `pending_a1b2c3d4`)
- On successful publish, event moved to `gcal_event` with Google's actual ID
- No ID mapping table needed - just delete pending and insert gcal

**Publish Workflow:**
```
User creates event → pending_event (60% opacity)
User clicks "Push to GCal" → API call
Success → move to gcal_event (100% opacity), delete pending
Failure → store error in pending_event, keep for retry
```

**Benefits:**
- Semantic clarity (separate concerns)
- Performance (no need to filter gcal_event by status)
- Data integrity (gcal_event always mirrors Google's state)
- Simpler version history tracking

**Trade-offs:**
- Additional table (acceptable, only 1 extra table in Tier 2)
- Move operation on publish (fast, single transaction)

---

### ADR-015: Sync Status via Existing `data_source_refresh` Table

**Decision:** Use existing `data_source_refresh` table for sync status indicators (not a new table)

**Alternatives:**
- New `sync_status` table per date
- Boolean flags in `date_state` table
- Infer from presence/absence of events

**Rationale:**

**Why Reuse Existing Table:**
1. **No new table needed** - Leverages existing refresh tracking infrastructure
2. **Richer metadata** - Already tracks `last_refreshed_at`, `success`, `error_message`, `records_fetched`
3. **Date range support** - Can track sync for multi-day ranges efficiently
4. **Distinguish empty vs never synced** - Record exists with `records_fetched=0` vs no record

**Sync Indicator Logic:**
```sql
-- Green indicator: Recent successful sync
SELECT last_refreshed_at > datetime('now', '-1 hour') AND success = TRUE

-- Grey indicator: No sync, old sync, or failed sync
SELECT NOT EXISTS(...) OR last_refreshed_at < ... OR success = FALSE
```

**UI Display:**
- **Green:** Synced within staleness threshold (configurable, default 1 hour)
- **Grey:** Never synced, stale, or last sync failed

**Benefits:**
- Reuses existing infrastructure
- No schema changes needed
- Can track sync errors per date range
- Configurable staleness threshold
- Supports batched date range syncs

**Trade-offs:**
- Date range overlap queries slightly more complex (mitigated by indexes)
- Need to handle partial overlaps (e.g., synced Jan 1-10, checking Jan 5)

**Index Performance:**
```sql
CREATE INDEX idx_refresh_date ON data_source_refresh(
    source_name, start_date, end_date
);
```

Fast lookups even with date range overlaps.

---

## Phase-Specific Architecture

This section documents architectural patterns that evolve across the three development phases.

### Tier 1: Read-Only Viewer (Foundation)

**Goal:** Local mirror of Google Calendar with save/restore capability

**Database Tables (7):**
- `gcal_event` - Synced calendar events
- `gcal_event_version` - Version history
- `save_state` - Snapshots for rollback
- `audit_log` - Operation tracking
- `config` - App configuration
- `data_source_refresh` - Sync metadata
- `system_state` - App-level state

**Key Architectural Patterns:**

**1. Sync from Google Calendar:**
```csharp
// Tier 1: Simple one-way sync
async Task SyncFromGoogleCalendar(DateTime startDate, DateTime endDate) {
    var request = _calendarService.Events.List("primary");
    request.TimeMin = startDate;
    request.TimeMax = endDate;
    request.ShowDeleted = true;  // Track deletions

    var events = await request.ExecuteAsync();

    foreach (var googleEvent in events.Items) {
        var localEvent = await _db.GcalEvents.FindAsync(googleEvent.Id);

        if (localEvent == null) {
            // New event - insert
            await _db.GcalEvents.AddAsync(MapToEntity(googleEvent));
        } else if (localEvent.GcalEtag != googleEvent.ETag) {
            // Changed event - Tier 1: GoogleWins
            UpdateFromGoogle(localEvent, googleEvent);
        }
        // else: No change, skip
    }

    await _db.SaveChangesAsync();

    // Track sync completion
    await _db.DataSourceRefreshes.AddAsync(new DataSourceRefresh {
        SourceName = "gcal",
        StartDate = startDate,
        EndDate = endDate,
        LastRefreshedAt = DateTime.UtcNow,
        RecordsFetched = events.Items.Count,
        Success = true
    });
    await _db.SaveChangesAsync();
}
```

**2. Save/Restore Workflow:**
```csharp
// Create save state (snapshot of gcal_event table)
async Task CreateSaveState(string saveName, string description) {
    var allEvents = await _db.GcalEvents.ToListAsync();
    var snapshot = allEvents.ToDictionary(
        e => e.GcalEventId,
        e => new {
            e.Summary, e.Description, e.StartDateTime,
            e.EndDateTime, e.ColorId, e.GcalEtag
        }
    );

    await _db.SaveStates.AddAsync(new SaveState {
        SaveName = saveName,
        SaveDescription = description,
        SnapshotData = JsonSerializer.Serialize(snapshot)
    });
    await _db.SaveChangesAsync();
}

// Restore from save state (push to Google Calendar)
async Task RestoreFromSaveState(int saveId) {
    var saveState = await _db.SaveStates.FindAsync(saveId);
    var snapshot = JsonSerializer.Deserialize<Dictionary<string, object>>(
        saveState.SnapshotData
    );

    foreach (var (eventId, eventData) in snapshot) {
        var request = new Event {
            Summary = eventData.Summary,
            // ... other fields
        };

        // Push to Google Calendar
        await _calendarService.Events.Update(
            request, "primary", eventId
        ).ExecuteAsync();

        // Update local cache
        var localEvent = await _db.GcalEvents.FindAsync(eventId);
        UpdateFromSnapshot(localEvent, eventData);
    }

    await _db.SaveChangesAsync();
}
```

**3. UI Event Rendering:**
```csharp
// Tier 1: All events opaque (100% opacity)
// No translucent events (user can't create local edits)
foreach (var calendarEvent in gcalEvents) {
    var uiEvent = new CalendarEventViewModel {
        Id = calendarEvent.GcalEventId,
        Summary = calendarEvent.Summary,
        Opacity = 1.0,  // Always 100% in Tier 1
        IsSelected = false
    };
    EventList.Add(uiEvent);
}
```

**Tier 1 Constraints:**
- ❌ No local editing (read-only)
- ❌ No pending events
- ❌ No conflict resolution (GoogleWins always)
- ✅ Save/restore via Google Calendar API
- ✅ Version history tracking
- ✅ Offline viewing of cached data

---

### Tier 2: Editing & Publishing (Interaction)

**Goal:** Full manual calendar management with local edits and push to Google Calendar

**Database Changes (+1 Table = 8 Total):**
- **NEW:** `pending_event` - Unpushed local edits

**Key Architectural Patterns:**

**1. Create/Edit Events Locally:**
```csharp
// User creates new event
async Task CreateEvent(EventEditModel model) {
    var pendingEvent = new PendingEvent {
        PendingEventId = $"pending_{Guid.NewGuid():N}"[..16],  // "pending_a1b2c3d4"
        CalendarId = "primary",
        Summary = model.Summary,
        StartDateTime = model.StartDateTime,
        EndDateTime = model.EndDateTime,
        AppCreated = true,
        SourceSystem = "manual",
        ReadyToPublish = false  // User must explicitly publish
    };

    await _db.PendingEvents.AddAsync(pendingEvent);
    await _db.SaveChangesAsync();

    // UI shows event with 60% opacity
}

// User edits existing Google Calendar event
async Task EditEvent(string gcalEventId, EventEditModel model) {
    var gcalEvent = await _db.GcalEvents.FindAsync(gcalEventId);

    // Move to pending_event with changes
    var pendingEvent = new PendingEvent {
        PendingEventId = $"pending_{Guid.NewGuid():N}"[..16],
        CalendarId = gcalEvent.CalendarId,
        Summary = model.Summary,  // Modified
        StartDateTime = model.StartDateTime,
        EndDateTime = model.EndDateTime,
        AppCreated = gcalEvent.AppCreated,
        SourceSystem = gcalEvent.SourceSystem,
        ReadyToPublish = false
    };

    await _db.PendingEvents.AddAsync(pendingEvent);
    // Keep original in gcal_event until publish succeeds
    await _db.SaveChangesAsync();
}
```

**2. Push to Google Calendar:**
```csharp
async Task PublishPendingEvents() {
    var pendingEvents = await _db.PendingEvents
        .Where(e => e.ReadyToPublish)
        .ToListAsync();

    foreach (var pending in pendingEvents) {
        try {
            var request = new Event {
                Summary = pending.Summary,
                Start = new EventDateTime { DateTime = pending.StartDateTime },
                End = new EventDateTime { DateTime = pending.EndDateTime },
                Description = $"{pending.Description}\n\n" +
                    $"Published by Google Calendar Management on {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            };

            // Create in Google Calendar
            var created = await _calendarService.Events.Insert(
                request, pending.CalendarId
            ).ExecuteAsync();

            // Move to gcal_event with real Google ID
            await _db.GcalEvents.AddAsync(new GcalEvent {
                GcalEventId = created.Id,  // Real Google ID
                CalendarId = pending.CalendarId,
                Summary = pending.Summary,
                StartDateTime = pending.StartDateTime,
                EndDateTime = pending.EndDateTime,
                AppCreated = true,
                AppPublished = true,
                AppPublishedAt = DateTime.UtcNow,
                GcalEtag = created.ETag,
                GcalUpdatedAt = created.Updated,
                SourceSystem = pending.SourceSystem
            });

            // Delete from pending
            _db.PendingEvents.Remove(pending);
            await _db.SaveChangesAsync();

        } catch (GoogleApiException ex) {
            // Log error, keep in pending for retry
            pending.PublishError = ex.Message;
            pending.PublishAttemptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
```

**3. UI Event Rendering with Opacity:**
```csharp
// Tier 2: Render both gcal_event and pending_event
async Task<List<CalendarEventViewModel>> LoadEventsForDate(DateTime date) {
    var events = new List<CalendarEventViewModel>();

    // Published events (100% opacity)
    var gcalEvents = await _db.GcalEvents
        .Where(e => e.StartDateTime.Date == date.Date)
        .ToListAsync();
    foreach (var evt in gcalEvents) {
        events.Add(new CalendarEventViewModel {
            Id = evt.GcalEventId,
            Summary = evt.Summary,
            Opacity = 1.0,  // 100% - published
            IsPending = false
        });
    }

    // Pending events (60% opacity)
    var pendingEvents = await _db.PendingEvents
        .Where(e => e.StartDateTime.Date == date.Date)
        .ToListAsync();
    foreach (var evt in pendingEvents) {
        events.Add(new CalendarEventViewModel {
            Id = evt.PendingEventId,
            Summary = evt.Summary,
            Opacity = 0.6,  // 60% - unpushed
            IsPending = true
        });
    }

    return events;
}
```

**4. Conflict Resolution - MergeTimestamp:**
```csharp
// Tier 2: Compare timestamps on sync
async Task SyncFromGoogleCalendar(DateTime startDate, DateTime endDate) {
    var events = await _calendarService.Events.List("primary").ExecuteAsync();

    foreach (var googleEvent in events.Items) {
        var localEvent = await _db.GcalEvents.FindAsync(googleEvent.Id);

        if (localEvent != null && localEvent.GcalEtag != googleEvent.ETag) {
            // Conflict detected - Tier 2: MergeTimestamp
            if (googleEvent.Updated > localEvent.AppLastModifiedAt) {
                // Google's version is newer - update local
                UpdateFromGoogle(localEvent, googleEvent);
            } else {
                // Local version is newer - push to Google
                await PushToGoogle(localEvent);
            }
        }
    }

    await _db.SaveChangesAsync();
}
```

**Tier 2 New Capabilities:**
- ✅ Create/edit events locally
- ✅ Translucent display for unpushed events
- ✅ Push to Google Calendar
- ✅ Conflict resolution (MergeTimestamp)
- ✅ Red outline for selected events
- ⚠️ No data sources yet (manual entry only)

---

### Tier 3: Data Sources & Automation (Intelligence)

**Goal:** Transform backfilling into satisfying ritual with automated data integration

**Database Changes (+7 Tables = 15 Total):**
- **NEW:** `toggl_data` - Toggl Track time entries
- **NEW:** `youtube_data` - YouTube watch history
- **NEW:** `call_log_data` - iOS call logs
- **NEW:** `generated_event_source` - Coalesced event links
- **NEW:** `date_state` - Per-date completion tracking
- **NEW:** `tracked_gap` - Date range gaps
- **NEW:** `weekly_state` - ISO 8601 week tracking

**Key Architectural Patterns:**

**1. Data Source Import & Coalescing:**
```csharp
// Import Toggl data and apply coalescing
async Task ImportTogglData(DateTime startDate, DateTime endDate) {
    var timeEntries = await _togglApi.GetTimeEntries(startDate, endDate);

    // Store all entries
    foreach (var entry in timeEntries) {
        await _db.TogglData.AddAsync(new TogglData {
            TogglId = entry.Id,
            Description = entry.Description,
            StartTime = entry.Start,
            EndTime = entry.End,
            DurationSeconds = entry.Duration,
            VisibleAsEvent = entry.Duration >= 300  // ≥5 minutes
        });
    }
    await _db.SaveChangesAsync();

    // Coalesce phone activity
    var phoneEntries = await _db.TogglData
        .Where(e => e.Description == "Phone" || e.Description == "ToDelete")
        .OrderBy(e => e.StartTime)
        .ToListAsync();

    var coalesced = CoalescePhoneActivity(phoneEntries);

    foreach (var block in coalesced) {
        // Create pending event (will be published after user approval)
        var pendingEvent = await CreatePendingEvent(
            summary: "Phone Activity",
            start: block.Start,
            end: block.End,
            sourceSystem: "toggl"
        );

        // Link source entries
        foreach (var sourceEntry in block.SourceEntries) {
            await _db.GeneratedEventSources.AddAsync(new GeneratedEventSource {
                GcalEventId = pendingEvent.PendingEventId,  // Will update after publish
                SourceTable = "toggl_data",
                SourceId = sourceEntry.TogglId
            });
        }
    }

    await _db.SaveChangesAsync();
}
```

**2. Conflict Resolution - LocalWins for Verified Events:**
```csharp
// Tier 3: Protect verified events
async Task SyncFromGoogleCalendar(DateTime startDate, DateTime endDate) {
    var events = await _calendarService.Events.List("primary").ExecuteAsync();

    foreach (var googleEvent in events.Items) {
        var localEvent = await _db.GcalEvents.FindAsync(googleEvent.Id);

        if (localEvent != null && localEvent.GcalEtag != googleEvent.ETag) {
            // Conflict detected
            var dateState = await _db.DateStates.FindAsync(
                localEvent.StartDateTime.Date
            );

            if (localEvent.AppCreated &&
                dateState?.CompleteWalkthroughApproval == true) {
                // Tier 3: LocalWins - verified event
                await PushToGoogle(localEvent);
                LogConflictResolution(localEvent.GcalEventId, "LocalWins");
            } else if (googleEvent.Updated > localEvent.AppLastModifiedAt) {
                // Not verified - use timestamp
                UpdateFromGoogle(localEvent, googleEvent);
                LogConflictResolution(localEvent.GcalEventId, "MergeTimestamp-GoogleWins");
            } else {
                // Local newer but not verified
                await PushToGoogle(localEvent);
                LogConflictResolution(localEvent.GcalEventId, "MergeTimestamp-LocalWins");
            }
        }
    }

    await _db.SaveChangesAsync();
}
```

**3. Date State Tracking:**
```csharp
// Update date state after publishing data sources
async Task PublishDataSourceEvents(DateTime date, string sourceType) {
    // Publish all pending events for this date/source
    var pendingEvents = await _db.PendingEvents
        .Where(e => e.StartDateTime.Date == date.Date &&
                    e.SourceSystem == sourceType)
        .ToListAsync();

    foreach (var pending in pendingEvents) {
        await PublishToGoogleCalendar(pending);
    }

    // Update date_state
    var dateState = await _db.DateStates.FindOrCreateAsync(date.Date);
    switch (sourceType) {
        case "toggl":
            dateState.TogglDataPublished = true;
            dateState.TogglDataPublishedAt = DateTime.UtcNow;
            break;
        case "youtube":
            dateState.YoutubeDataPublished = true;
            dateState.YoutubeDataPublishedAt = DateTime.UtcNow;
            break;
        case "call_log":
            dateState.CallLogDataPublished = true;
            dateState.CallLogDataPublishedAt = DateTime.UtcNow;
            break;
    }

    await _db.SaveChangesAsync();
}

// Complete walkthrough approval
async Task CompleteWalkthroughApproval(DateTime date) {
    var dateState = await _db.DateStates.FindOrCreateAsync(date.Date);
    dateState.CompleteWalkthroughApproval = true;
    dateState.CompleteWalkthroughApprovalAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();

    // All events for this date now protected by LocalWins conflict resolution
}
```

**4. Contiguity Tracking:**
```csharp
// Check if contiguity is maintained
async Task<bool> IsContiguousSinceStart() {
    var startDate = await _db.SystemStates
        .Where(s => s.StateName == "contiguity_start_date")
        .Select(s => DateTime.Parse(s.StateValue))
        .FirstOrDefaultAsync();

    if (startDate == default) return true;  // No start date set

    // Check each date from start to today
    for (var date = startDate.Date; date <= DateTime.Today; date = date.AddDays(1)) {
        var dateState = await _db.DateStates.FindAsync(date);

        if (dateState == null ||
            (!dateState.CompleteWalkthroughApproval && !dateState.PartOfTrackedGap)) {
            // Gap found - contiguity broken
            return false;
        }
    }

    return true;  // Contiguous
}
```

**Tier 3 Full Capabilities:**
- ✅ All Tier 1 & 2 features
- ✅ Toggl, YouTube, Call Log imports
- ✅ Coalescing algorithms
- ✅ Per-date state tracking
- ✅ Complete walkthrough approval
- ✅ LocalWins conflict resolution for verified events
- ✅ Contiguity tracking
- ✅ Weekly status Excel sync

---

## Additional Architectural Considerations

### 1. Event Ownership Flag (`app_created`)

**Purpose:** Track whether events were created by this app vs external sources

**Implementation:**
```csharp
// When syncing from Google Calendar
var gcalEvent = new GcalEvent {
    GcalEventId = googleEvent.Id,
    AppCreated = googleEvent.Description?.Contains(
        "Published by Google Calendar Management"
    ) ?? false
};
```

**Benefits:**
- Simple boolean flag (not complex source classification)
- Easy to query: "Show me all events I created in the app"
- Used in Tier 3 conflict resolution (LocalWins for `app_created=TRUE` + verified)
- Protects user's canonical data from external modifications

**Usage in Conflict Resolution:**
```csharp
if (localEvent.AppCreated && dateState?.CompleteWalkthroughApproval == true) {
    // This is the user's verified truth - protect it
    await PushToGoogle(localEvent);
}
```

---

### 2. Year View Performance Optimization

**Challenge:** Loading 365+ days of events for year view can be slow

**Recommended Approach:**
```csharp
// Lazy load year view with virtualization
public class YearViewModel {
    // Load only visible months
    public async Task LoadVisibleMonths(int firstVisibleMonth, int lastVisibleMonth) {
        var startDate = new DateTime(_year, firstVisibleMonth, 1);
        var endDate = new DateTime(_year, lastVisibleMonth,
            DateTime.DaysInMonth(_year, lastVisibleMonth));

        // Only load events for visible range
        var events = await _db.GcalEvents
            .Where(e => e.StartDateTime >= startDate && e.StartDateTime <= endDate)
            .AsNoTracking()  // Read-only for performance
            .ToListAsync();

        // Aggregate by date for year view (don't show individual events)
        var dateGroups = events.GroupBy(e => e.StartDateTime.Date)
            .Select(g => new DateSummary {
                Date = g.Key,
                EventCount = g.Count(),
                HasAppCreatedEvents = g.Any(e => e.AppCreated)
            });

        UpdateYearView(dateGroups);
    }
}
```

**Optimization Techniques:**
1. **Virtualization:** Only load visible months as user scrolls
2. **Aggregation:** Show event counts, not individual events
3. **AsNoTracking:** Read-only queries for 30-40% performance boost
4. **Indexed Queries:** Use `idx_gcal_event_date` index for fast range queries
5. **Caching:** Cache month summaries in memory

**Expected Performance:**
- Load 1 month: ~5-10ms (300-500 events)
- Load full year (lazy): Same as 1 month (only visible portion)
- Scroll to new month: ~10ms (incremental load)

---

### 3. Conflict Resolution Code Patterns

**Pattern 1: ETag-Based Optimistic Concurrency**
```csharp
public class GcalSyncService {
    async Task<bool> PushEventToGoogle(GcalEvent localEvent) {
        try {
            var request = MapToGoogleEvent(localEvent);

            // Include If-Match header with current ETag
            var updateRequest = _calendarService.Events.Update(
                request, localEvent.CalendarId, localEvent.GcalEventId
            );

            // Google will reject if ETag doesn't match
            var updated = await updateRequest.ExecuteAsync();

            // Success - update local ETag
            localEvent.GcalEtag = updated.ETag;
            localEvent.GcalUpdatedAt = updated.Updated;
            await _db.SaveChangesAsync();

            return true;

        } catch (GoogleApiException ex) when (ex.HttpStatusCode == 412) {
            // 412 Precondition Failed - ETag mismatch
            await HandleConflict(localEvent);
            return false;
        }
    }

    async Task HandleConflict(GcalEvent localEvent) {
        // Fetch latest from Google
        var googleEvent = await _calendarService.Events.Get(
            localEvent.CalendarId, localEvent.GcalEventId
        ).ExecuteAsync();

        // Apply phase-specific resolution
        var phase = await GetCurrentPhase();
        switch (phase) {
            case 1:
                // GoogleWins
                UpdateFromGoogle(localEvent, googleEvent);
                break;

            case 2:
                // MergeTimestamp
                if (googleEvent.Updated > localEvent.AppLastModifiedAt) {
                    UpdateFromGoogle(localEvent, googleEvent);
                } else {
                    await RetryPushToGoogle(localEvent);
                }
                break;

            case 3:
                // LocalWins for verified events
                var dateState = await _db.DateStates.FindAsync(
                    localEvent.StartDateTime.Date
                );
                if (localEvent.AppCreated &&
                    dateState?.CompleteWalkthroughApproval == true) {
                    await RetryPushToGoogle(localEvent);
                } else if (googleEvent.Updated > localEvent.AppLastModifiedAt) {
                    UpdateFromGoogle(localEvent, googleEvent);
                } else {
                    await RetryPushToGoogle(localEvent);
                }
                break;
        }

        await _db.SaveChangesAsync();
    }
}
```

**Pattern 2: Audit Trail for Conflict Resolutions**
```csharp
async Task LogConflictResolution(
    string eventId,
    string resolution,
    string googleEtag,
    string localEtag
) {
    await _db.AuditLogs.AddAsync(new AuditLog {
        OperationTyp = "conflict_resolution",
        OperationDetails = JsonSerializer.Serialize(new {
            EventId = eventId,
            Resolution = resolution,  // "GoogleWins", "LocalWins", "MergeTimestamp"
            GoogleEtag = googleEtag,
            LocalEtag = localEtag
        }),
        UserAction = false,  // Automatic
        Success = true
    });
    await _db.SaveChangesAsync();
}
```

**Pattern 3: Graceful Degradation on Sync Errors**
```csharp
async Task SyncWithResilience(DateTime startDate, DateTime endDate) {
    try {
        await SyncFromGoogleCalendar(startDate, endDate);

        // Update sync status - success
        await _db.DataSourceRefreshes.AddAsync(new DataSourceRefresh {
            SourceName = "gcal",
            StartDate = startDate,
            EndDate = endDate,
            Success = true,
            LastRefreshedAt = DateTime.UtcNow
        });

    } catch (GoogleApiException ex) {
        // Log error, keep stale data
        await _db.DataSourceRefreshes.AddAsync(new DataSourceRefresh {
            SourceName = "gcal",
            StartDate = startDate,
            EndDate = endDate,
            Success = false,
            ErrorMessage = ex.Message,
            LastRefreshedAt = DateTime.UtcNow
        });

        // UI shows grey indicator, user can retry
        // App continues working with cached data
    }

    await _db.SaveChangesAsync();
}
```

---

## References & Related Documents

- **Product Requirements:** [PRD.md](PRD.md)
- **Epic Breakdown:** [epics.md](epics.md)
- **Key Decisions Rationale:** [_key-decisions.md](\_key-decisions.md)
- **Database Schema Details:** [_database-schemas.md](\_database-schemas.md)
- **Technology Stack Research:** [_technology-stack.md](\_technology-stack.md)

## Next Steps

**After Architecture Approval:**

1. **Run:** `workflow validate-architecture` (optional validation)
2. **Run:** `workflow solutioning-gate-check` (verify PRD + Architecture + Stories alignment)
3. **Run:** `workflow sprint-planning` (create sprint status tracking)
4. **Begin Implementation:** Start with Epic 1 (Foundation & Core Infrastructure)

**First Story to Implement:**
- Project initialization: `dotnet new winui3 -n GoogleCalendarManagement -f net9.0`
- Setup database with initial EF Core migration
- Configure dependency injection and logging

---

_Generated by BMAD Decision Architecture Workflow v1.3.2_
_Date: 2026-01-30_
_For: Sarunas Budreckis_
_Project: Google Calendar Management_

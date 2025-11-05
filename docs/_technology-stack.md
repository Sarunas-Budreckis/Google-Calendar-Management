# Technology Stack Research - Google Calendar Management

**Research Date:** 2025-11-05
**Project:** Google Calendar Management
**Platform:** Windows Desktop Application

## Overview

This document contains research findings for all technologies, APIs, and libraries used in Phase 1 of the Google Calendar Management application.

---

## 1. Framework & Runtime

### .NET 9

**Status:** ✅ Selected

**Key Features:**
- Latest LTS release
- Enhanced performance over .NET 8
- Full Windows desktop support
- Modern C# language features
- Native AOT compilation support (future optimization)

**Installation:**
- SDK: Download from https://dotnet.microsoft.com/download/dotnet/9.0
- Runtime included with SDK

**Project Type:** Console Application or WinUI 3 Application template

---

## 2. UI Framework

### WinUI 3 (Windows App SDK)

**Status:** ✅ Selected

**Why WinUI 3:**
- **Modern Fluent Design** - Native Windows 11 aesthetic
- **Future-proof** - Microsoft's recommended path for native Windows apps
- **Built-in CalendarView control** - Perfect for our use case
- **Native performance** - Not web-based like Electron
- **Windows App SDK 1.6+** - Stable and production-ready

**Key Components:**
- `Microsoft.UI.Xaml.Controls.CalendarView` - Main calendar display
- XAML for declarative UI
- MVVM pattern support
- Data binding for dynamic event display

**NuGet Packages:**
```xml
<PackageReference Include="Microsoft.WindowsAppSDK" Version="1.6.x" />
<PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.x" />
```

**Third-Party Calendar Libraries (Optional):**
- **Syncfusion WinUI Calendar** - Advanced features, commercial license
- **ComponentOne WinUI Edition** - Enterprise-grade controls, commercial

**Recommendation:** Start with built-in CalendarView, evaluate third-party if needed for Phase 2+

**Resources:**
- Official docs: https://learn.microsoft.com/en-us/windows/apps/winui/
- CalendarView docs: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.calendarview

**Alternative Considered:** WPF (more mature ecosystem, but WinUI 3 is better for future)

---

## 3. Database & ORM

### SQLite

**Status:** ✅ Selected

**Why SQLite:**
- **Single file database** - Easy backup and portability
- **Cross-platform** - Works on Windows, Linux, Mac
- **No server required** - Perfect for desktop app
- **Excellent performance** - Handles our expected data volume easily
- **ACID compliant** - Transaction support for rollbacks
- **Full-text search** - For future fuzzy search feature

**Data Volume Estimates:**
- ~1,825 date records (5 years)
- ~10,000+ Toggl entries
- ~15,000+ total events
- **Verdict:** Well within SQLite's capabilities (tested up to terabytes)

**File Location:** `{AppData}/GoogleCalendarManagement/calendar.db`

### Entity Framework Core

**Status:** ✅ Selected

**Version:** EF Core 9 (matches .NET 9)

**Why EF Core:**
- **Code-first migrations** - Easy schema evolution
- **LINQ queries** - Type-safe, readable
- **Change tracking** - Automatic for updates
- **Async/await** - Non-blocking database operations
- **Excellent .NET 9 support**

**NuGet Packages:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.x" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.x" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.x" />
```

**Best Practices:**
```csharp
// Configuration
services.AddDbContext<CalendarDbContext>(options =>
    options.UseSqlite("Data Source=calendar.db"));

// Async queries
var events = await context.GcalEvents
    .Where(e => e.StartDatetime >= startDate)
    .AsNoTracking() // Read-only
    .ToListAsync();

// Transactions for batch operations
using var transaction = await context.Database.BeginTransactionAsync();
try
{
    // Multiple operations
    await context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**Resources:**
- EF Core + SQLite: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/
- Sample project: https://github.com/jasonsturges/sqlite-dotnet-core

---

## 4. API Integrations

### 4.1 Google Calendar API

**Status:** ✅ Active, Well-Supported

**NuGet Package:**
```xml
<PackageReference Include="Google.Apis.Calendar.v3" Version="1.69.0.x" />
<PackageReference Include="Google.Apis.Auth" Version="1.69.0.x" />
```

**Platform Support:**
- ✅ .NET 6.0+
- ✅ .NET Standard 2.0
- ✅ .NET Framework 4.6.2+
- ❌ UWP, Xamarin (not needed for us)

**Authentication:**
OAuth 2.0 with refresh tokens (perfect for desktop apps)

```csharp
// Desktop app flow
UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
    new ClientSecrets { ClientId = "...", ClientSecret = "..." },
    new[] { CalendarService.Scope.Calendar },
    "user",
    CancellationToken.None,
    new FileDataStore("GoogleCalendar.Auth"));

var service = new CalendarService(new BaseClientService.Initializer
{
    HttpClientInitializer = credential,
    ApplicationName = "Google Calendar Management"
});
```

**Key Operations:**
```csharp
// List events
var request = service.Events.List("primary");
request.TimeMin = startDate;
request.TimeMax = endDate;
request.MaxResults = 2500;
var events = await request.ExecuteAsync();

// Insert event
var newEvent = new Event
{
    Summary = "Event Title",
    Start = new EventDateTime { DateTime = startTime },
    End = new EventDateTime { DateTime = endTime },
    Description = "..."
};
await service.Events.Insert(newEvent, "primary").ExecuteAsync();

// Update event (for rollback)
var existing = await service.Events.Get("primary", eventId).ExecuteAsync();
existing.Summary = "Updated Title";
await service.Events.Update(existing, "primary", eventId).ExecuteAsync();

// Batch requests (for multiple operations)
var batch = new BatchRequest(service);
// Add multiple requests...
await batch.ExecuteAsync();
```

**ETags for Conflict Detection:**
```csharp
// When updating, send ETag
var request = service.Events.Update(eventData, "primary", eventId);
request.IfMatchETag = currentEtag;
// Returns 412 Precondition Failed if ETag doesn't match
```

**Incremental Sync (Efficiency):**
```csharp
// First sync
var request = service.Events.List("primary");
var response = await request.ExecuteAsync();
string syncToken = response.NextSyncToken;

// Subsequent syncs (only changed events)
request = service.Events.List("primary");
request.SyncToken = syncToken;
var changes = await request.ExecuteAsync();
```

**Important Findings:**
- ❌ **No built-in version history** - Must maintain our own
- ❌ **Cannot rollback to previous ETag** - Must send full event data
- ✅ ETags prevent conflicting updates
- ✅ Batch API for multiple operations
- ✅ Incremental sync reduces API calls

**Quota & Limits:**
- 1,000,000 queries/day (free tier)
- Batch requests count as single query
- Rate limit: 10 queries/second/user

**Resources:**
- API Reference: https://developers.google.com/calendar/api/v3/reference
- .NET Client Library: https://googleapis.dev/dotnet/Google.Apis.Calendar.v3/latest/

---

### 4.2 Toggl Track API v9

**Status:** ✅ Active, RESTful API

**No Official .NET SDK** - Custom HttpClient implementation required

**Base URL:** `https://api.track.toggl.com/api/v9`

**Authentication:**
```http
Authorization: Basic {base64(api_token:api_token)}
```

**Key Endpoints:**
```csharp
// Get time entries
GET /me/time_entries?start_date={iso8601}&end_date={iso8601}

// Response
[{
    "id": 1234567890,
    "workspace_id": 123456,
    "project_id": 234567,
    "description": "Phone",
    "start": "2025-11-05T14:30:00+00:00",
    "stop": "2025-11-05T14:35:00+00:00",
    "duration": 300,
    "tags": ["personal"],
    "created_with": "api"
}]
```

**Implementation Pattern:**
```csharp
public class TogglService
{
    private readonly HttpClient _httpClient;

    public TogglService(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("Toggl");
        var apiToken = configuration["Toggl:ApiToken"];
        var auth = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{apiToken}:{apiToken}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", auth);
    }

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(
        DateTime startDate, DateTime endDate)
    {
        var url = $"/me/time_entries?start_date={startDate:o}&end_date={endDate:o}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<TimeEntry>>(json);
    }
}
```

**Pagination Strategy:**
3-month historical limit exists, so for large date ranges:
```csharp
public async Task<List<TimeEntry>> GetAllTimeEntriesAsync(
    DateTime startDate, DateTime endDate)
{
    var allEntries = new List<TimeEntry>();
    var current = startDate;

    while (current < endDate)
    {
        var chunkEnd = current.AddMonths(3);
        if (chunkEnd > endDate) chunkEnd = endDate;

        var entries = await GetTimeEntriesAsync(current, chunkEnd);
        allEntries.AddRange(entries);

        current = chunkEnd;
        await Task.Delay(100); // Rate limiting
    }

    return allEntries;
}
```

**Retry Policy with Polly:**
```csharp
services.AddHttpClient("Toggl")
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt =>
            TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

**Resources:**
- API Docs: https://developers.track.toggl.com/docs/api/time_entries/
- Get API token: Toggl Track → Profile Settings → API Token

---

### 4.3 YouTube Data API v3

**Status:** ⚠️ Watch History NOT Available (Deprecated 2016)

**NuGet Package:**
```xml
<PackageReference Include="Google.Apis.YouTube.v3" Version="1.69.0.x" />
```

**Use Case:** Video metadata ONLY (duration, channel, title)

**Critical Finding:**
- ❌ Cannot access watch history via API
- ✅ Can fetch video metadata by video ID
- ✅ Batch requests for multiple videos

**Implementation:**
```csharp
var youtubeService = new YouTubeService(new BaseClientService.Initializer
{
    ApiKey = configuration["YouTube:ApiKey"],
    ApplicationName = "Google Calendar Management"
});

// Batch fetch video metadata
var request = youtubeService.Videos.List("snippet,contentDetails");
request.Id = string.Join(",", videoIds); // Up to 50 IDs
var response = await request.ExecuteAsync();

foreach (var video in response.Items)
{
    var duration = XmlConvert.ToTimeSpan(video.ContentDetails.Duration); // ISO 8601
    var channel = video.Snippet.ChannelTitle;
    var title = video.Snippet.Title;
    // Store in database
}
```

**YouTube Watch History Data Source:**

**Google Takeout** (Manual Process)
1. Go to https://takeout.google.com
2. Deselect all, select only "YouTube and YouTube Music"
3. Under "All YouTube data included" → Choose only "History"
4. **Format:** JSON (not HTML)
5. Create export
6. Download when ready (email notification)
7. Extract `watch-history.json`

**JSON Structure:**
```json
[{
    "header": "YouTube",
    "title": "Watched Video Title",
    "titleUrl": "https://www.youtube.com/watch?v=VIDEO_ID",
    "subtitles": [{
        "name": "Channel Name",
        "url": "https://www.youtube.com/channel/CHANNEL_ID"
    }],
    "time": "2025-11-05T14:30:00Z"
}]
```

**Parsing Implementation:**
```csharp
var json = await File.ReadAllTextAsync(takeoutFilePath);
var history = JsonSerializer.Deserialize<List<YouTubeHistoryItem>>(json);

foreach (var item in history)
{
    var videoId = ExtractVideoId(item.TitleUrl);
    // Store watch time, video ID
    // Later: batch fetch metadata from YouTube API
}
```

**Future Enhancement:**
Chrome extension for real-time tracking (see Future Work section)

**Resources:**
- YouTube Data API: https://developers.google.com/youtube/v3/docs
- Takeout parser example: https://github.com/teodoran/youtube-watch-history-converter

---

### 4.4 Microsoft Graph API (Outlook Calendar)

**Status:** ✅ Active, Well-Supported

**NuGet Package:**
```xml
<PackageReference Include="Microsoft.Graph" Version="5.x" />
<PackageReference Include="Azure.Identity" Version="1.x" />
```

**Authentication:**
OAuth 2.0 with delegated permissions

**Works on Personal Device with Work Account:** ✅ YES

```csharp
var scopes = new[] {
    "Calendars.ReadWrite",
    "offline_access" // For refresh token
};

var options = new InteractiveBrowserCredentialOptions
{
    ClientId = configuration["AzureAd:ClientId"],
    TenantId = configuration["AzureAd:TenantId"],
    RedirectUri = new Uri("http://localhost")
};

var credential = new InteractiveBrowserCredential(options);
var graphClient = new GraphServiceClient(credential, scopes);

// Get calendar events
var events = await graphClient.Me.Calendar.Events
    .GetAsync(config =>
    {
        config.QueryParameters.StartDateTime = startDate.ToString("o");
        config.QueryParameters.EndDateTime = endDate.ToString("o");
        config.QueryParameters.Top = 100;
    });

foreach (var evt in events.Value)
{
    // evt.Subject, evt.Start, evt.End, etc.
}
```

**Refresh Token Handling:**
```csharp
// Refresh token valid for 90 days
// Store securely in local config
// Token refresh handled automatically by Azure.Identity library
```

**Important:**
- User must re-authenticate every 90 days
- Works with work/school accounts (Azure AD)
- Requires Azure AD app registration (one-time setup)

**Azure AD App Setup:**
1. Go to https://portal.azure.com
2. Azure Active Directory → App registrations → New registration
3. Name: "Google Calendar Management"
4. Redirect URI: http://localhost
5. API permissions → Add "Calendars.ReadWrite" (delegated)
6. Copy Client ID and Tenant ID

**Resources:**
- Graph Calendar API: https://learn.microsoft.com/en-us/graph/outlook-calendar-concept-overview
- Tutorial: https://andrewhalil.com/2022/06/15/how-to-use-an-ms-graph-api-calendar-with-net-core/

---

### 4.5 Microsoft Graph API (Excel Cloud Sync)

**Status:** ✅ Active

**Same NuGet packages as Outlook**

**Use Case:** Update weekly status Excel file in Microsoft Cloud (OneDrive/SharePoint)

```csharp
// Get workbook
var workbook = await graphClient.Me.Drive.Items[fileId].Workbook.GetAsync();

// Update cell
var range = await graphClient.Me.Drive.Items[fileId]
    .Workbook.Worksheets["Sheet1"]
    .Range("B2")
    .GetAsync();

range.Values = new string[][] { new[] { "Yes" } };

await graphClient.Me.Drive.Items[fileId]
    .Workbook.Worksheets["Sheet1"]
    .Range("B2")
    .PatchAsync(range);
```

**Alternative:**
Could export local file and let user manually upload. Evaluate complexity vs. benefit.

**Resources:**
- Excel API: https://learn.microsoft.com/en-us/graph/api/resources/excel

---

## 5. Utilities & Libraries

### 5.1 ISO 8601 Week Calculation

**Built-in:** ✅ `System.Globalization.ISOWeek`

```csharp
using System.Globalization;

var date = new DateTime(2025, 11, 5);
int weekNumber = ISOWeek.GetWeekOfYear(date); // 45
int year = ISOWeek.GetYear(date); // 2025

// Week 1 = week with at least 4 days in new year
// Weeks start on Monday
```

**Perfect for our weekly_state table!**

---

### 5.2 JSON Serialization

**Built-in:** ✅ `System.Text.Json`

```csharp
using System.Text.Json;

// Serialize
var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});

// Deserialize
var obj = JsonSerializer.Deserialize<MyType>(json);
```

**High performance, modern, built-in.** No need for Newtonsoft.Json.

---

### 5.3 HTTP Client

**Built-in:** ✅ `HttpClient` with `IHttpClientFactory`

```csharp
// Startup
services.AddHttpClient("Toggl");
services.AddHttpClient("YouTube");

// Usage
public class MyService
{
    private readonly HttpClient _httpClient;

    public MyService(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("Toggl");
    }
}
```

**With Polly for retry policies:**
```xml
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.x" />
```

```csharp
services.AddHttpClient("Toggl")
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, attempt =>
            TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

---

### 5.4 Logging

**Serilog** + **Microsoft.Extensions.Logging.ILogger**

```xml
<PackageReference Include="Serilog" Version="4.x" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.x" />
<PackageReference Include="Serilog.Sinks.File" Version="6.x" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.x" />
```

**Configuration:**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

services.AddLogging(builder =>
{
    builder.AddSerilog();
});
```

**Usage:**
```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task DoWorkAsync()
    {
        _logger.LogInformation("Starting work at {Time}", DateTime.Now);
        try
        {
            // Work
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work failed");
        }
    }
}
```

**Structured logging perfect for debugging API calls and data processing.**

---

## 6. Development Tools

### Visual Studio 2022 or Rider

**Recommended:** Visual Studio 2022 Community (free)

**Features:**
- Full WinUI 3 support
- XAML designer
- EF Core migrations tooling
- Excellent debugging
- NuGet package management

### .NET CLI

```bash
# Create project
dotnet new winui3 -n GoogleCalendarManagement

# Add packages
dotnet add package Microsoft.EntityFrameworkCore.Sqlite

# EF Core migrations
dotnet ef migrations add InitialCreate
dotnet ef database update

# Run
dotnet run
```

---

## 7. Testing Recommendations

**Unit Testing:**
```xml
<PackageReference Include="xUnit" Version="2.x" />
<PackageReference Include="Moq" Version="4.x" />
<PackageReference Include="FluentAssertions" Version="6.x" />
```

**Integration Testing:**
- In-memory SQLite for database tests
- Mock HTTP responses for API tests

---

## 8. Deployment

**Windows Desktop:**
- MSIX package for Windows Store distribution
- Or self-contained .exe with all dependencies

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

---

## 9. Future Work - Chrome Extension

**Purpose:** Real-time YouTube watch history tracking

**Technology:**
- Manifest V3 (current standard)
- Chrome Storage API for local data
- Export to JSON for app import

**Implementation:**
```javascript
// background.js
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
    if (tab.url?.includes('youtube.com/watch')) {
        const videoId = new URL(tab.url).searchParams.get('v');
        chrome.storage.local.set({
            [`watch_${Date.now()}`]: {
                videoId,
                timestamp: new Date().toISOString()
            }
        });
    }
});

// Export function
function exportHistory() {
    chrome.storage.local.get(null, (items) => {
        const json = JSON.stringify(items);
        // Download as file
    });
}
```

**Phase:** Post-Phase 1 investigation story

---

## 10. Configuration Management

**appsettings.json:**
```json
{
  "Google": {
    "Calendar": {
      "ClientId": "...",
      "ClientSecret": "..."
    },
    "YouTube": {
      "ApiKey": "..."
    }
  },
  "Toggl": {
    "ApiToken": "..."
  },
  "AzureAd": {
    "ClientId": "...",
    "TenantId": "..."
  },
  "Database": {
    "ConnectionString": "Data Source=calendar.db"
  }
}
```

**User Secrets (Development):**
```bash
dotnet user-secrets init
dotnet user-secrets set "Google:Calendar:ClientId" "your-client-id"
```

**Production:** Encrypted config file or Windows Credential Manager

---

## Summary - Final Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 9 |
| UI Framework | WinUI 3 | 1.6+ |
| Database | SQLite | Latest |
| ORM | Entity Framework Core | 9.0 |
| Google Calendar | Google.Apis.Calendar.v3 | 1.69+ |
| Toggl Track | Custom HttpClient | v9 API |
| YouTube | Google.Apis.YouTube.v3 | 1.69+ |
| Outlook Calendar | Microsoft.Graph | 5.x |
| Excel Sync | Microsoft.Graph | 5.x |
| ISO Weeks | System.Globalization.ISOWeek | Built-in |
| JSON | System.Text.Json | Built-in |
| HTTP | HttpClient + Polly | Built-in + 9.0 |
| Logging | Serilog + ILogger | 4.x + Built-in |

**All packages are actively maintained and production-ready for 2025.**

---

**Document Version:** 1.0
**Last Updated:** 2025-11-05
**Status:** Phase 1 Research Complete

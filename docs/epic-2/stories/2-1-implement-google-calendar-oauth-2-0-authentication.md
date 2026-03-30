# Story 2.1: Implement Google Calendar OAuth 2.0 Authentication

Status: ready-for-dev

## Story

As a **user**,
I want to **connect my Google Calendar account via OAuth 2.0**,
so that **the app can securely access my calendar data and maintain my credentials persistently across sessions**.

## Acceptance Criteria

1. **AC-2.1.1 — Initial Auth Flow:** Given the app has no stored credentials, when the user clicks "Connect Google Calendar" in Settings, the system browser opens to Google's OAuth consent screen.

2. **AC-2.1.2 — Encrypted Token Storage:** Given the user completes OAuth consent, the app receives access and refresh tokens, encrypts them with Windows DPAPI (`ProtectedData.Protect`, `DataProtectionScope.CurrentUser`), and stores the Base64-encoded ciphertext in `AppMetadata` (key = `"GcalTokenResponse"`) — no plaintext credentials persist on disk.

3. **AC-2.1.3 — Silent Startup Auth:** Given a stored refresh token in `AppMetadata`, when the app starts, `GoogleCalendarService.IsAuthenticatedAsync()` silently loads and validates the token without user interaction.

4. **AC-2.1.4 — Automatic Token Refresh:** Given a valid access token that expires mid-session, the Google SDK credential object automatically refreshes it before the next API call with no user-visible interruption.

5. **AC-2.1.5 — Reconnect / Account Switch:** Given the user clicks "Reconnect Google Calendar" in Settings, `RevokeAndClearTokensAsync()` clears the `AppMetadata` row and a new OAuth flow begins, supporting account switching.

6. **AC-2.1.6 — Auth Error Handling:** Given authentication fails (network error, user cancels, invalid credentials), a user-friendly error message is displayed (no stack trace), `OperationResult.Success = false` is returned, and no partial token state is persisted.

## Tasks / Subtasks

- [ ] **Task 1: Add NuGet packages** (AC: 2.1.1, 2.1.2)
  - [ ] Add `Google.Apis.Calendar.v3` version 1.73.0.3993 to `GoogleCalendarManagement.csproj`
  - [ ] Add `Google.Apis.Auth` version 1.73.0.x to `GoogleCalendarManagement.csproj`
  - [ ] Add `Microsoft.Extensions.Http.Polly` version 9.0.x to `GoogleCalendarManagement.csproj`
  - [ ] Run `dotnet build -p:Platform=x64` — confirm build succeeds with new packages

- [ ] **Task 2: Create `OperationResult<T>` shared record** (AC: all)
  - [ ] Create `Services/OperationResult.cs` with `Ok(T data)` and `Failure(string message)` static factories
  - [ ] Verify no duplicate definition exists in the codebase before creating

- [ ] **Task 3: Implement `ITokenStorageService` and `DpapiTokenStorageService`** (AC: 2.1.2, 2.1.3, 2.1.5)
  - [ ] Create `Services/ITokenStorageService.cs` with `StoreTokenAsync(TokenResponse)`, `LoadTokenAsync()`, and `ClearTokenAsync()` methods
  - [ ] Create `Services/DpapiTokenStorageService.cs`:
    - `StoreTokenAsync`: serialize `TokenResponse` to JSON, encrypt with `ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)`, Base64-encode, upsert `AppMetadata` row (key = `"GcalTokenResponse"`)
    - `LoadTokenAsync`: read `AppMetadata` row, Base64-decode, `ProtectedData.Unprotect`, deserialize to `TokenResponse`; on `CryptographicException` log `Error` and return null
    - `ClearTokenAsync`: delete `AppMetadata` row where key = `"GcalTokenResponse"`
  - [ ] Register `ITokenStorageService` → `DpapiTokenStorageService` as Singleton in `App.xaml.cs` DI

- [ ] **Task 4: Create `IGoogleCalendarService` interface and `GoogleCalendarService` (auth portion)** (AC: 2.1.1, 2.1.3, 2.1.4, 2.1.5, 2.1.6)
  - [ ] Create `Services/IGoogleCalendarService.cs` with the full interface from the tech spec:
    - `AuthenticateAsync(CancellationToken ct = default) → Task<OperationResult<OAuthStatus>>`
    - `IsAuthenticatedAsync() → Task<OperationResult<bool>>`
    - `RevokeAndClearTokensAsync() → Task`
    - `FetchAllEventsAsync(...)` and `FetchIncrementalEventsAsync(...)` as stubs (`throw new NotImplementedException()`) — implemented in Story 2.2
  - [ ] Create `Services/GoogleCalendarService.cs`:
    - `AuthenticateAsync`: load `client_secret.json` from `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\client_secret.json`; if missing, return `OperationResult.Failure("client_secret.json not found. See README for setup instructions.")`. Call `GoogleWebAuthorizationBroker.AuthorizeAsync()` with scope `https://www.googleapis.com/auth/calendar` and loopback redirect (port 0). On success: call `_tokenStorage.StoreTokenAsync(credential.Token)`, log `"Google Calendar auth succeeded for {AccountEmail}"`, write `audit_log` entry (`operation_type = "gcal_auth"`), return `OperationResult.Ok(OAuthStatus.Authenticated)`. On `OperationCanceledException`: return `OperationResult.Failure("Authentication cancelled by user.")`. On any other exception: log `Error` with structured message, return `OperationResult.Failure` with friendly message.
    - `IsAuthenticatedAsync`: call `_tokenStorage.LoadTokenAsync()`; return `OperationResult.Ok(true)` if token not null and not expired, else `OperationResult.Ok(false)`. Must complete in < 200ms.
    - `RevokeAndClearTokensAsync`: revoke credential via Google SDK if token is loaded; call `_tokenStorage.ClearTokenAsync()`; write `audit_log` entry (`operation_type = "gcal_revoke"`); log `Information`.
  - [ ] Create `Services/OAuthStatus.cs` enum: `{ Authenticated, NotAuthenticated }`
  - [ ] Register `IGoogleCalendarService` → `GoogleCalendarService` as Singleton in DI

- [ ] **Task 5: Create `SettingsViewModel` with auth commands** (AC: 2.1.1, 2.1.5, 2.1.6)
  - [ ] Create `ViewModels/SettingsViewModel.cs` (or update if it already exists)
  - [ ] Add `IsConnected` observable property (`bool`) — initialized by calling `IsAuthenticatedAsync()` in constructor or `OnNavigatedTo`
  - [ ] Add `ConnectionStatusText` computed string: `"Connected"` when `IsConnected`, `"Not connected"` otherwise
  - [ ] Add `ConnectGoogleCalendarCommand` (async relay command): calls `AuthenticateAsync()`; on success set `IsConnected = true`, send `WeakReferenceMessenger.Default.Send(new AuthenticationSucceededMessage())`; on failure show `ContentDialog` with `OperationResult.ErrorMessage`
  - [ ] Add `DisconnectGoogleCalendarCommand` (async relay command): calls `RevokeAndClearTokensAsync()`, set `IsConnected = false`

- [ ] **Task 6: Update `SettingsPage.xaml`** (AC: 2.1.1, 2.1.5)
  - [ ] Add "Connect Google Calendar" `Button` bound to `ConnectGoogleCalendarCommand`, visible when `!IsConnected`
  - [ ] Add "Reconnect Google Calendar" `Button` bound to `DisconnectGoogleCalendarCommand` (then re-triggers Connect), visible when `IsConnected`
  - [ ] Add `TextBlock` showing `ConnectionStatusText`

- [ ] **Task 7: Add credentials folder setup and `.gitignore` update** (AC: 2.1.2)
  - [ ] Add check in `App.OnLaunched`: if `client_secret.json` is missing, log `Warning` (not crash); app remains functional in offline/disconnected state
  - [ ] Verify `client_secret.json` and `credentials/` directory are in `.gitignore`
  - [ ] Update `README.md` with Google Cloud Console setup steps (enable Calendar API, create OAuth 2.0 credentials, download `client_secret.json`, place in `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\`)

- [ ] **Task 8: Write unit tests** (AC: all)
  - [ ] Create `GoogleCalendarManagement.Tests/Unit/AuthenticationTests.cs`
  - [ ] `DpapiTokenStorage_StoredValue_IsEncryptedNotPlaintext`: mock `CalendarDbContext`; call `StoreTokenAsync`, assert the stored `AppMetadata.Value` is NOT the raw JSON string (Base64 ciphertext differs from source JSON)
  - [ ] `DpapiTokenStorage_LoadAfterStore_RoundTrips`: store a token, load it back, assert key field values match original
  - [ ] `DpapiTokenStorage_Clear_RemovesMetadataRow`: after `ClearTokenAsync()`, assert `LoadTokenAsync()` returns null
  - [ ] `GoogleCalendarService_CancelledAuth_ReturnsFailure`: mock `GoogleWebAuthorizationBroker` to throw `OperationCanceledException`; assert `OperationResult.Success == false` and `ErrorMessage` contains "cancelled"
  - [ ] `GoogleCalendarService_MissingCredentialsFile_ReturnsFailure`: point credentials path to a non-existent file; call `AuthenticateAsync()`; assert `Success == false` and `ErrorMessage` contains "client_secret.json"
  - [ ] `GoogleCalendarService_IsAuthenticated_ReturnsFalse_WhenNoToken`: `LoadTokenAsync()` returns null; assert `IsAuthenticatedAsync()` returns `OperationResult.Ok(false)`

- [ ] **Task 9: Final validation** (All ACs)
  - [ ] Run `dotnet build -p:Platform=x64` — no errors
  - [ ] Run `dotnet test` — all tests pass (24 existing + new auth tests)
  - [ ] Manual: Settings → "Connect Google Calendar" → browser opens Google consent screen (AC-2.1.1)
  - [ ] Manual: Complete OAuth flow → inspect `AppMetadata` table → verify value is Base64 ciphertext, NOT JSON (AC-2.1.2)
  - [ ] Manual: Restart app → no re-auth prompt, Settings shows "Connected" (AC-2.1.3)
  - [ ] Manual: Click "Reconnect Google Calendar" → browser reopens for fresh auth (AC-2.1.5)
  - [ ] Manual: Cancel auth in browser → app shows friendly error, no crash (AC-2.1.6)
  - [ ] Manual: `audit_log` table contains `gcal_auth` entry after connect (Observability)

## Dev Notes

### Architecture Patterns and Constraints

**Technology Stack:**
- .NET 9.0.12, WinUI 3 (Windows App SDK 1.8.3), xUnit + Moq + FluentAssertions
- `Google.Apis.Auth` 1.73.0.x provides `GoogleWebAuthorizationBroker` with PKCE built-in — no manual PKCE implementation required
- `System.Security.Cryptography.ProtectedData` for DPAPI — no additional NuGet package needed (built-in .NET)
- All `IGoogleCalendarService` methods return `OperationResult<T>` — never propagate exceptions to callers

**Critical Architecture Decisions:**

- **OAuth scope must include write:** Request scope `https://www.googleapis.com/auth/calendar` (read + write) now, even though Epic 2 only reads. Pre-provisioning write scope avoids a second consent prompt when Epic 6 enables event publishing. [Source: epic-2/tech-spec.md#Security]

- **Token storage location:** `AppMetadata` table in `%LOCALAPPDATA%\GoogleCalendarManagement\calendar.db`, key = `"GcalTokenResponse"`. Value = `Base64(ProtectedData.Protect(UTF8(JSON(TokenResponse))))`. Never written to disk unencrypted.

- **`client_secret.json` must NOT be in source control.** Runtime path: `%LOCALAPPDATA%\GoogleCalendarManagement\credentials\client_secret.json`. Verify `.gitignore` covers this. Missing file = friendly error, not crash. [Source: epic-2/tech-spec.md#Risks R2]

- **Decryption failure on machine migration (R4):** If `ProtectedData.Unprotect` throws `CryptographicException`, log `Error` and return null from `LoadTokenAsync`. `IsAuthenticatedAsync` returns false → UI shows "Not connected" → user reconnects. No data loss (calendar data is in Google). No app crash.

- **`audit_log` writes required:** `ConnectGoogleCalendarAsync` writes `operation_type = "gcal_auth"`. `RevokeAndClearTokensAsync` writes `"gcal_revoke"`. Use `CalendarDbContext.AuditLog.Add(new AuditLog { ... })` per Epic 1 patterns. No sensitive data (tokens, email) in the log row.

- **Startup performance constraint:** `IsAuthenticatedAsync()` called on app start must complete in < 200ms per NFR-P1. This means: decrypt token from DB (fast), check expiry (in-memory). No network call on startup. [Source: epic-2/tech-spec.md#Performance]

**`OperationResult<T>` Pattern:**
```csharp
// Services/OperationResult.cs
public record OperationResult<T>(bool Success, T? Data, string? ErrorMessage)
{
    public static OperationResult<T> Ok(T data) => new(true, data, null);
    public static OperationResult<T> Failure(string message) => new(false, default, message);
}
```

**`DpapiTokenStorageService` Encrypt/Decrypt Pattern:**
```csharp
// Encrypt and store:
var json = JsonSerializer.Serialize(tokenResponse);
var bytes = Encoding.UTF8.GetBytes(json);
var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
var base64 = Convert.ToBase64String(encrypted);
// Upsert AppMetadata: Key="GcalTokenResponse", Value=base64

// Load and decrypt:
var base64 = appMetadata.Value;
var encrypted = Convert.FromBase64String(base64);
var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
var json = Encoding.UTF8.GetString(bytes);
return JsonSerializer.Deserialize<TokenResponse>(json);
```

**Required Serilog Structured Log Events (from tech-spec Observability):**
```csharp
_logger.LogInformation("Google Calendar auth initiated");
_logger.LogInformation("Google Calendar auth succeeded for {AccountEmail}", email);
_logger.LogError("Google Calendar auth failed: {Error}", ex.Message);
```
Use `ILogger<GoogleCalendarService>` via DI injection — NOT `Log.Logger` directly.

### Project Structure Notes

**Files to create (new):**
```
GoogleCalendarManagement/
├── Services/
│   ├── OperationResult.cs               # New — shared result type
│   ├── OAuthStatus.cs                   # New — enum
│   ├── ITokenStorageService.cs          # New
│   ├── DpapiTokenStorageService.cs      # New
│   ├── IGoogleCalendarService.cs        # New (full interface; stubs for 2.2+ methods)
│   └── GoogleCalendarService.cs         # New (auth only; stub FetchAll/Incremental)
├── ViewModels/
│   └── SettingsViewModel.cs             # New or update
└── Views/
    └── SettingsPage.xaml                # Update — add Connect/Disconnect buttons

GoogleCalendarManagement.Tests/
└── Unit/
    └── AuthenticationTests.cs           # New

%LOCALAPPDATA%\GoogleCalendarManagement\
└── credentials\
    └── client_secret.json               # NOT in source control — developer provides
```

**Architecture target vs. actual convention:**
- Architecture doc places `IGoogleCalendarService` in `GoogleCalendarManagement.Core/Interfaces/`. However, Epic 1 established the convention of placing services directly in the main project's `Services/` folder. Follow Epic 1 precedent until a Core project extraction is planned.

**Epic 1 infrastructure available — use, do not recreate:**
- `CalendarDbContext` with `AppMetadata` `DbSet<AppMetadata>` — use for token storage upsert
- `ILoggingService` / Serilog already configured — inject `ILogger<T>` via constructor DI
- `IErrorHandlingService` Singleton — use for critical error escalation only
- `services.AddLogging(builder => builder.AddSerilog())` already wired — no changes needed for logging setup

### Learnings from Previous Story

**From Story 1-6-implement-application-logging-and-error-handling-infrastructure (Status: Done)**

- **Package version discrepancy pattern:** Check exact NuGet availability before pinning. In Story 1.6, `Serilog.Sinks.Console` 6.0.1 was unavailable; 6.1.0 was used. Apply same care to `Google.Apis.Auth` 1.73.0.x — verify the exact available patch version before committing to the csproj.

- **`services.AddLogging(builder => builder.AddSerilog())` — use this pattern** when adding services in `App.xaml.cs`. `services.AddSerilog()` requires `Serilog.Extensions.Hosting` which is NOT installed. Do not add it; use the corrected form already in the codebase.

- **`ErrorHandlingService.SetWindow(Window)` timing:** Must be called AFTER `window.Activate()` in `App.OnLaunched`. Any `ContentDialog` shown from `SettingsViewModel` must obtain `XamlRoot` from `App.MainWindow.Content.XamlRoot` — not from a static reference.

- **Serilog in tests — no `WriteTo.TextWriter`:** This method was removed from Serilog 4.x core. Unit tests in `AuthenticationTests.cs` that need to capture log output must use a custom `InMemorySink : ILogEventSink` (same pattern as `LoggingTests.cs`). Do not attempt `WriteTo.TextWriter`.

- **Services to REUSE (do not recreate):**
  - `ILoggingService` at `Services/ILoggingService.cs`
  - `LoggingService` at `Services/LoggingService.cs`
  - `IErrorHandlingService` at `Services/IErrorHandlingService.cs`
  - `ErrorHandlingService` at `Services/ErrorHandlingService.cs`

- **Test suite baseline:** 24 tests passing. New `AuthenticationTests.cs` must not break any existing test.

[Source: docs/epic-1/stories/1-6-implement-application-logging-and-error-handling-infrastructure.md#Dev-Agent-Record]

### References

- [Epic 2 Tech Spec — Acceptance Criteria (authoritative)](../tech-spec.md#acceptance-criteria-authoritative)
- [Epic 2 Tech Spec — Security](../tech-spec.md#security) — DPAPI, client secret, token rotation
- [Epic 2 Tech Spec — Workflows Flow 1](../tech-spec.md#workflows-and-sequencing) — auth sequence diagram
- [Epic 2 Tech Spec — Observability](../tech-spec.md#observability) — required log events
- [Epic 2 Tech Spec — Risks R2, R4](../tech-spec.md#risks-assumptions-open-questions) — missing credentials / machine migration
- [Epic 2 Tech Spec — Traceability AC #1–6](../tech-spec.md#traceability-mapping) — test scenario mapping per AC
- [Architecture — Decision §17](../../architecture.md) — graceful error handling with Serilog + Polly
- [Architecture — Project Structure](../../architecture.md#project-structure) — file placement conventions
- [Architecture — Decision Summary](../../architecture.md#decision-summary) — Google.Apis.Calendar.v3 1.73.0.3993

## Dev Agent Record

### Context Reference

- [Story Context XML](2-1-implement-google-calendar-oauth-2-0-authentication.context.xml) — Generated 2026-03-30

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List

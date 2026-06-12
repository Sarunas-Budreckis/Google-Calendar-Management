# Epic 7 — Key Decisions

---

## Story 7.8: Outlook Work Calendar Sync — Authentication Approach

**Date:** 2026-06-08
**Status:** Decided

### Decision: Manual Graph API Token via Graph Explorer (no OAuth in-app)

The app will NOT implement OAuth authentication for Outlook. Instead, the user manually retrieves a short-lived access token from [Graph Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer) and pastes it into the app when a calendar sync is needed.

---

### Investigation Summary

The Microsoft Graph API (`/me/calendarView`) was confirmed to work correctly — 20 events were successfully fetched using a Graph Explorer token. The API path itself is not a problem.

The blocker is **Mayo Clinic's Azure AD tenant policy**, which requires admin consent for any third-party app registration before users can grant it OAuth access, regardless of:

- Auth flow used (device code, WAM interactive, browser-based)
- Whether the permission (`Calendars.Read`) is delegated (normally user-consentable)
- Whether the app is Microsoft-published (Graph Explorer's own client ID was also blocked programmatically)

Graph Explorer works in the browser because it uses an existing authenticated browser session — not because the auth wall doesn't exist.

**Auth paths tested and outcomes:**

| Method | Result |
|---|---|
| Graph Explorer web UI | ✅ Works — uses pre-existing browser SSO session |
| Device code flow — Graph Explorer client ID | ❌ Admin consent required |
| Device code flow — custom app registration | ❌ Admin consent required |
| WAM interactive — custom app registration | ❌ Admin consent required |
| Direct API call with Graph Explorer token | ✅ Works perfectly |

**ICS fallback considered:** OWA may offer an ICS subscribe URL, but Graph API was preferred because it returns richer structured data (subject, start, end, isAllDay, organizer, location, body preview, recurrence metadata) versus ICS which would require local parsing.

---

### Chosen Approach

1. User opens [Graph Explorer](https://developer.microsoft.com/en-us/graph/graph-explorer) and signs in with their Mayo Clinic account (already authenticated via browser SSO)
2. User copies the access token from the Graph Explorer "Access token" tab
3. User pastes the token into the app's Outlook sync configuration
4. The app calls `GET /me/calendarView` with that token
5. Token expires after ~1 hour; user repeats when they want to refresh

**Why this is acceptable:**
- Sync frequency for work calendar doesn't need to be automatic or frequent
- The user controls exactly when to refresh (intentional pull, not push)
- No IT approval required, no app registration changes needed
- Graph Explorer is already approved in the Mayo Clinic tenant

---

### Future Upgrade Path

If Mayo Clinic IT ever grants admin consent for the registered app (`0becc8e7-4105-4495-ad93-c714f123bb40`, tenant `a25fff9c-3f63-4fb2-9a8a-d9bdd0321f9a`), the app can be upgraded to use MSAL.NET with WAM broker (same pattern as the `active-directory-dotnet-desktop-msgraph-v2` sample). The token storage and Graph API call code would remain unchanged — only the token acquisition path changes.

Admin consent URL for future use:
```
https://login.microsoftonline.com/a25fff9c-3f63-4fb2-9a8a-d9bdd0321f9a/adminconsent?client_id=0becc8e7-4105-4495-ad93-c714f123bb40&redirect_uri=https://login.microsoftonline.com/common/oauth2/nativeclient
```

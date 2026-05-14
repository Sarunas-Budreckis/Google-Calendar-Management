# Story 7.10: Spotify / stats.fm Integration

**Epic:** 7 — Additional Data Source Integrations
**Status:** Draft
**Dependencies:** Story 7.1 (data_source registry), Story 5.5 (left panel day mode)

---

## User Story

As a **user**,
I want **to view my Spotify listening history from stats.fm in the left panel as a vertical dot timeline**,
so that **I can see what I was listening to at any point in a day**.

---

## Background

stats.fm (formerly Spotistats) aggregates Spotify streaming history — including full historical data from a Spotify Extended History import. It exposes an **unofficial, undocumented REST API** authenticated via a personal bearer token. There is no official OAuth flow for third-party apps; the token is obtained from the user's browser session after logging in to stats.fm via Spotify.

The preferred auth method is OAuth (if stats.fm ever publishes one); for now, the implementation uses a personal API token stored in app settings. The token can be extracted from browser dev tools (Network tab, Authorization header) while logged in to stats.fm.

This source is **display-only** — no candidate event generation. The drilldown shows a vertical dot timeline of tracks played; hovering/clicking a dot shows the track name, artist, and duration.

---

## Acceptance Criteria

**Schema:**

`spotify_stream`:
- `id` (integer, PK)
- `played_at` (datetime)
- `track_name` (text)
- `artist_name` (text)
- `album_name` (text, nullable)
- `duration_ms` (integer) — track duration in milliseconds
- `ms_played` (integer) — actual ms listened (from stats.fm; may be less than full track)

**API Token Configuration:**

**Given** I navigate to Settings > Data Sources > Spotify
**Then** I see a text field for "stats.fm API Token"

**And** I can enter a bearer token manually

**And** a "Test Connection" button validates the token by calling a known stats.fm endpoint (e.g., `GET https://api.stats.fm/api/v1/users/me`)

**And** the token is stored encrypted (DPAPI, same pattern as other credentials)

**Import Flow:**

**Given** a valid API token is configured
**When** I click "Import Streams" (from global mode or drilldown) with a date range
**Then** the app calls the stats.fm streams endpoint for that date range

**And** streams are upserted into `spotify_stream` (deduplicate on `played_at` + `track_name`)

**And** the import is logged to `data_source_import_log`

**Compact Card:**

**Given** a day is selected with stream data
**When** the Spotify card is shown
**Then** the card displays:
- Total listening time for the day (sum of `ms_played`)
- Number of tracks

**Drilldown View:**

**Given** I expand the Spotify source for a selected day
**Then** I see a 24-hour vertical timeline with a dot at each `played_at` timestamp

**And** hovering or clicking a dot shows: track name, artist, album, duration, ms played

**And** no "Create Candidate Events" button — this source is display-only

---

## Technical Notes

- stats.fm API base URL: `https://api.stats.fm/api/v1/`
- Known endpoints (unofficial — verify at implementation time):
  - `GET /users/me` — validate token
  - `GET /users/{userId}/streams` — streaming history with `after` and `before` date params
- Auth header: `Authorization: Bearer {token}`
- The userId can be fetched from `/users/me` after token validation
- stats.fm has no rate limit documentation; implement conservative throttling (e.g., 1 req/sec, exponential backoff on 429)
- **Future note:** If stats.fm publishes an official OAuth flow, migrate from personal token to OAuth. Watch the [stats.fm GitHub org](https://github.com/statsfm) for API announcements
- The vertical dot timeline component is shared with Toggl Phone (7.4) and ComfyUI (7.12) — implement as a reusable `VerticalDotTimeline` control if not already done
- `ms_played` < `duration_ms` indicates the user skipped the track before it finished — show this distinction in the dot hover detail

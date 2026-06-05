using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class MapsTimelineSegment
{
    public string? LocationName { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public string? ActivityType { get; init; }
    public bool IsVisit { get; init; }
}

public sealed class MapsTimelineParser
{
    private readonly ILogger<MapsTimelineParser> _logger;

    public MapsTimelineParser(ILogger<MapsTimelineParser> logger)
    {
        _logger = logger;
    }

    public (DateOnly? MinDate, DateOnly? MaxDate) ExtractDateRange(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var timestamps = new List<DateTime>();

            if (root.TryGetProperty("timelineObjects", out var timelineObjects))
            {
                CollectTimestampsFromOldFormat(timelineObjects, timestamps);
            }
            else if (root.TryGetProperty("semanticSegments", out var semanticSegments))
            {
                CollectTimestampsFromNewFormat(semanticSegments, timestamps);
            }
            else
            {
                _logger.LogWarning("Maps Timeline JSON has no recognizable top-level format key (timelineObjects or semanticSegments)");
            }

            if (timestamps.Count == 0)
            {
                return (null, null);
            }

            var min = DateOnly.FromDateTime(timestamps.Min().ToLocalTime());
            var max = DateOnly.FromDateTime(timestamps.Max().ToLocalTime());
            return (min, max);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract date range from Maps Timeline JSON");
            return (null, null);
        }
    }

    public IReadOnlyList<MapsTimelineSegment> GetSegmentsForDate(string rawJson, DateOnly date)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var segments = new List<MapsTimelineSegment>();

            if (root.TryGetProperty("timelineObjects", out var timelineObjects))
            {
                ParseSegmentsFromOldFormat(timelineObjects, date, segments);
            }
            else if (root.TryGetProperty("semanticSegments", out var semanticSegments))
            {
                ParseSegmentsFromNewFormat(semanticSegments, date, segments);
            }

            return segments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Maps Timeline segments for date {Date}", date);
            return [];
        }
    }

    // -------------------------------------------------------------------------
    // Old format (timelineObjects array with placeVisit / activitySegment)
    // -------------------------------------------------------------------------

    private void CollectTimestampsFromOldFormat(JsonElement array, List<DateTime> timestamps)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("placeVisit", out var visit))
            {
                TryAddTimestamps(visit, timestamps);
            }
            else if (item.TryGetProperty("activitySegment", out var activity))
            {
                TryAddTimestamps(activity, timestamps);
            }
            else
            {
                _logger.LogDebug("Unknown timelineObject type encountered; skipping");
            }
        }
    }

    private void TryAddTimestamps(JsonElement obj, List<DateTime> timestamps)
    {
        if (!obj.TryGetProperty("duration", out var duration))
        {
            return;
        }

        if (TryParseTimestamp(duration, "startTimestamp", out var start))
        {
            timestamps.Add(start);
        }

        if (TryParseTimestamp(duration, "endTimestamp", out var end))
        {
            timestamps.Add(end);
        }
    }

    private void ParseSegmentsFromOldFormat(JsonElement array, DateOnly date, List<MapsTimelineSegment> segments)
    {
        foreach (var item in array.EnumerateArray())
        {
            MapsTimelineSegment? segment = null;

            if (item.TryGetProperty("placeVisit", out var visit))
            {
                segment = ParseOldPlaceVisit(visit);
            }
            else if (item.TryGetProperty("activitySegment", out var activity))
            {
                segment = ParseOldActivitySegment(activity);
            }
            else
            {
                _logger.LogDebug("Unknown timelineObject type encountered; skipping");
            }

            if (segment is not null && OverlapsDate(segment.StartTime, segment.EndTime, date))
            {
                segments.Add(segment);
            }
        }
    }

    private MapsTimelineSegment? ParseOldPlaceVisit(JsonElement visit)
    {
        if (!visit.TryGetProperty("duration", out var duration))
        {
            return null;
        }

        if (!TryParseTimestamp(duration, "startTimestamp", out var start) ||
            !TryParseTimestamp(duration, "endTimestamp", out var end))
        {
            return null;
        }

        string? locationName = null;
        if (visit.TryGetProperty("location", out var location) &&
            location.TryGetProperty("name", out var nameEl))
        {
            locationName = nameEl.GetString();
        }

        return new MapsTimelineSegment
        {
            LocationName = locationName,
            StartTime = start,
            EndTime = end,
            IsVisit = true
        };
    }

    private MapsTimelineSegment? ParseOldActivitySegment(JsonElement activity)
    {
        if (!activity.TryGetProperty("duration", out var duration))
        {
            return null;
        }

        if (!TryParseTimestamp(duration, "startTimestamp", out var start) ||
            !TryParseTimestamp(duration, "endTimestamp", out var end))
        {
            return null;
        }

        string? activityType = null;
        if (activity.TryGetProperty("activityType", out var typeEl))
        {
            activityType = typeEl.GetString();
        }

        return new MapsTimelineSegment
        {
            StartTime = start,
            EndTime = end,
            ActivityType = activityType,
            IsVisit = false
        };
    }

    // -------------------------------------------------------------------------
    // New format (semanticSegments array with visit / activity properties)
    // -------------------------------------------------------------------------

    private void CollectTimestampsFromNewFormat(JsonElement array, List<DateTime> timestamps)
    {
        foreach (var segment in array.EnumerateArray())
        {
            if (TryParseIso8601(segment, "startTime", out var start))
            {
                timestamps.Add(start);
            }

            if (TryParseIso8601(segment, "endTime", out var end))
            {
                timestamps.Add(end);
            }
        }
    }

    private void ParseSegmentsFromNewFormat(JsonElement array, DateOnly date, List<MapsTimelineSegment> segments)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (!TryParseIso8601(item, "startTime", out var start) ||
                !TryParseIso8601(item, "endTime", out var end))
            {
                continue;
            }

            if (!OverlapsDate(start, end, date))
            {
                continue;
            }

            string? locationName = null;
            string? activityType = null;
            bool isVisit = false;

            if (item.TryGetProperty("visit", out var visit))
            {
                isVisit = true;
                // New format visits don't always carry location names in the JSON
                if (visit.TryGetProperty("topCandidate", out var candidate) &&
                    candidate.TryGetProperty("semanticType", out var typeEl))
                {
                    locationName = FormatSemanticType(typeEl.GetString());
                }
            }
            else if (item.TryGetProperty("activity", out var activity))
            {
                if (activity.TryGetProperty("topCandidate", out var candidate) &&
                    candidate.TryGetProperty("type", out var typeEl))
                {
                    activityType = typeEl.GetString();
                }
            }
            else
            {
                _logger.LogDebug("Unknown semanticSegment type; skipping");
                continue;
            }

            segments.Add(new MapsTimelineSegment
            {
                LocationName = locationName,
                StartTime = start,
                EndTime = end,
                ActivityType = activityType,
                IsVisit = isVisit
            });
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool TryParseTimestamp(JsonElement obj, string propertyName, out DateTime result)
    {
        result = default;
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        var raw = prop.GetString();
        if (raw is null)
        {
            return false;
        }

        // Old format uses ISO 8601 with Z suffix
        if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out result))
        {
            if (result.Kind == DateTimeKind.Unspecified)
            {
                result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
            }

            return true;
        }

        return false;
    }

    private static bool TryParseIso8601(JsonElement obj, string propertyName, out DateTime result)
    {
        result = default;
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        var raw = prop.GetString();
        if (raw is null)
        {
            return false;
        }

        if (DateTimeOffset.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
        {
            result = dto.UtcDateTime;
            return true;
        }

        return false;
    }

    private static bool OverlapsDate(DateTime startUtc, DateTime endUtc, DateOnly date)
    {
        var localStart = startUtc.ToLocalTime().Date;
        var localEnd = endUtc.ToLocalTime().Date;
        var target = date.ToDateTime(TimeOnly.MinValue);
        return localStart <= target && localEnd >= target;
    }

    private static string? FormatSemanticType(string? rawType)
    {
        if (rawType is null)
        {
            return null;
        }

        return rawType switch
        {
            "HOME" => "Home",
            "WORK" => "Work",
            "SEARCHED_ADDRESS" => "Searched address",
            "UNKNOWN" or "TYPE_UNKNOWN" => null,
            _ => rawType
        };
    }
}

using System.Globalization;

namespace GoogleCalendarManagement.Services;

public static class IcsParser
{
    public sealed record ParsedEvent(
        string Uid,
        string Summary,
        string? Description,
        DateTime StartUtc,
        DateTime EndUtc,
        bool IsAllDay);

    public sealed record ParseResult(
        bool IsValidCalendar,
        IReadOnlyList<ParsedEvent> Events,
        int InvalidEventCount,
        int SkippedRecurringEventCount,
        string? ErrorMessage);

    public static ParseResult ParseIcs(string icsContent)
    {
        if (string.IsNullOrWhiteSpace(icsContent))
        {
            return InvalidCalendar("The selected file is empty or is not a valid ICS calendar.");
        }

        var lines = UnfoldLines(icsContent);
        if (!lines.Any(line => string.Equals(line.Trim(), "BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase)))
        {
            return InvalidCalendar("The selected file is not a valid ICS calendar.");
        }

        var events = new List<ParsedEvent>();
        var invalidEventCount = 0;
        var skippedRecurringEventCount = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            if (!string.Equals(lines[i].Trim(), "BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var eventLines = new List<string>();
            var endFound = false;

            for (i++; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].Trim(), "END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    endFound = true;
                    break;
                }

                eventLines.Add(lines[i]);
            }

            if (!endFound)
            {
                invalidEventCount++;
                break;
            }

            var parsedEvent = ParseEvent(eventLines, ref invalidEventCount, ref skippedRecurringEventCount);
            if (parsedEvent is not null)
            {
                events.Add(parsedEvent);
            }
        }

        return new ParseResult(
            IsValidCalendar: true,
            Events: events,
            InvalidEventCount: invalidEventCount,
            SkippedRecurringEventCount: skippedRecurringEventCount,
            ErrorMessage: null);
    }

    private static ParseResult InvalidCalendar(string message)
    {
        return new ParseResult(
            IsValidCalendar: false,
            Events: [],
            InvalidEventCount: 0,
            SkippedRecurringEventCount: 0,
            ErrorMessage: message);
    }

    private static ParsedEvent? ParseEvent(
        IReadOnlyList<string> eventLines,
        ref int invalidEventCount,
        ref int skippedRecurringEventCount)
    {
        string? uid = null;
        string? summary = null;
        string? description = null;
        DateTime? startUtc = null;
        DateTime? endUtc = null;
        bool isAllDay = false;
        var hasRRule = false;

        foreach (var rawLine in eventLines)
        {
            if (!TryParseProperty(rawLine, out var propertyName, out var parameters, out var value))
            {
                continue;
            }

            switch (propertyName)
            {
                case "UID":
                    uid = value;
                    break;
                case "SUMMARY":
                    summary = UnescapeText(value);
                    break;
                case "DESCRIPTION":
                    description = string.IsNullOrEmpty(value) ? null : UnescapeText(value);
                    break;
                case "DTSTART":
                    if (!TryParseDateValue(parameters, value, out var parsedStartUtc, out var parsedIsAllDay))
                    {
                        invalidEventCount++;
                        return null;
                    }

                    startUtc = parsedStartUtc;
                    isAllDay = parsedIsAllDay;
                    break;
                case "DTEND":
                    if (!TryParseDateValue(parameters, value, out var parsedEndUtc, out _))
                    {
                        invalidEventCount++;
                        return null;
                    }

                    endUtc = parsedEndUtc;
                    break;
                case "RRULE":
                    hasRRule = true;
                    break;
            }
        }

        if (hasRRule)
        {
            skippedRecurringEventCount++;
            return null;
        }

        if (string.IsNullOrWhiteSpace(uid) ||
            string.IsNullOrWhiteSpace(summary) ||
            startUtc is null ||
            endUtc is null)
        {
            invalidEventCount++;
            return null;
        }

        return new ParsedEvent(
            uid.Trim(),
            summary.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description,
            DateTime.SpecifyKind(startUtc.Value, DateTimeKind.Utc),
            DateTime.SpecifyKind(endUtc.Value, DateTimeKind.Utc),
            isAllDay);
    }

    private static List<string> UnfoldLines(string content)
    {
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var rawLines = normalized.Split('\n');
        var lines = new List<string>();

        foreach (var rawLine in rawLines)
        {
            if ((rawLine.StartsWith(' ') || rawLine.StartsWith('\t')) && lines.Count > 0)
            {
                lines[^1] += rawLine[1..];
                continue;
            }

            lines.Add(rawLine);
        }

        return lines;
    }

    private static bool TryParseProperty(
        string line,
        out string propertyName,
        out IReadOnlyDictionary<string, string> parameters,
        out string value)
    {
        propertyName = string.Empty;
        parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        value = string.Empty;

        var separatorIndex = FindValueSeparatorIndex(line);
        if (separatorIndex < 0)
        {
            return false;
        }

        var propertySegment = line[..separatorIndex];
        value = line[(separatorIndex + 1)..];
        var segments = propertySegment.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        propertyName = segments[0].Trim().ToUpperInvariant();
        var parameterMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 1; i < segments.Length; i++)
        {
            var equalsIndex = segments[i].IndexOf('=');
            if (equalsIndex <= 0 || equalsIndex == segments[i].Length - 1)
            {
                continue;
            }

            var key = segments[i][..equalsIndex].Trim().ToUpperInvariant();
            var parameterValue = segments[i][(equalsIndex + 1)..].Trim().Trim('"');
            parameterMap[key] = parameterValue;
        }

        parameters = parameterMap;
        return true;
    }

    private static int FindValueSeparatorIndex(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ':' && (i == 0 || line[i - 1] != '\\'))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseDateValue(
        IReadOnlyDictionary<string, string> parameters,
        string value,
        out DateTime parsedUtc,
        out bool isAllDay)
    {
        parsedUtc = default;
        isAllDay = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        isAllDay = parameters.TryGetValue("VALUE", out var valueType) &&
                   string.Equals(valueType, "DATE", StringComparison.OrdinalIgnoreCase);

        if (!isAllDay && !value.Contains('T', StringComparison.Ordinal))
        {
            isAllDay = true;
        }

        if (isAllDay)
        {
            if (!DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return false;
            }

            parsedUtc = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return true;
        }

        return TryParseDateTimeToUtc(parameters, value, out parsedUtc);
    }

    private static bool TryParseDateTimeToUtc(
        IReadOnlyDictionary<string, string> parameters,
        string value,
        out DateTime parsedUtc)
    {
        parsedUtc = default;

        if (value.EndsWith('Z'))
        {
            return TryParseUtcDateTime(value, out parsedUtc);
        }

        if (!TryParseLocalDateTime(value, out var parsedLocal))
        {
            return false;
        }

        if (parameters.TryGetValue("TZID", out var timeZoneId) && !string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                parsedUtc = TimeZoneInfo.ConvertTimeToUtc(parsedLocal, timeZone);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        parsedUtc = parsedLocal.ToUniversalTime();
        return true;
    }

    private static bool TryParseUtcDateTime(string value, out DateTime parsedUtc)
    {
        return DateTime.TryParseExact(
            value,
            ["yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmm'Z'"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsedUtc);
    }

    private static bool TryParseLocalDateTime(string value, out DateTime parsedLocal)
    {
        if (!DateTime.TryParseExact(
                value,
                ["yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmm"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedLocal))
        {
            return false;
        }

        parsedLocal = DateTime.SpecifyKind(parsedLocal, DateTimeKind.Local);
        return true;
    }

    private static string UnescapeText(string value)
    {
        return value
            .Replace(@"\N", "\n", StringComparison.Ordinal)
            .Replace(@"\n", "\n", StringComparison.Ordinal)
            .Replace(@"\,", ",", StringComparison.Ordinal)
            .Replace(@"\;", ";", StringComparison.Ordinal)
            .Replace(@"\\", @"\", StringComparison.Ordinal);
    }
}

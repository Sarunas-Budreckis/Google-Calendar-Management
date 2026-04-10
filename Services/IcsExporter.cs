using System.Text;
using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public static class IcsExporter
{
    private const string CrLf = "\r\n";

    public static string GenerateIcs(IEnumerable<GcalEvent> events)
    {
        return GenerateIcs(events, DateTime.UtcNow);
    }

    public static string GenerateIcs(IEnumerable<GcalEvent> events, DateTime dtStampUtc)
    {
        ArgumentNullException.ThrowIfNull(events);

        var stampUtc = NormalizeUtc(dtStampUtc);
        var builder = new StringBuilder();

        AppendLine(builder, "BEGIN:VCALENDAR");
        AppendLine(builder, "VERSION:2.0");
        AppendLine(builder, "PRODID:-//Google Calendar Management//EN");
        AppendLine(builder, "CALSCALE:GREGORIAN");

        foreach (var calendarEvent in events)
        {
            AppendEvent(builder, calendarEvent, stampUtc);
        }

        AppendLine(builder, "END:VCALENDAR");
        return builder.ToString();
    }

    private static void AppendEvent(StringBuilder builder, GcalEvent calendarEvent, DateTime dtStampUtc)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (string.IsNullOrWhiteSpace(calendarEvent.GcalEventId))
        {
            throw new InvalidOperationException("Cannot export an event without a gcal_event_id.");
        }

        if (!calendarEvent.StartDatetime.HasValue)
        {
            throw new InvalidOperationException($"Cannot export event '{calendarEvent.GcalEventId}' without a start datetime.");
        }

        var startUtc = NormalizeUtc(calendarEvent.StartDatetime.Value);
        var isAllDay = calendarEvent.IsAllDay ?? false;
        var endUtc = NormalizeEndUtc(calendarEvent.EndDatetime, startUtc, isAllDay);

        AppendLine(builder, "BEGIN:VEVENT");
        AppendLine(builder, $"UID:{EscapeIcsText(calendarEvent.GcalEventId)}");
        AppendLine(builder, $"SUMMARY:{EscapeIcsText(calendarEvent.Summary ?? string.Empty)}");

        if (isAllDay)
        {
            AppendLine(builder, $"DTSTART;VALUE=DATE:{startUtc:yyyyMMdd}");
            AppendLine(builder, $"DTEND;VALUE=DATE:{endUtc:yyyyMMdd}");
        }
        else
        {
            AppendLine(builder, $"DTSTART:{FormatUtcDateTime(startUtc)}");
            AppendLine(builder, $"DTEND:{FormatUtcDateTime(endUtc)}");
        }

        AppendLine(builder, $"DTSTAMP:{FormatUtcDateTime(dtStampUtc)}");
        AppendLine(builder, $"LAST-MODIFIED:{FormatUtcDateTime(NormalizeUtc(calendarEvent.UpdatedAt))}");

        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            AppendLine(builder, $"DESCRIPTION:{EscapeIcsText(calendarEvent.Description)}");
        }

        AppendLine(builder, "END:VEVENT");
    }

    private static DateTime NormalizeEndUtc(DateTime? endUtc, DateTime startUtc, bool isAllDay)
    {
        if (endUtc.HasValue)
        {
            return NormalizeUtc(endUtc.Value);
        }

        return isAllDay ? startUtc.AddDays(1) : startUtc;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string FormatUtcDateTime(DateTime value)
    {
        return NormalizeUtc(value).ToString("yyyyMMdd'T'HHmmss'Z'");
    }

    private static void AppendLine(StringBuilder builder, string value)
    {
        // RFC 5545 §3.1: fold lines longer than 75 octets using CRLF + SPACE.
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= 75)
        {
            builder.Append(value);
            builder.Append(CrLf);
            return;
        }

        var offset = 0;
        var firstLine = true;
        while (offset < bytes.Length)
        {
            var maxOctets = firstLine ? 75 : 74; // continuation lines start with a SPACE (1 octet)
            var chunkLength = Math.Min(maxOctets, bytes.Length - offset);

            // Walk back to a safe UTF-8 character boundary if the chunk splits a multi-byte sequence.
            while (chunkLength > 0 && (bytes[offset + chunkLength - 1] & 0xC0) == 0x80)
            {
                chunkLength--;
            }

            if (!firstLine)
            {
                builder.Append(' ');
            }

            builder.Append(Encoding.UTF8.GetString(bytes, offset, chunkLength));
            builder.Append(CrLf);

            offset += chunkLength;
            firstLine = false;
        }
    }

    private static string EscapeIcsText(string text)
    {
        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
    }
}

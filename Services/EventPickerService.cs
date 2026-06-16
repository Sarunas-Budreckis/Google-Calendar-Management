using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public sealed class EventPickerService : IEventPickerService
{
    private readonly IEventRepository _eventRepository;
    private readonly IColorMappingService _colorMappingService;

    public EventPickerService(IEventRepository eventRepository, IColorMappingService colorMappingService)
    {
        _eventRepository = eventRepository;
        _colorMappingService = colorMappingService;
    }

    public async Task<EventPickerResult> GetCandidatesAsync(
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        string? searchText,
        CancellationToken ct = default)
    {
        if (rangeEnd <= rangeStart)
            throw new ArgumentException("rangeEnd must be after rangeStart.", nameof(rangeEnd));

        var windowDays = string.IsNullOrEmpty(searchText) ? 90 : 365;
        var from = DateOnly.FromDateTime(rangeStart.AddDays(-windowDays).Date);
        var to = DateOnly.FromDateTime(rangeEnd.AddDays(windowDays).Date);

        var all = await _eventRepository.GetByDateRangeAsync(from, to, ct);

        var approved = all.Where(e => e.Lifecycle == "approved" && !e.IsDeleted).ToList();

        var rangeMid = rangeStart + (rangeEnd - rangeStart) / 2;

        var concurrent = new List<Event>();
        var other = new List<Event>();

        foreach (var ev in approved)
        {
            if (ev.StartDatetime is null)
                continue;

            var startUtc = NormalizeUtc(ev.StartDatetime.Value);
            var endUtc = NormalizeUtc(ev.EndDatetime ?? ev.StartDatetime.Value);

            if (startUtc < rangeEnd.UtcDateTime && endUtc > rangeStart.UtcDateTime)
                concurrent.Add(ev);
            else
                other.Add(ev);
        }

        concurrent.Sort((a, b) => NormalizeUtc(a.StartDatetime!.Value).CompareTo(NormalizeUtc(b.StartDatetime!.Value)));

        other.Sort((a, b) =>
        {
            var distA = Math.Abs((MidOf(a) - rangeMid.UtcDateTime).TotalSeconds);
            var distB = Math.Abs((MidOf(b) - rangeMid.UtcDateTime).TotalSeconds);
            return distA.CompareTo(distB);
        });

        var concurrentItems = ApplySearch(concurrent, searchText)
            .Select(e => ToItem(e, isConcurrent: true))
            .ToList();

        var otherItems = ApplySearch(other, searchText)
            .Select(e => ToItem(e, isConcurrent: false))
            .ToList();

        return new EventPickerResult(concurrentItems, otherItems);
    }

    private static IEnumerable<Event> ApplySearch(IEnumerable<Event> events, string? searchText)
    {
        if (string.IsNullOrEmpty(searchText))
            return events;

        return events.Where(e =>
            e.Summary?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true);
    }

    private EventPickerItem ToItem(Event ev, bool isConcurrent)
    {
        var colorKey = _colorMappingService.NormalizeColorKey(ev.ColorId);
        var startUtc = NormalizeUtc(ev.StartDatetime!.Value);
        var endUtc = NormalizeUtc(ev.EndDatetime ?? ev.StartDatetime.Value);

        return new EventPickerItem(
            EventId: ev.EventId,
            Summary: ev.Summary ?? string.Empty,
            StartLocal: startUtc.ToLocalTime(),
            EndLocal: endUtc.ToLocalTime(),
            ColorId: ev.ColorId,
            ColorHex: _colorMappingService.GetHexColor(colorKey),
            IsConcurrent: isConcurrent);
    }

    private static DateTime MidOf(Event ev)
    {
        var startUtc = NormalizeUtc(ev.StartDatetime!.Value);
        var endUtc = NormalizeUtc(ev.EndDatetime ?? ev.StartDatetime.Value);
        return startUtc + (endUtc - startUtc) / 2;
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
}

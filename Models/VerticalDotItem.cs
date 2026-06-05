namespace GoogleCalendarManagement.Models;

public sealed record VerticalDotItem(
    DateTime Timestamp,
    string PrimaryLabel,
    string? SecondaryLabel,
    string? TertiaryLabel,
    bool IsPartial);

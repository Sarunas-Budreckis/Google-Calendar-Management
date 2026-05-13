using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public interface IDataSourceCardProvider
{
    string SourceKey { get; }

    UIElement? CreateCompactSummaryView(DateOnly date);

    UIElement CreateDrilldownView(DateOnly date);

    bool? HasDataForDay(DateOnly date);
}

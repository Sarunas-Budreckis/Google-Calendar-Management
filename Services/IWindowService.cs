using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public interface IWindowService
{
    void SetWindow(Window window);

    Window? GetWindow();

    XamlRoot? GetXamlRoot();
}

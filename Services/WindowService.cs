using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class WindowService : IWindowService
{
    private Window? _window;

    public void SetWindow(Window window)
    {
        _window = window;
    }

    public XamlRoot? GetXamlRoot()
    {
        return _window?.Content?.XamlRoot;
    }
}

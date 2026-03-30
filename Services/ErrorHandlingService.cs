using Microsoft.UI.Xaml.Controls;
using Serilog;

namespace GoogleCalendarManagement.Services;

public class ErrorHandlingService : IErrorHandlingService
{
    private Microsoft.UI.Xaml.Window? _window;

    internal void SetWindow(Microsoft.UI.Xaml.Window window) => _window = window;

    public void Register()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            Log.Fatal(ex, "Unhandled CLR exception (IsTerminating={IsTerminating})", args.IsTerminating);
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved Task exception");
            args.SetObserved();
        };

        Application.Current.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled WinUI exception: {Message}", args.Message);
            args.Handled = true;
            HandleCriticalError(args.Exception, "Unhandled WinUI exception");
        };
    }

    public void HandleCriticalError(Exception ex, string context)
    {
        Log.Fatal(ex, "Critical error in {Context}", context);
        Log.CloseAndFlush();

        if (_window?.DispatcherQueue is { } queue)
        {
            queue.TryEnqueue(async () =>
            {
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Unexpected Error",
                        Content = "An unexpected error occurred. The application will close. Details have been saved to the log file.",
                        CloseButtonText = "Exit",
                        XamlRoot = _window.Content?.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    Application.Current.Exit();
                }
            });
        }
        else
        {
            Application.Current.Exit();
        }
    }
}

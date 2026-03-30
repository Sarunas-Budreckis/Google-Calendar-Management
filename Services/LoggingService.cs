using System.Reflection;
using Serilog;
using Serilog.Events;

namespace GoogleCalendarManagement.Services;

public class LoggingService : ILoggingService
{
    public void Configure()
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GoogleCalendarManagement", "logs");
        Directory.CreateDirectory(logFolder);

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "GoogleCalendarManagement")
            .Enrich.WithProperty("Version",
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown")
            .WriteTo.Console()
            .WriteTo.File(
                path: Path.Combine(logFolder, "app-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}

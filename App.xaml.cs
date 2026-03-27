using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private IServiceProvider? serviceProvider;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Configure dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();

            // Create and activate main window (must happen before RunStartupAsync for ContentDialog XamlRoot)
            window = new Window
            {
                Title = "Google Calendar Management"
            };

            // Set window size to 1024x768 with minimum 800x600
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (appWindow is not null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1024, Height = 768 });
            }

            // Set window content
            var rootGrid = new Grid();
            var textBlock = new TextBlock
            {
                Text = "Google Calendar Management - Loading...",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rootGrid.Children.Add(textBlock);
            window.Content = rootGrid;

            window.Activate();

            // Run migration service on startup
            using (var scope = serviceProvider.CreateScope())
            {
                var migrationService = scope.ServiceProvider.GetRequiredService<IMigrationService>();
                try
                {
                    await migrationService.RunStartupAsync();
                }
                catch (Exception ex)
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var backupDir = Path.Combine(localAppData, "GoogleCalendarManagement");
                    var dialog = new ContentDialog
                    {
                        Title = "Startup Error",
                        Content = $"Database error on startup: {ex.Message}\n\nPlease restore from a backup in:\n{backupDir}",
                        CloseButtonText = "Exit"
                    };
                    dialog.XamlRoot = window.Content.XamlRoot;
                    await dialog.ShowAsync();
                    Application.Current.Exit();
                    return;
                }
            }

            // Verify DI works by resolving a service
            var logger = serviceProvider.GetService<ILogger<App>>();
            logger?.LogInformation("Application started successfully.");
        }

        /// <summary>
        /// Configures the dependency injection container with application services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbFolder = Path.Combine(localAppData, "GoogleCalendarManagement");
            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "calendar.db");

            var dbOptions = new DatabaseOptions
            {
                ConnectionString = $"Data Source={dbPath}"
            };
            services.AddSingleton(dbOptions);
            services.AddDbContext<CalendarDbContext>(options =>
                options.UseSqlite(dbOptions.ConnectionString)
                       .AddInterceptors(new SqliteConnectionInterceptor()));

            services.AddScoped<IMigrationService, MigrationService>();
        }
    }
}

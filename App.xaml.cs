using System.Diagnostics;
using System.Reflection;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using GoogleCalendarManagement.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GoogleCalendarManagement
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? window;
        private IServiceProvider? serviceProvider;

        public static T GetRequiredService<T>() where T : notnull
        {
            if (Current is not App app || app.serviceProvider is null)
            {
                throw new InvalidOperationException("Application services are not available.");
            }

            return app.serviceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            // Configure Serilog before anything else so DI/startup failures are captured.
            new LoggingService().Configure();
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            var startupTimer = Stopwatch.StartNew();

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

            var windowService = serviceProvider.GetRequiredService<IWindowService>();
            windowService.SetWindow(window);

            startupTimer.Stop();
            var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            Log.Information("Google Calendar Management v{Version} started. Launch to window: {ElapsedMs}ms",
                appVersion, startupTimer.ElapsedMilliseconds);

            // Wire global exception handlers (after window is active so XamlRoot is available for dialogs)
            var errorHandlingService = serviceProvider.GetRequiredService<IErrorHandlingService>();
            if (errorHandlingService is ErrorHandlingService svc)
                svc.SetWindow(window);
            errorHandlingService.Register();

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
                    Log.Error(ex, "Database error on startup");
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var backupDir = Path.Combine(localAppData, "GoogleCalendarManagement");
                    var dialog = new ContentDialog
                    {
                        Title = "Startup Error",
                        Content = $"A database error occurred on startup. Please restore from a backup in:\n{backupDir}",
                        CloseButtonText = "Exit"
                    };
                    dialog.XamlRoot = window.Content.XamlRoot;
                    await dialog.ShowAsync();
                    Log.CloseAndFlush();
                    Application.Current.Exit();
                    return;
                }
            }

            var logger = serviceProvider.GetService<ILogger<App>>();
            var googleCalendarOptions = serviceProvider.GetRequiredService<GoogleCalendarOptions>();
            // Keep the OAuth client secret in LocalAppData\GoogleCalendarManagement\credentials\client_secret.json,
            // not in the workspace, so local setup does not leak into source control.
            Directory.CreateDirectory(googleCalendarOptions.CredentialsDirectoryPath);
            if (!File.Exists(googleCalendarOptions.ClientSecretPath))
            {
                logger?.LogWarning(
                    "Google Calendar credentials file not found at {CredentialsPath}. The app will remain available in a disconnected state.",
                    googleCalendarOptions.ClientSecretPath);
            }

            var mainPage = serviceProvider.GetRequiredService<MainPage>();
            window.Content = mainPage;
            await mainPage.ViewModel.InitializeAsync();

            logger?.LogInformation("Application started successfully.");
        }

        /// <summary>
        /// Configures the dependency injection container with application services.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        private static void ConfigureServices(ServiceCollection services)
        {
            // Bridge Serilog to Microsoft.Extensions.Logging ILogger<T>
            services.AddLogging(builder => builder.AddSerilog());

            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbFolder = Path.Combine(localAppData, "GoogleCalendarManagement");
            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "calendar.db");

            var dbOptions = new DatabaseOptions
            {
                ConnectionString = $"Data Source={dbPath}"
            };
            services.AddSingleton(dbOptions);
            services.AddSingleton(new GoogleCalendarOptions(dbFolder));
            services.AddDbContext<CalendarDbContext>(options =>
                options.UseSqlite(dbOptions.ConnectionString)
                       .AddInterceptors(new SqliteConnectionInterceptor()));
            services.AddDbContextFactory<CalendarDbContext>(options =>
                options.UseSqlite(dbOptions.ConnectionString)
                       .AddInterceptors(new SqliteConnectionInterceptor()));

            services.AddScoped<IMigrationService, MigrationService>();
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<IIcsFileSavePickerService, IcsFileSavePickerService>();
            services.AddSingleton<ITokenStorageService, DpapiTokenStorageService>();
            services.AddSingleton<IGoogleAuthorizationBroker, GoogleAuthorizationBrokerAdapter>();
            services.AddSingleton<IGoogleCalendarService, GoogleCalendarService>();
            services.AddSingleton<ISyncManager, SyncManager>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IGcalEventRepository, GcalEventRepository>();
            services.AddSingleton<ISystemStateRepository, SystemStateRepository>();
            services.AddSingleton<IColorMappingService, ColorMappingService>();
            services.AddSingleton<ICalendarQueryService, CalendarQueryService>();
            services.AddTransient<IIcsExportService, IcsExportService>();
            services.AddTransient<IIcsImportService, IcsImportService>();
            services.AddSingleton<ISyncStatusService, SyncStatusService>();
            services.AddSingleton<INavigationStateService, NavigationStateService>();
            services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<EventDetailsPanelViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<MainPage>();
            services.AddTransient<EventDetailsPanelControl>();
            services.AddTransient<YearViewControl>();
            services.AddTransient<MonthViewControl>();
            services.AddTransient<WeekViewControl>();
            services.AddTransient<DayViewControl>();
        }
    }
}

using System.Diagnostics;
using System.Reflection;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Infrastructure;
using GoogleCalendarManagement.Constants;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.Services.DataLinking;
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
            var importRegistry = serviceProvider.GetRequiredService<DataSourceImportHandlerRegistry>();
            var projectorRegistry = serviceProvider.GetRequiredService<IDataPointProjectorRegistry>();
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<TogglSleepImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<TogglTransitImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<MapsTimelineImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<SpotifyImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<TogglPhoneImportHandler>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<TogglSleepCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<TogglTransitCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<MapsTimelineCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<TogglPhoneCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<SpotifyCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<Civ5CardProvider>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<Civ5ImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<CallLogImportHandler>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<CallLogCardProvider>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<ComfyUICardProvider>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<ComfyUIImportHandler>());
            RegisterImportHandler(importRegistry, projectorRegistry, serviceProvider.GetRequiredService<OutlookImportHandler>());
            serviceProvider.GetRequiredService<DataSourceCardProviderRegistry>()
                .Register(serviceProvider.GetRequiredService<OutlookCardProvider>());
            var csvHandler = serviceProvider.GetRequiredService<TogglCsvImportHandler>();
            RegisterProjectorIfAvailable(projectorRegistry, csvHandler);
            importRegistry.RegisterCsvHandler(TogglSleepImportService.SourceKey, csvHandler);
            importRegistry.RegisterCsvHandler(TogglTransitImportService.SourceKey, csvHandler);
            importRegistry.RegisterCsvHandler(TogglPhoneCardProvider.SourceKey, csvHandler);

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
                appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "GCalAppIcon.ico"));
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
            var startupXamlRoot = await WaitForXamlRootAsync(rootGrid);

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
                    var dialog = new ContentDialog
                    {
                        Title = "Startup Error",
                        Content = $"A database error occurred on startup. Please restore from a backup in:\n{Path.Combine(ProjectPaths.GetProjectRoot(), "database", "backups")}",
                        CloseButtonText = "Exit",
                        XamlRoot = startupXamlRoot
                    };
                    if (startupXamlRoot is not null)
                    {
                        await dialog.ShowAsync();
                    }
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

            // Fire-and-forget startup drift check: heals any data_point registry gaps in the
            // background. Do NOT await on the UI thread — orphans are logged, never surfaced as a dialog.
            var sweepProvider = serviceProvider;
            _ = Task.Run(async () =>
            {
                try
                {
                    await sweepProvider
                        .GetRequiredService<IDataPointReconciliationSweepService>()
                        .RunStartupDriftCheckAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Startup data point reconciliation sweep failed.");
                }
            });

            logger?.LogInformation("Application started successfully.");
        }

        private static void RegisterImportHandler(
            DataSourceImportHandlerRegistry importRegistry,
            IDataPointProjectorRegistry projectorRegistry,
            IDataSourceImportHandler handler)
        {
            importRegistry.Register(handler);
            RegisterProjectorIfAvailable(projectorRegistry, handler);
        }

        private static void RegisterProjectorIfAvailable(
            IDataPointProjectorRegistry projectorRegistry,
            IDataSourceImportHandler handler)
        {
            var projector = handler.GetProjector();
            if (projector is not null && projectorRegistry.GetProjector(projector.SourceKey) is null)
            {
                projectorRegistry.Register(projector);
            }
        }

        private static async Task<XamlRoot?> WaitForXamlRootAsync(UIElement element)
        {
            if (element.XamlRoot is not null)
            {
                return element.XamlRoot;
            }

            if (element is FrameworkElement { IsLoaded: false } frameworkElement)
            {
                var loadedTaskSource = new TaskCompletionSource<object?>();
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    loadedTaskSource.TrySetResult(null);
                };

                frameworkElement.Loaded += loadedHandler;

                try
                {
                    await Task.WhenAny(loadedTaskSource.Task, Task.Delay(TimeSpan.FromSeconds(1)));
                }
                finally
                {
                    frameworkElement.Loaded -= loadedHandler;
                }
            }

            for (var attempt = 0; attempt < 10 && element.XamlRoot is null; attempt++)
            {
                await Task.Delay(50);
            }

            return element.XamlRoot;
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

            var projectRoot = ProjectPaths.GetProjectRoot();
            var dbFolder = Path.Combine(projectRoot, "database");
            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "calendar.db");

            var dbOptions = new DatabaseOptions
            {
                ConnectionString = $"Data Source={dbPath}"
            };
            services.AddSingleton(dbOptions);
            services.AddSingleton(new GoogleCalendarOptions(projectRoot));
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
            services.AddSingleton<IEventRepository, EventRepository>();
            services.AddSingleton<IEventIdentityService, EventIdentityService>();
            services.AddSingleton<ILinkService, LinkService>();
            services.AddSingleton<IEventPickerService, EventPickerService>();
            services.AddSingleton<ISourcePointerResolverRegistry, SourcePointerResolverRegistry>();
            services.AddSingleton<IDataPointProjectorRegistry, DataPointProjectorRegistry>();
            services.AddSingleton<IDataPointReconciliationSweepService, DataPointReconciliationSweepService>();
            services.AddSingleton<IConfigRepository, ConfigRepository>();
            services.AddSingleton<IDataSourceRepository, DataSourceRepository>();
            services.AddSingleton<ICoverageService, CoverageService>();
            services.AddSingleton<DataSourceImportHandlerRegistry>();
            services.AddSingleton<DataSourceCardProviderRegistry>();
            services.AddSingleton<ITogglSleepRepository, TogglSleepRepository>();
            services.AddSingleton<ITogglSleepQualityRepository, TogglSleepQualityRepository>();
            services.AddSingleton<TogglSleepCardProvider>();
            services.AddSingleton<IPendingEventDraftService, PendingEventDraftService>();
            services.AddSingleton<IEventPublishService, EventPublishService>();
            services.AddSingleton<ISystemStateRepository, SystemStateRepository>();
            services.AddSingleton<IColorMappingService, ColorMappingService>();
            services.AddSingleton<ICalendarQueryService, CalendarQueryService>();
            services.AddTransient<IIcsExportService, IcsExportService>();
            services.AddTransient<IIcsImportService, IcsImportService>();
            services.AddHttpClient<ITogglApiClient, TogglApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.track.toggl.com/");
            });
            services.AddSingleton<ITogglSleepImportService, TogglSleepImportService>();
            services.AddSingleton<TogglSleepImportHandler>();
            services.AddSingleton<ITogglTransitRepository, TogglTransitRepository>();
            services.AddSingleton<EightFifteenRuleService>();
            services.AddSingleton<TogglTransitCardProvider>();
            services.AddSingleton<ITogglTransitImportService, TogglTransitImportService>();
            services.AddSingleton<TogglTransitImportHandler>();
            services.AddSingleton<IMapsTimelineRepository, MapsTimelineRepository>();
            services.AddSingleton<MapsTimelineParser>();
            services.AddSingleton<MapsTimelineImportHandler>();
            services.AddSingleton<MapsTimelineCardProvider>();
            services.AddSingleton<ITogglPhoneRuleRepository, TogglPhoneRuleRepository>();
            services.AddSingleton<ITogglPhoneClassificationService, TogglPhoneClassificationService>();
            services.AddSingleton<ITogglCsvImportService, TogglCsvImportService>();
            services.AddSingleton<TogglCsvImportHandler>();
            services.AddSingleton<ITogglPhoneRepository, TogglPhoneRepository>();
            services.AddSingleton<TogglSlidingWindowService>();
            services.AddSingleton<TogglPhoneCardProvider>();
            services.AddSingleton<TogglPhoneImportHandler>();
            services.AddSingleton<ISyncStatusService, SyncStatusService>();
            services.AddSingleton<INavigationStateService, NavigationStateService>();
            services.AddSingleton<ICalendarSelectionService, CalendarSelectionService>();
            services.AddSingleton<ICalendarDaySelectionService, CalendarDaySelectionService>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<ICalendarViewRangeProvider>(provider => provider.GetRequiredService<MainViewModel>());
            services.AddSingleton<EventDetailsPanelViewModel>();
            services.AddSingleton<DataSourcePanelViewModel>();
            services.AddTransient<TogglSleepCompactCardViewModel>();
            services.AddTransient<TogglSleepDrilldownViewModel>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<MainPage>();
            services.AddTransient<EventDetailsPanelControl>();
            services.AddTransient<DataSourcePanelControl>();
            services.AddTransient<TogglSleepCompactCardControl>();
            services.AddTransient<TogglSleepDrilldownControl>();
            services.AddTransient<TogglTransitCompactCardViewModel>();
            services.AddTransient<TogglTransitDrilldownViewModel>();
            services.AddTransient<TogglTransitCompactCardControl>();
            services.AddTransient<TogglTransitDrilldownControl>();
            services.AddTransient<MapsTimelineCompactCardViewModel>();
            services.AddTransient<MapsTimelineDrilldownViewModel>();
            services.AddTransient<MapsTimelineCompactCardControl>();
            services.AddTransient<MapsTimelineDrilldownControl>();
            services.AddTransient<TogglPhoneRulesViewModel>();
            services.AddTransient<TogglPhoneCompactCardViewModel>();
            services.AddTransient<TogglPhoneDrilldownViewModel>();
            services.AddTransient<TogglPhoneCompactCardControl>();
            services.AddTransient<TogglPhoneDrilldownControl>();
            services.AddTransient<TogglPhoneRulesControl>();
            services.AddSingleton<ICiv5SessionRepository, Civ5SessionRepository>();
            services.AddSingleton<ICiv5SaveScannerService, Civ5SaveScannerService>();
            services.AddSingleton<Civ5CardProvider>();
            services.AddSingleton<Civ5ImportHandler>();
            services.AddTransient<Civ5CompactCardViewModel>();
            services.AddTransient<Civ5DrilldownViewModel>();
            services.AddTransient<Civ5CompactCardControl>();
            services.AddTransient<Civ5DrilldownControl>();
            services.AddSingleton<ICallLogRepository, CallLogRepository>();
            services.AddSingleton<ICallLogImportService, CallLogImportService>();
            services.AddSingleton<CallLogImportHandler>();
            services.AddSingleton<CallLogCardProvider>();
            services.AddTransient<CallLogCompactCardViewModel>();
            services.AddTransient<CallLogDrilldownViewModel>();
            services.AddTransient<CallLogCompactCardControl>();
            services.AddTransient<CallLogDrilldownControl>();
            services.AddHttpClient<IStatsFmApiClient, StatsFmApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.stats.fm/");
            });
            services.AddSingleton<ISpotifyStreamRepository, SpotifyStreamRepository>();
            services.AddSingleton<ISpotifyImportService, SpotifyImportService>();
            services.AddSingleton<SpotifyImportHandler>();
            services.AddSingleton<SpotifyCardProvider>();
            services.AddTransient<SpotifyCompactCardViewModel>();
            services.AddTransient<SpotifyDrilldownViewModel>();
            services.AddTransient<SpotifyCompactCardControl>();
            services.AddTransient<SpotifyDrilldownControl>();
            services.AddSingleton<IComfyUIRepository, ComfyUIRepository>();
            services.AddSingleton<IComfyUIFolderScannerService, ComfyUIFolderScannerService>();
            services.AddSingleton<ComfyUICardProvider>();
            services.AddSingleton<ComfyUIImportHandler>();
            services.AddTransient<ComfyUICompactCardViewModel>();
            services.AddTransient<ComfyUIDrilldownViewModel>();
            services.AddTransient<ComfyUICompactCardControl>();
            services.AddTransient<ComfyUIDrilldownControl>();
            services.AddHttpClient<IGraphApiClient, GraphApiClient>(client =>
            {
                client.BaseAddress = new Uri("https://graph.microsoft.com/");
            });
            services.AddSingleton<IOutlookEventRepository, OutlookEventRepository>();
            services.AddSingleton<IOutlookImportService, OutlookImportService>();
            services.AddSingleton<OutlookImportHandler>();
            services.AddSingleton<OutlookCardProvider>();
            services.AddTransient<OutlookCompactCardViewModel>();
            services.AddTransient<OutlookDrilldownViewModel>();
            services.AddTransient<OutlookCompactCardControl>();
            services.AddTransient<OutlookDrilldownControl>();
            services.AddTransient<YearViewControl>();
            services.AddTransient<MonthViewControl>();
            services.AddTransient<WeekViewControl>();
            services.AddTransient<DayViewControl>();

            // Data Linking — ClumpBlock providers
            services.AddSingleton<IClumpBlockProvider, Civ5ClumpBlockProvider>();
            services.AddSingleton<IClumpBlockProvider, ComfyUIClumpBlockProvider>();
            services.AddSingleton<IClumpBlockProvider, PhoneClumpBlockProvider>();
            services.AddSingleton<IClumpBlockProvider>(sp => new TrivialClumpBlockProvider(
                SourceKeys.CallLog,
                sp.GetRequiredService<IDbContextFactory<CalendarDbContext>>(),
                sp.GetRequiredService<EightFifteenRuleService>()));
            services.AddSingleton<IClumpBlockProvider>(sp => new TrivialClumpBlockProvider(
                SourceKeys.Toggl,
                sp.GetRequiredService<IDbContextFactory<CalendarDbContext>>(),
                sp.GetRequiredService<EightFifteenRuleService>()));
            services.AddSingleton<IClumpBlockProviderRegistry, ClumpBlockProviderRegistry>();
        }
    }
}

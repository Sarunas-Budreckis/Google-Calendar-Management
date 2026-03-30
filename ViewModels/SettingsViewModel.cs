using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IContentDialogService _dialogService;
    private readonly ISyncManager _syncManager;
    private readonly IDbContextFactory<CalendarDbContext> _contextFactory;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _isConnected;
    private bool _isSyncing;
    private string _syncStatusText = "Connect Google Calendar to enable sync.";
    private int _syncProgressValue;
    private string _lastSyncText = "Never";
    private int _pagesFetched;
    private CancellationTokenSource? _syncCancellationTokenSource;
    private Task? _initializationTask;

    public SettingsViewModel(
        IGoogleCalendarService googleCalendarService,
        IContentDialogService dialogService,
        ISyncManager syncManager,
        IDbContextFactory<CalendarDbContext> contextFactory,
        ILogger<SettingsViewModel> logger)
    {
        _googleCalendarService = googleCalendarService;
        _dialogService = dialogService;
        _syncManager = syncManager;
        _contextFactory = contextFactory;
        _logger = logger;

        ConnectGoogleCalendarCommand = new AsyncRelayCommand(ConnectGoogleCalendarAsync, () => !IsSyncing);
        DisconnectGoogleCalendarCommand = new AsyncRelayCommand(ReconnectGoogleCalendarAsync, () => IsConnected && !IsSyncing);
        SyncWithGoogleCalendarCommand = new AsyncRelayCommand(SyncWithGoogleCalendarAsync, () => IsConnected && !IsSyncing);
        CancelSyncCommand = new RelayCommand(CancelSync, () => IsSyncing);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (!SetProperty(ref _isConnected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(ConnectButtonVisibility));
            OnPropertyChanged(nameof(ReconnectButtonVisibility));
            OnPropertyChanged(nameof(SyncControlsVisibility));
            UpdateCommandStates();
        }
    }

    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (!SetProperty(ref _isSyncing, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CancelSyncButtonVisibility));
            UpdateCommandStates();
        }
    }

    public string ConnectionStatusText => IsConnected ? "Connected" : "Not connected";

    public Visibility ConnectButtonVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ReconnectButtonVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SyncControlsVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CancelSyncButtonVisibility => IsSyncing ? Visibility.Visible : Visibility.Collapsed;

    public string SyncStatusText
    {
        get => _syncStatusText;
        private set => SetProperty(ref _syncStatusText, value);
    }

    public int SyncProgressValue
    {
        get => _syncProgressValue;
        private set
        {
            if (!SetProperty(ref _syncProgressValue, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SyncProgressText));
        }
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        private set => SetProperty(ref _lastSyncText, value);
    }

    public string SyncProgressText => IsSyncing || SyncProgressValue > 0
        ? $"Pages fetched: {_pagesFetched} | Events processed: {SyncProgressValue}"
        : "No sync in progress.";

    public IAsyncRelayCommand ConnectGoogleCalendarCommand { get; }

    public IAsyncRelayCommand DisconnectGoogleCalendarCommand { get; }

    public IAsyncRelayCommand SyncWithGoogleCalendarCommand { get; }

    public IRelayCommand CancelSyncCommand { get; }

    public Task InitializeAsync()
    {
        return _initializationTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        await RefreshConnectionStateAsync();
        await RefreshLastSyncTextAsync();
    }

    private async Task ConnectGoogleCalendarAsync()
    {
        var result = await _googleCalendarService.AuthenticateAsync();
        if (result.Success && result.Data == OAuthStatus.Authenticated)
        {
            IsConnected = true;
            SyncStatusText = "Google Calendar connected. Ready to sync.";
            WeakReferenceMessenger.Default.Send(new AuthenticationSucceededMessage());
            return;
        }

        await _dialogService.ShowErrorAsync(
            "Google Calendar Connection",
            result.ErrorMessage ?? "Unable to connect Google Calendar.");
    }

    private async Task ReconnectGoogleCalendarAsync()
    {
        await _googleCalendarService.RevokeAndClearTokensAsync();
        IsConnected = false;
        await ConnectGoogleCalendarAsync();
    }

    private async Task RefreshConnectionStateAsync()
    {
        var result = await _googleCalendarService.IsAuthenticatedAsync();
        IsConnected = result.Success && result.Data;
        if (!IsConnected && !IsSyncing)
        {
            SyncStatusText = "Connect Google Calendar to enable sync.";
        }
    }

    private async Task SyncWithGoogleCalendarAsync()
    {
        _syncCancellationTokenSource?.Dispose();
        _syncCancellationTokenSource = new CancellationTokenSource();
        _pagesFetched = 0;
        SyncProgressValue = 0;
        SyncStatusText = "Starting Google Calendar sync...";
        IsSyncing = true;

        try
        {
            var progress = new Progress<SyncProgress>(value =>
            {
                _pagesFetched = value.PagesFetched;
                SyncProgressValue = value.EventsProcessed;
                SyncStatusText = value.StatusMessage;
            });

            var result = await Task.Run(
                () => _syncManager.SyncAsync(
                    progress: progress,
                    ct: _syncCancellationTokenSource.Token),
                _syncCancellationTokenSource.Token);

            if (result.WasCancelled)
            {
                SyncStatusText = $"Sync cancelled. {result.EventsAdded + result.EventsUpdated + result.EventsDeleted} event(s) were saved before stopping.";
                return;
            }

            if (result.Success)
            {
                SyncStatusText =
                    $"Sync complete. Added {result.EventsAdded}, updated {result.EventsUpdated}, deleted {result.EventsDeleted}.";
                await RefreshLastSyncTextAsync();
                return;
            }

            SyncStatusText = result.ErrorMessage ?? "Unable to sync Google Calendar.";
            await _dialogService.ShowErrorAsync("Google Calendar Sync", SyncStatusText);
        }
        catch (OperationCanceledException) when (_syncCancellationTokenSource?.IsCancellationRequested == true)
        {
            SyncStatusText = "Sync cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while syncing Google Calendar.");
            SyncStatusText = "Unable to sync Google Calendar.";
            await _dialogService.ShowErrorAsync(
                "Google Calendar Sync",
                "Unable to sync Google Calendar. Check the log for details and try again.");
        }
        finally
        {
            IsSyncing = false;
        }
    }

    private void CancelSync()
    {
        _syncCancellationTokenSource?.Cancel();
    }

    private async Task RefreshLastSyncTextAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var refresh = await context.DataSourceRefreshes
                .AsNoTracking()
                .Where(item => item.SourceName == "gcal" && item.Success == true && item.LastRefreshedAt.HasValue)
                .OrderByDescending(item => item.LastRefreshedAt)
                .FirstOrDefaultAsync();

            LastSyncText = refresh?.LastRefreshedAt?.ToLocalTime().ToString("g") ?? "Never";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to load the last successful Google Calendar sync timestamp.");
            LastSyncText = "Unavailable";
        }
    }

    private void UpdateCommandStates()
    {
        ConnectGoogleCalendarCommand.NotifyCanExecuteChanged();
        DisconnectGoogleCalendarCommand.NotifyCanExecuteChanged();
        SyncWithGoogleCalendarCommand.NotifyCanExecuteChanged();
        CancelSyncCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(SyncProgressText));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const string MaskedTokenValue = "•••••";

    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IContentDialogService _dialogService;
    private readonly IConfigRepository _configRepository;
    private readonly ITogglApiClient _togglApiClient;
    private bool _isConnected;
    private bool _isTestingTogglConnection;
    private string _togglApiToken = "";
    private string? _togglConnectionTestResult;
    private Task? _initializationTask;

    public SettingsViewModel(
        IGoogleCalendarService googleCalendarService,
        IContentDialogService dialogService,
        IConfigRepository configRepository,
        ITogglApiClient togglApiClient)
    {
        _googleCalendarService = googleCalendarService;
        _dialogService = dialogService;
        _configRepository = configRepository;
        _togglApiClient = togglApiClient;

        ConnectGoogleCalendarCommand = new AsyncRelayCommand(ConnectGoogleCalendarAsync);
        DisconnectGoogleCalendarCommand = new AsyncRelayCommand(ReconnectGoogleCalendarAsync, () => IsConnected);
        SaveTogglApiTokenCommand = new AsyncRelayCommand<string?>(SaveTogglApiTokenAsync);
        TestTogglConnectionCommand = new AsyncRelayCommand<string?>(TestTogglConnectionAsync, _ => !IsTestingTogglConnection);
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
            DisconnectGoogleCalendarCommand.NotifyCanExecuteChanged();
        }
    }

    public string ConnectionStatusText => IsConnected ? "Connected" : "Not connected";

    public Visibility ConnectButtonVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ReconnectButtonVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand ConnectGoogleCalendarCommand { get; }

    public IAsyncRelayCommand DisconnectGoogleCalendarCommand { get; }

    public IAsyncRelayCommand<string?> SaveTogglApiTokenCommand { get; }

    public IAsyncRelayCommand<string?> TestTogglConnectionCommand { get; }

    public string TogglApiToken
    {
        get => _togglApiToken;
        set => SetProperty(ref _togglApiToken, value);
    }

    public bool IsTestingTogglConnection
    {
        get => _isTestingTogglConnection;
        private set
        {
            if (SetProperty(ref _isTestingTogglConnection, value))
            {
                OnPropertyChanged(nameof(CanTestTogglConnection));
                OnPropertyChanged(nameof(TogglConnectionProgressVisibility));
                TestTogglConnectionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanTestTogglConnection => !IsTestingTogglConnection;

    public Visibility TogglConnectionProgressVisibility => IsTestingTogglConnection ? Visibility.Visible : Visibility.Collapsed;

    public string? TogglConnectionTestResult
    {
        get => _togglConnectionTestResult;
        private set => SetProperty(ref _togglConnectionTestResult, value);
    }

    public Task InitializeAsync()
    {
        return _initializationTask ??= RefreshConnectionStateAsync();
    }

    private async Task ConnectGoogleCalendarAsync()
    {
        var result = await _googleCalendarService.AuthenticateAsync();
        if (result.Success && result.Data == OAuthStatus.Authenticated)
        {
            IsConnected = true;
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

        var storedToken = await _configRepository.GetConfigValueAsync(TogglSleepImportService.TogglApiTokenConfigKey);
        TogglApiToken = string.IsNullOrWhiteSpace(storedToken) ? "" : MaskedTokenValue;
    }

    private async Task SaveTogglApiTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token == MaskedTokenValue)
        {
            TogglConnectionTestResult = "Enter a Toggl API token before saving.";
            return;
        }

        await _configRepository.SetConfigValueAsync(
            TogglSleepImportService.TogglApiTokenConfigKey,
            token.Trim(),
            configType: "secret",
            description: "Encrypted Toggl Track API token",
            encrypt: true);

        TogglApiToken = MaskedTokenValue;
        TogglConnectionTestResult = "Token saved.";
    }

    private async Task TestTogglConnectionAsync(string? token)
    {
        IsTestingTogglConnection = true;
        TogglConnectionTestResult = null;

        try
        {
            var tokenToTest = token;
            if (string.IsNullOrWhiteSpace(tokenToTest) || tokenToTest == MaskedTokenValue)
            {
                tokenToTest = await _configRepository.GetConfigValueAsync(TogglSleepImportService.TogglApiTokenConfigKey);
            }

            if (string.IsNullOrWhiteSpace(tokenToTest))
            {
                TogglConnectionTestResult = "Enter a Toggl API token before testing.";
                return;
            }

            var connected = await _togglApiClient.TestConnectionAsync(tokenToTest.Trim());
            TogglConnectionTestResult = connected ? "Connected" : "Toggl rejected the token.";
        }
        catch (Exception ex) when (ex is TogglApiException or HttpRequestException or TaskCanceledException)
        {
            TogglConnectionTestResult = ex is TaskCanceledException
                ? "Connection test timed out."
                : ex.Message;
        }
        finally
        {
            IsTestingTogglConnection = false;
        }
    }
}

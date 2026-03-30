using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IGoogleCalendarService _googleCalendarService;
    private readonly IContentDialogService _dialogService;
    private bool _isConnected;

    public SettingsViewModel(
        IGoogleCalendarService googleCalendarService,
        IContentDialogService dialogService)
    {
        _googleCalendarService = googleCalendarService;
        _dialogService = dialogService;

        ConnectGoogleCalendarCommand = new AsyncRelayCommand(ConnectGoogleCalendarAsync);
        DisconnectGoogleCalendarCommand = new AsyncRelayCommand(ReconnectGoogleCalendarAsync);

        _ = RefreshConnectionStateAsync();
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
        }
    }

    public string ConnectionStatusText => IsConnected ? "Connected" : "Not connected";

    public Visibility ConnectButtonVisibility => IsConnected ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ReconnectButtonVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand ConnectGoogleCalendarCommand { get; }

    public IAsyncRelayCommand DisconnectGoogleCalendarCommand { get; }

    private async Task RefreshConnectionStateAsync()
    {
        var result = await _googleCalendarService.IsAuthenticatedAsync();
        IsConnected = result.Success && result.Data;
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
}

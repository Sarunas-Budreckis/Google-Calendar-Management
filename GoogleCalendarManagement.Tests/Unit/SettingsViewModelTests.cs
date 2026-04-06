using FluentAssertions;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Moq;

namespace GoogleCalendarManagement.Tests.Unit;

public sealed class SettingsViewModelTests
{
    [Fact]
    public async Task InitializeAsync_AuthenticatedUser_ShowsConnectedState()
    {
        var viewModel = CreateViewModel(isAuthenticated: true);

        await viewModel.InitializeAsync();

        viewModel.IsConnected.Should().BeTrue();
        viewModel.ConnectionStatusText.Should().Be("Connected");
        viewModel.ConnectButtonVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Collapsed);
        viewModel.ReconnectButtonVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
    }

    [Fact]
    public async Task InitializeAsync_DisconnectedUser_ShowsNotConnectedState()
    {
        var viewModel = CreateViewModel(isAuthenticated: false);

        await viewModel.InitializeAsync();

        viewModel.IsConnected.Should().BeFalse();
        viewModel.ConnectionStatusText.Should().Be("Not connected");
        viewModel.ConnectButtonVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
        viewModel.ReconnectButtonVisibility.Should().Be(Microsoft.UI.Xaml.Visibility.Collapsed);
    }

    [Fact]
    public async Task ConnectGoogleCalendarCommand_Success_UpdatesConnectionState()
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(service => service.IsAuthenticatedAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(false));
        googleCalendarService
            .Setup(service => service.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<OAuthStatus>.Ok(OAuthStatus.Authenticated));

        var viewModel = new SettingsViewModel(
            googleCalendarService.Object,
            Mock.Of<IContentDialogService>());

        await viewModel.InitializeAsync();
        await viewModel.ConnectGoogleCalendarCommand.ExecuteAsync(null);

        viewModel.IsConnected.Should().BeTrue();
        viewModel.ConnectionStatusText.Should().Be("Connected");
    }

    private static SettingsViewModel CreateViewModel(bool isAuthenticated)
    {
        var googleCalendarService = new Mock<IGoogleCalendarService>();
        googleCalendarService
            .Setup(service => service.IsAuthenticatedAsync())
            .ReturnsAsync(OperationResult<bool>.Ok(isAuthenticated));
        googleCalendarService
            .Setup(service => service.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<OAuthStatus>.Ok(OAuthStatus.Authenticated));

        return new SettingsViewModel(
            googleCalendarService.Object,
            Mock.Of<IContentDialogService>());
    }
}

using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GoogleCalendarManagement.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    public SettingsViewModel ViewModel { get; }

    private async void SettingsPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void SaveTogglTokenButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ViewModel.SaveTogglApiTokenCommand.ExecuteAsync(TogglApiTokenBox.Password);
    }

    private void TestTogglConnectionButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ViewModel.TestTogglConnectionCommand.ExecuteAsync(TogglApiTokenBox.Password);
    }
}

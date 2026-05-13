using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class DataSourcePanelControl : UserControl
{
    public DataSourcePanelControl(DataSourcePanelViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public DataSourcePanelViewModel ViewModel { get; }

    private async void DataSourcePanelControl_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMinimized = true;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsMinimized = false;
    }

    private async void DayHeader_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (ViewModel.OpenDayNameHeaderCommand.CanExecute(null))
        {
            await ViewModel.OpenDayNameHeaderCommand.ExecuteAsync(null);
        }
    }
}

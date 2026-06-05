using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class TogglPhoneRulesControl : UserControl
{
    public TogglPhoneRulesControl(TogglPhoneRulesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public TogglPhoneRulesViewModel ViewModel { get; }
}

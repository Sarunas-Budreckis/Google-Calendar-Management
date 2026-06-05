using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class TogglPhoneCompactCardControl : UserControl
{
    public TogglPhoneCompactCardControl(TogglPhoneCompactCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public TogglPhoneCompactCardViewModel ViewModel { get; }

    public Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        return ViewModel.LoadAsync(date, ct);
    }
}

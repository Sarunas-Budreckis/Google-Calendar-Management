using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class TogglTransitCompactCardControl : UserControl
{
    public TogglTransitCompactCardControl(TogglTransitCompactCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public TogglTransitCompactCardViewModel ViewModel { get; }

    public Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        return ViewModel.LoadAsync(date, ct);
    }
}

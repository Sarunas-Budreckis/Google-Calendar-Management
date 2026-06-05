using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class MapsTimelineDrilldownControl : UserControl
{
    public MapsTimelineDrilldownControl(MapsTimelineDrilldownViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public MapsTimelineDrilldownViewModel ViewModel { get; }

    public Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        return ViewModel.LoadAsync(date, ct);
    }
}

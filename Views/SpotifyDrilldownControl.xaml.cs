using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class SpotifyDrilldownControl : UserControl
{
    public SpotifyDrilldownControl(SpotifyDrilldownViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public SpotifyDrilldownViewModel ViewModel { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        await ViewModel.LoadAsync(date, ct);
        Timeline.SetItems(ViewModel.DotItems);
    }
}

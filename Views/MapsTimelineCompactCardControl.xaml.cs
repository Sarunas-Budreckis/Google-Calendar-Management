using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class MapsTimelineCompactCardControl : UserControl
{
    public MapsTimelineCompactCardControl(MapsTimelineCompactCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public MapsTimelineCompactCardViewModel ViewModel { get; }

    public Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        return ViewModel.LoadAsync(date, ct);
    }
}

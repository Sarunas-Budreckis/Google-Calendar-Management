using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class Civ5CompactCardControl : UserControl
{
    public Civ5CompactCardControl(Civ5CompactCardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public Civ5CompactCardViewModel ViewModel { get; }

    public Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        return ViewModel.LoadAsync(date, ct);
    }
}

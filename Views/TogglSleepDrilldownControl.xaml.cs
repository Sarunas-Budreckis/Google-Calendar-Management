using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace GoogleCalendarManagement.Views;

public sealed partial class TogglSleepDrilldownControl : UserControl
{
    private bool _suppressValueChanged;

    public TogglSleepDrilldownControl(TogglSleepDrilldownViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    public TogglSleepDrilldownViewModel ViewModel { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        await ViewModel.LoadAsync(date, ct);
        SyncQualityBox();
    }

    private void SyncQualityBox()
    {
        _suppressValueChanged = true;
        QualityBox.Value = ViewModel.Quality.HasValue ? (double)ViewModel.Quality.Value : double.NaN;
        _suppressValueChanged = false;
    }

    private async void QualityBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressValueChanged)
        {
            return;
        }

        var newValue = args.NewValue;
        var quality = double.IsNaN(newValue) ? (int?)null : (int)newValue;
        await ViewModel.SetQualityAsync(quality);
    }

    private async void ClearQuality_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.SetQualityAsync(null);
        SyncQualityBox();
    }
}

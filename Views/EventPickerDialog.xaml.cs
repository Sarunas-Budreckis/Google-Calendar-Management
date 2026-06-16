using GoogleCalendarManagement.ViewModels;

namespace GoogleCalendarManagement.Views;

public sealed partial class EventPickerDialog : ContentDialog
{
    public EventPickerViewModel ViewModel { get; }

    public EventPickerDialog(EventPickerViewModel vm)
    {
        ViewModel = vm;
        this.InitializeComponent();
        DataContext = vm;

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EventPickerViewModel.SelectedItem))
                IsPrimaryButtonEnabled = vm.SelectedItem != null;
        };

        IsPrimaryButtonEnabled = false;
        PrimaryButtonClick += Dialog_PrimaryButtonClick;
    }

    private async void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        args.Cancel = true;
        try
        {
            await ViewModel.ConfirmLinkCommand.ExecuteAsync(null);
            if (!ViewModel.HasError)
                sender.Hide();
        }
        finally
        {
            deferral.Complete();
        }
    }
}

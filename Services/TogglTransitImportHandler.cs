using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Services;

public sealed class TogglTransitImportHandler : IDataSourceImportHandler
{
    private readonly ITogglTransitImportService _importService;
    private readonly IContentDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly ICalendarViewRangeProvider _viewRangeProvider;

    public TogglTransitImportHandler(
        ITogglTransitImportService importService,
        IContentDialogService dialogService,
        IWindowService windowService,
        ICalendarViewRangeProvider viewRangeProvider)
    {
        _importService = importService;
        _dialogService = dialogService;
        _windowService = windowService;
        _viewRangeProvider = viewRangeProvider;
    }

    public string SourceKey => TogglTransitImportService.SourceKey;

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        var selection = await ShowDateRangeDialogAsync(ct);
        if (selection is null)
        {
            return;
        }

        var result = selection.Value.Result;
        if (result is null)
        {
            return;
        }

        if (result.Success)
        {
            await _dialogService.ShowMessageAsync(
                "Toggl Driving Import",
                $"Imported {result.RecordsFetched} driving entries.",
                "OK");
            return;
        }

        await _dialogService.ShowErrorAsync(
            "Toggl Driving Import",
            result.ErrorMessage ?? "Unable to import Toggl driving entries.");
    }

    private async Task<(DateOnly From, DateOnly To, TogglTransitImportResult? Result)?> ShowDateRangeDialogAsync(CancellationToken ct)
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return null;
        }

        var (defaultFrom, defaultTo) = _viewRangeProvider.GetCurrentViewDisplayRange();
        var fromPicker = new DatePicker
        {
            Header = "Import from",
            Date = ToDateTimeOffset(defaultFrom)
        };
        var toPicker = new DatePicker
        {
            Header = "Import to",
            Date = ToDateTimeOffset(defaultTo)
        };
        var validationText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Text = "Start date must be before end date.",
            Visibility = Visibility.Collapsed
        };
        var progressRing = new ProgressRing
        {
            Width = 24,
            Height = 24,
            IsActive = false,
            Visibility = Visibility.Collapsed
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Choose the inclusive date range to import driving sessions from Toggl Track.",
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                fromPicker,
                toPicker,
                validationText,
                progressRing
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Import Toggl Driving",
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            XamlRoot = xamlRoot
        };
        TogglTransitImportResult? importResult = null;

        void UpdateValidation()
        {
            var from = DateOnly.FromDateTime(fromPicker.Date.Date);
            var to = DateOnly.FromDateTime(toPicker.Date.Date);
            var isValid = from <= to;
            validationText.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;
            dialog.IsPrimaryButtonEnabled = isValid;
        }

        fromPicker.DateChanged += (_, _) => UpdateValidation();
        toPicker.DateChanged += (_, _) => UpdateValidation();
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();
            try
            {
                dialog.IsPrimaryButtonEnabled = false;
                dialog.IsSecondaryButtonEnabled = false;
                progressRing.IsActive = true;
                progressRing.Visibility = Visibility.Visible;

                var from = DateOnly.FromDateTime(fromPicker.Date.Date);
                var to = DateOnly.FromDateTime(toPicker.Date.Date);
                importResult = await _importService.ImportAsync(from, to, ct);
                dialog.Hide();
            }
            finally
            {
                deferral.Complete();
            }
        };
        UpdateValidation();

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        catch (Exception ex) when (ex is COMException or TaskCanceledException)
        {
            return null;
        }

        if (result != ContentDialogResult.Primary && importResult is null)
        {
            return null;
        }

        return (
            DateOnly.FromDateTime(fromPicker.Date.Date),
            DateOnly.FromDateTime(toPicker.Date.Date),
            importResult);
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue));
}

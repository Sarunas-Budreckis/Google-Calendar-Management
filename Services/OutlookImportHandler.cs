using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GoogleCalendarManagement.Services;

public sealed class OutlookImportHandler : IDataSourceImportHandler
{
    private readonly IOutlookImportService _importService;
    private readonly IContentDialogService _dialogService;
    private readonly IWindowService _windowService;
    private readonly ICalendarViewRangeProvider _viewRangeProvider;

    public OutlookImportHandler(
        IOutlookImportService importService,
        IContentDialogService dialogService,
        IWindowService windowService,
        ICalendarViewRangeProvider viewRangeProvider)
    {
        _importService = importService;
        _dialogService = dialogService;
        _windowService = windowService;
        _viewRangeProvider = viewRangeProvider;
    }

    public string SourceKey => OutlookImportService.SourceKey;

    public bool IsApiFetch => true;

    public IDataPointProjector GetProjector() => new OutlookProjector();

    public async Task TriggerImportAsync(CancellationToken ct = default)
    {
        var result = await ShowSyncDialogAsync(ct);
        if (result is null)
        {
            return;
        }

        if (result.Value.ImportResult is null)
        {
            return;
        }

        var importResult = result.Value.ImportResult;
        if (importResult.Success)
        {
            var message = importResult.NewRecords == 0 && importResult.UpdatedRecords == 0
                ? "No events found in the selected date range."
                : importResult.NewRecords == 0
                    ? $"All {importResult.UpdatedRecords} events already up to date."
                    : importResult.UpdatedRecords > 0
                        ? $"Synced {importResult.NewRecords} new events ({importResult.UpdatedRecords} already up to date)."
                        : $"Synced {importResult.NewRecords} work events.";
            await _dialogService.ShowMessageAsync("Outlook Sync", message, "OK");
        }
        else
        {
            await _dialogService.ShowErrorAsync(
                "Outlook Sync",
                importResult.ErrorMessage ?? "Unable to sync Outlook calendar.");
        }
    }

    private async Task<(DateOnly From, DateOnly To, OutlookImportResult? ImportResult)?> ShowSyncDialogAsync(CancellationToken ct)
    {
        var xamlRoot = _windowService.GetXamlRoot();
        if (xamlRoot is null)
        {
            return null;
        }

        var (defaultFrom, defaultTo) = _viewRangeProvider.GetCurrentViewDisplayRange();

        var tokenBox = new TextBox
        {
            Header = "Access token (from Graph Explorer)",
            PlaceholderText = "Paste your Bearer token here...",
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            Height = 80
        };
        var fromPicker = new DatePicker { Header = "Sync from", Date = ToDateTimeOffset(defaultFrom) };
        var toPicker = new DatePicker { Header = "Sync to", Date = ToDateTimeOffset(defaultTo) };
        var instructionText = new TextBlock
        {
            Text = "Open Graph Explorer, sign in with your Mayo Clinic account, then copy the access token from the \"Access token\" tab.",
            TextWrapping = TextWrapping.WrapWholeWords,
            Opacity = 0.8
        };
        var graphExplorerLink = new HyperlinkButton
        {
            Content = "Open Graph Explorer",
            NavigateUri = new Uri("https://developer.microsoft.com/en-us/graph/graph-explorer"),
            Padding = new Microsoft.UI.Xaml.Thickness(0)
        };
        var validationText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            Text = "",
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var progressRing = new ProgressRing
        {
            Width = 24, Height = 24, IsActive = false, Visibility = Visibility.Collapsed
        };
        var content = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                instructionText,
                graphExplorerLink,
                tokenBox,
                fromPicker,
                toPicker,
                validationText,
                progressRing
            }
        };

        var dialog = new ContentDialog
        {
            Title = "Sync Outlook Work Calendar",
            PrimaryButtonText = "Sync",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = content,
            XamlRoot = xamlRoot
        };

        OutlookImportResult? importResult = null;

        void UpdateValidation()
        {
            var from = DateOnly.FromDateTime(fromPicker.Date.Date);
            var to = DateOnly.FromDateTime(toPicker.Date.Date);
            var token = tokenBox.Text.Trim();
            var dateValid = from <= to;
            var tokenValid = token.Length > 0;

            if (!dateValid)
            {
                validationText.Text = "Start date must be before end date.";
                validationText.Visibility = Visibility.Visible;
            }
            else if (!tokenValid)
            {
                validationText.Text = "Paste your access token to continue.";
                validationText.Visibility = Visibility.Visible;
            }
            else
            {
                validationText.Visibility = Visibility.Collapsed;
            }

            dialog.IsPrimaryButtonEnabled = dateValid && tokenValid;
        }

        tokenBox.TextChanged += (_, _) => UpdateValidation();
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
                var token = tokenBox.Text.Trim();
                importResult = await _importService.ImportAsync(token, from, to, ct);
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

    private static DateTimeOffset ToDateTimeOffset(DateOnly date)
    {
        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue));
    }
}

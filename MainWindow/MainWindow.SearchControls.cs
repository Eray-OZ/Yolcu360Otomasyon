using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border SearchPanel => SearchViewControl.FindControl<Border>("SearchPanel")!;
    private Border SearchResultsPanel => SearchViewControl.FindControl<Border>("SearchResultsPanel")!;
    private TextBlock SearchStatusTextBlock => SearchViewControl.FindControl<TextBlock>("SearchStatusTextBlock")!;
    private TextBox PickupLocationTextBox => SearchViewControl.FindControl<TextBox>("PickupLocationTextBox")!;
    private TextBox PickupDateTextBox => SearchViewControl.FindControl<TextBox>("PickupDateTextBox")!;
    private TextBox ReturnDateTextBox => SearchViewControl.FindControl<TextBox>("ReturnDateTextBox")!;
    private TextBox PickupTimeTextBox => SearchViewControl.FindControl<TextBox>("PickupTimeTextBox")!;
    private TextBox ReturnTimeTextBox => SearchViewControl.FindControl<TextBox>("ReturnTimeTextBox")!;
    private ComboBox TransmissionComboBox => SearchViewControl.FindControl<ComboBox>("TransmissionComboBox")!;
    private ComboBox FuelComboBox => SearchViewControl.FindControl<ComboBox>("FuelComboBox")!;
    private Button SearchButton => SearchViewControl.FindControl<Button>("SearchButton")!;
    private TextBox CollectionNameTextBox => SearchViewControl.FindControl<TextBox>("CollectionNameTextBox")!;
    private Button SaveResultsButton => SearchViewControl.FindControl<Button>("SaveResultsButton")!;
    private DataGrid ResultsDataGrid => SearchViewControl.FindControl<DataGrid>("ResultsDataGrid")!;
    private Border BrowserSectionPanel => BrowserViewControl.FindControl<Border>("BrowserSectionPanel")!;
    private NativeWebView EmbeddedBrowser => BrowserViewControl.FindControl<NativeWebView>("EmbeddedBrowser")!;

    private void ConfigureSearchViewEvents()
    {
        SearchButton.Click += SearchButton_Click;
        SaveResultsButton.Click += SaveResultsButton_Click;
    }

    private void SearchTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowSearchSection();
    }

    private async void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        try
        {
            ShowBrowserSection();
            SetSearchStatus("Gömülü tarayıcı açılıyor...");

            var baService = GetBAService();
            await baService.OpenYolcu360HomeAsync();

            var title = await baService.GetTitleAsync();
            SetSearchStatus($"Gömülü tarayıcı hazır. Title: {title}");
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Gömülü tarayıcı hatası: {ex.Message}");
        }
    }

    private BAService GetBAService()
    {
        if (_baService is not null)
            return _baService;

        _baService = new BAService(EmbeddedBrowser);
        _baService.ProgressChanged += message =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SetSearchStatus(message);
            });
        };

        return _baService;
    }
}

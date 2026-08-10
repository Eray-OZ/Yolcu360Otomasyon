using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private Border SearchPanelControl => SearchViewRootControl.FindControl<Border>("SearchPanel")!;
    private Border SearchResultsPanelControl => SearchViewRootControl.FindControl<Border>("SearchResultsPanel")!;
    private TextBlock SearchStatusTextBlockControl => SearchViewRootControl.FindControl<TextBlock>("SearchStatusTextBlock")!;
    private TextBox PickupLocationTextBoxControl => SearchViewRootControl.FindControl<TextBox>("PickupLocationTextBox")!;
    private TextBox PickupDateTextBoxControl => SearchViewRootControl.FindControl<TextBox>("PickupDateTextBox")!;
    private TextBox ReturnDateTextBoxControl => SearchViewRootControl.FindControl<TextBox>("ReturnDateTextBox")!;
    private TextBox PickupTimeTextBoxControl => SearchViewRootControl.FindControl<TextBox>("PickupTimeTextBox")!;
    private TextBox ReturnTimeTextBoxControl => SearchViewRootControl.FindControl<TextBox>("ReturnTimeTextBox")!;
    private ComboBox TransmissionComboBoxControl => SearchViewRootControl.FindControl<ComboBox>("TransmissionComboBox")!;
    private ComboBox FuelComboBoxControl => SearchViewRootControl.FindControl<ComboBox>("FuelComboBox")!;
    private Button SearchButtonControl => SearchViewRootControl.FindControl<Button>("SearchButton")!;
    private TextBox CollectionNameTextBoxControl => SearchViewRootControl.FindControl<TextBox>("CollectionNameTextBox")!;
    private Button SaveResultsButtonControl => SearchViewRootControl.FindControl<Button>("SaveResultsButton")!;
    private DataGrid ResultsDataGridControl => SearchViewRootControl.FindControl<DataGrid>("ResultsDataGrid")!;
    private Border BrowserSectionPanelControl => BrowserViewRootControl.FindControl<Border>("BrowserSectionPanel")!;
    private NativeWebView EmbeddedBrowserControl => BrowserViewRootControl.FindControl<NativeWebView>("EmbeddedBrowser")!;

    private void ConfigureSearchViewEvents()
    {
        SearchButtonControl.Click += SearchButton_Click;
        SaveResultsButtonControl.Click += SaveResultsButton_Click;
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

        _baService = new BAService(EmbeddedBrowserControl);
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

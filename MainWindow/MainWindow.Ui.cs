using Avalonia.Controls;
using Avalonia.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private void ShowSearchSection()
    {
        ShowContentSection(
            visiblePanel: SearchPanelControl,
            activeButton: SearchTabButton,
            showSearchResults: _latestResults is not null && _latestResults.Count > 0);
    }

    private void ShowHistorySection()
    {
        ShowContentSection(HistoryPanelControl, HistoryTabButton);
    }

    private void ShowPaymentsSection()
    {
        ShowContentSection(PaymentsPanelControl, PaymentsTabButton);
    }

    private void ShowPaymentCheckoutSection()
    {
        ShowContentSection(PaymentCheckoutPanelControl, PaymentsTabButton);
    }

    private void ShowBrowserSection()
    {
        ShowContentSection(BrowserSectionPanelControl, NativeWebViewTestButton, showNativeWebViewTest: true);
    }

    private void ShowContentSection(
        Control visiblePanel,
        Button activeButton,
        bool showSearchResults = false,
        bool showNativeWebViewTest = false)
    {
        SearchViewRootControl.IsVisible = ReferenceEquals(visiblePanel, SearchPanelControl);
        SearchPanelControl.IsVisible = ReferenceEquals(visiblePanel, SearchPanelControl);
        SearchResultsPanelControl.IsVisible = showSearchResults;
        HistoryViewRootControl.IsVisible = ReferenceEquals(visiblePanel, HistoryPanelControl);
        HistoryPanelControl.IsVisible = ReferenceEquals(visiblePanel, HistoryPanelControl);
        PaymentsViewRootControl.IsVisible =
            ReferenceEquals(visiblePanel, PaymentsPanelControl) ||
            ReferenceEquals(visiblePanel, PaymentCheckoutPanelControl);
        PaymentsPanelControl.IsVisible = ReferenceEquals(visiblePanel, PaymentsPanelControl);
        PaymentCheckoutPanelControl.IsVisible = ReferenceEquals(visiblePanel, PaymentCheckoutPanelControl);
        BrowserViewRootControl.IsVisible = ReferenceEquals(visiblePanel, BrowserSectionPanelControl);
        BrowserSectionPanelControl.IsVisible = ReferenceEquals(visiblePanel, BrowserSectionPanelControl);

        SearchTabButton.Classes.Set("primary", ReferenceEquals(activeButton, SearchTabButton));
        HistoryTabButton.Classes.Set("primary", ReferenceEquals(activeButton, HistoryTabButton));
        PaymentsTabButton.Classes.Set("primary", ReferenceEquals(activeButton, PaymentsTabButton));
        NativeWebViewTestButton.Classes.Set("primary", ReferenceEquals(activeButton, NativeWebViewTestButton));
        NativeWebViewTestButton.IsVisible = showNativeWebViewTest;
    }

    private void SetSearchStatus(string message)
    {
        SearchStatusTextBlockControl.Text = message;
    }

    private void SetHistoryStatus(string message)
    {
        HistoryStatusTextBlockControl.Text = message;
    }

    private void SetVehicleStatus(string message)
    {
        VehicleStatusTextBlockControl.Text = message;
    }

    private void SetCheckoutStatus(string message)
    {
        CheckoutStatusTextBlockControl.Text = message;
    }

    private void SetPaymentsStatus(string message)
    {
        PaymentsStatusTextBlockControl.Text = message;
    }

    private void ConfigureResultsGrid()
    {
        ResultsDataGridControl.AutoGenerateColumns = false;
        ResultsDataGridControl.Columns.Clear();

        AddTextColumn(ResultsDataGridControl, "Araç", nameof(SearchResultItem.Title), 2);
        AddTextColumn(ResultsDataGridControl, "Detay", nameof(SearchResultItem.Subtitle), 2);
        AddTextColumn(ResultsDataGridControl, "Toplam Fiyat", nameof(SearchResultItem.Price), 1);
        AddTextColumn(ResultsDataGridControl, "Günlük", nameof(SearchResultItem.DailyPrice), 1);
        AddTextColumn(ResultsDataGridControl, "Vites", nameof(SearchResultItem.Transmission), 1);
        AddTextColumn(ResultsDataGridControl, "Yakıt", nameof(SearchResultItem.FuelType), 1);
        AddTextColumn(ResultsDataGridControl, "Şirket", nameof(SearchResultItem.Supplier), 1);
        AddTextColumn(ResultsDataGridControl, "Teslim", nameof(SearchResultItem.PickupInfo), 2);
    }

    private void ConfigureCollectionsGrid()
    {
        CollectionsDataGridControl.AutoGenerateColumns = false;
        CollectionsDataGridControl.Columns.Clear();

        AddTextColumn(CollectionsDataGridControl, "Kayıt Adı", nameof(KoleksiyonListItem.OzelAd), 1.8);
        AddTextColumn(CollectionsDataGridControl, "Alış Yeri", nameof(KoleksiyonListItem.AlisYeri), 1.4);
        AddTextColumn(CollectionsDataGridControl, "Alış", nameof(KoleksiyonListItem.AlisTarihi), 1.1, "dd.MM.yyyy");
        AddTextColumn(CollectionsDataGridControl, "Dönüş", nameof(KoleksiyonListItem.DonusTarihi), 1.1, "dd.MM.yyyy");
        AddTextColumn(CollectionsDataGridControl, "Vites", nameof(KoleksiyonListItem.SecilenVitesFiltresi), 1);
        AddTextColumn(CollectionsDataGridControl, "Yakıt", nameof(KoleksiyonListItem.SecilenYakitFiltresi), 1);
        AddTextColumn(CollectionsDataGridControl, "Araç Sayısı", nameof(KoleksiyonListItem.AracSayisi), 1);
        AddTextColumn(CollectionsDataGridControl, "Tarih", nameof(KoleksiyonListItem.OlusturmaTarihi), 1.4, "dd.MM.yyyy HH:mm");
    }

    private void ConfigurePaymentsGrid()
    {
        PaymentsDataGridControl.AutoGenerateColumns = false;
        PaymentsDataGridControl.Columns.Clear();

        AddTextColumn(PaymentsDataGridControl, "Referans", nameof(OdemeListItem.ReferansNo), 1.6);
        AddTextColumn(PaymentsDataGridControl, "Kayıt", nameof(OdemeListItem.KoleksiyonAdi), 1.8);
        AddTextColumn(PaymentsDataGridControl, "Tutar", nameof(OdemeListItem.Tutar), 1, "N2");
        AddTextColumn(PaymentsDataGridControl, "PB", nameof(OdemeListItem.ParaBirimi), 0.7);
        AddTextColumn(PaymentsDataGridControl, "Durum", nameof(OdemeListItem.Durum), 1);
        AddTextColumn(PaymentsDataGridControl, "Sağlayıcı", nameof(OdemeListItem.Saglayici), 1.1);
        AddTextColumn(PaymentsDataGridControl, "Kart", nameof(OdemeListItem.KartSon4), 0.8);
        AddTextColumn(PaymentsDataGridControl, "Tarih", nameof(OdemeListItem.OdemeTarihi), 1.3, "dd.MM.yyyy HH:mm");
    }

    private static void AddTextColumn(
        DataGrid dataGrid,
        string header,
        string bindingPath,
        double width,
        string? stringFormat = null)
    {
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(bindingPath)
            {
                StringFormat = stringFormat
            },
            Width = new DataGridLength(width, DataGridLengthUnitType.Star)
        });
    }

    private static string NormalizeDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}

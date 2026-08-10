using Avalonia.Controls;
using Avalonia.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private void ShowSearchSection()
    {
        ShowContentSection(
            visiblePanel: SearchPanel,
            activeButton: SearchTabButton,
            showSearchResults: _latestResults is not null && _latestResults.Count > 0);
    }

    private void ShowHistorySection()
    {
        ShowContentSection(HistoryPanel, HistoryTabButton);
    }

    private void ShowPaymentsSection()
    {
        ShowContentSection(PaymentsPanel, PaymentsTabButton);
    }

    private void ShowPaymentCheckoutSection()
    {
        ShowContentSection(PaymentCheckoutPanel, PaymentsTabButton);
    }

    private void ShowBrowserSection()
    {
        ShowContentSection(BrowserSectionPanel, NativeWebViewTestButton, showNativeWebViewTest: true);
    }

    private void ShowContentSection(
        Control visiblePanel,
        Button activeButton,
        bool showSearchResults = false,
        bool showNativeWebViewTest = false)
    {
        SearchPanel.IsVisible = ReferenceEquals(visiblePanel, SearchPanel);
        SearchResultsPanel.IsVisible = showSearchResults;
        HistoryPanel.IsVisible = ReferenceEquals(visiblePanel, HistoryPanel);
        PaymentsPanel.IsVisible = ReferenceEquals(visiblePanel, PaymentsPanel);
        PaymentCheckoutPanel.IsVisible = ReferenceEquals(visiblePanel, PaymentCheckoutPanel);
        BrowserSectionPanel.IsVisible = ReferenceEquals(visiblePanel, BrowserSectionPanel);

        SearchTabButton.Classes.Set("primary", ReferenceEquals(activeButton, SearchTabButton));
        HistoryTabButton.Classes.Set("primary", ReferenceEquals(activeButton, HistoryTabButton));
        PaymentsTabButton.Classes.Set("primary", ReferenceEquals(activeButton, PaymentsTabButton));
        NativeWebViewTestButton.Classes.Set("primary", ReferenceEquals(activeButton, NativeWebViewTestButton));
        NativeWebViewTestButton.IsVisible = showNativeWebViewTest;
    }

    private void SetSearchStatus(string message)
    {
        SearchStatusTextBlock.Text = message;
    }

    private void SetHistoryStatus(string message)
    {
        HistoryStatusTextBlock.Text = message;
    }

    private void SetVehicleStatus(string message)
    {
        VehicleStatusTextBlock.Text = message;
    }

    private void SetCheckoutStatus(string message)
    {
        CheckoutStatusTextBlock.Text = message;
    }

    private void SetPaymentsStatus(string message)
    {
        PaymentsStatusTextBlock.Text = message;
    }

    private void ConfigureResultsGrid()
    {
        ResultsDataGrid.AutoGenerateColumns = false;
        ResultsDataGrid.Columns.Clear();

        AddTextColumn(ResultsDataGrid, "Araç", nameof(SearchResultItem.Title), 2);
        AddTextColumn(ResultsDataGrid, "Detay", nameof(SearchResultItem.Subtitle), 2);
        AddTextColumn(ResultsDataGrid, "Toplam Fiyat", nameof(SearchResultItem.Price), 1);
        AddTextColumn(ResultsDataGrid, "Günlük", nameof(SearchResultItem.DailyPrice), 1);
        AddTextColumn(ResultsDataGrid, "Vites", nameof(SearchResultItem.Transmission), 1);
        AddTextColumn(ResultsDataGrid, "Yakıt", nameof(SearchResultItem.FuelType), 1);
        AddTextColumn(ResultsDataGrid, "Şirket", nameof(SearchResultItem.Supplier), 1);
        AddTextColumn(ResultsDataGrid, "Teslim", nameof(SearchResultItem.PickupInfo), 2);
    }

    private void ConfigureCollectionsGrid()
    {
        CollectionsDataGrid.AutoGenerateColumns = false;
        CollectionsDataGrid.Columns.Clear();

        AddTextColumn(CollectionsDataGrid, "Kayıt Adı", nameof(KoleksiyonListItem.OzelAd), 1.8);
        AddTextColumn(CollectionsDataGrid, "Alış Yeri", nameof(KoleksiyonListItem.AlisYeri), 1.4);
        AddTextColumn(CollectionsDataGrid, "Alış", nameof(KoleksiyonListItem.AlisTarihi), 1.1, "dd.MM.yyyy");
        AddTextColumn(CollectionsDataGrid, "Dönüş", nameof(KoleksiyonListItem.DonusTarihi), 1.1, "dd.MM.yyyy");
        AddTextColumn(CollectionsDataGrid, "Vites", nameof(KoleksiyonListItem.SecilenVitesFiltresi), 1);
        AddTextColumn(CollectionsDataGrid, "Yakıt", nameof(KoleksiyonListItem.SecilenYakitFiltresi), 1);
        AddTextColumn(CollectionsDataGrid, "Araç Sayısı", nameof(KoleksiyonListItem.AracSayisi), 1);
        AddTextColumn(CollectionsDataGrid, "Tarih", nameof(KoleksiyonListItem.OlusturmaTarihi), 1.4, "dd.MM.yyyy HH:mm");
    }

    private void ConfigurePaymentsGrid()
    {
        PaymentsDataGrid.AutoGenerateColumns = false;
        PaymentsDataGrid.Columns.Clear();

        AddTextColumn(PaymentsDataGrid, "Referans", nameof(OdemeListItem.ReferansNo), 1.6);
        AddTextColumn(PaymentsDataGrid, "Kayıt", nameof(OdemeListItem.KoleksiyonAdi), 1.8);
        AddTextColumn(PaymentsDataGrid, "Tutar", nameof(OdemeListItem.Tutar), 1, "N2");
        AddTextColumn(PaymentsDataGrid, "PB", nameof(OdemeListItem.ParaBirimi), 0.7);
        AddTextColumn(PaymentsDataGrid, "Durum", nameof(OdemeListItem.Durum), 1);
        AddTextColumn(PaymentsDataGrid, "Sağlayıcı", nameof(OdemeListItem.Saglayici), 1.1);
        AddTextColumn(PaymentsDataGrid, "Kart", nameof(OdemeListItem.KartSon4), 0.8);
        AddTextColumn(PaymentsDataGrid, "Tarih", nameof(OdemeListItem.OdemeTarihi), 1.3, "dd.MM.yyyy HH:mm");
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

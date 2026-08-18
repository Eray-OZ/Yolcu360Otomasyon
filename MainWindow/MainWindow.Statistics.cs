using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    // Extra - Statistics START
    private async void StatisticsTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating)
            return;

        ShowStatisticsSection();
        await LoadStatisticsAsync();
    }

    private void ShowStatisticsSection()
    {
        SearchPanel.IsVisible = false;
        FlightPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        PaymentsPanel.IsVisible = false;
        StatisticsPanel.IsVisible = true;
        PaymentCheckoutPanel.IsVisible = false;
        // Extra - Flight Car Recommendation START
        FlightCarRecommendationPanel.IsVisible = false;
        // Extra - Flight Car Recommendation END
        BrowserSectionPanel.IsVisible = false;

        SearchTabButton.Classes.Set("primary", false);
        FlightTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", false);
        StatisticsTabButton.Classes.Set("primary", true);
    }

    private void ConfigureStatisticsGrids()
    {
        ConfigureStatisticsGrid(StatisticsVehiclesDataGrid, "Araç Modeli", "Kayıt / İşlem");
        ConfigureStatisticsGrid(StatisticsCitiesDataGrid, "Lokasyon / Şehir", "Kayıt Sayısı");
        ConfigureStatisticsGrid(StatisticsSuppliersDataGrid, "Tedarikçi Firma", "Araç Sayısı");
        ConfigureStatisticsGrid(StatisticsTransmissionDataGrid, "Vites Tipi", "Araç Sayısı");
    }

    private static void ConfigureStatisticsGrid(DataGrid grid, string nameHeader, string countHeader)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = nameHeader,
            Binding = new Binding(nameof(IstatistikSatir.Ad)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = countHeader,
            Binding = new Binding(nameof(IstatistikSatir.Sayi)),
            Width = new DataGridLength(110)
        });
    }

    private async Task LoadStatisticsAsync()
    {
        if (_activeUser is null)
            return;

        try
        {
            var summary = await _statisticsService.GetSummaryAsync(_activeUser.Id);

            // 1. Genel Metrikler
            StatisticsCollectionsTextBlock.Text = summary.KoleksiyonSayisi.ToString();
            StatisticsVehiclesTextBlock.Text = summary.AracSayisi.ToString();
            StatisticsTotalPaymentTextBlock.Text = $"{summary.ToplamOdeme:N2} TL";
            StatisticsPaymentsTextBlock.Text = summary.OdemeSayisi.ToString();
            StatisticsPaymentsBreakdownTextBlock.Text = $"({summary.AracOdemeSayisi} Araç / {summary.UcakOdemeSayisi} Uçak)";

            // 2. Fiyat & Harcama Analizi
            StatisticsHighestPaymentTextBlock.Text = $"{summary.EnYuksekKiralama:N2} TL";
            StatisticsLowestPaymentTextBlock.Text = $"{summary.EnDusukKiralama:N2} TL";
            StatisticsAvgPaymentTextBlock.Text = $"{summary.OrtalamaOdeme:N2} TL";
            StatisticsAvgVehiclePriceTextBlock.Text = $"{summary.OrtalamaAracFiyati:N2} TL";
            StatisticsVehiclePriceRangeTextBlock.Text = $"Min: {summary.EnDusukAracFiyati:N0} TL - Max: {summary.EnYuksekAracFiyati:N0} TL";

            // 3. Tablo ve Listeler
            StatisticsVehiclesDataGrid.ItemsSource = summary.EnCokKiralananAraclar;
            StatisticsCitiesDataGrid.ItemsSource = summary.EnCokKiralananSehirler;
            StatisticsSuppliersDataGrid.ItemsSource = summary.EnCokTedarikciler;
            StatisticsTransmissionDataGrid.ItemsSource = summary.VitesDagitimi;
        }
        catch
        {
            // İstatistik panelinde durum mesajı gösterilmez.
        }
    }

    // Extra - Statistics END
}

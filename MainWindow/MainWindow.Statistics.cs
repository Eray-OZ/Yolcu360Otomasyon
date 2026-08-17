using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
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
        BrowserSectionPanel.IsVisible = false;

        SearchTabButton.Classes.Set("primary", false);
        FlightTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", false);
        StatisticsTabButton.Classes.Set("primary", true);
    }

    private async Task LoadStatisticsAsync()
    {
        if (_activeUser is null)
            return;

        StatisticsStatusTextBlock.Text = "İstatistikler yükleniyor...";

        try
        {
            var summary = await _statisticsService.GetSummaryAsync(_activeUser.Id);

            StatisticsTotalSearchesTextBlock.Text = summary.ToplamArama.ToString(CultureInfo.InvariantCulture);
            StatisticsSuccessfulSearchesTextBlock.Text = summary.BasariliArama.ToString(CultureInfo.InvariantCulture);
            StatisticsTotalResultsTextBlock.Text = summary.ToplamSonuc.ToString(CultureInfo.InvariantCulture);
            StatisticsAverageDurationTextBlock.Text = $"{summary.OrtalamaSureSaniye:N1} sn";
            StatisticsCollectionsTextBlock.Text = summary.KoleksiyonSayisi.ToString(CultureInfo.InvariantCulture);
            StatisticsVehiclesTextBlock.Text = summary.AracSayisi.ToString(CultureInfo.InvariantCulture);
            StatisticsPaymentsTextBlock.Text = summary.OdemeSayisi.ToString(CultureInfo.InvariantCulture);
            StatisticsTotalPaymentTextBlock.Text = $"{summary.ToplamOdeme:N2} TL";
            StatisticsStatusTextBlock.Text = $"Başarısız arama: {summary.BasarisizArama}";
        }
        catch (Exception ex)
        {
            StatisticsStatusTextBlock.Text = $"İstatistikler yüklenemedi: {ex.Message}";
        }
    }

    private async Task RecordSearchStatisticSafelyAsync(
        Stopwatch? timer,
        string searchType,
        bool success,
        int resultCount)
    {
        if (_activeUser is null || timer is null)
            return;

        try
        {
            await _statisticsService.RecordSearchAsync(
                _activeUser.Id,
                searchType,
                success,
                resultCount,
                timer.Elapsed);
        }
        catch
        {
            // Statistics must never break a search flow.
        }
    }
    // Extra - Statistics END
}

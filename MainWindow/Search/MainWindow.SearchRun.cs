using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        SearchButtonControl.IsEnabled = false;
        SetSearchStatus("Arama hazırlanıyor...");

        try
        {
            if (!TryBuildSearchFilter(out var filter))
                return;

            if (_activeUser is null)
            {
                SetSearchStatus("Önce giriş yapılmalı.");
                return;
            }

            var results = await RunEmbeddedSearchAsync(filter);
            await DisplaySearchResultsAsync(results);
            await Task.Delay(800);
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Arama hatası: {ex.Message}");
        }
        finally
        {
            SearchButtonControl.IsEnabled = true;
            ShowSearchSection();
        }
    }

    private bool TryBuildSearchFilter(out SearchFilter filter)
    {
        filter = new SearchFilter();

        if (!DateTime.TryParseExact(
                PickupDateTextBoxControl.Text?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var pickupDate)
            || !DateTime.TryParseExact(
                ReturnDateTextBoxControl.Text?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var returnDate))
        {
            SetSearchStatus("Tarih formatı gecersiz. Ornek: 2026-08-10");
            return false;
        }

        filter = new SearchFilter
        {
            PickupLocation = PickupLocationTextBoxControl.Text?.Trim() ?? string.Empty,
            PickupDate = pickupDate.Date,
            ReturnDate = returnDate.Date,
            PickupTime = PickupTimeTextBoxControl.Text?.Trim() ?? "10:00",
            ReturnTime = ReturnTimeTextBoxControl.Text?.Trim() ?? "18:00",
            TransmissionType = GetComboBoxTag(TransmissionComboBoxControl),
            FuelType = GetComboBoxTag(FuelComboBoxControl)
        };
        _latestSearchFilter = filter;

        if (!string.IsNullOrWhiteSpace(filter.PickupLocation))
            return true;

        SetSearchStatus("Alış yeri boş olamaz.");
        return false;
    }

    private async Task<List<SearchResultItem>> RunEmbeddedSearchAsync(SearchFilter filter)
    {
        ShowBrowserSection();
        SetSearchStatus("Gömülü tarayıcı arama formu hazırlanıyor...");

        var baService = GetBAService();
        if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            await baService.RestoreSessionAsync(_activeUser.SessionStatePath);

        SetSearchStatus("Araçlar aranıyor...");

        await baService.OpenYolcu360HomeAsync();
        await baService.FillPickupLocationAsync(filter.PickupLocation);
        await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
        await baService.SelectTimeAsync(0, filter.PickupTime);
        await baService.SelectTimeAsync(1, filter.ReturnTime);
        await baService.ClickSearchButtonAsync();
        await baService.WaitForSearchResultsAsync();
        await baService.ApplyResultFiltersAsync(filter);

        SetSearchStatus("Arama sonuçları okunuyor...");
        return await baService.ReadSearchResultsAsync();
    }

    private async Task DisplaySearchResultsAsync(List<SearchResultItem> results)
    {
        _latestResults = results;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ResultsDataGridControl.ItemsSource = null;
            ResultsDataGridControl.ItemsSource = _latestResults;
            SearchResultsPanelControl.IsVisible = _latestResults.Count > 0;
        });

        SetSearchStatus(_latestResults.Count == 0
            ? "Arama tamamlandı, sonuç bulunamadı."
            : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}");
    }

    private static string GetComboBoxTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }
}

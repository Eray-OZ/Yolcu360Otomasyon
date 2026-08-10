using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private async void SearchButton_Click(object? sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
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
            SearchButton.IsEnabled = true;
            ShowSearchSection();
        }
    }

    private bool TryBuildSearchFilter(out SearchFilter filter)
    {
        filter = new SearchFilter();

        if (!DateTime.TryParseExact(
                PickupDateTextBox.Text?.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var pickupDate)
            || !DateTime.TryParseExact(
                ReturnDateTextBox.Text?.Trim(),
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
            PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
            PickupDate = pickupDate.Date,
            ReturnDate = returnDate.Date,
            PickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00",
            ReturnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00",
            TransmissionType = GetComboBoxTag(TransmissionComboBox),
            FuelType = GetComboBoxTag(FuelComboBox)
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

        var embeddedBrowser = GetEmbeddedBrowserAutomationService();
        if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            await embeddedBrowser.RestoreSessionAsync(_activeUser.SessionStatePath);

        SetSearchStatus("Araçlar aranıyor...");

        await embeddedBrowser.OpenYolcu360HomeAsync();
        await embeddedBrowser.FillPickupLocationAsync(filter.PickupLocation);
        await embeddedBrowser.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
        await embeddedBrowser.SelectTimeAsync(0, filter.PickupTime);
        await embeddedBrowser.SelectTimeAsync(1, filter.ReturnTime);
        await embeddedBrowser.ClickSearchButtonAsync();
        await embeddedBrowser.WaitForSearchResultsAsync();
        await embeddedBrowser.ApplyResultFiltersAsync(filter);

        SetSearchStatus("Arama sonuçları okunuyor...");
        return await embeddedBrowser.ReadSearchResultsAsync();
    }

    private async Task DisplaySearchResultsAsync(List<SearchResultItem> results)
    {
        _latestResults = results;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ResultsDataGrid.ItemsSource = null;
            ResultsDataGrid.ItemsSource = _latestResults;
            SearchResultsPanel.IsVisible = _latestResults.Count > 0;
        });

        SetSearchStatus(_latestResults.Count == 0
            ? "Arama tamamlandı, sonuç bulunamadı."
            : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}");
    }

    private async void SaveResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            SetSearchStatus("Önce giriş yapılmalı.");
            return;
        }

        if (_activeUser.Id <= 0)
        {
            var latestUser = await _databaseService.GetUserByEmailAsync(_activeUser.Email);
            if (latestUser is null)
            {
                SetSearchStatus("Aktif kullanıcı veritabanında bulunamadı.");
                return;
            }

            _activeUser = latestUser;
        }

        if (_latestResults.Count == 0)
        {
            SetSearchStatus("Kaydedilecek sonuç yok.");
            return;
        }

        if (_latestSearchFilter is null)
        {
            SetSearchStatus("Önce geçerli bir arama yapılmalı.");
            return;
        }

        var ozelAd = CollectionNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ozelAd))
        {
            SetSearchStatus("Özel kayıt adı girin.");
            return;
        }

        SaveResultsButton.IsEnabled = false;
        try
        {
            var collectionId = await _databaseService.SaveCollectionAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
            CollectionNameTextBox.Text = string.Empty;
            SetSearchStatus($"{_latestResults.Count} sonuç \"{ozelAd}\" adıyla kaydedildi.");
            await LoadHistoryAsync();
            ShowHistorySection();

            var collections = (CollectionsDataGrid.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList() ?? new List<KoleksiyonListItem>();
            var savedCollection = collections.FirstOrDefault(item => item.Id == collectionId);
            if (savedCollection is not null)
                CollectionsDataGrid.SelectedItem = savedCollection;
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Kaydetme hatası: {ex.Message}");
        }
        finally
        {
            SaveResultsButton.IsEnabled = true;
        }
    }

    private void SearchTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowSearchSection();
    }

    private static string GetComboBoxTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }

    private async void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        try
        {
            ShowBrowserSection();
            SetSearchStatus("Gömülü tarayıcı açılıyor...");

            var embeddedBrowser = GetEmbeddedBrowserAutomationService();
            await embeddedBrowser.OpenYolcu360HomeAsync();

            var title = await embeddedBrowser.GetTitleAsync();
            SetSearchStatus($"Gömülü tarayıcı hazır. Title: {title}");
        }
        catch (Exception ex)
        {
            SetSearchStatus($"Gömülü tarayıcı hatası: {ex.Message}");
        }
    }

    private EmbeddedBrowserAutomationService GetEmbeddedBrowserAutomationService()
    {
        if (_embeddedBrowserAutomationService is not null)
            return _embeddedBrowserAutomationService;

        _embeddedBrowserAutomationService = new EmbeddedBrowserAutomationService(EmbeddedBrowser);
        _embeddedBrowserAutomationService.ProgressChanged += message =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SetSearchStatus(message);
            });
        };

        return _embeddedBrowserAutomationService;
    }

}

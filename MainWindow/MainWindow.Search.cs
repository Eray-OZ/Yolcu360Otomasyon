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
        SearchStatusTextBlock.Text = "Arama hazırlanıyor...";

        try
        {
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
                SearchStatusTextBlock.Text = "Tarih formatı gecersiz. Ornek: 2026-08-10";
                return;
            }

            var pickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00";
            var returnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00";

            var filter = new SearchFilter
            {
                PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
                PickupDate = pickupDate.Date,
                ReturnDate = returnDate.Date,
                PickupTime = pickupTime,
                ReturnTime = returnTime,
                TransmissionType = GetComboBoxTag(TransmissionComboBox),
                FuelType = GetComboBoxTag(FuelComboBox)
            };
            _latestSearchFilter = filter;

            if (string.IsNullOrWhiteSpace(filter.PickupLocation))
            {
                SearchStatusTextBlock.Text = "Alış yeri boş olamaz.";
                return;
            }

            if (_activeUser is null)
            {
                SearchStatusTextBlock.Text = "Önce giriş yapılmalı.";
                return;
            }

            ShowBrowserSection();
            SearchStatusTextBlock.Text = "Gömülü tarayıcı arama formu hazırlanıyor...";

            var embeddedBrowser = CreateEmbeddedBrowserAutomationService();
            if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            {
                await embeddedBrowser.RestoreSessionAsync(_activeUser.SessionStatePath);
            }

            await embeddedBrowser.OpenYolcu360HomeAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış yeri seçiyor...";
            await embeddedBrowser.FillPickupLocationAsync(filter.PickupLocation);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı tarihleri seçiyor...";
            await embeddedBrowser.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış saatini seçiyor...";
            await embeddedBrowser.SelectTimeAsync(0, filter.PickupTime);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı bırakış saatini seçiyor...";
            await embeddedBrowser.SelectTimeAsync(1, filter.ReturnTime);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı araç ara butonuna tıklıyor...";
            await embeddedBrowser.ClickSearchButtonAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı sonuçları bekliyor...";
            await embeddedBrowser.WaitForSearchResultsAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı filtreleri uyguluyor...";
            await embeddedBrowser.ApplyResultFiltersAsync(filter);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı sonuçları okuyor...";
            var results = await embeddedBrowser.ReadSearchResultsAsync();
            _latestResults = results;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _latestResults;
                SearchResultsPanel.IsVisible = _latestResults.Count > 0;
            });

            SearchStatusTextBlock.Text = _latestResults.Count == 0
                ? "Arama tamamlandı, sonuç bulunamadı."
                : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}";

            await Task.Delay(800);
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Arama hatası: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
            ShowSearchSection();
        }
    }

    private async void SaveResultsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            SearchStatusTextBlock.Text = "Önce giriş yapılmalı.";
            return;
        }

        if (_activeUser.Id <= 0)
        {
            var latestUser = await _databaseService.GetUserByEmailAsync(_activeUser.Email);
            if (latestUser is null)
            {
                SearchStatusTextBlock.Text = "Aktif kullanıcı veritabanında bulunamadı.";
                return;
            }

            _activeUser = latestUser;
        }

        if (_latestResults.Count == 0)
        {
            SearchStatusTextBlock.Text = "Kaydedilecek sonuç yok.";
            return;
        }

        if (_latestSearchFilter is null)
        {
            SearchStatusTextBlock.Text = "Önce geçerli bir arama yapılmalı.";
            return;
        }

        var ozelAd = CollectionNameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ozelAd))
        {
            SearchStatusTextBlock.Text = "Özel kayıt adı girin.";
            return;
        }

        SaveResultsButton.IsEnabled = false;
        try
        {
            var collectionId = await _databaseService.SaveCollectionAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
            CollectionNameTextBox.Text = string.Empty;
            SearchStatusTextBlock.Text = $"{_latestResults.Count} sonuç \"{ozelAd}\" adıyla kaydedildi.";
            await LoadHistoryAsync();
            ShowHistorySection();

            var collections = (CollectionsDataGrid.ItemsSource as IEnumerable<KoleksiyonListItem>)?.ToList() ?? new List<KoleksiyonListItem>();
            var savedCollection = collections.FirstOrDefault(item => item.Id == collectionId);
            if (savedCollection is not null)
                CollectionsDataGrid.SelectedItem = savedCollection;
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Kaydetme hatası: {ex.Message}";
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
            SearchStatusTextBlock.Text = "Gömülü tarayıcı açılıyor...";

            var embeddedBrowser = CreateEmbeddedBrowserAutomationService();
            await embeddedBrowser.OpenYolcu360HomeAsync();

            var title = await embeddedBrowser.GetTitleAsync();
            SearchStatusTextBlock.Text = $"Gömülü tarayıcı hazır. Title: {title}";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Gömülü tarayıcı hatası: {ex.Message}";
        }
    }

    private EmbeddedBrowserAutomationService CreateEmbeddedBrowserAutomationService()
    {
        var embeddedBrowser = new EmbeddedBrowserAutomationService(EmbeddedBrowser);
        embeddedBrowser.ProgressChanged += message =>
        {
            Console.WriteLine($"[EmbeddedWebViewUI] {message}");
            Dispatcher.UIThread.Post(() =>
            {
                SearchStatusTextBlock.Text = message;
            });
        };

        return embeddedBrowser;
    }
}

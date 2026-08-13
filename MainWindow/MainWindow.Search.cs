using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    // Extra - Location Suggestion START
    private async void PickupLocationTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressPickupLocationSuggestionLookup)
            return;

        var input = PickupLocationTextBox.Text?.Trim() ?? string.Empty;
        var requestVersion = ++_pickupLocationSuggestionRequestVersion;
        var previousCts = _pickupLocationSuggestionCts;
        CancelPickupLocationSuggestionRequest(previousCts);

        if (input.Length < 2)
        {
            HidePickupLocationSuggestions();
            return;
        }

        var cts = new CancellationTokenSource();
        _pickupLocationSuggestionCts = cts;

        try
        {
            var suggestions = await _locationSuggestionService.GetSuggestionsAsync(input, cts.Token);
            if (cts.IsCancellationRequested || requestVersion != _pickupLocationSuggestionRequestVersion)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested || requestVersion != _pickupLocationSuggestionRequestVersion)
                    return;

                PickupLocationSuggestionsListBox.ItemsSource = suggestions;
                PickupLocationSuggestionsPanel.IsVisible = suggestions.Count > 0;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (requestVersion != _pickupLocationSuggestionRequestVersion)
                    return;

                PickupLocationSuggestionsListBox.ItemsSource = null;
                PickupLocationSuggestionsPanel.IsVisible = false;
                SearchStatusTextBlock.Text = $"Alış yeri önerileri alınamadı: {ex.Message}";
            });
        }
    }

    private void PickupLocationSuggestionsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PickupLocationSuggestionsListBox.SelectedItem is not LocationSuggestionItem suggestion)
            return;

        _suppressPickupLocationSuggestionLookup = true;
        PickupLocationTextBox.Text = suggestion.MainText;
        _suppressPickupLocationSuggestionLookup = false;

        HidePickupLocationSuggestions();
    }

    private void HidePickupLocationSuggestions()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(HidePickupLocationSuggestions);
            return;
        }

        PickupLocationSuggestionsListBox.SelectedItem = null;
        PickupLocationSuggestionsListBox.ItemsSource = null;
        PickupLocationSuggestionsPanel.IsVisible = false;
    }

    private static void CancelPickupLocationSuggestionRequest(CancellationTokenSource? cts)
    {
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
    // Extra - Location Suggestion END

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

            var baService = CreateBAService();
            if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            {
                await baService.RestoreSessionAsync(_activeUser.SessionStatePath);
            }

            SearchStatusTextBlock.Text = "Araçlar aranıyor...";

            await baService.OpenYolcu360HomeAsync();
            await baService.FillPickupLocationAsync(filter.PickupLocation);
            await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
            await baService.SelectTimeAsync(0, filter.PickupTime);
            await baService.SelectTimeAsync(1, filter.ReturnTime);
            await baService.ClickSearchButtonAsync();
            await baService.WaitForSearchResultsAsync();
            await baService.ApplyResultFiltersAsync(filter);

            SearchStatusTextBlock.Text = "Arama sonuçları okunuyor...";
            var results = await baService.ReadSearchResultsAsync();
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
            // Extra - Dynamic Collections START
            var collectionId = await _dynamicCollectionService.SaveSnapshotAsync(_activeUser.Id, ozelAd, _latestSearchFilter, _latestResults);
            // Extra - Dynamic Collections END
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

    private BAService CreateBAService()
    {
        var baService = new BAService(EmbeddedBrowser);
        baService.ProgressChanged += message =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                SearchStatusTextBlock.Text = message;
            });
        };

        return baService;
    }
}

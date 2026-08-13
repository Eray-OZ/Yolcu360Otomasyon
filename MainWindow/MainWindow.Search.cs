using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private void PickupDateBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        PickupDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void ReturnDateBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ReturnDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void SearchDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSearchDateTexts();
    }

    private void UpdateSearchDateTexts()
    {
        PickupDateTextBlock.Text = FormatSearchDate(PickupDatePicker.SelectedDate);
        ReturnDateTextBlock.Text = FormatSearchDate(ReturnDatePicker.SelectedDate);
    }

    private static string FormatSearchDate(DateTime? date)
    {
        return date?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
    }


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

        _pickupLocationSuggestionRequestVersion++;
        CancelPickupLocationSuggestionRequest(_pickupLocationSuggestionCts);
        _pickupLocationSuggestionCts = null;
        _suppressPickupLocationSuggestionLookup = true;
        PickupLocationTextBox.Text = suggestion.MainText;
        HidePickupLocationSuggestions();
        Dispatcher.UIThread.Post(() => _suppressPickupLocationSuggestionLookup = false);
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
            var pickupDate = PickupDatePicker.SelectedDate?.Date;
            var returnDate = ReturnDatePicker.SelectedDate?.Date;

            if (pickupDate is null || returnDate is null)
            {
                SearchStatusTextBlock.Text = "Alış ve dönüş tarihi seçilmelidir.";
                return;
            }

            var pickupTime = GetComboBoxTag(PickupTimeComboBox);
            var returnTime = GetComboBoxTag(ReturnTimeComboBox);

            if (string.IsNullOrWhiteSpace(pickupTime) || string.IsNullOrWhiteSpace(returnTime))
            {
                SearchStatusTextBlock.Text = "Alış ve dönüş saati seçilmelidir.";
                return;
            }

            var filter = new SearchFilter
            {
                PickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty,
                PickupDate = pickupDate.Value.Date,
                ReturnDate = returnDate.Value.Date,
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

            ResultsDataGrid.ItemsSource = null;
            _searchResultsPlaceholderText = "Arama yapılıyor, bekleyiniz...";
            SetSearchResultsPlaceholder(_searchResultsPlaceholderText);
            var showBrowserDuringSearch = ShowBrowserDuringSearchCheckBox.IsChecked == true;
            if (showBrowserDuringSearch)
                ShowBrowserSection();
            else
                KeepBrowserAliveBehindSearch();

            SearchStatusTextBlock.Text = showBrowserDuringSearch
                ? "Gömülü tarayıcı arama formu hazırlanıyor..."
                : "Arama arka planda yapılıyor...";

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

            SearchStatusTextBlock.Text = "Arama sonuçları okunuyor...";
            var results = await baService.ReadSearchResultsAsync();

            if (results.Count > 0)
            {
                await baService.ApplyResultFiltersAsync(filter);
                SearchStatusTextBlock.Text = "Filtrelenmiş arama sonuçları okunuyor...";
                results = await baService.ReadSearchResultsAsync();
            }

            _latestResults = results;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _latestResults;
                _searchResultsPlaceholderText = _latestResults.Count == 0
                    ? "Sonuç bulunamadı."
                    : "Sonuçları görmek için arama yapın.";
                SetSearchResultsPlaceholder(_latestResults.Count == 0 ? _searchResultsPlaceholderText : null);
            });

            SearchStatusTextBlock.Text = _latestResults.Count == 0
                ? "Arama tamamlandı, sonuç bulunamadı."
                : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Arama hatası: {ex.Message}";
            _searchResultsPlaceholderText = "Arama sırasında hata oluştu.";
            SetSearchResultsPlaceholder(_searchResultsPlaceholderText);
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

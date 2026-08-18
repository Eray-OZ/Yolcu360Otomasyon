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
    private void FlightDepartureDateBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        FlightDepartureDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void FlightReturnDateBox_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        FlightReturnDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }

    private void FlightRoundTripCheckBox_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        var isRoundTrip = FlightRoundTripCheckBox.IsChecked == true;
        FlightReturnDateContainer.IsVisible = isRoundTrip;

        if (isRoundTrip)
        {
            if (FlightReturnDatePicker.SelectedDate is null)
            {
                var depDate = FlightDepartureDatePicker.SelectedDate ?? DateTime.Today.AddDays(7);
                FlightReturnDatePicker.SelectedDate = depDate.AddDays(3);
            }
        }
        else
        {
            FlightReturnDatePicker.SelectedDate = null;
        }

        UpdateFlightDateTexts();
    }

    private void FlightDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateFlightDateTexts();
    }

    private void UpdateFlightDateTexts()
    {
        FlightDepartureDateTextBlock.Text = FormatSearchDate(FlightDepartureDatePicker.SelectedDate);
        FlightReturnDateTextBlock.Text = FlightReturnDatePicker.SelectedDate is null
            ? "Tek yön"
            : FormatSearchDate(FlightReturnDatePicker.SelectedDate);
    }

    private async void FlightFromTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFlightFromSuggestionLookup)
            return;

        _selectedFlightFromSuggestion = null;
        await LoadFlightLocationSuggestionsAsync(
            FlightFromTextBox,
            FlightFromSuggestionsPanel,
            FlightFromSuggestionsListBox,
            "Nereden",
            isFrom: true);
    }

    private async void FlightToTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressFlightToSuggestionLookup)
            return;

        _selectedFlightToSuggestion = null;
        await LoadFlightLocationSuggestionsAsync(
            FlightToTextBox,
            FlightToSuggestionsPanel,
            FlightToSuggestionsListBox,
            "Nereye",
            isFrom: false);
    }

    private void FlightFromSuggestionsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FlightFromSuggestionsListBox.SelectedItem is not LocationSuggestionItem suggestion)
            return;

        _flightFromSuggestionRequestVersion++;
        CancelPickupLocationSuggestionRequest(_flightFromSuggestionCts);
        _flightFromSuggestionCts = null;
        _selectedFlightFromSuggestion = suggestion;
        _suppressFlightFromSuggestionLookup = true;
        FlightFromTextBox.Text = GetFlightSuggestionText(suggestion);

        HideFlightLocationSuggestions(FlightFromSuggestionsPanel, FlightFromSuggestionsListBox);
        Dispatcher.UIThread.Post(() => _suppressFlightFromSuggestionLookup = false);
    }

    private void FlightToSuggestionsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FlightToSuggestionsListBox.SelectedItem is not LocationSuggestionItem suggestion)
            return;

        _flightToSuggestionRequestVersion++;
        CancelPickupLocationSuggestionRequest(_flightToSuggestionCts);
        _flightToSuggestionCts = null;
        _selectedFlightToSuggestion = suggestion;
        _suppressFlightToSuggestionLookup = true;
        FlightToTextBox.Text = GetFlightSuggestionText(suggestion);

        HideFlightLocationSuggestions(FlightToSuggestionsPanel, FlightToSuggestionsListBox);
        Dispatcher.UIThread.Post(() => _suppressFlightToSuggestionLookup = false);
    }

    private async Task LoadFlightLocationSuggestionsAsync(
        TextBox textBox,
        Border panel,
        ListBox listBox,
        string fieldName,
        bool isFrom)
    {
        var input = textBox.Text?.Trim() ?? string.Empty;
        var requestVersion = isFrom
            ? ++_flightFromSuggestionRequestVersion
            : ++_flightToSuggestionRequestVersion;

        var previousCts = isFrom ? _flightFromSuggestionCts : _flightToSuggestionCts;
        CancelPickupLocationSuggestionRequest(previousCts);

        if (input.Length < 2)
        {
            HideFlightLocationSuggestions(panel, listBox);
            return;
        }

        var cts = new CancellationTokenSource();
        if (isFrom)
            _flightFromSuggestionCts = cts;
        else
            _flightToSuggestionCts = cts;

        try
        {
            var suggestions = await _flightLocationSuggestionService.GetSuggestionsAsync(input, cts.Token);
            if (cts.IsCancellationRequested || !IsFlightSuggestionRequestCurrent(isFrom, requestVersion))
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cts.IsCancellationRequested || !IsFlightSuggestionRequestCurrent(isFrom, requestVersion))
                    return;

                listBox.ItemsSource = suggestions;
                panel.IsVisible = suggestions.Count > 0;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsFlightSuggestionRequestCurrent(isFrom, requestVersion))
                    return;

                listBox.ItemsSource = null;
                panel.IsVisible = false;
                FlightStatusTextBlock.Text = $"{fieldName} önerileri alınamadı: {ex.Message}";
            });
        }
    }

    private static string GetFlightSuggestionText(LocationSuggestionItem suggestion)
    {
        return suggestion.MainText;
    }

    private bool IsFlightSuggestionRequestCurrent(bool isFrom, int requestVersion)
    {
        return isFrom
            ? requestVersion == _flightFromSuggestionRequestVersion
            : requestVersion == _flightToSuggestionRequestVersion;
    }

    private static void HideFlightLocationSuggestions(Border panel, ListBox listBox)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => HideFlightLocationSuggestions(panel, listBox));
            return;
        }

        listBox.SelectedItem = null;
        listBox.ItemsSource = null;
        panel.IsVisible = false;
    }

    private void FlightTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowFlightSection();
    }

    private async void FlightSearchButton_Click(object? sender, RoutedEventArgs e)
    {
        FlightSearchButton.IsEnabled = false;
        FlightStatusTextBlock.Text = "Uçuş araması hazırlanıyor...";
        FlightResultsDataGrid.ItemsSource = null;
        SetFlightResultsPlaceholder("Uçuşlar aranıyor, bekleyiniz...");
        try
        {
            var departureDate = FlightDepartureDatePicker.SelectedDate?.Date;
            if (departureDate is null)
            {
                FlightStatusTextBlock.Text = "Gidiş tarihi seçilmeli.";
                return;
            }

            var returnDate = FlightReturnDatePicker.SelectedDate?.Date;
            if (FlightRoundTripCheckBox.IsChecked == true && returnDate is null)
            {
                FlightStatusTextBlock.Text = "Gidiş-dönüş araması için dönüş tarihi seçilmeli.";
                return;
            }

            var filter = new FlightSearchFilter
            {
                FromLocation = FlightFromTextBox.Text?.Trim() ?? string.Empty,
                ToLocation = FlightToTextBox.Text?.Trim() ?? string.Empty,
                DepartureDate = departureDate.Value.Date,
                ReturnDate = returnDate,
                IsRoundTrip = FlightRoundTripCheckBox.IsChecked == true || returnDate is not null,
                OnlyNonStop = FlightOnlyNonStopCheckBox.IsChecked == true
            };

            if (string.IsNullOrWhiteSpace(filter.FromLocation) || string.IsNullOrWhiteSpace(filter.ToLocation))
            {
                FlightStatusTextBlock.Text = "Nereden ve nereye alanları boş olamaz.";
                return;
            }

            var fromText = FlightFromTextBox.Text?.Trim() ?? string.Empty;
            var toText = FlightToTextBox.Text?.Trim() ?? string.Empty;

            LocationSuggestionItem? fromSuggestion = null;
            if (_selectedFlightFromSuggestion is not null &&
                (string.Equals(_selectedFlightFromSuggestion.MainText, fromText, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(_selectedFlightFromSuggestion.Description, fromText, StringComparison.OrdinalIgnoreCase)))
            {
                fromSuggestion = _selectedFlightFromSuggestion;
            }
            else
            {
                fromSuggestion = await _flightLocationSuggestionService.ResolveBestSuggestionAsync(fromText);
            }

            LocationSuggestionItem? toSuggestion = null;
            if (_selectedFlightToSuggestion is not null &&
                (string.Equals(_selectedFlightToSuggestion.MainText, toText, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(_selectedFlightToSuggestion.Description, toText, StringComparison.OrdinalIgnoreCase)))
            {
                toSuggestion = _selectedFlightToSuggestion;
            }
            else
            {
                toSuggestion = await _flightLocationSuggestionService.ResolveBestSuggestionAsync(toText);
            }

            if (fromSuggestion is not null)
            {
                filter.FromLocation = fromSuggestion.MainText;
                filter.FromPlaceCode = fromSuggestion.PlaceCode;
                filter.FromPlaceId = fromSuggestion.PlaceId;
                filter.FromPlaceType = fromSuggestion.Type;
            }

            if (toSuggestion is not null)
            {
                filter.ToLocation = toSuggestion.MainText;
                filter.ToPlaceCode = toSuggestion.PlaceCode;
                filter.ToPlaceId = toSuggestion.PlaceId;
                filter.ToPlaceType = toSuggestion.Type;
            }

            if (_activeUser is null)
            {
                FlightStatusTextBlock.Text = "Önce giriş yapılmalı.";
                return;
            }

            ShowBrowserSection();
            FlightStatusTextBlock.Text = "Gömülü tarayıcı uçuş araması için hazırlanıyor...";

            var baService = CreateBAService(attachProgress: false);
            if (!string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
                await baService.RestoreSessionAsync(_activeUser.SessionStatePath);

            await baService.SearchFlightTicketsAsync(filter);

            FlightStatusTextBlock.Text = "Uçuş sonuçları okunuyor...";
            _latestFlightResults = await baService.ReadFlightResultsAsync();
            foreach (var flightResult in _latestFlightResults)
            {
                if (string.IsNullOrWhiteSpace(flightResult.FromLocation))
                    flightResult.FromLocation = filter.FromLocation;

                if (string.IsNullOrWhiteSpace(flightResult.ToLocation))
                    flightResult.ToLocation = filter.ToLocation;

                if (string.IsNullOrWhiteSpace(flightResult.Route))
                    flightResult.Route = $"{flightResult.FromLocation} → {flightResult.ToLocation}";
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FlightResultsDataGrid.ItemsSource = null;
                FlightResultsDataGrid.ItemsSource = _latestFlightResults;
                SetFlightResultsPlaceholder(_latestFlightResults.Count == 0 ? "Uçuş sonucu bulunamadı." : null);
            });

            FlightStatusTextBlock.Text = _latestFlightResults.Count == 0
                ? "Uçuş araması tamamlandı, sonuç bulunamadı."
                : $"{_latestFlightResults.Count} uçuş sonucu listelendi.";
        }
        catch (Exception ex)
        {
            FlightStatusTextBlock.Text = $"Uçuş arama hatası: {ex.Message}";
            SetFlightResultsPlaceholder("Uçuş araması sırasında hata oluştu.");
        }
        finally
        {
            FlightSearchButton.IsEnabled = true;
            ShowFlightSection();
        }
    }

    private FlightResultItem? _selectedFlightResult;

    private void FlightResultsDataGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedFlightResult = FlightResultsDataGrid.SelectedItem as FlightResultItem;
        FlightCreatePaymentButton.IsEnabled = _selectedFlightResult is not null;
        if (_selectedFlightResult is not null)
        {
            FlightStatusTextBlock.Text = $"{_selectedFlightResult.Airline} ({_selectedFlightResult.Route}) seçildi - {_selectedFlightResult.Price}.";
        }
    }

    private void FlightCreatePaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null)
        {
            FlightStatusTextBlock.Text = "Ödeme için önce giriş yapılmalı.";
            return;
        }

        if (_selectedFlightResult is null)
        {
            FlightStatusTextBlock.Text = "Lütfen ödeme yapmak için bir uçuş seçin.";
            return;
        }

        var flight = _selectedFlightResult;
        var flightPrice = ParseFlightPrice(flight.Price);

        _paymentPreviewItems = new List<OdemeHazirlikItem>
        {
            new OdemeHazirlikItem
            {
                KoleksiyonId = null,
                KoleksiyonAdi = $"[Uçak Bileti] {flight.Airline} ({flight.FromLocation} - {flight.ToLocation}) {flight.DepartureTime}-{flight.ArrivalTime}",
                Tutar = flightPrice
            }
        };

        PrepareCheckoutSummary();
        ShowPaymentCheckoutSection();
    }

    private static decimal ParseFlightPrice(string? priceText)
    {
        var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
        return parsed > 0 ? parsed : 500.00m;
    }
}

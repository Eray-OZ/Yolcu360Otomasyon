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

        // Extra - Flight Car Recommendation START
        var isRoundTrip = FlightRoundTripCheckBox.IsChecked == true && FlightReturnDatePicker.SelectedDate != null;
        _isLastFlightRoundTrip = isRoundTrip;
        _lastPaidRoundTripFlight = isRoundTrip ? flight : null;
        _lastPaidDepartureDate = FlightDepartureDatePicker.SelectedDate ?? DateTime.Today.AddDays(7);
        _lastPaidReturnDate = FlightReturnDatePicker.SelectedDate ?? DateTime.Today.AddDays(10);
        // Extra - Flight Car Recommendation END

        PrepareCheckoutSummary();
        ShowPaymentCheckoutSection();
    }

    private static decimal ParseFlightPrice(string? priceText)
    {
        var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
        return parsed > 0 ? parsed : 500.00m;
    }

    // Extra - Flight Car Recommendation START
    private bool _isLastFlightRoundTrip;
    private FlightResultItem? _lastPaidRoundTripFlight;
    private DateTime? _lastPaidDepartureDate;
    private DateTime? _lastPaidReturnDate;

    private void PrepareFlightCarRecommendationView()
    {
        if (_lastPaidRoundTripFlight is null || _lastPaidDepartureDate is null || _lastPaidReturnDate is null)
            return;

        var flight = _lastPaidRoundTripFlight;
        var destination = flight.ToLocation;
        var departureDate = _lastPaidDepartureDate.Value;
        var returnDate = _lastPaidReturnDate.Value;
        var pickupTime = CalculatePickupTime(flight.ArrivalTime);
        var returnTime = "16:00";

        FlightCarRecLocationTextBlock.Text = destination;
        FlightCarRecPickupTextBlock.Text = $"{departureDate:dd.MM.yyyy} - {pickupTime}";
        FlightCarRecReturnTextBlock.Text = $"{returnDate:dd.MM.yyyy} - {returnTime}";
    }

    private static string CalculatePickupTime(string? arrivalTime)
    {
        if (TimeSpan.TryParse(arrivalTime?.Trim(), out var ts))
        {
            var target = ts.Add(TimeSpan.FromMinutes(30));
            var minute = target.Minutes < 15 ? 0 : (target.Minutes < 45 ? 30 : 0);
            var hour = target.Minutes >= 45 ? (target.Hours + 1) % 24 : target.Hours;
            return $"{hour:00}:{minute:00}";
        }
        return "14:30";
    }

    private static void SetComboBoxSelectedTime(ComboBox comboBox, string time)
    {
        var match = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (i.Tag as string) == time || (i.Content as string) == time);
        if (match is not null)
            comboBox.SelectedItem = match;
    }

    private async void AcceptFlightCarRecommendationButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastPaidRoundTripFlight is null || _lastPaidDepartureDate is null || _lastPaidReturnDate is null)
        {
            ShowPaymentsSection();
            await LoadPaymentsAsync();
            return;
        }

        var flight = _lastPaidRoundTripFlight;
        var destination = flight.ToLocation;
        var departureDate = _lastPaidDepartureDate.Value;
        var returnDate = _lastPaidReturnDate.Value;
        var pickupTime = CalculatePickupTime(flight.ArrivalTime);
        var returnTime = "16:00";

        PickupLocationTextBox.Text = destination;
        PickupDatePicker.SelectedDate = departureDate;
        ReturnDatePicker.SelectedDate = returnDate;
        SetComboBoxSelectedTime(PickupTimeComboBox, pickupTime);
        SetComboBoxSelectedTime(ReturnTimeComboBox, returnTime);
        UpdateSearchDateTexts();

        ShowSearchSection();
        SearchStatusTextBlock.Text = $"{destination} için arka planda araçlar aranıyor...";

        var filter = new SearchFilter
        {
            PickupLocation = destination,
            PickupDate = departureDate,
            ReturnDate = returnDate,
            PickupTime = pickupTime,
            ReturnTime = returnTime,
            TransmissionType = "Farketmez",
            FuelType = "Farketmez"
        };

        _ = RunBackgroundCarSearchFromFlightAsync(filter);
    }

    private async void DeclineFlightCarRecommendationButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowPaymentsSection();
        await LoadPaymentsAsync();
        PaymentsStatusTextBlock.Text = "iyzico sandbox ödeme kaydı oluşturuldu.";
    }

    private async Task RunBackgroundCarSearchFromFlightAsync(SearchFilter filter)
    {
        SearchButton.IsEnabled = false;
        try
        {
            KeepBrowserAliveOffscreen();
            SearchStatusTextBlock.Text = $"{filter.PickupLocation} için arka planda araçlar aranıyor...";

            var baService = CreateBAService(attachProgress: false);
            if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            {
                await baService.RestoreSessionAsync(_activeUser.SessionStatePath);
            }

            await baService.OpenYolcu360HomeAsync();
            await baService.FillPickupLocationAsync(filter.PickupLocation);
            await baService.SelectDateRangeAsync(filter.PickupDate, filter.ReturnDate);
            await baService.SelectTimeAsync(0, filter.PickupTime);
            await baService.SelectTimeAsync(1, filter.ReturnTime);
            await baService.ClickSearchButtonAsync();
            await baService.WaitForSearchResultsAsync();

            SearchStatusTextBlock.Text = "Arama sonuçları okunuyor...";
            var results = await baService.ReadSearchResultsAsync();

            _latestResults = results;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _latestResults;
                SetSearchResultsPlaceholder(_latestResults.Count == 0 ? "Kiralık araç bulunamadı." : null);
            });

            SearchStatusTextBlock.Text = _latestResults.Count == 0
                ? "Araç araması tamamlandı, sonuç bulunamadı."
                : $"{_latestResults.Count} araç bulundu ({filter.PickupLocation}).";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Araç arama hatası: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }
    // Extra - Flight Car Recommendation END
}

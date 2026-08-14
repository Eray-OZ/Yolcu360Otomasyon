using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
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
            if (!DateTime.TryParseExact(
                    FlightDepartureDateTextBox.Text?.Trim(),
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var departureDate))
            {
                FlightStatusTextBlock.Text = "Gidiş tarihi formatı geçersiz. Örnek: 2026-08-20";
                return;
            }

            DateTime? returnDate = null;
            var returnDateText = FlightReturnDateTextBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(returnDateText))
            {
                if (!DateTime.TryParseExact(
                        returnDateText,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedReturnDate))
                {
                    FlightStatusTextBlock.Text = "Dönüş tarihi formatı geçersiz. Örnek: 2026-08-25";
                    return;
                }

                returnDate = parsedReturnDate.Date;
            }

            var filter = new FlightSearchFilter
            {
                FromLocation = FlightFromTextBox.Text?.Trim() ?? string.Empty,
                ToLocation = FlightToTextBox.Text?.Trim() ?? string.Empty,
                DepartureDate = departureDate.Date,
                ReturnDate = returnDate,
                IsRoundTrip = FlightRoundTripCheckBox.IsChecked == true || returnDate is not null,
                OnlyNonStop = FlightOnlyNonStopCheckBox.IsChecked == true
            };

            if (string.IsNullOrWhiteSpace(filter.FromLocation) || string.IsNullOrWhiteSpace(filter.ToLocation))
            {
                FlightStatusTextBlock.Text = "Nereden ve nereye alanları boş olamaz.";
                return;
            }

            var fromSuggestion = _selectedFlightFromSuggestion ??
                await _flightLocationSuggestionService.ResolveBestSuggestionAsync(filter.FromLocation);
            var toSuggestion = _selectedFlightToSuggestion ??
                await _flightLocationSuggestionService.ResolveBestSuggestionAsync(filter.ToLocation);

            if (fromSuggestion is null || toSuggestion is null)
            {
                FlightStatusTextBlock.Text = "Uçuş için nereden/nereye önerilerinden seçim yapılmalı.";
                return;
            }

            filter.FromLocation = fromSuggestion.MainText;
            filter.FromPlaceCode = fromSuggestion.PlaceCode;
            filter.FromPlaceId = fromSuggestion.PlaceId;
            filter.FromPlaceType = fromSuggestion.Type;
            filter.ToLocation = toSuggestion.MainText;
            filter.ToPlaceCode = toSuggestion.PlaceCode;
            filter.ToPlaceId = toSuggestion.PlaceId;
            filter.ToPlaceType = toSuggestion.Type;

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
            FlightStatusTextBlock.Text = "Uçuş sonuçları bekleniyor...";
            await baService.WaitForFlightResultsAsync();

            FlightStatusTextBlock.Text = "Uçuş sonuçları okunuyor...";
            _latestFlightResults = await baService.ReadFlightResultsAsync();
            foreach (var flightResult in _latestFlightResults)
            {
                flightResult.FromLocation = filter.FromLocation;
                flightResult.ToLocation = filter.ToLocation;
                flightResult.Route = $"{filter.FromLocation} → {filter.ToLocation}";
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
}

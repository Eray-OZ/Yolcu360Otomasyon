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

        _suppressFlightFromSuggestionLookup = true;
        FlightFromTextBox.Text = GetFlightSuggestionText(suggestion);
        _suppressFlightFromSuggestionLookup = false;

        HideFlightLocationSuggestions(FlightFromSuggestionsPanel, FlightFromSuggestionsListBox);
    }

    private void FlightToSuggestionsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (FlightToSuggestionsListBox.SelectedItem is not LocationSuggestionItem suggestion)
            return;

        _suppressFlightToSuggestionLookup = true;
        FlightToTextBox.Text = GetFlightSuggestionText(suggestion);
        _suppressFlightToSuggestionLookup = false;

        HideFlightLocationSuggestions(FlightToSuggestionsPanel, FlightToSuggestionsListBox);
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
            var suggestions = await _locationSuggestionService.GetSuggestionsAsync(input, cts.Token);
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

            Console.WriteLine($"[FlightUI] From='{filter.FromLocation}' To='{filter.ToLocation}' Departure='{filter.DepartureDate:yyyy-MM-dd}' Return='{filter.ReturnDate:yyyy-MM-dd}'");

            if (string.IsNullOrWhiteSpace(filter.FromLocation) || string.IsNullOrWhiteSpace(filter.ToLocation))
            {
                FlightStatusTextBlock.Text = "Nereden ve nereye alanları boş olamaz.";
                return;
            }

            if (_activeUser is null)
            {
                FlightStatusTextBlock.Text = "Önce giriş yapılmalı.";
                return;
            }

            ShowBrowserSection();
            FlightStatusTextBlock.Text = "Gömülü tarayıcı uçuş araması için hazırlanıyor...";

            var baService = CreateBAService();
            if (!string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
                await baService.RestoreSessionAsync(_activeUser.SessionStatePath);

            baService.ProgressChanged += message =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    FlightStatusTextBlock.Text = message;
                });
            };

            await baService.SearchFlightTicketsAsync(filter);
            FlightStatusTextBlock.Text = "Uçuş araması başlatıldı. Sonuç HTML'i geldikten sonra okuma kısmı ayrı Flight koduyla eklenecek.";
        }
        catch (Exception ex)
        {
            FlightStatusTextBlock.Text = $"Uçuş arama hatası: {ex.Message}";
        }
        finally
        {
            FlightSearchButton.IsEnabled = true;
        }
    }
}

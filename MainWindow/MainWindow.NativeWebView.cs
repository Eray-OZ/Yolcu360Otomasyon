using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            ShowBrowserSection();
            SearchStatusTextBlock.Text = "Gömülü tarayıcı açılıyor...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

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

    private async void EmbeddedSearchTestButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var pickupLocation = PickupLocationTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(pickupLocation))
            {
                SearchStatusTextBlock.Text = "Gömülü test için alış yeri girilmeli.";
                return;
            }

            if (!DateTime.TryParseExact(
                    PickupDateTextBox.Text?.Trim(),
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var pickupDate))
            {
                pickupDate = DateTime.Today.AddDays(3);
            }

            if (!DateTime.TryParseExact(
                    ReturnDateTextBox.Text?.Trim(),
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var returnDate))
            {
                returnDate = pickupDate.AddDays(4);
            }

            var pickupTime = PickupTimeTextBox.Text?.Trim() ?? "10:00";
            var returnTime = ReturnTimeTextBox.Text?.Trim() ?? "18:00";

            var filter = new SearchFilter
            {
                PickupLocation = pickupLocation,
                PickupDate = pickupDate.Date,
                ReturnDate = returnDate.Date,
                PickupTime = pickupTime,
                ReturnTime = returnTime,
                TransmissionType = GetComboBoxTag(TransmissionComboBox),
                FuelType = GetComboBoxTag(FuelComboBox)
            };
            _latestSearchFilter = filter;

            ShowBrowserSection();
            SearchStatusTextBlock.Text = "Gömülü tarayıcı arama formu hazırlanıyor...";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            var embeddedBrowser = CreateEmbeddedBrowserAutomationService();
            if (_activeUser is not null && !string.IsNullOrWhiteSpace(_activeUser.SessionStatePath))
            {
                await embeddedBrowser.RestoreSessionAsync(_activeUser.SessionStatePath);
            }

            await embeddedBrowser.OpenYolcu360HomeAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış yeri seçiyor...";
            await embeddedBrowser.FillPickupLocationAsync(pickupLocation);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı tarihleri seçiyor...";
            await embeddedBrowser.SelectDateRangeAsync(pickupDate, returnDate);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış saatini seçiyor...";
            await embeddedBrowser.SelectTimeAsync(0, pickupTime);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı bırakış saatini seçiyor...";
            await embeddedBrowser.SelectTimeAsync(1, returnTime);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı araç ara butonuna tıklıyor...";
            await embeddedBrowser.ClickSearchButtonAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı sonuçları bekliyor...";
            await embeddedBrowser.WaitForSearchResultsAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı sonuçları okuyor...";
            var results = await embeddedBrowser.ReadSearchResultsAsync();
            _latestResults = results;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = _latestResults;
            });

            SearchStatusTextBlock.Text = _latestResults.Count == 0
                ? "Gömülü arama tamamlandı, sonuç bulunamadı."
                : $"{_latestResults.Count} sonuç listelendi. İlk sonuç: {_latestResults[0].Title} | {_latestResults[0].Price}";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Gömülü arama test hatası: {ex.Message}";
        }
        finally
        {
            await Task.Delay(1500);
            ShowSearchSection();
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

using Avalonia.Interactivity;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            ShowSearchSection();
            EmbeddedBrowserPanel.IsVisible = true;
            SearchStatusTextBlock.Text = "Gömülü tarayıcı açılıyor...";

            var embeddedBrowser = new EmbeddedBrowserAutomationService(EmbeddedBrowser);
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

            EmbeddedBrowserPanel.IsVisible = true;
            SearchStatusTextBlock.Text = "Gömülü tarayıcı arama formu hazırlanıyor...";

            var embeddedBrowser = new EmbeddedBrowserAutomationService(EmbeddedBrowser);
            await embeddedBrowser.OpenYolcu360HomeAsync();

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış yeri seçiyor...";
            await embeddedBrowser.FillPickupLocationAsync(pickupLocation);

            SearchStatusTextBlock.Text = "Gömülü tarayıcı alış yeri seçimini tamamladı.";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Gömülü arama test hatası: {ex.Message}";
        }
    }
}

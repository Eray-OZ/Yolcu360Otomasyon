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
            await embeddedBrowser.NavigateAsync("https://www.yolcu360.com/");

            var title = await embeddedBrowser.GetTitleAsync();
            SearchStatusTextBlock.Text = $"Gömülü tarayıcı hazır. Title: {title}";
        }
        catch (Exception ex)
        {
            SearchStatusTextBlock.Text = $"Gömülü tarayıcı hatası: {ex.Message}";
        }
    }
}

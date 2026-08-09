using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private async void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
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

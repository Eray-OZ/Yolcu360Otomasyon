using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowSearchSection();
        EmbeddedBrowserPanel.IsVisible = true;
        EmbeddedBrowser.Navigate(new Uri("https://www.yolcu360.com/"));
    }
}

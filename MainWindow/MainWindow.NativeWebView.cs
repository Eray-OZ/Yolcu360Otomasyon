using Avalonia.Interactivity;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private void NativeWebViewTestButton_Click(object? sender, RoutedEventArgs e)
    {
        var testWindow = new NativeWebViewTestWindow();
        testWindow.Show();
    }
}

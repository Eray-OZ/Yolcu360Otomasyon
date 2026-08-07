using Avalonia.Controls;

namespace Yolcu360Otomasyon;

public partial class NativeWebViewTestWindow : Window
{
    public NativeWebViewTestWindow()
    {
        InitializeComponent();
        NativeBrowser.NavigationStarted += (_, args) =>
        {
            NativeWebViewStatusTextBlock.Text = $"Yükleniyor: {args.Request}";
        };
        NativeBrowser.NavigationCompleted += async (_, args) =>
        {
            NativeWebViewStatusTextBlock.Text = args.IsSuccess
                ? $"Yüklendi: {NativeBrowser.Source}"
                : $"Yükleme başarısız: {NativeBrowser.Source}";

            if (args.IsSuccess)
                await ReadTitleAsync();
        };
    }

    private void ExampleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NativeBrowser.Navigate(new Uri("https://example.com"));
    }

    private void Yolcu360Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        NativeBrowser.Navigate(new Uri("https://www.yolcu360.com/"));
    }

    private async void JsTestButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ReadTitleAsync();
    }

    private async Task ReadTitleAsync()
    {
        try
        {
            var title = await NativeBrowser.InvokeScript("document.title");
            NativeWebViewStatusTextBlock.Text = $"JS çalıştı. Title: {title}";
        }
        catch (Exception ex)
        {
            NativeWebViewStatusTextBlock.Text = $"JS hatası: {ex.Message}";
        }
    }
}

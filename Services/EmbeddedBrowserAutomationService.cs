using Avalonia.Controls;
using Avalonia.Threading;
using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

public sealed class EmbeddedBrowserAutomationService
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private readonly NativeWebView _browser;

    public event Action<string>? ProgressChanged;

    public EmbeddedBrowserAutomationService(NativeWebView browser)
    {
        _browser = browser;
    }

    public async Task NavigateAsync(string url, TimeSpan? timeout = null)
    {
        var target = new Uri(url);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs args)
        {
            Report($"Gömülü tarayıcı yükleme tamamlandı: {args.Request}");
            completion.TrySetResult(args.IsSuccess);
        }

        _browser.NavigationCompleted += OnNavigationCompleted;

        try
        {
            Report($"Gömülü tarayıcı gidiyor: {url}");
            await Dispatcher.UIThread.InvokeAsync(() => _browser.Navigate(target), DispatcherPriority.Render);

            using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(45));
            await using var registration = timeoutCts.Token.Register(() => completion.TrySetCanceled(timeoutCts.Token));

            var succeeded = await completion.Task;
            if (!succeeded)
                throw new InvalidOperationException($"Sayfa yüklenemedi: {url}");
        }
        finally
        {
            _browser.NavigationCompleted -= OnNavigationCompleted;
        }
    }

    public Task<string?> EvaluateScriptAsync(string script)
    {
        return Dispatcher.UIThread.InvokeAsync(() => _browser.InvokeScript(script));
    }

    public async Task<string> GetTitleAsync()
    {
        return await EvaluateScriptAsync("document.title") ?? string.Empty;
    }

    public async Task OpenYolcu360HomeAsync()
    {
        Report("Yolcu360 ana sayfası açılıyor...");
        await NavigateAsync(Yolcu360HomeUrl);
        Report("Sayfanın hazır olması bekleniyor...");
        await WaitForDocumentReadyAsync();
        Report("Başlangıç popup'ı bekleniyor...");
        await Task.Delay(2_500);
        var popupClosed = await CloseInitialPopupAsync();
        Report(popupClosed ? "Başlangıç popup'ı kapatıldı." : "Başlangıç popup'ı görünmedi.");
    }

    public async Task WaitForDocumentReadyAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));

        while (DateTimeOffset.UtcNow < deadline)
        {
            var readyState = await EvaluateScriptAsync("document.readyState");
            if (string.Equals(readyState?.Trim('"'), "complete", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Gömülü tarayıcı sayfa hazır durumuna geçmedi.");
    }

    public async Task<bool> CloseInitialPopupAsync()
    {
        var result = await EvaluateScriptAsync(
            """
            (() => {
                const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                if (!closeButton) return false;

                const rect = closeButton.getBoundingClientRect();
                const style = window.getComputedStyle(closeButton);
                const visible = rect.width > 0 &&
                    rect.height > 0 &&
                    style.visibility !== 'hidden' &&
                    style.display !== 'none';

                if (!visible) return false;

                closeButton.click();
                return true;
            })();
            """);

        return string.Equals(result?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task FillPickupLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        var locationJson = JsonSerializer.Serialize(location.Trim());

        Report("Alış yeri inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => !!document.querySelector('#inputPickUpLocation'))();
            """,
            TimeSpan.FromSeconds(20));

        Report($"Alış yeri yazılıyor: {location}");
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector('#inputPickUpLocation');
                input.focus();
                input.value = {{locationJson}};
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: {{locationJson}} }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            })();
            """);

        Report("Alış yeri önerileri bekleniyor...");
        await WaitForScriptTrueAsync(
            """
            (() => document.querySelectorAll('.search-autocomplete .location-item, .location-item').length > 0)();
            """,
            TimeSpan.FromSeconds(10));

        Report("Alış yeri önerisi seçiliyor...");
        var selected = await EvaluateScriptAsync(
            $$"""
            (() => {
                const targetText = {{locationJson}};
                const normalize = value => (value || '')
                    .toLocaleLowerCase('tr-TR')
                    .replace(/\s+/g, ' ')
                    .trim();
                const target = normalize(targetText);
                const items = Array.from(document.querySelectorAll('.search-autocomplete .location-item, .location-item'))
                    .filter(item => {
                        const rect = item.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });

                const exactMainText = items.find(item =>
                    normalize(item.querySelector('strong')?.textContent || '') === target);
                const exactFullText = items.find(item =>
                    normalize(item.textContent || '').startsWith(target));
                const selected = exactMainText || exactFullText || items[0];
                if (!selected) return false;

                selected.scrollIntoView({ block: 'center', inline: 'center' });
                selected.click();
                return true;
            })();
            """);

        if (!string.Equals(selected?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Alış yeri önerisi seçilemedi.");

        Report("Alış yeri önerisi seçildi.");
    }

    private async Task WaitForScriptTrueAsync(string script, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await EvaluateScriptAsync(script);
            if (string.Equals(result?.Trim('"'), "true", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException("Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı.");
    }

    private void Report(string message)
    {
        ProgressChanged?.Invoke(message);
    }
}

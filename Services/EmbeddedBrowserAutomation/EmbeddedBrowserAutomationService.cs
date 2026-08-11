using Avalonia.Controls;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string PickupLocationInputSelector = "#inputPickUpLocation";
    private const string LocationSuggestionSelector = ".search-autocomplete__item, .search-autocomplete-mobile__item, .search-autocomplete .location-item, .location-item";
    private const string DateTimeGroupSelector = "[modaltitle='Alış ve Bırakış Tarihi']";
    private const string DatePickerSelector = ".dp__main.dp__theme_light";
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

    public async Task<bool> EvaluateBooleanScriptAsync(string script)
    {
        var result = await EvaluateScriptAsync(script);
        return IsScriptTrue(result);
    }

    public async Task<T?> EvaluateJsonScriptAsync<T>(string script)
    {
        var result = await EvaluateScriptAsync(script);
        if (string.IsNullOrWhiteSpace(result))
            return default;

        var cleanJson = result.Trim();
        if (cleanJson.StartsWith("\"") && cleanJson.EndsWith("\""))
        {
            try
            {
                cleanJson = System.Text.Json.JsonSerializer.Deserialize<string>(cleanJson) ?? cleanJson;
            }
            catch
            {
                // Fallback to original cleanJson if unescaping fails
            }
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(cleanJson);
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
        var popupClosed = await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));
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

        return IsScriptTrue(result);
    }

    private async Task<bool> WaitForInitialPopupAndCloseAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await CloseInitialPopupAsync())
                return true;

            await Task.Delay(250);
        }

        return false;
    }

    public Task<string?> GetSearchDomDiagnosticAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const compact = value => (value || '').replace(/\s+/g, ' ').trim();
                const inputs = Array.from(document.querySelectorAll('input, textarea'))
                    .slice(0, 20)
                    .map((el, index) => ({
                        index,
                        id: el.id || '',
                        name: el.getAttribute('name') || '',
                        type: el.getAttribute('type') || '',
                        placeholder: el.getAttribute('placeholder') || '',
                        value: el.value || '',
                        ariaLabel: el.getAttribute('aria-label') || '',
                        visible: (() => {
                            const rect = el.getBoundingClientRect();
                            return rect.width > 0 && rect.height > 0;
                        })()
                    }));

                const possibleLocationElements = Array.from(document.querySelectorAll('[id*="location" i], [placeholder*="alış" i], [placeholder*="teslim" i], [class*="location" i], [class*="autocomplete" i]'))
                    .slice(0, 20)
                    .map((el, index) => ({
                        index,
                        tag: el.tagName,
                        id: el.id || '',
                        className: el.className || '',
                        placeholder: el.getAttribute('placeholder') || '',
                        text: compact(el.textContent).slice(0, 120),
                        visible: (() => {
                            const rect = el.getBoundingClientRect();
                            return rect.width > 0 && rect.height > 0;
                        })()
                    }));

                return JSON.stringify({
                    url: location.href,
                    title: document.title,
                    inputCount: document.querySelectorAll('input, textarea').length,
                    pickupById: !!document.querySelector('#inputPickUpLocation'),
                    inputs,
                    possibleLocationElements
                });
            })();
            """);
    }

    private async Task WaitForScriptTrueAsync(string script, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            var result = await EvaluateScriptAsync(script);
            if (IsScriptTrue(result))
                return;

            await Task.Delay(250);
        }

        throw new TimeoutException($"Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı. Son kontrol sonucu: {await EvaluateScriptAsync(script)}");
    }

    private async Task<bool> WaitForScriptTrueOrTimeoutAsync(string script, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(250);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await EvaluateBooleanScriptAsync(script))
                return true;

            await Task.Delay(interval);
        }

        return false;
    }

    private void Report(string message)
    {
        ProgressChanged?.Invoke(message);
    }

    private static bool IsScriptTrue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Trim('"');
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
    }
}

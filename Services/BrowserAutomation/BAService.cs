using Avalonia.Controls;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private const string Yolcu360HomeUrl = "https://www.yolcu360.com/";
    private const string PickupLocationInputSelector = "#inputPickUpLocation";
    private const string LocationSuggestionSelector = ".search-autocomplete__item, .search-autocomplete-mobile__item, .search-autocomplete .location-item, .location-item";
    private const string DateTimeGroupSelector = "[modaltitle='Alış ve Bırakış Tarihi']";
    private const string DatePickerSelector = ".dp__main.dp__theme_light";
    private static readonly TimeSpan InitialPopupDelay = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ScriptPollingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan FilterPanelReadyDelay = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan FilterRefreshDelay = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan ResultsRefreshDelay = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan ResultsPollingDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan LocationSelectionApplyDelay = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan DatePickerActionDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DatePickerSelectionDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DatePickerMenuPollingDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CalendarNavigationDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan TimePickerOpenDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan TimePickerSelectionDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SearchButtonPreparationDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SearchButtonAfterClickDelay = TimeSpan.FromMilliseconds(1000);
    private static readonly TimeSpan LogoutNavigationDelay = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PaymentPageHydrationDelay = TimeSpan.FromMilliseconds(2000);
    private static readonly TimeSpan PaymentTabSelectionDelay = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan PaymentFormSubmitPreparationDelay = TimeSpan.FromMilliseconds(1250);
    private readonly NativeWebView _browser;

    public event Action<string>? ProgressChanged;

    public BAService(NativeWebView browser)
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
        await Task.Delay(InitialPopupDelay);
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

            await Task.Delay(ScriptPollingDelay);
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

            await Task.Delay(ScriptPollingDelay);
        }

        throw new TimeoutException($"Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı. Son kontrol sonucu: {await EvaluateScriptAsync(script)}");
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

    private static string ToJson<T>(T value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static string ToJson<T>(T value, System.Text.Json.JsonSerializerOptions options)
    {
        return System.Text.Json.JsonSerializer.Serialize(value, options);
    }

    private async Task<bool> SetInputValueAsync(string selector, string value, bool blurAfterChange = true)
    {
        var selectorJson = ToJson(selector);
        var valueJson = ToJson(value);
        var blurAfterChangeJson = ToJson(blurAfterChange);

        return await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{selectorJson}});
                if (!input) return false;
                input.focus();

                const proto = input instanceof HTMLInputElement ? Object.getPrototypeOf(input) : null;
                const desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                if (desc && desc.set) {
                    desc.set.call(input, {{valueJson}});
                } else {
                    input.value = {{valueJson}};
                }

                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                if ({{blurAfterChangeJson}}) {
                    input.dispatchEvent(new Event('blur', { bubbles: true }));
                }
                return true;
            })();
            """);
    }

    private async Task<bool> ClickElementAsync(string selector)
    {
        var selectorJson = ToJson(selector);

        return await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const element = document.querySelector({{selectorJson}});
                if (!element) return false;
                return window.__embeddedClickElement
                    ? window.__embeddedClickElement(element)
                    : false;
            })();
            """);
    }

    private async Task<bool> ClickButtonByTextAsync(string text, string? preferredSelector = null)
    {
        var textJson = ToJson(text);
        var preferredSelectorJson = ToJson(preferredSelector);

        return await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const preferredSelector = {{preferredSelectorJson}};
                const targetText = ({{textJson}} || '').trim().toLocaleLowerCase('tr-TR');
                const preferred = preferredSelector ? document.querySelector(preferredSelector) : null;
                const button = preferred ||
                    Array.from(document.querySelectorAll('button, input[type="submit"]'))
                        .find(b => (b.textContent || b.value || '').trim().toLocaleLowerCase('tr-TR').includes(targetText));

                if (!button) return false;
                return window.__embeddedClickElement
                    ? window.__embeddedClickElement(button)
                    : false;
            })();
            """);
    }

    private Task EnsureEmbeddedClickHelperAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                if (window.__embeddedClickElement) return true;

                window.__embeddedClickElement = element => {
                    if (!element) return false;
                    element.scrollIntoView({ block: 'center', inline: 'nearest' });

                    const rect = element.getBoundingClientRect();
                    const style = window.getComputedStyle(element);
                    const enabled = !element.disabled &&
                        element.getAttribute('aria-disabled') !== 'true' &&
                        style.pointerEvents !== 'none' &&
                        rect.width > 0 &&
                        rect.height > 0;

                    if (!enabled) return false;

                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    if (typeof PointerEvent === 'function') {
                        element.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        element.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }

                    element.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    element.dispatchEvent(new MouseEvent('mouseup', opts));
                    element.dispatchEvent(new MouseEvent('click', opts));
                    if (typeof element.click === 'function') element.click();
                    return true;
                };

                return true;
            })();
            """);
    }
}

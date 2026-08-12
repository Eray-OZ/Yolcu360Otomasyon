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
        await EnsureJavaScriptHelpersAsync();
        Report("Başlangıç popup'ı bekleniyor...");
        var popupClosed = await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));
        Report(popupClosed ? "Başlangıç popup'ı kapatıldı." : "Başlangıç popup'ı görünmedi.");
    }

    private async Task EnsureJavaScriptHelpersAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                window.__ba = window.__ba || {};

                window.__ba.normalizeText = value =>
                    (value || '').replace(/\s+/g, ' ').trim();

                window.__ba.normalizeTr = value =>
                    window.__ba.normalizeText(value).toLocaleLowerCase('tr-TR');

                window.__ba.compactTr = value =>
                    window.__ba.normalizeTr(value).replace(/\s/g, '');

                window.__ba.isVisible = element => {
                    if (!element) return false;

                    const rect = element.getBoundingClientRect();
                    const style = getComputedStyle(element);

                    return rect.width > 0 &&
                        rect.height > 0 &&
                        style.display !== 'none' &&
                        style.visibility !== 'hidden';
                };

                window.__ba.clickLikeUser = (element, closestSelector) => {
                    if (!element) return false;

                    element.scrollIntoView({ block: 'center', inline: 'nearest' });

                    const rect = element.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const pointTarget = document.elementFromPoint(x, y);
                    const eventTarget = closestSelector
                        ? (pointTarget?.closest?.(closestSelector) || pointTarget || element)
                        : (pointTarget || element);
                    const eventOptions = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    const dispatchPointer = (target, type, buttons = 0) => {
                        if (!target) return;
                        if (typeof PointerEvent === 'function') {
                            target.dispatchEvent(new PointerEvent(type, {
                                ...eventOptions,
                                pointerId: 1,
                                pointerType: 'mouse',
                                isPrimary: true,
                                buttons
                            }));
                        }
                    };

                    const dispatchMouse = (target, type, buttons = 0) => {
                        if (!target) return;
                        target.dispatchEvent(new MouseEvent(type, { ...eventOptions, buttons }));
                    };

                    for (const target of [eventTarget, element]) {
                        dispatchPointer(target, 'pointerover');
                        dispatchMouse(target, 'mouseover');
                        dispatchMouse(target, 'mousemove');
                        dispatchPointer(target, 'pointerdown', 1);
                        dispatchMouse(target, 'mousedown', 1);
                        dispatchPointer(target, 'pointerup');
                        dispatchMouse(target, 'mouseup');
                        dispatchMouse(target, 'click');
                    }

                    return {
                        clicked: true,
                        pointTargetText: window.__ba.normalizeText(pointTarget?.textContent).slice(0, 120)
                    };
                };
                return true;
            })();
            """);
    }

    public async Task WaitForDocumentReadyAsync(TimeSpan? timeout = null)
    {
        var ready = await WaitUntilAsync(
            async () =>
            {
            var readyState = await EvaluateScriptAsync("document.readyState");
                return string.Equals(readyState?.Trim('"'), "complete", StringComparison.OrdinalIgnoreCase);
            },
            timeout ?? TimeSpan.FromSeconds(20));

        if (!ready)
            throw new TimeoutException("Gömülü tarayıcı sayfa hazır durumuna geçmedi.");
    }

    public async Task<bool> CloseInitialPopupAsync()
    {
        var result = await EvaluateScriptAsync(
            """
            (() => {
                const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');
                if (!window.__ba?.isVisible(closeButton)) {
                    return false;
                }

                closeButton.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task<bool> WaitForInitialPopupAndCloseAsync(TimeSpan timeout)
    {
        return await WaitUntilAsync(CloseInitialPopupAsync, timeout);
    }

    public Task<string?> GetSearchDomDiagnosticAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const normalizeText = window.__ba?.normalizeText || (value => (value || '').replace(/\s+/g, ' ').trim());
                const isVisible = window.__ba?.isVisible || (() => false);

                const inputs = Array
                    .from(document.querySelectorAll('input, textarea'))
                    .slice(0, 20)
                    .map((input, index) => ({
                        index,
                        id: input.id || '',
                        name: input.getAttribute('name') || '',
                        type: input.getAttribute('type') || '',
                        placeholder: input.getAttribute('placeholder') || '',
                        value: input.value || '',
                        ariaLabel: input.getAttribute('aria-label') || '',
                        visible: isVisible(input)
                    }));

                const locationCandidateSelector = [
                    '[id*="location" i]',
                    '[placeholder*="alış" i]',
                    '[placeholder*="teslim" i]',
                    '[class*="location" i]',
                    '[class*="autocomplete" i]'
                ].join(',');

                const possibleLocationElements = Array
                    .from(document.querySelectorAll(locationCandidateSelector))
                    .slice(0, 20)
                    .map((element, index) => ({
                        index,
                        tag: element.tagName,
                        id: element.id || '',
                        className: element.className || '',
                        placeholder: element.getAttribute('placeholder') || '',
                        text: normalizeText(element.textContent).slice(0, 120),
                        visible: isVisible(element)
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
        var completed = await WaitUntilAsync(
            async () =>
            {
                var result = await EvaluateScriptAsync(script);
                return IsScriptTrue(result);
            },
            timeout);

        if (!completed)
            throw new TimeoutException($"Gömülü tarayıcı beklenen sayfa durumuna ulaşmadı. Son kontrol sonucu: {await EvaluateScriptAsync(script)}");
    }

    private async Task<bool> WaitForScriptTrueOrTimeoutAsync(string script, TimeSpan timeout, TimeSpan? pollInterval = null)
    {
        return await WaitUntilAsync(
            () => EvaluateBooleanScriptAsync(script),
            timeout,
            pollInterval ?? TimeSpan.FromMilliseconds(250));
    }
    
    private static async Task<bool> WaitUntilAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan? pollInterval = null)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var timer = new PeriodicTimer(pollInterval ?? TimeSpan.FromMilliseconds(250));

        if (await condition())
            return true;

        try
        {
            while (await timer.WaitForNextTickAsync(timeoutCts.Token))
            {
                if (await condition())
                    return true;
            }
        }
        catch (OperationCanceledException)
        {
            return false;
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

using Avalonia.Controls;
using Avalonia.Threading;
using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

public sealed class EmbeddedBrowserAutomationService
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

        return IsScriptTrue(result);
    }

    public async Task FillPickupLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidOperationException("Alış yeri boş bırakılamaz.");

        var locationJson = JsonSerializer.Serialize(location.Trim());
        var pickupLocationInputSelectorJson = JsonSerializer.Serialize(PickupLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
        var diagnostic = await GetSearchDomDiagnosticAsync();
        Report($"Gömülü DOM: {diagnostic}");

        Report("Alış yeri inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            $$"""
            (() => !!document.querySelector({{pickupLocationInputSelectorJson}}))();
            """,
            TimeSpan.FromSeconds(20));

        Report($"Alış yeri yazılıyor: {location}");
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const text = {{locationJson}};
                input.focus();
                input.value = '';
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'deleteContentBackward', data: null }));

                for (const char of text) {
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                    input.value += char;
                    input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                }

                input.dispatchEvent(new Event('change', { bubbles: true }));
                return true;
            })();
            """);

        Report("Alış yeri önerileri bekleniyor...");
        await WaitForLocationSuggestionsAsync(LocationSuggestionSelector, TimeSpan.FromSeconds(12));

        var selectionApplied = false;
        for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
        {
            Report($"Alış yeri önerisi seçiliyor. Deneme: {attempt}");
            var selected = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const input = document.querySelector({{pickupLocationInputSelectorJson}});
                    const targetText = {{locationJson}};
                    const normalize = value => (value || '')
                        .toLocaleLowerCase('tr-TR')
                        .replace(/\s+/g, ' ')
                        .trim();
                    const compact = value => normalize(value).replace(/\s/g, '');
                    const target = normalize(targetText);
                    const visible = item => {
                        const rect = item.getBoundingClientRect();
                        const style = getComputedStyle(item);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    };
                    const getMainText = item => normalize(
                        item.querySelector('strong, .search-autocomplete__item__text-wrapper span:first-child, .search-autocomplete-mobile__item__text-wrapper span:first-child, div > div:first-child')?.textContent || ''
                    );
                    const getScore = item => {
                        const fullText = normalize(item.textContent || '');
                        const mainText = getMainText(item);
                        const compactText = compact(item.textContent || '');
                        const hasAirportText =
                            fullText.includes('airport') ||
                            fullText.includes('havalimanı') ||
                            fullText.includes('sabiha') ||
                            fullText.includes('saw') ||
                            fullText.includes('ist)');

                        if (mainText === target) return 0;
                        if (compactText === compact(`${targetText} Türkiye`) || compactText === compact(`${targetText}, Türkiye`)) return 1;
                        if (fullText === target) return 2;
                        if (!hasAirportText && mainText.startsWith(target + ' ')) return 3;
                        if (!hasAirportText && fullText.startsWith(target)) return 4;
                        if (mainText.startsWith(target)) return 5;
                        if (fullText.startsWith(target)) return 6;
                        if (mainText.includes(target)) return 7;
                        if (fullText.includes(target)) return 8;
                        return 9;
                    };

                    const items = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                        .filter(item => visible(item) && (!input || (item !== input && !item.contains(input))));
                    const selected = items
                        .sort((a, b) => {
                            const score = getScore(a) - getScore(b);
                            if (score !== 0) return score;
                            const ar = a.getBoundingClientRect();
                            const br = b.getBoundingClientRect();
                            return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                        })[0];

                    if (!selected) return JSON.stringify({ clicked: false, reason: 'öneri bulunamadı', itemCount: items.length });

                    selected.scrollIntoView({ block: 'center', inline: 'nearest' });
                    const rect = selected.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const pointTarget = document.elementFromPoint(x, y);
                    const eventTarget = pointTarget?.closest?.({{locationSuggestionSelectorJson}}) || pointTarget || selected;
                    const eventOptions = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    const dispatchPointer = (target, type, buttons = 0) => {
                        if (!target) return;
                        if (typeof PointerEvent === 'function') {
                            target.dispatchEvent(new PointerEvent(type, { ...eventOptions, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons }));
                        }
                    };
                    const dispatchMouse = (target, type, buttons = 0) => {
                        if (!target) return;
                        target.dispatchEvent(new MouseEvent(type, { ...eventOptions, buttons }));
                    };

                    for (const target of [eventTarget, selected]) {
                        dispatchPointer(target, 'pointerover');
                        dispatchMouse(target, 'mouseover');
                        dispatchMouse(target, 'mousemove');
                        dispatchPointer(target, 'pointerdown', 1);
                        dispatchMouse(target, 'mousedown', 1);
                        dispatchPointer(target, 'pointerup');
                        dispatchMouse(target, 'mouseup');
                        dispatchMouse(target, 'click');
                    }

                    return JSON.stringify({
                        clicked: true,
                        selectedText: (selected.textContent || '').replace(/\s+/g, ' ').trim(),
                        pointTargetText: (pointTarget?.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120),
                        inputValue: input?.value || '',
                        remainingSuggestions: document.querySelectorAll({{locationSuggestionSelectorJson}}).length
                    });
                })();
                """);

            Report($"Alış yeri seçim sonucu: {selected}");
            await Task.Delay(700);
            selectionApplied = await IsPickupLocationSelectionAppliedAsync();
        }

        if (!selectionApplied)
            throw new InvalidOperationException("Alış yeri önerisi seçilemedi.");

        Report("Alış yeri önerisi seçildi.");
    }

    private async Task<bool> IsPickupLocationSelectionAppliedAsync()
    {
        var pickupLocationInputSelectorJson = JsonSerializer.Serialize(PickupLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{pickupLocationInputSelectorJson}});
                const visibleSuggestions = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                    .filter(item => {
                        const rect = item.getBoundingClientRect();
                        const style = getComputedStyle(item);
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            style.display !== 'none' &&
                            style.visibility !== 'hidden';
                    });
                return !!input && input.value.trim().length > 0 && visibleSuggestions.length === 0;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task WaitForLocationSuggestionsAsync(string selector, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? lastResult = null;
        var selectorJson = JsonSerializer.Serialize(selector);

        while (DateTimeOffset.UtcNow < deadline)
        {
            lastResult = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const items = Array.from(document.querySelectorAll({{selectorJson}}));
                    const visibleItems = items.filter(item => {
                        const rect = item.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0;
                    });
                    return JSON.stringify({
                        total: items.length,
                        visible: visibleItems.length,
                        text: visibleItems.slice(0, 3).map(item => (item.textContent || '').replace(/\s+/g, ' ').trim())
                    });
                })();
                """);

            var summary = (lastResult ?? string.Empty).Trim('"');
            Report($"Alış yeri önerileri: {summary}");

            if (summary.Contains("\"visible\":", StringComparison.OrdinalIgnoreCase) &&
                !summary.Contains("\"visible\":0", StringComparison.OrdinalIgnoreCase))
                return;

            await Task.Delay(350);
        }

        throw new TimeoutException($"Alış yeri önerileri gelmedi. Son durum: {lastResult}");
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

    private void Report(string message)
    {
        Console.WriteLine($"[EmbeddedWebView] {message}");
        ProgressChanged?.Invoke(message);
    }

    private static bool IsScriptTrue(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().Trim('"');
        return string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase);
    }
}

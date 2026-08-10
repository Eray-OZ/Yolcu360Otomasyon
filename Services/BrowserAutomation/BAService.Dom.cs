using Avalonia.Threading;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
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
                // Fallback to original cleanJson if unescaping fails.
            }
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(cleanJson);
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

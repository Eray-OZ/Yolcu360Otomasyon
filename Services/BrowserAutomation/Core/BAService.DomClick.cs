namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
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

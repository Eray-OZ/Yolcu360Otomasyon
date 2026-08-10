namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private Task<string?> SelectTimeOptionAsync(string time)
    {
        var timeJson = ToJson(time);

        return EvaluateScriptAsync(
            $$"""
            (() => {
                const target = {{timeJson}};
                const visible = el => {
                    const r = el.getBoundingClientRect();
                    const s = window.getComputedStyle(el);
                    return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
                };

                const options = Array.from(document.querySelectorAll('.dropdown-item, [role="option"], li, .time-option, div[class*="option"], div[class*="item"]'))
                    .filter(visible);

                let found = options.find(o => {
                    const txt = (o.textContent || '').trim();
                    return txt === target || txt.startsWith(target);
                });

                if (!found) {
                    const allLeafs = Array.from(document.querySelectorAll('div, li, span, button'))
                        .filter(el => {
                            if (!visible(el)) return false;
                            const t = (el.textContent || '').trim();
                            return (t === target || t.startsWith(target)) && el.children.length === 0;
                        });
                    if (allLeafs.length > 0) found = allLeafs[0];
                }

                if (found) {
                    found.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                    const rect = found.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    if (typeof PointerEvent === 'function') {
                        found.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        found.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    found.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    found.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                    found.click();
                    return true;
                }

                return false;
            })();
            """);
    }
}

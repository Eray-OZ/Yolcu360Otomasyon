namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
{
    public async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return;

        Report($"Saat seçimi yapılıyor (index {timePickerIndex}): {time}");
        var timeJson = ToJson(time.Trim());
        var indexJson = ToJson(timePickerIndex);

        var opened = await EvaluateScriptAsync(
            $$"""
            (() => {
                const groups = document.querySelectorAll('[modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                if (groups.length > {{indexJson}}) {
                    const group = groups[{{indexJson}}];
                    const timeBox = group.querySelectorAll(':scope > div')[1] || group.querySelector('select, input, div[class*="time"]');
                    if (timeBox) {
                        timeBox.click();
                        return true;
                    }
                }

                const timeElements = Array.from(document.querySelectorAll('div, select, button, input'))
                    .filter(el => {
                        const txt = (el.textContent || el.value || '').trim();
                        const style = window.getComputedStyle(el);
                        const rect = el.getBoundingClientRect();
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && /^\d{2}:\d{2}$/.test(txt);
                    });

                if (timeElements.length > {{indexJson}}) {
                    const targetEl = timeElements[{{indexJson}}];
                    targetEl.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                    targetEl.click();
                    return true;
                }

                return false;
            })();
            """);

        if (!IsScriptTrue(opened))
        {
            Report($"Saat kutusu [{timePickerIndex}] tetiklenemedi veya açılamadı.");
            return;
        }

        await Task.Delay(TimePickerOpenDelay);

        var selected = await EvaluateScriptAsync(
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

        if (IsScriptTrue(selected))
        {
            Report($"Saat seçildi: {time}");
        }
        else
        {
            Report($"Saat '{time}' seçeneklerde bulunamadı.");
        }

        await Task.Delay(TimePickerSelectionDelay);
    }
}

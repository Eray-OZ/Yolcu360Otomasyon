namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> OpenDatePickerAsync()
    {
        var datePickerSelectorJson = ToJson(DatePickerSelector);
        var dateTimeGroupSelectorJson = ToJson(DateTimeGroupSelector);

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const labelEl = Array.from(document.querySelectorAll('span, div, label, p'))
                    .find(el => {
                        const txt = (el.textContent || '').trim();
                        return txt === 'Alış Tarihi' || txt === 'Alış ve Bırakış Tarihi';
                    });
                const pickerFromLabel = labelEl?.closest('.dp__main, [modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                const pickerBySelector = document.querySelector({{datePickerSelectorJson}}) || document.querySelector({{dateTimeGroupSelectorJson}});

                const target = pickerFromLabel || pickerBySelector || labelEl;
                if (!target) return 'false';

                target.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = target.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;

                const triggerEvents = (el) => {
                    if (!el) return;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                    if (typeof PointerEvent === 'function') {
                        el.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                        el.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                    }
                    el.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                    el.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                    el.dispatchEvent(new MouseEvent('click', opts));
                    if (typeof el.click === 'function') el.click();
                };

                triggerEvents(target);
                const innerInput = target.querySelector('input, .dp__input, .dp__icon');
                if (innerInput && innerInput !== target) {
                    triggerEvents(innerInput);
                }

                return 'true';
            })();
            """);

        return IsScriptTrue(result);
    }
}

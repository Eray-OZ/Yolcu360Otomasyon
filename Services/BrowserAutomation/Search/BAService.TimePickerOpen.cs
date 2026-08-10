namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private Task<string?> OpenTimePickerAsync(int timePickerIndex)
    {
        var indexJson = ToJson(timePickerIndex);

        return EvaluateScriptAsync(
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
    }
}

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> ClickFilterOptionAsync(string filterName, string filterPrefix, string[] targetTexts)
    {
        var targetTextsJson = ToJson(targetTexts);
        var filterPrefixJson = ToJson(filterPrefix);

        Report($"{filterName} aranıyor ({string.Join(", ", targetTexts)})...");

        var success = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const targets = {{targetTextsJson}};
                const prefix = {{filterPrefixJson}};

                const normalize = value => (value || '')
                    .toLocaleLowerCase('tr-TR')
                    .replace(/\s+/g, ' ')
                    .trim();

                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                const normalizedTargets = targets.map(normalize);

                let labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."], input[name^="${prefix}."]`)).filter(visible);

                if (labels.length === 0) {
                    labels = Array.from(document.querySelectorAll('label, input[type="checkbox"], input[type="radio"]')).filter(visible);
                }

                const score = text => {
                    if (normalizedTargets.includes(text)) return 0;
                    if (normalizedTargets.some(target => text.startsWith(target + ' '))) return 1;
                    if (normalizedTargets.some(target => text.includes(target))) return 2;
                    return 3;
                };

                const candidates = labels
                    .map(el => {
                        const text = normalize(el.textContent || el.value || el.getAttribute('aria-label') || '');
                        return { el, text };
                    })
                    .filter(item => item.text.length > 0)
                    .sort((a, b) => score(a.text) - score(b.text));

                const match = candidates.find(item => score(item.text) < 3);
                if (!match) return false;

                const targetEl = match.el;
                targetEl.scrollIntoView({ block: 'center', inline: 'nearest' });

                ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(type => {
                    targetEl.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window }));
                });
                targetEl.click();

                const checkbox = targetEl.querySelector?.('input[type="checkbox"], input[type="radio"]') || (targetEl.tagName === 'INPUT' ? targetEl : null);
                if (checkbox && !checkbox.checked) {
                    checkbox.click();
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }

                return true;
            })();
            """);

        Report(success
            ? $"{filterName} başarıyla uygulandı."
            : $"UYARI: {filterName} bulunamadı veya uygulanamadı.");

        return success;
    }
}

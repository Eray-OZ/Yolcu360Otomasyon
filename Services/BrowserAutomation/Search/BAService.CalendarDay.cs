namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task<bool> ClickCalendarDayAsync(DateTime date)
    {
        var dayJson = ToJson(date.Day);
        var monthJson = ToJson(TurkishMonthNames[date.Month - 1]);
        var yearJson = ToJson(date.Year.ToString());

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                    .find(m => {
                        const s = window.getComputedStyle(m);
                        const r = m.getBoundingClientRect();
                        return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                    });
                if (!menu) return false;

                const dayTarget = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget = {{yearJson}};

                const allCalendars = Array.from(menu.querySelectorAll('.dp__calendar'));
                let searchRoot = null;

                for (const cal of allCalendars) {
                    const hdr = cal.querySelector('.dp__month_year_select, .dp__calendar_header_item, .dp__month_year_wrap, .dp__calendar_header');
                    const hdrText = (hdr?.textContent || '').trim();
                    if (hdrText.includes(monthTarget) && hdrText.includes(yearTarget)) {
                        searchRoot = cal;
                        break;
                    }
                }
                if (!searchRoot) searchRoot = menu;

                const selectors = [
                    '.dp__cell_inner',
                    '.dp__calendar_item button',
                    '.dp__calendar_item > div',
                    '.dp__calendar_item'
                ];

                for (const sel of selectors) {
                    const candidates = Array.from(searchRoot.querySelectorAll(sel))
                        .filter(c => {
                            const text = (c.textContent || '').trim();
                            const num = parseInt(text, 10);
                            if (!text || isNaN(num)) return false;
                            const item = c.closest('.dp__calendar_item') ?? c;
                            return !item.classList.contains('dp__cell_offset') &&
                                   !item.classList.contains('dp__cell_disabled') &&
                                   !c.classList.contains('dp__cell_offset') &&
                                   !c.classList.contains('dp__cell_disabled');
                        });

                    const cell = candidates.find(c => parseInt((c.textContent || '').trim(), 10) === dayTarget);
                    if (cell) {
                        cell.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                        const rect = cell.getBoundingClientRect();
                        const x = rect.left + rect.width / 2;
                        const y = rect.top + rect.height / 2;
                        const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                        if (typeof PointerEvent === 'function') {
                            cell.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                            cell.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                        }
                        cell.dispatchEvent(new MouseEvent('mouseover', opts));
                        cell.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                        cell.dispatchEvent(new MouseEvent('mouseup', opts));
                        cell.click();
                        return true;
                    }
                }
                return false;
            })();
            """);

        return IsScriptTrue(result);
    }
}

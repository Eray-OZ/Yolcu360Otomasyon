namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private async Task NavigateToMonthAsync(DateTime target)
    {
        for (var attempt = 0; attempt < 24; attempt++)
        {
            var headerText = await EvaluateScriptAsync(
                """
                (() => {
                    const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                        .find(m => {
                            const s = window.getComputedStyle(m);
                            const r = m.getBoundingClientRect();
                            return s.display !== 'none' && s.visibility !== 'hidden' && r.width > 0;
                        });
                    if (!menu) return '';
                    const headers = Array.from(menu.querySelectorAll('.dp__month_year_select, .dp__calendar_header_item, .dp__month_year_wrap, .dp__calendar_header'));
                    return headers.map(h => (h.textContent || '').trim()).join(' ');
                })();
                """);

            var currentText = (headerText ?? string.Empty).Trim('"');
            Report($"Takvim başlığı: '{currentText}' | Hedef: {target:MMMM yyyy}");

            if (IsTargetMonthVisible(currentText, target))
                return;

            var goBack = ShouldGoBack(currentText, target);
            var navSuccess = await ClickCalendarNavAsync(forward: !goBack);
            if (!navSuccess)
            {
                Report("Takvim yönlendirme butonuna tıklanamadı.");
                break;
            }

            await Task.Delay(CalendarNavigationDelay);
        }
    }

    private async Task<bool> ClickCalendarNavAsync(bool forward)
    {
        var forwardJson = ToJson(forward);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const forward = {{forwardJson}};
                const next = document.querySelector("[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']");
                const prev = document.querySelector("[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']");
                const navBtns = Array.from(document.querySelectorAll('.dp__nav_btn'));

                const btn = forward
                    ? (next || (navBtns.length > 1 ? navBtns[navBtns.length - 1] : navBtns[0]))
                    : (prev || navBtns[0]);

                if (!btn) return false;

                btn.click();
                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task<bool> ClickCalendarDayAsync(DateTime date)
    {
        var dayJson = ToJson(date.Day);
        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };
        var monthJson = ToJson(turkishMonths[date.Month - 1]);
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

    private static bool IsTargetMonthVisible(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        var monthName = turkishMonths[target.Month - 1];
        var yearStr = target.Year.ToString();

        return headerText.Contains(monthName, StringComparison.OrdinalIgnoreCase)
            && headerText.Contains(yearStr);
    }

    private static bool ShouldGoBack(string headerText, DateTime target)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        foreach (var part in headerText.Split(' '))
        {
            if (int.TryParse(part, out var year))
            {
                if (year > target.Year) return true;
                if (year < target.Year) return false;
                break;
            }
        }

        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        for (var i = 0; i < turkishMonths.Length; i++)
        {
            if (headerText.Contains(turkishMonths[i], StringComparison.OrdinalIgnoreCase))
                return (i + 1) > target.Month;
        }

        return false;
    }
}

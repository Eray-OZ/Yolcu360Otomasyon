using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

public sealed partial class EmbeddedBrowserAutomationService
{
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
            selectionApplied = await WaitForPickupLocationSelectionAppliedAsync(TimeSpan.FromSeconds(3));
        }

        if (!selectionApplied)
            throw new InvalidOperationException("Alış yeri önerisi seçilemedi.");

        Report("Alış yeri önerisi seçildi.");
    }

    public async Task SelectDateRangeAsync(DateTime pickupDate, DateTime returnDate)
    {
        Report($"Alış ve Bırakış tarihleri seçiliyor: {pickupDate:dd.MM.yyyy} – {returnDate:dd.MM.yyyy}");

        Report("Tarih seçici açılıyor...");
        var opened = await OpenDatePickerAsync();
        if (!opened)
            throw new InvalidOperationException("Tarih seçici (datepicker) açılamadı.");

        Report("Tarih takvimi bekleniyor...");
        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));

        Report($"Alış tarihi için ay kontrol ediliyor: {pickupDate:MMMM yyyy}");
        await NavigateToMonthAsync(pickupDate);

        Report($"Alış tarihi seçiliyor: {pickupDate:dd.MM.yyyy}");
        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi ({pickupDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Alış tarihi seçildi: {pickupDate:dd.MM.yyyy}");
        await WaitForCalendarSelectionStateAsync(pickupDate, TimeSpan.FromSeconds(2));

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            Report($"Bırakış tarihi için ay geziliyor: {returnDate:MMMM yyyy}");
            await NavigateToMonthAsync(returnDate);
        }

        Report($"Bırakış tarihi seçiliyor: {returnDate:dd.MM.yyyy}");
        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi ({returnDate:dd.MM.yyyy}) takvimde seçilemedi.");

        Report($"Bırakış tarihi seçildi: {returnDate:dd.MM.yyyy}");
        await WaitForCalendarSelectionStateAsync(returnDate, TimeSpan.FromSeconds(2));

        await ConfirmDatePickerAsync();
        await WaitForDatePickerClosedAsync(TimeSpan.FromSeconds(4));
    }

    private async Task<bool> OpenDatePickerAsync()
    {
        var datePickerSelectorJson = JsonSerializer.Serialize(DatePickerSelector);
        var dateTimeGroupSelectorJson = JsonSerializer.Serialize(DateTimeGroupSelector);

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

    private async Task WaitForDatePickerMenuAsync(TimeSpan timeout)
    {
        var menuVisible = await WaitUntilAsync(
            async () => IsScriptTrue(await EvaluateScriptAsync(
                """
                (() => {
                    const menus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap, .dp__calendar'));
                    return menus.some(m => {
                        const rect = m.getBoundingClientRect();
                        const style = window.getComputedStyle(m);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    });
                })();
                """)),
            timeout);

        if (!menuVisible)
            throw new TimeoutException("Tarih seçici takvim menüsü (dp__menu) görünmedi.");
    }

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

            await WaitForCalendarHeaderChangedOrTargetVisibleAsync(currentText, target, TimeSpan.FromSeconds(3));
        }
    }

    private async Task<bool> ClickCalendarNavAsync(bool forward)
    {
        var forwardJson = JsonSerializer.Serialize(forward);
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
        var dayJson = JsonSerializer.Serialize(date.Day);
        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };
        var monthJson = JsonSerializer.Serialize(turkishMonths[date.Month - 1]);
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

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

    private async Task ConfirmDatePickerAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                const selectBtn = document.querySelector('.dp__action_select, button.dp__action_select, .dp__select');
                if (selectBtn) {
                    selectBtn.click();
                    return true;
                }
                return false;
            })();
            """);
    }

    public async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return;

        Report($"Saat seçimi yapılıyor (index {timePickerIndex}): {time}");
        var timeJson = JsonSerializer.Serialize(time.Trim());
        var indexJson = JsonSerializer.Serialize(timePickerIndex);

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

        await WaitForTimeOptionVisibleAsync(time.Trim(), TimeSpan.FromSeconds(5));

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

        await WaitForTimeSelectionAppliedAsync(timePickerIndex, time.Trim(), TimeSpan.FromSeconds(3));
    }

    public async Task ClickSearchButtonAsync()
    {
        Report("Araç Ara butonuna tıklanıyor...");

        await EvaluateScriptAsync(
            """
            (() => {
                if (document.activeElement && typeof document.activeElement.blur === 'function') {
                    document.activeElement.blur();
                }
                const menus = document.querySelectorAll('.dp__menu, .search-autocomplete');
                menus.forEach(m => {
                    if (m.style) m.style.display = 'none';
                });
            })();
            """);

        await WaitForFloatingMenusClosedAsync(TimeSpan.FromSeconds(3));

        var result = await EvaluateScriptAsync(
            """
            (() => {
                const btn = document.querySelector('#search') ||
                            document.querySelector('button[type="submit"]') ||
                            Array.from(document.querySelectorAll('button')).find(b => (b.textContent || '').includes('Ara'));

                if (!btn) return JSON.stringify({ success: false, reason: 'Search button not found' });

                btn.scrollIntoView({ block: 'center', inline: 'center' });

                const rect = btn.getBoundingClientRect();
                const style = window.getComputedStyle(btn);
                const enabled = !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true' &&
                    style.pointerEvents !== 'none' &&
                    rect.width > 0 &&
                    rect.height > 0;

                if (!enabled) {
                    return JSON.stringify({ success: false, reason: 'Button disabled or hidden', text: (btn.textContent || '').trim() });
                }

                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                if (typeof PointerEvent === 'function') {
                    btn.dispatchEvent(new PointerEvent('pointerdown', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true, buttons: 1 }));
                    btn.dispatchEvent(new PointerEvent('pointerup', { ...opts, pointerId: 1, pointerType: 'mouse', isPrimary: true }));
                }
                btn.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                btn.dispatchEvent(new MouseEvent('mouseup', { ...opts }));
                btn.dispatchEvent(new MouseEvent('click', opts));
                if (typeof btn.click === 'function') btn.click();

                return JSON.stringify({ success: true, text: (btn.textContent || '').trim() });
            })();
            """);

        Report($"Araç Ara buton tıklama sonucu: {result}");
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

    private async Task<bool> WaitForPickupLocationSelectionAppliedAsync(TimeSpan timeout)
    {
        return await WaitUntilAsync(IsPickupLocationSelectionAppliedAsync, timeout);
    }

    private Task<bool> WaitForCalendarHeaderChangedOrTargetVisibleAsync(string previousHeader, DateTime target, TimeSpan timeout)
    {
        return WaitUntilAsync(
            async () =>
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
                return !string.Equals(currentText, previousHeader, StringComparison.Ordinal) ||
                    IsTargetMonthVisible(currentText, target);
            },
            timeout);
    }

    private Task<bool> WaitForCalendarSelectionStateAsync(DateTime date, TimeSpan timeout)
    {
        var dayJson = JsonSerializer.Serialize(date.Day);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const day = {{dayJson}};
                const selectedClassNames = ['selected', 'active', 'range_start', 'range_end', 'dp__active_date', 'dp__range_start', 'dp__range_end'];
                const menus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap, .dp__calendar'));
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                return menus.filter(visible).some(menu => {
                    return Array.from(menu.querySelectorAll('.dp__cell_inner, .dp__calendar_item button, .dp__calendar_item > div, .dp__calendar_item'))
                        .some(cell => {
                            const text = (cell.textContent || '').trim();
                            if (parseInt(text, 10) !== day) return false;
                            const classText = `${cell.className || ''} ${cell.closest?.('.dp__calendar_item')?.className || ''}`.toLowerCase();
                            return cell.getAttribute('aria-selected') === 'true' ||
                                selectedClassNames.some(name => classText.includes(name));
                        });
                });
            })();
            """,
            timeout);
    }

    private Task<bool> WaitForDatePickerClosedAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                const visibleMenus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                    .filter(menu => {
                        const rect = menu.getBoundingClientRect();
                        const style = window.getComputedStyle(menu);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    });
                return visibleMenus.length === 0;
            })();
            """,
            timeout);
    }

    private Task<bool> WaitForTimeOptionVisibleAsync(string time, TimeSpan timeout)
    {
        var timeJson = JsonSerializer.Serialize(time);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const target = {{timeJson}};
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                return Array.from(document.querySelectorAll('.dropdown-item, [role="option"], li, .time-option, div[class*="option"], div[class*="item"], div, span, button'))
                    .filter(visible)
                    .some(el => {
                        const text = (el.textContent || el.value || '').trim();
                        return text === target || text.startsWith(target);
                    });
            })();
            """,
            timeout);
    }

    private Task<bool> WaitForTimeSelectionAppliedAsync(int timePickerIndex, string time, TimeSpan timeout)
    {
        var timeJson = JsonSerializer.Serialize(time);
        var indexJson = JsonSerializer.Serialize(timePickerIndex);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const target = {{timeJson}};
                const index = {{indexJson}};
                const groups = document.querySelectorAll('[modaltitle="Alış ve Bırakış Tarihi"], [modaltitlecmskey="pickup_and_dropoff_date"]');
                const group = groups.length > index ? groups[index] : null;
                const text = (group?.textContent || '').trim();
                if (text.includes(target)) return true;

                const inputs = Array.from(document.querySelectorAll('input, select'));
                return inputs.some(input => ((input.value || '').trim() === target));
            })();
            """,
            timeout);
    }

    private Task<bool> WaitForFloatingMenusClosedAsync(TimeSpan timeout)
    {
        return WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };

                return Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap, .search-autocomplete'))
                    .filter(visible)
                    .length === 0;
            })();
            """,
            timeout);
    }

    private async Task WaitForLocationSuggestionsAsync(string selector, TimeSpan timeout)
    {
        string? lastResult = null;
        var selectorJson = JsonSerializer.Serialize(selector);

        var found = await WaitUntilAsync(
            async () =>
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

                return summary.Contains("\"visible\":", StringComparison.OrdinalIgnoreCase) &&
                    !summary.Contains("\"visible\":0", StringComparison.OrdinalIgnoreCase);
            },
            timeout,
            TimeSpan.FromMilliseconds(350));

        if (!found)
            throw new TimeoutException($"Alış yeri önerileri gelmedi. Son durum: {lastResult}");
    }
}

using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
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
                    const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                    const compact = window.__ba?.compactTr || (value => normalize(value).replace(/\s/g, ''));
                    const target = normalize(targetText);
                    const isVisible = window.__ba?.isVisible || (() => false);
                    const getMainText = item => normalize(
                        item.querySelector('strong, .search-autocomplete__item__text-wrapper span:first-child, .search-autocomplete-mobile__item__text-wrapper span:first-child, div > div:first-child')?.textContent || ''
                    );
                    const getScore = item => {
                        const fullText = normalize(item.textContent || '');
                        const mainText = getMainText(item);
                        const compactText = compact(item.textContent || '');

                        if (mainText === target) return 0;
                        if (compactText === compact(`${targetText} Türkiye`) || compactText === compact(`${targetText}, Türkiye`)) return 1;
                        if (fullText === target) return 2;
                        if (mainText.startsWith(target)) return 3;
                        if (fullText.startsWith(target)) return 4;
                        if (mainText.includes(target)) return 5;
                        if (fullText.includes(target)) return 6;
                        return 7;
                    };

                    const items = Array.from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                        .filter(item => isVisible(item) && (!input || (item !== input && !item.contains(input))));
                    const selected = items
                        .sort((a, b) => {
                            const score = getScore(a) - getScore(b);
                            if (score !== 0) return score;
                            const ar = a.getBoundingClientRect();
                            const br = b.getBoundingClientRect();
                            return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                        })[0];

                    if (!selected) return JSON.stringify({ clicked: false, reason: 'öneri bulunamadı', itemCount: items.length });

                    const clickResult = window.__ba.clickLikeUser(selected, {{locationSuggestionSelectorJson}});

                    return JSON.stringify({
                        clicked: clickResult.clicked,
                        selectedText: (selected.textContent || '').replace(/\s+/g, ' ').trim(),
                        pointTargetText: clickResult.pointTargetText,
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

        var opened = await OpenDatePickerAsync();
        if (!opened)
            throw new InvalidOperationException("Tarih seçici (datepicker) açılamadı.");

        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));

        await NavigateToMonthAsync(pickupDate);

        var pickupSelected = await ClickCalendarDayAsync(pickupDate);
        if (!pickupSelected)
            throw new InvalidOperationException($"Alış tarihi ({pickupDate:dd.MM.yyyy}) takvimde seçilemedi.");

        await WaitForCalendarSelectionStateAsync(pickupDate, TimeSpan.FromSeconds(2));

        if (returnDate.Year != pickupDate.Year || returnDate.Month != pickupDate.Month)
        {
            await NavigateToMonthAsync(returnDate);
        }

        var returnSelected = await ClickCalendarDayAsync(returnDate);
        if (!returnSelected)
            throw new InvalidOperationException($"Bırakış tarihi ({returnDate:dd.MM.yyyy}) takvimde seçilemedi.");

        await WaitForCalendarSelectionStateAsync(returnDate, TimeSpan.FromSeconds(2));

        await ConfirmDatePickerAsync();
        await WaitForDatePickerClosedAsync(TimeSpan.FromSeconds(4));
    }

    private async Task<bool> OpenDatePickerAsync()
    {
        var result = await EvaluateScriptAsync(
            """
            (() => {
                const dateRows = document.querySelectorAll('[modaltitlecmskey="pickup_and_dropoff_date"]');
                const target = dateRows[0]?.querySelector('[data-cms-key="pickup_date"]');
                if (!target) return 'false';

                target.scrollIntoView({ block: 'center', inline: 'nearest' });
                target.click();
                target.dispatchEvent(new MouseEvent('click', {
                    bubbles: true,
                    cancelable: true,
                    view: window
                }));

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
            var currentText = await GetVisibleCalendarHeaderAsync();

            if (IsTargetMonthVisible(currentText, target))
                return;

            var goBack = ShouldGoBack(currentText, target);
            var navSuccess = await ClickCalendarNavAsync(forward: !goBack);
            if (!navSuccess)
            {
                break;
            }

            await WaitForCalendarHeaderChangedOrTargetVisibleAsync(currentText, target, TimeSpan.FromSeconds(3));
        }
    }

    private async Task<string> GetVisibleCalendarHeaderAsync()
    {
        var headerText = await EvaluateScriptAsync(
            """
            (() => {
                const isVisible = window.__ba?.isVisible || (() => false);
                const calendar = Array
                    .from(document.querySelectorAll('.dp__instance_calendar'))
                    .find(isVisible);

                if (!calendar) return '';

                const selects = calendar.querySelectorAll('.dp__month_year_select');
                const month = (selects[0]?.textContent || '').trim();
                const year = (selects[1]?.textContent || '').trim();

                return `${month} ${year}`.trim();
            })();
            """);

        return (headerText ?? string.Empty).Trim('"');
    }

    private async Task<bool> ClickCalendarNavAsync(bool forward)
    {
        var buttonSelector = forward
            ? "button[aria-label='Next month']"
            : "button[aria-label='Previous month']";
        var buttonSelectorJson = JsonSerializer.Serialize(buttonSelector);

        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const isVisible = window.__ba?.isVisible || (() => false);
                const button = document.querySelector({{buttonSelectorJson}});

                if (!button ||
                    !isVisible(button) ||
                    button.disabled ||
                    button.getAttribute('aria-disabled') === 'true') {
                    return false;
                }

                button.click();
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
                const dayTarget = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget = {{yearJson}};
                const isVisible = window.__ba?.isVisible || (() => false);
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                const calendar = Array
                    .from(document.querySelectorAll('.dp__instance_calendar'))
                    .find(isVisible);

                if (!calendar) {
                    return false;
                }

                const headerValues = Array
                    .from(calendar.querySelectorAll('.dp__month_year_select'))
                    .map(element => normalize(element.textContent || ''));

                if (!headerValues.includes(normalize(monthTarget)) ||
                    !headerValues.includes(normalize(yearTarget))) {
                    return false;
                }

                const dayCell = Array
                    .from(calendar.querySelectorAll('.dp__calendar_item'))
                    .find(cell => {
                        if (!isVisible(cell)) return false;
                        if (cell.getAttribute('aria-disabled') === 'true') return false;

                        const inner = cell.querySelector('.dp__cell_inner') || cell;
                        if (inner.classList.contains('dp__cell_offset') ||
                            inner.classList.contains('dp__cell_disabled')) {
                            return false;
                        }

                        return parseInt((inner.textContent || '').trim(), 10) === dayTarget;
                    });

                if (!dayCell) {
                    return false;
                }

                const target = dayCell.querySelector('.dp__cell_inner') || dayCell;
                target.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                target.click();
                target.dispatchEvent(new MouseEvent('click', {
                    bubbles: true,
                    cancelable: true,
                    view: window
                }));

                return true;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task ConfirmDatePickerAsync()
    {
        await EvaluateScriptAsync(
            """
            (() => {
                const button = document.querySelector('.dp__action_select, .dp__select');
                if (!button) return false;

                const rect = button.getBoundingClientRect();
                const style = getComputedStyle(button);

                const isClickable = rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
                    !button.disabled &&
                    button.getAttribute('aria-disabled') !== 'true';

                if (!isClickable) return false;

                button.click();
                return true;
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
                const active = document.activeElement;
                if (active && typeof active.blur === 'function') {
                    active.blur();
                }

                document
                    .querySelectorAll('.dp__menu, .search-autocomplete')
                    .forEach(menu => {
                        menu.style.display = 'none';
                    });

                return true;
            })();
            """);

        await WaitForFloatingMenusClosedAsync(TimeSpan.FromSeconds(3));

        var result = await EvaluateScriptAsync(
            """
            (() => {
                const btn = document.querySelector('#search');

                if (!btn) {
                    return JSON.stringify({
                        success: false,
                        reason: 'Search button #search not found'
                    });
                }

                btn.scrollIntoView({ block: 'center', inline: 'center' });

                const rect = btn.getBoundingClientRect();
                const style = getComputedStyle(btn);
                const isClickable =
                    rect.width > 0 &&
                    rect.height > 0 &&
                    style.display !== 'none' &&
                    style.visibility !== 'hidden' &&
                    style.pointerEvents !== 'none' &&
                    !btn.disabled &&
                    btn.getAttribute('aria-disabled') !== 'true';

                if (!isClickable) {
                    return JSON.stringify({
                        success: false,
                        reason: 'Search button exists but is not clickable',
                        text: (btn.textContent || '').trim()
                    });
                }

                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const eventOptions = {
                    bubbles: true,
                    cancelable: true,
                    view: window,
                    clientX: x,
                    clientY: y
                };

                btn.dispatchEvent(new MouseEvent('mousedown', { ...eventOptions, buttons: 1 }));
                btn.dispatchEvent(new MouseEvent('mouseup', eventOptions));
                btn.click();

                return JSON.stringify({
                    success: true,
                    text: (btn.textContent || '').trim()
                });
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
                const pickupInput = document.querySelector({{pickupLocationInputSelectorJson}});

                const isVisible = window.__ba?.isVisible || (() => false);

                const openSuggestions = Array
                    .from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                    .filter(isVisible);

                const hasPickupText = !!pickupInput &&
                    pickupInput.value.trim().length > 0;

                return hasPickupText && openSuggestions.length === 0;
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
                var currentText = await GetVisibleCalendarHeaderAsync();
                return !string.Equals(currentText, previousHeader, StringComparison.Ordinal) ||
                    IsTargetMonthVisible(currentText, target);
            },
            timeout);
    }

    private Task<bool> WaitForCalendarSelectionStateAsync(DateTime date, TimeSpan timeout)
    {
        var dayJson = JsonSerializer.Serialize(date.Day);
        var turkishMonths = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };
        var monthJson = JsonSerializer.Serialize(turkishMonths[date.Month - 1]);
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const day = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget = {{yearJson}};
                const normalize = value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim();
                const compact = value => normalize(value).replace(/\s/g, '');
                const targetMonth = normalize(monthTarget);
                const targetYear = normalize(yearTarget);
                const targetHeaderText = normalize(`${monthTarget} ${yearTarget}`);
                const targetHeaderCompact = compact(`${monthTarget} ${yearTarget}`);
                const selectedClassNames = ['selected', 'active', 'range_start', 'range_end', 'dp__active_date', 'dp__range_start', 'dp__range_end'];
                const visible = el => {
                    const rect = el.getBoundingClientRect();
                    const style = window.getComputedStyle(el);
                    return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                };
                const hasTargetHeader = el => {
                    const text = normalize(el?.textContent || '');
                    const compactText = compact(el?.textContent || '');
                    return text.includes(targetHeaderText) ||
                        compactText.includes(targetHeaderCompact) ||
                        (text.includes(targetMonth) && text.includes(targetYear));
                };
                const menus = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap')).filter(visible);

                const findTargetRoot = menu => {
                    const calendars = Array.from(menu.querySelectorAll('.dp__calendar')).filter(visible);

                    for (const cal of calendars) {
                        const owner = cal.closest('.dp__instance_calendar, .dp__calendar_wrap') || cal.parentElement || cal;
                        const hdr = owner.querySelector('.dp__month_year_select, .dp__month_year_wrap, .dp__month_year_row, .dp__calendar_header');
                        if (hasTargetHeader(hdr) || hasTargetHeader(owner)) return cal;
                    }

                    const section = Array.from(menu.querySelectorAll('.dp__instance_calendar, .dp__calendar_wrap, .dp__month_year_row, .dp__calendar_next, .dp__calendar'))
                        .filter(el => visible(el) && hasTargetHeader(el) && el.querySelector('.dp__calendar, .dp__calendar_item, .dp__cell_inner'))
                        .sort((a, b) => a.getBoundingClientRect().width - b.getBoundingClientRect().width)[0];
                    if (section) return section.querySelector('.dp__calendar') || section;

                    const targetHeader = Array.from(menu.querySelectorAll('.dp__month_year_select, .dp__month_year_wrap, .dp__month_year_row, .dp__calendar_header_item, .dp__calendar_header'))
                        .filter(el => visible(el) && hasTargetHeader(el))[0];
                    if (targetHeader && calendars.length > 0) {
                        const headerRect = targetHeader.getBoundingClientRect();
                        const headerCenterX = headerRect.left + headerRect.width / 2;
                        return calendars
                            .map(cal => {
                                const rect = cal.getBoundingClientRect();
                                const centerX = rect.left + rect.width / 2;
                                return { cal, distance: Math.abs(centerX - headerCenterX) };
                            })
                            .sort((a, b) => a.distance - b.distance)[0]?.cal || null;
                    }

                    if (calendars.length === 1 && hasTargetHeader(menu)) return calendars[0];
                    return null;
                };

                return menus.filter(visible).some(menu => {
                    const root = findTargetRoot(menu);
                    if (!root) return false;
                    return Array.from(root.querySelectorAll('.dp__cell_inner, .dp__calendar_item button, .dp__calendar_item > div, .dp__calendar_item'))
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
                const isVisible = window.__ba?.isVisible || (() => false);

                const visibleDatePickerMenus = Array
                    .from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
                    .filter(isVisible);

                return visibleDatePickerMenus.length === 0;
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
                const isVisible = window.__ba?.isVisible || (() => false);

                return Array
                    .from(document.querySelectorAll('.relative.inline-block li, li'))
                    .filter(isVisible)
                    .some(option => {
                        const text = (option.textContent || '').trim();
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
                const isVisible = window.__ba?.isVisible || (() => false);

                const floatingMenuSelector = [
                    '.dp__menu',
                    '.dp__outer_menu_wrap',
                    '.search-autocomplete'
                ].join(',');

                return Array.from(document.querySelectorAll(floatingMenuSelector))
                    .filter(isVisible)
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
                    const isVisible = window.__ba?.isVisible || (() => false);

                    const suggestions = Array
                        .from(document.querySelectorAll({{selectorJson}}));

                    const visibleSuggestions = suggestions.filter(isVisible);

                    return JSON.stringify({
                        total: suggestions.length,
                        visible: visibleSuggestions.length,
                        text: visibleSuggestions
                            .slice(0, 3)
                            .map(suggestion => (suggestion.textContent || '').replace(/\s+/g, ' ').trim())
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

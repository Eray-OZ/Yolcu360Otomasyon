using System.Text.Json;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private static readonly string[] TurkishMonthNames =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
    };

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

    // Extra - Dropoff Location START
    // Enables Yolcu360 "different dropoff" checkbox and selects the requested dropoff suggestion.
    public async Task FillDropoffLocationAsync(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return;

        var locationJson = JsonSerializer.Serialize(location.Trim());
        var differentDropoffCheckboxSelectorJson = JsonSerializer.Serialize(DifferentDropoffCheckboxSelector);
        var dropoffLocationInputSelectorJson = JsonSerializer.Serialize(DropoffLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);

        Report("Farklı bırakış yeri seçeneği açılıyor...");
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const checkbox = document.querySelector({{differentDropoffCheckboxSelectorJson}});
                if (checkbox && !checkbox.checked) {
                    const label = checkbox.closest('label') || checkbox;
                    label.click();
                    checkbox.checked = true;
                    checkbox.dispatchEvent(new Event('input', { bubbles: true }));
                    checkbox.dispatchEvent(new Event('change', { bubbles: true }));
                }
                return true;
            })();
            """);

        Report("Bırakış yeri inputu bekleniyor...");
        await WaitForScriptTrueAsync(
            $$"""
            (() => !!document.querySelector({{dropoffLocationInputSelectorJson}}))();
            """,
            TimeSpan.FromSeconds(10));

        Report($"Bırakış yeri yazılıyor: {location}");
        await EvaluateScriptAsync(
            $$"""
            (() => {
                const input = document.querySelector({{dropoffLocationInputSelectorJson}});
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

        Report("Bırakış yeri önerileri bekleniyor...");
        await WaitForLocationSuggestionsAsync(LocationSuggestionSelector, TimeSpan.FromSeconds(12));

        var selectionApplied = false;
        for (var attempt = 1; attempt <= 3 && !selectionApplied; attempt++)
        {
            Report($"Bırakış yeri önerisi seçiliyor. Deneme: {attempt}");
            var selected = await EvaluateScriptAsync(
                $$"""
                (() => {
                    const input = document.querySelector({{dropoffLocationInputSelectorJson}});
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

            Report($"Bırakış yeri seçim sonucu: {selected}");
            selectionApplied = await WaitForDropoffLocationSelectionAppliedAsync(TimeSpan.FromSeconds(3));
        }

        if (!selectionApplied)
            throw new InvalidOperationException("Bırakış yeri önerisi seçilemedi.");

        Report("Bırakış yeri önerisi seçildi.");
    }
    // Extra - Dropoff Location END

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
        var monthJson = JsonSerializer.Serialize(TurkishMonthNames[date.Month - 1]);
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
                    .from(calendar.querySelectorAll('.dp__calendar_item[aria-disabled="false"]'))
                    .find(cell => {
                        const inner = cell.querySelector('.dp__cell_inner');

                        return inner &&
                            !inner.classList.contains('dp__cell_offset') &&
                            parseInt(inner.textContent.trim(), 10) === dayTarget;
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

    public async Task SelectTimeAsync(int timePickerIndex, string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return;

        Report($"Saat seçimi yapılıyor (index {timePickerIndex}): {time}");
        var timeJson = JsonSerializer.Serialize(time.Trim());
        var labelJson = JsonSerializer.Serialize(timePickerIndex == 0 ? "Alış Saati" : "Bırakış Saati");

        var selected = await WaitUntilAsync(
            async () => await EvaluateBooleanScriptAsync(
                $$"""
                (() => {
                    const target = {{timeJson}};
                    const labelText = {{labelJson}};
                    const isVisible = window.__ba?.isVisible || (() => false);
                    const label = Array
                        .from(document.querySelectorAll('span'))
                        .find(item => isVisible(item) && item.textContent.trim() === labelText);
                    const timeRoot = label?.closest('.flex.flex-col.min-w-0');
                    const timeBox = timeRoot?.querySelector('.relative.inline-block .cursor-pointer');

                    if (!timeRoot || !timeBox) {
                        return false;
                    }

                    const dropdown = Array
                        .from(timeRoot?.querySelectorAll('.absolute.z-50') || [])
                        .find(isVisible);

                    if (!dropdown) {
                        timeBox.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                        timeBox.click();
                        return false;
                    }

                    const option = Array
                        .from(dropdown.querySelectorAll('li.cursor-pointer'))
                        .find(item => isVisible(item) && item.textContent.trim() === target);

                    if (!option) {
                        return false;
                    }

                    option.scrollIntoView({ block: 'nearest', inline: 'nearest' });
                    option.click();
                    return true;
                })();
                """),
            TimeSpan.FromSeconds(5));

        if (selected)
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

                const style = getComputedStyle(btn);
                const isVisible = window.__ba?.isVisible || (() => false);
                const isClickable =
                    isVisible(btn) &&
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

                const rect = btn.getBoundingClientRect();
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

        var monthName = TurkishMonthNames[target.Month - 1];
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

        for (var i = 0; i < TurkishMonthNames.Length; i++)
        {
            if (headerText.Contains(TurkishMonthNames[i], StringComparison.OrdinalIgnoreCase))
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

    // Extra - Dropoff Location START
    // Verifies that Yolcu360 accepted the optional dropoff location selection.
    private async Task<bool> IsDropoffLocationSelectionAppliedAsync()
    {
        var dropoffLocationInputSelectorJson = JsonSerializer.Serialize(DropoffLocationInputSelector);
        var locationSuggestionSelectorJson = JsonSerializer.Serialize(LocationSuggestionSelector);
        var result = await EvaluateScriptAsync(
            $$"""
            (() => {
                const dropoffInput = document.querySelector({{dropoffLocationInputSelectorJson}});
                const isVisible = window.__ba?.isVisible || (() => false);

                const openSuggestions = Array
                    .from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
                    .filter(isVisible);

                const hasDropoffText = !!dropoffInput &&
                    dropoffInput.value.trim().length > 0;

                return hasDropoffText && openSuggestions.length === 0;
            })();
            """);

        return IsScriptTrue(result);
    }

    private async Task<bool> WaitForDropoffLocationSelectionAppliedAsync(TimeSpan timeout)
    {
        return await WaitUntilAsync(IsDropoffLocationSelectionAppliedAsync, timeout);
    }
    // Extra - Dropoff Location END

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
        var monthJson = JsonSerializer.Serialize(TurkishMonthNames[date.Month - 1]);
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

        return WaitForScriptTrueOrTimeoutAsync(
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
                    .from(calendar.querySelectorAll('.dp__calendar_item[aria-disabled="false"]'))
                    .find(cell => {
                        const inner = cell.querySelector('.dp__cell_inner');

                        return inner &&
                            !inner.classList.contains('dp__cell_offset') &&
                            parseInt(inner.textContent.trim(), 10) === dayTarget;
                    });

                return dayCell?.getAttribute('aria-selected') === 'true';
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

    private Task<bool> WaitForTimeSelectionAppliedAsync(int timePickerIndex, string time, TimeSpan timeout)
    {
        var timeJson = JsonSerializer.Serialize(time);
        var labelJson = JsonSerializer.Serialize(timePickerIndex == 0 ? "Alış Saati" : "Bırakış Saati");

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                const target = {{timeJson}};
                const labelText = {{labelJson}};
                const isVisible = window.__ba?.isVisible || (() => false);
                const label = Array
                    .from(document.querySelectorAll('span'))
                    .find(item => isVisible(item) && item.textContent.trim() === labelText);
                const timeRoot = label?.closest('.flex.flex-col.min-w-0');
                const text = (timeRoot?.textContent || '').trim();
                return text.includes(target);
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

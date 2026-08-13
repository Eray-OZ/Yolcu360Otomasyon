using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private const string Yolcu360FlightUrl = "https://www.yolcu360.com/ucak-bileti";
    private const string FlightFromInputSelector = "#inputPickUpLocation";
    private const string FlightToInputSelector = "#inputDropOffLocation";
    private const string FlightLocationSuggestionSelector = ".search-autocomplete.w-full .search-autocomplete__item.location-item, .search-autocomplete__item.location-item, .search-autocomplete .location-item";
    private const string FlightSearchButtonSelector = "#flight_search";
    private const string FlightInputResolverScript =
        """
        const resolveFlightInput = selector => {
            const isVisible = window.__ba?.isVisible || (() => false);
            const looksLikeLocationInput = input => {
                if (!input) return false;
                const attrs = `${input.id || ''} ${input.name || ''} ${input.placeholder || ''} ${input.getAttribute('data-cms-key') || ''} ${input.className || ''}`.toLocaleLowerCase('tr-TR');
                return attrs.includes('pickup') ||
                    attrs.includes('dropoff') ||
                    attrs.includes('location') ||
                    attrs.includes('airport') ||
                    attrs.includes('havalimanı') ||
                    attrs.includes('şehir') ||
                    attrs.includes('city');
            };

            const allMatches = Array.from(document.querySelectorAll(selector));
            const exactMatch = allMatches.find(input =>
                input.closest('#inputPickUpLocation-label, #inputDropOffLocation-label') &&
                isVisible(input) &&
                !input.disabled &&
                input.getAttribute('readonly') === null
            );
            if (exactMatch) return exactMatch;

            const flightMatches = allMatches.filter(input =>
                input.closest('.flight-search-bar-wrapper, .flight-search-bar__search') &&
                isVisible(input) &&
                !input.disabled &&
                input.getAttribute('readonly') === null
            );
            const selectorMatch = flightMatches[0] || allMatches.find(input =>
                isVisible(input) &&
                !input.disabled &&
                input.getAttribute('readonly') === null
            );
            if (selectorMatch) return selectorMatch;

            return Array
                .from(document.querySelectorAll('input'))
                .filter(input => isVisible(input) && !input.disabled && input.getAttribute('readonly') === null)
                .find(looksLikeLocationInput) || allMatches[0] || null;
        };
        """;

    public async Task SearchFlightTicketsAsync(FlightSearchFilter filter)
    {
        if (filter is null)
            throw new InvalidOperationException("Uçuş arama filtresi boş olamaz.");

        if (string.IsNullOrWhiteSpace(filter.FromLocation) || string.IsNullOrWhiteSpace(filter.ToLocation))
            throw new InvalidOperationException("Uçuş araması için nereden ve nereye alanları zorunlu.");

        Report("Yolcu360 uçak bileti sayfası açılıyor...");
        await NavigateAsync(Yolcu360FlightUrl);
        await WaitForDocumentReadyAsync();
        await EnsureJavaScriptHelpersAsync();
        await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));

        await SelectFlightTripTypeAsync(filter.IsRoundTrip);
        await FillFlightLocationAsync(FlightFromInputSelector, filter.FromLocation, "Nereden");
        await FillFlightLocationAsync(FlightToInputSelector, filter.ToLocation, "Nereye");
        await SelectFlightDateAsync("flight_departure_date", filter.DepartureDate, "Gidiş tarihi");

        if (filter.IsRoundTrip && filter.ReturnDate is not null)
            await SelectFlightDateAsync("flight_return_date", filter.ReturnDate.Value, "Dönüş tarihi");

        if (filter.OnlyNonStop)
            await ToggleFlightOnlyNonStopAsync();

        await ClickFlightSearchButtonAsync();
    }

    private async Task SelectFlightTripTypeAsync(bool isRoundTrip)
    {
        var targetCmsKeyJson = JsonSerializer.Serialize(isRoundTrip ? "flight_type_2" : "flight_type_1");
        Report(isRoundTrip ? "Uçuş tipi Gidiş-Dönüş seçiliyor..." : "Uçuş tipi Tek Yön seçiliyor...");

        var clicked = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const cmsKey = {{targetCmsKeyJson}};
                const container = document.querySelector(`[data-cms-key="${cmsKey}"]`);
                const label = container?.querySelector('label') || container;
                if (!window.__ba?.isVisible(label)) return false;

                label.click();
                return true;
            })();
            """);

        if (!clicked)
            throw new InvalidOperationException("Uçuş tipi seçilemedi.");
    }

    private async Task FillFlightLocationAsync(string selector, string location, string fieldName)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var locationJson = JsonSerializer.Serialize(location.Trim());

        await OpenFlightLocationFieldAsync(selector, fieldName);

        Report($"{fieldName} alanı bekleniyor...");
        await WaitForScriptTrueAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{selectorJson}});
                return !!window.__ba?.isVisible(input) &&
                    !input.disabled &&
                    input.getAttribute('readonly') === null;
            })();
            """,
            TimeSpan.FromSeconds(20));

        Report($"{fieldName} yazılıyor: {location}");
        var fillResult = await EvaluateScriptAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{selectorJson}});
                const text = {{locationJson}};
                if (!input) {
                    return JSON.stringify({
                        success: false,
                        reason: 'input bulunamadı'
                    });
                }

                input.focus();
                input.click();
                try { input.setSelectionRange(0, (input.value || '').length); } catch {}

                const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
                const setValue = value => {
                    if (descriptor?.set) {
                        descriptor.set.call(input, value);
                    } else {
                        input.value = value;
                    }
                };

                input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Meta', metaKey: true }));
                input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'a', metaKey: true }));
                input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'a', metaKey: true }));
                input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'Meta' }));
                input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: 'Backspace' }));
                setValue('');
                input.setAttribute('value', '');
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'deleteContentBackward', data: null }));
                input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: 'Backspace' }));

                let current = '';
                for (const char of text) {
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, key: char }));
                    current += char;
                    setValue(current);
                    input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: char }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, key: char }));
                }

                setValue(text);
                input.setAttribute('value', text);
                input.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertReplacementText', data: text }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
                return JSON.stringify({
                    success: (input.value || '').trim() === text.trim(),
                    value: input.value || '',
                    expected: text,
                    id: input.id || '',
                    activeId: document.activeElement?.id || '',
                    visible: !!window.__ba?.isVisible(input),
                    wrapper: input.closest('.flight-search-bar-wrapper, .flight-search-bar__search') ? 'flight' : 'unknown'
                });
            })();
            """);

        Report($"{fieldName} yazma sonucu: {fillResult}");

        var valueApplied = await WaitForFlightInputValueAsync(selector, location, TimeSpan.FromSeconds(3));
        if (!valueApplied)
            throw new InvalidOperationException($"{fieldName} inputuna değer yazılamadı.");

        await WaitForFlightLocationSuggestionsAsync(fieldName, location, TimeSpan.FromSeconds(12));

        var selected = false;
        for (var attempt = 1; attempt <= 3 && !selected; attempt++)
        {
            Report($"{fieldName} önerisi seçiliyor. Deneme: {attempt}");
            var selectionResult = await ClickFlightLocationSuggestionAsync(selector, location);
            Report($"{fieldName} öneri seçim sonucu: {selectionResult}");
            selected = await WaitForFlightLocationSelectionAppliedAsync(selector, TimeSpan.FromSeconds(3));
        }

        if (!selected)
            throw new InvalidOperationException($"{fieldName} önerisi seçilemedi.");
    }

    private async Task OpenFlightLocationFieldAsync(string selector, string fieldName)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var labelSelector = selector == FlightFromInputSelector
            ? "#inputPickUpLocation-label, [data-cms-key='flight_welcome_from']"
            : "#inputDropOffLocation-label, [data-cms-key='flight_welcome_to']";
        var labelSelectorJson = JsonSerializer.Serialize(labelSelector);

        Report($"{fieldName} alanı açılıyor...");
        var openResult = await EvaluateScriptAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const isVisible = window.__ba?.isVisible || (() => false);
                const input = resolveFlightInput({{selectorJson}});
                const label = Array
                    .from(document.querySelectorAll({{labelSelectorJson}}))
                    .map(item => item.closest('label') || item)
                    .find(isVisible);

                const target = label || input;
                if (!target) {
                    return JSON.stringify({
                        opened: false,
                        reason: 'label/input bulunamadı'
                    });
                }

                target.scrollIntoView({ block: 'center', inline: 'nearest' });
                target.click();

                const resolvedAfterClick = resolveFlightInput({{selectorJson}});
                if (resolvedAfterClick) {
                    resolvedAfterClick.focus();
                    resolvedAfterClick.click();
                }

                return JSON.stringify({
                    opened: true,
                    targetText: (target.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120),
                    inputId: resolvedAfterClick?.id || '',
                    inputPlaceholder: resolvedAfterClick?.placeholder || '',
                    inputValue: resolvedAfterClick?.value || '',
                    activeTag: document.activeElement?.tagName || '',
                    activeId: document.activeElement?.id || ''
                });
            })();
            """);

        Report($"{fieldName} alan açma sonucu: {openResult}");
    }

    private async Task<string?> ClickFlightLocationSuggestionAsync(string inputSelector, string location)
    {
        var inputSelectorJson = JsonSerializer.Serialize(inputSelector);
        var suggestionSelectorJson = JsonSerializer.Serialize(FlightLocationSuggestionSelector);
        var locationJson = JsonSerializer.Serialize(location.Trim());

        return await EvaluateScriptAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{inputSelectorJson}});
                const targetText = {{locationJson}};
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                const compact = window.__ba?.compactTr || (value => normalize(value).replace(/\s/g, ''));
                const isVisible = window.__ba?.isVisible || (() => false);
                const target = normalize(targetText);

                const getMainText = item => normalize(
                    item.querySelector('strong, div > div:first-child, span:first-child')?.textContent || ''
                );

                const getScore = item => {
                    const fullText = normalize(item.textContent || '');
                    const mainText = getMainText(item);
                    const compactText = compact(item.textContent || '');

                    if (mainText === target) return 0;
                    if (compactText === compact(targetText)) return 1;
                    if (fullText === target) return 2;
                    if (mainText.startsWith(target)) return 3;
                    if (fullText.startsWith(target)) return 4;
                    if (mainText.includes(target)) return 5;
                    if (fullText.includes(target)) return 6;
                    return 7;
                };

                const visibleItems = Array
                    .from(document.querySelectorAll({{suggestionSelectorJson}}))
                    .filter(item => {
                        if (!isVisible(item) || (input && (item === input || item.contains(input)))) {
                            return false;
                        }

                        return true;
                    });

                const matchingItems = visibleItems.filter(item => {
                    const fullText = normalize(item.textContent || '');
                    const mainText = getMainText(item);
                    const compactText = compact(item.textContent || '');
                    const compactTarget = compact(targetText);

                    return fullText.includes(target) ||
                        target.includes(fullText) ||
                        mainText.includes(target) ||
                        target.includes(mainText) ||
                        compactText.includes(compactTarget) ||
                        compactTarget.includes(compactText);
                });

                const selected = (matchingItems.length > 0 ? matchingItems : visibleItems)
                    .sort((a, b) => {
                        const score = getScore(a) - getScore(b);
                        if (score !== 0) return score;

                        const ar = a.getBoundingClientRect();
                        const br = b.getBoundingClientRect();
                        return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                    })[0];

                if (!selected) {
                    return JSON.stringify({
                        clicked: false,
                        reason: 'öneri bulunamadı',
                        itemCount: visibleItems.length
                    });
                }

                selected.scrollIntoView({ block: 'center', inline: 'nearest' });
                const rect = selected.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const pointTarget = document.elementFromPoint(x, y);
                const targetElement = pointTarget?.closest?.({{suggestionSelectorJson}}) || pointTarget || selected;

                const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };
                selected.dispatchEvent(new MouseEvent('mouseover', opts));
                selected.dispatchEvent(new MouseEvent('mousemove', opts));
                selected.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
                selected.dispatchEvent(new MouseEvent('mouseup', opts));
                selected.dispatchEvent(new MouseEvent('click', opts));
                if (typeof selected.click === 'function') selected.click();

                const clickResult = {
                    clicked: true,
                    pointTargetText: (targetElement?.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120)
                };

                return JSON.stringify({
                    clicked: !!clickResult.clicked,
                    selectedText: (selected.textContent || '').replace(/\s+/g, ' ').trim(),
                    pointTargetText: clickResult.pointTargetText || '',
                    inputValue: input?.value || '',
                    remainingSuggestions: document.querySelectorAll({{suggestionSelectorJson}}).length,
                    visibleCount: visibleItems.length,
                    matchingCount: matchingItems.length
                });
            })();
            """);
    }

    private Task<bool> WaitForFlightInputValueAsync(string selector, string expectedValue, TimeSpan timeout)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var expectedJson = JsonSerializer.Serialize(expectedValue.Trim());

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{selectorJson}});
                const expected = {{expectedJson}};
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());

                const actual = normalize(input.value || '');
                const target = normalize(expected);

                return !!input &&
                    actual === target;
            })();
            """,
            timeout,
            TimeSpan.FromMilliseconds(250));
    }

    private Task<bool> WaitForFlightLocationSelectionAppliedAsync(string inputSelector, TimeSpan timeout)
    {
        var inputSelectorJson = JsonSerializer.Serialize(inputSelector);
        var suggestionSelectorJson = JsonSerializer.Serialize(FlightLocationSuggestionSelector);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{inputSelectorJson}});
                const isVisible = window.__ba?.isVisible || (() => false);
                const openSuggestions = Array
                    .from(document.querySelectorAll({{suggestionSelectorJson}}))
                    .filter(isVisible);

                return !!input &&
                    (input.value || '').trim().length > 0 &&
                    openSuggestions.length === 0;
            })();
            """,
            timeout);
    }

    private async Task WaitForFlightLocationSuggestionsAsync(string fieldName, string location, TimeSpan timeout)
    {
        var suggestionSelectorJson = JsonSerializer.Serialize(FlightLocationSuggestionSelector);
        var locationJson = JsonSerializer.Serialize(location.Trim());
        string? lastResult = null;

        var found = await WaitUntilAsync(
            async () =>
            {
                lastResult = await EvaluateScriptAsync(
                    $$"""
                    (() => {
                        const isVisible = window.__ba?.isVisible || (() => false);
                        const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                        const target = normalize({{locationJson}});
                        const suggestions = Array.from(document.querySelectorAll({{suggestionSelectorJson}}));
                        const visibleSuggestions = suggestions.filter(isVisible);
                        const matchingSuggestions = visibleSuggestions.filter(item => {
                            const text = normalize(item.textContent || '');
                            return text.includes(target) || target.includes(text);
                        });

                        return JSON.stringify({
                            total: suggestions.length,
                            visible: visibleSuggestions.length,
                            matching: matchingSuggestions.length,
                            text: visibleSuggestions
                                .slice(0, 3)
                                .map(item => (item.textContent || '').replace(/\s+/g, ' ').trim())
                        });
                    })();
                    """);

                var summary = (lastResult ?? string.Empty).Trim('"');
                var hasVisibleSuggestion = summary.Contains("\"visible\":", StringComparison.OrdinalIgnoreCase) &&
                    !summary.Contains("\"visible\":0", StringComparison.OrdinalIgnoreCase);

                if (hasVisibleSuggestion)
                    Report($"{fieldName} önerileri bulundu: {summary}");

                return hasVisibleSuggestion;
            },
            timeout,
            TimeSpan.FromMilliseconds(350));

        if (!found)
            throw new TimeoutException($"{fieldName} önerileri gelmedi. Son durum: {lastResult}");
    }

    private async Task SelectFlightDateAsync(string triggerCmsKey, DateTime date, string fieldName)
    {
        var triggerCmsKeyJson = JsonSerializer.Serialize(triggerCmsKey);
        var dayJson = JsonSerializer.Serialize(date.Day);
        var monthJson = JsonSerializer.Serialize(GetTurkishMonthName(date.Month));
        var yearJson = JsonSerializer.Serialize(date.Year.ToString());

        Report($"{fieldName} açılıyor: {date:dd.MM.yyyy}");
        var opened = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const cmsKey = {{triggerCmsKeyJson}};
                const trigger = Array
                    .from(document.querySelectorAll(`[triggerlabelcmskey="${cmsKey}"], [modaltitlecmskey="${cmsKey}"]`))
                    .find(window.__ba?.isVisible || (() => false));

                if (!trigger) return false;

                trigger.scrollIntoView({ block: 'center', inline: 'nearest' });
                trigger.click();
                return true;
            })();
            """);

        if (!opened)
            throw new InvalidOperationException($"{fieldName} alanı açılamadı.");

        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));
        await NavigateToMonthAsync(date);

        var selected = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const dayTarget = {{dayJson}};
                const monthTarget = {{monthJson}};
                const yearTarget = {{yearJson}};
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());
                const compact = window.__ba?.compactTr || (value => normalize(value).replace(/\s/g, ''));
                const isVisible = window.__ba?.isVisible || (() => false);
                const targetHeaderText = normalize(`${monthTarget} ${yearTarget}`);
                const targetHeaderCompact = compact(`${monthTarget} ${yearTarget}`);

                const hasTargetHeader = el => {
                    const text = normalize(el?.textContent || '');
                    const compactText = compact(el?.textContent || '');
                    return text.includes(targetHeaderText) || compactText.includes(targetHeaderCompact);
                };

                const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap')).find(isVisible);
                if (!menu) return false;

                const calendars = Array.from(menu.querySelectorAll('.dp__calendar')).filter(isVisible);
                let root = calendars.find(calendar => {
                    const owner = calendar.closest('.dp__instance_calendar, .dp__calendar_wrap') || calendar.parentElement || calendar;
                    return hasTargetHeader(owner);
                }) || calendars[0];

                if (!root) return false;

                const cell = Array.from(root.querySelectorAll('.dp__cell_inner, .dp__calendar_item button, .dp__calendar_item > div, .dp__calendar_item'))
                    .filter(isVisible)
                    .find(candidate => {
                        const text = (candidate.textContent || '').trim();
                        const item = candidate.closest('.dp__calendar_item') ?? candidate;
                        return parseInt(text, 10) === dayTarget &&
                            !item.classList.contains('dp__cell_offset') &&
                            !item.classList.contains('dp__cell_disabled') &&
                            !candidate.classList.contains('dp__cell_offset') &&
                            !candidate.classList.contains('dp__cell_disabled');
                    });

                if (!cell) return false;

                const clickResult = window.__ba.clickLikeUser
                    ? window.__ba.clickLikeUser(cell)
                    : (cell.click(), { clicked: true });

                return !!clickResult.clicked;
            })();
            """);

        if (!selected)
            throw new InvalidOperationException($"{fieldName} seçilemedi.");

        await WaitForDatePickerClosedAsync(TimeSpan.FromSeconds(4));
    }

    private async Task ToggleFlightOnlyNonStopAsync()
    {
        Report("Sadece aktarmasız uçuş filtresi seçiliyor...");

        await EvaluateBooleanScriptAsync(
            """
            (() => {
                const label = Array
                    .from(document.querySelectorAll('[data-cms-key="flight_show_only_nonstop"] label, label'))
                    .find(item => {
                        const text = (item.textContent || '').toLocaleLowerCase('tr-TR');
                        return window.__ba?.isVisible(item) && text.includes('aktarmasız');
                    });

                if (!label) return false;

                const input = label.querySelector('input[type="checkbox"]');
                if (input && input.checked) return true;

                label.click();
                return true;
            })();
            """);
    }

    private async Task ClickFlightSearchButtonAsync()
    {
        Report("Uçuş Ara butonuna tıklanıyor...");

        var clicked = await EvaluateBooleanScriptAsync(
            $$"""
            (() => {
                const button = document.querySelector({{JsonSerializer.Serialize(FlightSearchButtonSelector)}});
                if (!window.__ba?.isVisible(button)) return false;

                button.scrollIntoView({ block: 'center', inline: 'nearest' });
                button.click();
                return true;
            })();
            """);

        if (!clicked)
            throw new InvalidOperationException("Uçuş Ara butonu tıklanamadı.");
    }

    private static string GetTurkishMonthName(int month)
    {
        var months = new[]
        {
            "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        };

        return months[month - 1];
    }
}

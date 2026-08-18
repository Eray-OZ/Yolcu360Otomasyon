using System.Text.Json;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon.Services;

public sealed partial class BAService
{
    private const string Yolcu360FlightUrl = "https://www.yolcu360.com/ucak-bileti";
    private const string FlightFromInputSelector = "#inputPickUpLocation";
    private const string FlightToInputSelector = "#inputDropOffLocation";
    private const string FlightLocationSuggestionSelector = ".search-autocomplete .search-autocomplete__item, .search-autocomplete__item.location-item, .search-autocomplete .location-item, .search-autocomplete-mobile__item, .location-item";
    private const string FlightSearchButtonSelector = "#flight_search";
    private const string FlightInputResolverScript =
        """
        const resolveFlightInput = selector => {
            const isVisible = window.__ba?.isVisible || (() => false);
            const direct = document.querySelector(selector);
            if (direct && isVisible(direct)) return direct;

            const labelSelector = selector === '#inputPickUpLocation'
                ? '#inputPickUpLocation-label'
                : selector === '#inputDropOffLocation'
                    ? '#inputDropOffLocation-label'
                    : '';

            if (labelSelector) {
                const label = document.querySelector(labelSelector);
                const inside = label?.querySelector('input') || label?.querySelector(selector);
                if (inside && isVisible(inside)) return inside;
            }

            if (direct) return direct;

            const allInputs = Array.from(document.querySelectorAll('input')).filter(isVisible);
            if (selector === '#inputPickUpLocation') {
                return allInputs.find(i => (i.id || '').toLowerCase().includes('pickup') || (i.placeholder || '').toLowerCase().includes('kalkış') || (i.placeholder || '').toLowerCase().includes('nereden')) || allInputs[0] || null;
            }
            if (selector === '#inputDropOffLocation') {
                return allInputs.find(i => (i.id || '').toLowerCase().includes('dropoff') || (i.placeholder || '').toLowerCase().includes('varış') || (i.placeholder || '').toLowerCase().includes('nereye')) || allInputs[1] || null;
            }
            return null;
        };
        """;

    public async Task SearchFlightTicketsAsync(FlightSearchFilter filter)
    {
        if (filter is null)
            throw new InvalidOperationException("Uçuş arama filtresi boş olamaz.");

        if (string.IsNullOrWhiteSpace(filter.FromLocation) || string.IsNullOrWhiteSpace(filter.ToLocation))
            throw new InvalidOperationException("Uçuş araması için nereden ve nereye alanları zorunlu.");

        if (!string.IsNullOrWhiteSpace(filter.FromPlaceId) &&
            !string.IsNullOrWhiteSpace(filter.ToPlaceId) &&
            !string.IsNullOrWhiteSpace(filter.FromPlaceCode) &&
            !string.IsNullOrWhiteSpace(filter.ToPlaceCode))
        {
            var directSearchUrl = BuildFlightSearchUrl(filter);
            Report($"Uçuş arama sayfası açılıyor: {filter.FromLocation} ({filter.FromPlaceCode}) → {filter.ToLocation} ({filter.ToPlaceCode})");
            await NavigateAsync(directSearchUrl);
            await WaitForDocumentReadyAsync();
            await EnsureJavaScriptHelpersAsync();
            await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));

            if (filter.OnlyNonStop)
                await ToggleFlightOnlyNonStopAsync();

            await WaitForFlightResultsAsync(TimeSpan.FromSeconds(35));
            return;
        }

        Report("Yolcu360 uçak bileti sayfası açılıyor...");
        await NavigateAsync(Yolcu360FlightUrl);
        await WaitForDocumentReadyAsync();
        await EnsureJavaScriptHelpersAsync();
        await WaitForInitialPopupAndCloseAsync(TimeSpan.FromSeconds(5));

        await SelectFlightTripTypeAsync(filter.IsRoundTrip);
        await FillFlightLocationAsync(FlightFromInputSelector, filter.FromLocation, "Nereden");
        await FillFlightLocationAsync(FlightToInputSelector, filter.ToLocation, "Nereye");
        await SelectFlightDatesAsync(filter.DepartureDate, filter.ReturnDate, filter.IsRoundTrip);

        if (filter.OnlyNonStop)
            await ToggleFlightOnlyNonStopAsync();

        var previousResultsSignature = await GetFlightResultsSignatureAsync();
        await ClickFlightSearchButtonAsync();
        await WaitForFlightResultsChangedAsync(previousResultsSignature, TimeSpan.FromSeconds(35));
    }

    private static string BuildFlightSearchUrl(FlightSearchFilter filter)
    {
        var departureDateStr = filter.DepartureDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var queryParams = new List<string>
        {
            $"p_d={departureDateStr}",
            "sb=lowest_price_first",
            $"p_p={Uri.EscapeDataString(filter.FromPlaceCode ?? string.Empty)}",
            $"ppt={Uri.EscapeDataString(filter.FromPlaceType ?? "airport")}",
            $"pid={Uri.EscapeDataString(filter.FromPlaceId ?? string.Empty)}",
            $"d_p={Uri.EscapeDataString(filter.ToPlaceCode ?? string.Empty)}",
            $"dpt={Uri.EscapeDataString(filter.ToPlaceType ?? "airport")}",
            $"did={Uri.EscapeDataString(filter.ToPlaceId ?? string.Empty)}",
            "fc=economy",
            "pa=1"
        };

        if (filter.IsRoundTrip && filter.ReturnDate is not null)
        {
            var returnDateStr = filter.ReturnDate.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            queryParams.Add($"d_d={returnDateStr}");
        }

        return $"https://yolcu360.com/ucak-bileti/search?{string.Join("&", queryParams)}";
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
        var searchText = BuildFlightLocationSearchText(location);
        var searchTextJson = JsonSerializer.Serialize(searchText);

        Report($"{fieldName} alanı açılıyor...");
        await OpenFlightLocationFieldAsync(selector, fieldName);

        Report($"{fieldName} alanına yazılıyor: {searchText}");
        var writeResult = await FillFlightLocationInputAsync(selectorJson, searchTextJson);
        Report($"{fieldName} yazma sonucu: {writeResult}");

        Report($"{fieldName} önerileri bekleniyor...");
        await WaitForFlightLocationSuggestionsAsync(fieldName, location, TimeSpan.FromSeconds(12));

        var selected = false;
        for (var attempt = 1; attempt <= 3 && !selected; attempt++)
        {
            Report($"{fieldName} önerisi seçiliyor. Deneme: {attempt}");
            var selectResult = await ClickFlightLocationSuggestionAsync(selector, location);
            Report($"{fieldName} öneri seçim sonucu: {selectResult}");

            var clicked = false;
            try
            {
                using var doc = JsonDocument.Parse((selectResult ?? string.Empty).Trim('"'));
                clicked = doc.RootElement.TryGetProperty("clicked", out var clickedElem) && clickedElem.ValueKind == JsonValueKind.True;
            }
            catch {}

            if (clicked)
            {
                selected = await WaitForFlightLocationSelectionAppliedAsync(selector, location, TimeSpan.FromSeconds(4));
            }

            if (!selected && attempt < 3)
            {
                await OpenFlightLocationFieldAsync(selector, fieldName);
                await FillFlightLocationInputAsync(selectorJson, searchTextJson);
                await WaitForFlightLocationSuggestionsAsync(fieldName, location, TimeSpan.FromSeconds(5));
            }
        }

        if (!selected)
            throw new InvalidOperationException($"{fieldName} önerisi seçilemedi ({location}).");

        Report($"{fieldName} başarıyla seçildi: {location}");
    }

    private async Task<string?> FillFlightLocationInputAsync(string selectorJson, string searchTextJson)
    {
        return await EvaluateScriptAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{selectorJson}});
                const text = {{searchTextJson}};
                if (!input) {
                    return JSON.stringify({
                        success: false,
                        reason: 'input bulunamadı'
                    });
                }

                input.scrollIntoView({ block: 'center', inline: 'nearest' });
                input.focus();
                input.click();

                const nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value')?.set
                    || Object.getOwnPropertyDescriptor(Object.getPrototypeOf(input), 'value')?.set;

                if (nativeSetter) {
                    nativeSetter.call(input, '');
                } else {
                    input.value = '';
                }
                input.dispatchEvent(new Event('input', { bubbles: true, cancelable: true, composed: true }));

                let current = '';
                for (const char of text) {
                    current += char;
                    input.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key: char, code: `Key${char.toUpperCase()}` }));
                    input.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, cancelable: true, key: char }));
                    if (nativeSetter) {
                        nativeSetter.call(input, current);
                    } else {
                        input.value = current;
                    }
                    input.dispatchEvent(new InputEvent('input', { bubbles: true, cancelable: true, inputType: 'insertText', data: char }));
                    input.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, cancelable: true, key: char }));
                }

                input.dispatchEvent(new Event('change', { bubbles: true, cancelable: true, composed: true }));

                return JSON.stringify({
                    success: (input.value || '').length > 0,
                    value: input.value || '',
                    expected: text,
                    id: input.id || ''
                });
            })();
            """);
    }

    private async Task OpenFlightLocationFieldAsync(string selector, string fieldName)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var labelSelector = selector == FlightFromInputSelector
            ? "#inputPickUpLocation-label"
            : "#inputDropOffLocation-label";
        var labelSelectorJson = JsonSerializer.Serialize(labelSelector);

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
                if (window.__ba?.clickLikeUser) {
                    window.__ba.clickLikeUser(target);
                } else {
                    target.focus?.();
                    target.click?.();
                }

                const resolvedAfterClick = resolveFlightInput({{selectorJson}});
                if (resolvedAfterClick) {
                    resolvedAfterClick.focus();
                    resolvedAfterClick.click();
                }

                return JSON.stringify({
                    opened: true,
                    targetText: (target.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120),
                    inputId: resolvedAfterClick?.id || '',
                    inputValue: resolvedAfterClick?.value || ''
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
                const targetCompact = compact(targetText);

                const tokenize = value => normalize(value)
                    .replace(/[()]/g, ' ')
                    .split(/[\s,/-]+/)
                    .map(token => token.trim())
                    .filter(token => token.length >= 2 && !['airport', 'havalimanı', 'uluslararası', 'international', 'türkiye', 'turkey', 'tüm'].includes(token));

                const targetTokens = tokenize(targetText);

                const getMainText = item => {
                    const primary = item.querySelector('div > div:first-child, span:first-child, strong, p');
                    return normalize(primary?.textContent || item.textContent || '');
                };

                const getScore = item => {
                    const fullText = normalize(item.textContent || '');
                    const mainText = getMainText(item);
                    const itemCompact = compact(fullText);
                    const itemTokens = tokenize(fullText);
                    const commonTokenCount = itemTokens.filter(token => targetTokens.includes(token)).length;

                    if (mainText === target) return 0;
                    if (itemCompact === targetCompact) return 1;
                    if (fullText.includes(target) || target.includes(fullText)) return 2;
                    if (mainText.startsWith(target) || target.startsWith(mainText)) return 3;
                    if (targetTokens.length > 0 && commonTokenCount === targetTokens.length) return 4;
                    if (commonTokenCount >= 1) return 5;
                    return 10;
                };

                const allSuggestionElements = Array.from(document.querySelectorAll({{suggestionSelectorJson}}));
                const visibleItems = allSuggestionElements
                    .filter(item => isVisible(item) && (!input || (item !== input && !item.contains(input) && !input.contains(item))));

                if (visibleItems.length === 0) {
                    return JSON.stringify({
                        clicked: false,
                        reason: 'öneri bulunamadı',
                        itemCount: 0
                    });
                }

                const sorted = visibleItems.sort((a, b) => {
                    const score = getScore(a) - getScore(b);
                    if (score !== 0) return score;
                    const ar = a.getBoundingClientRect();
                    const br = b.getBoundingClientRect();
                    return ar.top === br.top ? ar.left - br.left : ar.top - br.top;
                });

                const selected = sorted[0];
                try { selected.scrollIntoView({ block: 'center', inline: 'nearest' }); } catch {}

                const triggerClick = el => {
                    if (!el) return;
                    const rect = el.getBoundingClientRect();
                    const x = rect.left + rect.width / 2;
                    const y = rect.top + rect.height / 2;
                    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

                    ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'].forEach(evtType => {
                        try {
                            if (evtType.startsWith('pointer') && typeof PointerEvent === 'function') {
                                el.dispatchEvent(new PointerEvent(evtType, { ...opts, pointerId: 1, isPrimary: true, buttons: evtType.includes('down') ? 1 : 0 }));
                            } else {
                                el.dispatchEvent(new MouseEvent(evtType, { ...opts, buttons: evtType.includes('down') ? 1 : 0 }));
                            }
                        } catch {}
                    });
                    try { el.click(); } catch {}
                };

                triggerClick(selected);
                Array.from(selected.querySelectorAll('div, span, p, strong, i')).forEach(triggerClick);

                return JSON.stringify({
                    clicked: true,
                    selectedText: (selected.textContent || '').replace(/\s+/g, ' ').trim(),
                    inputValue: input?.value || '',
                    visibleCount: visibleItems.length
                });
            })();
            """);
    }

    private Task<bool> WaitForFlightLocationSelectionAppliedAsync(string inputSelector, string expectedLocation, TimeSpan timeout)
    {
        var inputSelectorJson = JsonSerializer.Serialize(inputSelector);
        var expectedLocationJson = JsonSerializer.Serialize(expectedLocation.Trim());
        var suggestionSelectorJson = JsonSerializer.Serialize(FlightLocationSuggestionSelector);

        return WaitForScriptTrueOrTimeoutAsync(
            $$"""
            (() => {
                {{FlightInputResolverScript}}
                const input = resolveFlightInput({{inputSelectorJson}});
                if (!input) return false;
                const isVisible = window.__ba?.isVisible || (() => false);
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());

                const openSuggestions = Array
                    .from(document.querySelectorAll({{suggestionSelectorJson}}))
                    .filter(isVisible);

                if (openSuggestions.length > 0) return false;

                const actual = normalize(input.value || '');
                const expected = normalize({{expectedLocationJson}});
                return actual.length > 0;
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
                        const compact = window.__ba?.compactTr || (value => normalize(value).replace(/\s/g, ''));
                        const tokenize = value => normalize(value)
                            .replace(/[()]/g, ' ')
                            .split(/[\s,/-]+/)
                            .map(token => token.trim())
                            .filter(token => token.length >= 3 && !['airport', 'havalimanı', 'uluslararası', 'international', 'türkiye', 'turkey'].includes(token));
                        const target = normalize({{locationJson}});
                        const targetTokens = tokenize({{locationJson}});
                        const suggestions = Array.from(document.querySelectorAll({{suggestionSelectorJson}}));
                        const visibleSuggestions = suggestions.filter(isVisible);
                        const matchingSuggestions = visibleSuggestions.filter(item => {
                            const text = normalize(item.textContent || '');
                            const compactText = compact(item.textContent || '');
                            const compactTarget = compact({{locationJson}});
                            const itemTokens = tokenize(item.textContent || '');
                            const commonTokenCount = itemTokens.filter(token => targetTokens.includes(token)).length;

                            return text.includes(target) ||
                                target.includes(text) ||
                                compactText.includes(compactTarget) ||
                                compactTarget.includes(compactText) ||
                                commonTokenCount >= 1;
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

                return hasVisibleSuggestion;
            },
            timeout,
            TimeSpan.FromMilliseconds(350));

        if (!found)
            throw new TimeoutException($"{fieldName} önerileri gelmedi. Son durum: {lastResult}");
    }

    private async Task SelectFlightDatesAsync(DateTime departureDate, DateTime? returnDate, bool isRoundTrip)
    {
        Report($"Uçuş tarihi seçiliyor: Gidiş {departureDate:dd.MM.yyyy}" + (isRoundTrip && returnDate is not null ? $", Dönüş {returnDate.Value:dd.MM.yyyy}" : " (Tek yön)"));

        var opened = await OpenFlightDatePickerAsync("flight_departure_date", "Gidiş tarihi");
        if (!opened)
            throw new InvalidOperationException("Uçuş takvimi açılamadı.");

        await WaitForDatePickerMenuAsync(TimeSpan.FromSeconds(10));
        await NavigateToMonthAsync(departureDate);

        var depSelected = await ClickCalendarDayAsync(departureDate);
        if (!depSelected)
            throw new InvalidOperationException($"Gidiş tarihi ({departureDate:dd.MM.yyyy}) takvimde seçilemedi.");

        if (isRoundTrip && returnDate is not null)
        {
            await Task.Delay(300);
            if (returnDate.Value.Year != departureDate.Year || returnDate.Value.Month != departureDate.Month)
            {
                await NavigateToMonthAsync(returnDate.Value);
            }

            var retSelected = await ClickCalendarDayAsync(returnDate.Value);
            if (!retSelected)
                throw new InvalidOperationException($"Dönüş tarihi ({returnDate.Value:dd.MM.yyyy}) takvimde seçilemedi.");
        }

        await WaitForDatePickerClosedAsync(TimeSpan.FromSeconds(5));
    }

    private async Task<bool> OpenFlightDatePickerAsync(string triggerCmsKey, string fieldName)
    {
        var triggerCmsKeyJson = JsonSerializer.Serialize(triggerCmsKey);
        var fieldNameJson = JsonSerializer.Serialize(fieldName);

        Report($"{fieldName} açılıyor...");
        var openResult = await EvaluateScriptAsync(
            $$"""
            (() => {
                const cmsKey = {{triggerCmsKeyJson}};
                const fieldName = {{fieldNameJson}};
                const isVisible = window.__ba?.isVisible || (() => false);
                const normalize = window.__ba?.normalizeTr || (value => (value || '').toLocaleLowerCase('tr-TR').replace(/\s+/g, ' ').trim());

                const attributeSelector = `[triggerlabelcmskey="${cmsKey}"], [modaltitlecmskey="${cmsKey}"]`;
                const attributeMatches = Array.from(document.querySelectorAll(attributeSelector));
                const dataKeyMatches = Array
                    .from(document.querySelectorAll(`[data-cms-key="${cmsKey}"]`))
                    .map(element => element.closest('[triggerlabelcmskey], [modaltitlecmskey], label, div') || element);

                const targetText = normalize(fieldName);
                const textMatches = Array
                    .from(document.querySelectorAll('div, label, button, span'))
                    .filter(element => {
                        if (!isVisible(element)) return false;
                        const text = normalize(element.textContent || '');
                        return text === targetText || text.includes(targetText);
                    })
                    .map(element => element.closest('[triggerlabelcmskey], [modaltitlecmskey], label, div') || element);

                const candidates = [...attributeMatches, ...dataKeyMatches, ...textMatches];
                const trigger = candidates.find(isVisible);

                if (!trigger) {
                    return JSON.stringify({
                        opened: false,
                        reason: 'visible trigger not found',
                        attributeCount: attributeMatches.length,
                        dataKeyCount: dataKeyMatches.length,
                        textCount: textMatches.length
                    });
                }

                const clickable = trigger.querySelector('.icon-calendar, .flex.items-center, span') || trigger;
                clickable.scrollIntoView({ block: 'center', inline: 'nearest' });
                const clickResult = window.__ba?.clickLikeUser
                    ? window.__ba.clickLikeUser(clickable)
                    : (() => {
                        clickable.click();
                        return { clicked: true };
                    })();

                return JSON.stringify({
                    opened: true,
                    triggerText: (trigger.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 120),
                    triggerAttribute: trigger.getAttribute('triggerlabelcmskey') || trigger.getAttribute('modaltitlecmskey') || '',
                    clickResult
                });
            })();
            """);

        try
        {
            using var document = JsonDocument.Parse((openResult ?? string.Empty).Trim('"'));
            return document.RootElement.TryGetProperty("opened", out var openedElement) &&
                openedElement.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return IsScriptTrue(openResult);
        }
    }

    public async Task WaitForFlightResultsAsync(TimeSpan? timeout = null)
    {
        var isReady = await WaitForScriptTrueOrTimeoutAsync(
            """
            (() => {
                const isVisible = window.__ba?.isVisible || (() => false);
                const bodyText = (document.body?.innerText || '').toLocaleLowerCase('tr-TR');
                const hasNoResultText = bodyText.includes('sonuç bulunamadı') ||
                    bodyText.includes('uygun uçuş bulunamadı') ||
                    bodyText.includes('uçuş bulunamadı');

                if (hasNoResultText) {
                    return true;
                }

                return Array
                    .from(document.querySelectorAll('#flight_card_list .flight-card'))
                    .some(card =>
                        isVisible(card) &&
                        card.querySelector('#departuretime') &&
                        card.querySelector('#arrivaltime') &&
                        card.querySelector('[data-cms-key="flight_total_price"]')
                    );
            })();
            """,
            timeout ?? TimeSpan.FromSeconds(35),
            TimeSpan.FromMilliseconds(500));

        if (!isReady)
            throw new TimeoutException("Uçuş sonuçları zaman aşımı süresinde görünmedi.");
    }

    private Task<string?> GetFlightResultsSignatureAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const normalize = window.__ba?.normalizeText || (value => (value || '').replace(/\s+/g, ' ').trim());
                const isVisible = window.__ba?.isVisible || (() => false);
                const cards = Array
                    .from(document.querySelectorAll('#flight_card_list .flight-card'))
                    .filter(isVisible)
                    .slice(0, 5)
                    .map(card => normalize(card.textContent).slice(0, 300));

                return JSON.stringify({
                    url: location.href,
                    count: cards.length,
                    cards
                });
            })();
            """);
    }

    private async Task WaitForFlightResultsChangedAsync(string? previousSignature, TimeSpan timeout)
    {
        var previous = (previousSignature ?? string.Empty).Trim();
        var isReady = await WaitUntilAsync(
            async () =>
            {
                var currentSignature = (await GetFlightResultsSignatureAsync() ?? string.Empty).Trim();
                if (!string.Equals(currentSignature, previous, StringComparison.Ordinal))
                    return await IsFlightResultsReadyAsync();

                return await HasFlightNoResultTextAsync();
            },
            timeout,
            TimeSpan.FromMilliseconds(500));

        if (!isReady)
            throw new TimeoutException("Uçuş sonuçları yenilenmedi; eski sonuçlar okunmadı.");
    }

    private Task<bool> IsFlightResultsReadyAsync()
    {
        return EvaluateBooleanScriptAsync(
            """
            (() => {
                const isVisible = window.__ba?.isVisible || (() => false);
                return Array
                    .from(document.querySelectorAll('#flight_card_list .flight-card'))
                    .some(card =>
                        isVisible(card) &&
                        card.querySelector('#departuretime') &&
                        card.querySelector('#arrivaltime') &&
                        card.querySelector('[data-cms-key="flight_total_price"]')
                    );
            })();
            """);
    }

    private Task<bool> HasFlightNoResultTextAsync()
    {
        return EvaluateBooleanScriptAsync(
            """
            (() => {
                const bodyText = (document.body?.innerText || '').toLocaleLowerCase('tr-TR');
                return bodyText.includes('sonuç bulunamadı') ||
                    bodyText.includes('uygun uçuş bulunamadı') ||
                    bodyText.includes('uçuş bulunamadı');
            })();
            """);
    }

    public async Task<List<FlightResultItem>> ReadFlightResultsAsync()
    {
        await ScrollToLoadAllCardsAsync("#flight_card_list .flight-card", "Uçuş sonuçları");

        try
        {
            var items = await EvaluateJsonScriptAsync<List<FlightResultItem>>(
                """
                (() => {
                    const normalize = window.__ba?.normalizeText || (value => (value || '').replace(/\s+/g, ' ').trim());
                    const isVisible = window.__ba?.isVisible || (() => false);
                    const directFlightText = 'Aktarmasız';

                    const cards = Array
                        .from(document.querySelectorAll('#flight_card_list .flight-card'))
                        .filter(isVisible);

                    const items = cards.map(card => {
                        const departureInfo = card.querySelector('.flight_info__left');
                        const arrivalInfo = card.querySelector('.flight_info__right');
                        const middleInfo = card.querySelector('.flight_info__middle');
                        const priceText = normalize(card.querySelector('[data-cms-key="flight_total_price"]')?.textContent)
                            .replace(/^Toplam\s*:\s*/i, '');
                        const rawAirline = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
                        const airline = rawAirline.toUpperCase();
                        const departureAirport = normalize(departureInfo?.querySelector('p')?.textContent);
                        const arrivalAirport = normalize(arrivalInfo?.querySelector('p')?.textContent);
                        const transferText = normalize(middleInfo?.querySelector('[data-cms-key="flight_transfer"]')?.textContent);
                        const duration = normalize(
                            middleInfo
                                ?.querySelector('.transfer_wrapper > span:not([data-cms-key="flight_transfer"])')
                                ?.textContent
                        );

                        return {
                            airline,
                            route: [departureAirport, arrivalAirport].filter(Boolean).join(' → '),
                            fromLocation: departureAirport,
                            toLocation: arrivalAirport,
                            departureTime: normalize(departureInfo?.querySelector('#departuretime')?.textContent),
                            arrivalTime: normalize(arrivalInfo?.querySelector('#arrivaltime')?.textContent),
                            duration,
                            price: priceText,
                            detail: transferText || directFlightText
                        };
                    }).filter(item => item.price || item.departureTime || item.route);

                    const uniqueItems = [];
                    const seen = new Set();
                    for (const item of items) {
                        const key = `${item.airline}|${item.route}|${item.departureTime}|${item.arrivalTime}|${item.price}`;
                        if (seen.has(key)) continue;

                        seen.add(key);
                        uniqueItems.push(item);
                    }

                    return JSON.stringify(uniqueItems);
                })();
                """);

            return items ?? new List<FlightResultItem>();
        }
        catch
        {
            return new List<FlightResultItem>();
        }
    }

    public Task<string?> GetFlightFormSnapshotAsync()
    {
        return EvaluateScriptAsync(
            """
            (() => {
                const normalize = window.__ba?.normalizeText || (value => (value || '').replace(/\s+/g, ' ').trim());
                const fromInput = document.querySelector('#inputPickUpLocation');
                const toInput = document.querySelector('#inputDropOffLocation');
                const departureDate = document
                    .querySelector('[triggerlabelcmskey="flight_departure_date"], [modaltitlecmskey="flight_departure_date"]');
                const returnDate = document
                    .querySelector('[triggerlabelcmskey="flight_return_date"], [modaltitlecmskey="flight_return_date"]');

                return JSON.stringify({
                    fromInput: normalize(fromInput?.value),
                    toInput: normalize(toInput?.value),
                    departureDateText: normalize(departureDate?.textContent),
                    returnDateText: normalize(returnDate?.textContent),
                    url: location.href
                });
            })();
            """);
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

    private static string BuildFlightLocationSearchText(string location)
    {
        var text = (location ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var openParenIndex = text.LastIndexOf('(');
        var closeParenIndex = text.LastIndexOf(')');
        if (openParenIndex >= 0 && closeParenIndex > openParenIndex)
        {
            var airportCode = text[(openParenIndex + 1)..closeParenIndex].Trim();
            if (airportCode.Length == 3 && airportCode.All(char.IsLetter))
                return airportCode.ToUpperInvariant();
        }

        var commaIndex = text.IndexOf(',');
        if (commaIndex > 0)
            text = text[..commaIndex].Trim();

        return text
            .Replace("Uluslararası Havalimanı", "Havalimanı", StringComparison.OrdinalIgnoreCase)
            .Replace("International Airport", "Airport", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

}

// Source copy: JavaScriptCopies/039_BAService.SearchForm_FillPickupLocationAsync_line55.js
// Purpose: Select the best pickup location autocomplete suggestion.
//
// TODO before implementation:
// Ask again whether this simplified scoring is acceptable for the current
// autocomplete behavior. This replaces special blacklist checks like
// "airport", "havalimanı", "sabiha", "saw", "ist)" with generic text closeness.
//
// Scoring idea:
// Lower score means a better match.
// It prioritizes exact main text and city/country style suggestions before
// broader suggestions that only start with or include the target.
//
// Test carefully before moving this into the working C# automation code.

(() => {
    const input = document.querySelector({{pickupLocationInputSelectorJson}});
    const targetText = {{locationJson}};

    const normalize = value => (value || '')
        .toLocaleLowerCase('tr-TR')
        .replace(/\s+/g, ' ')
        .trim();

    const compact = value => normalize(value).replace(/\s/g, '');
    const target = normalize(targetText);

    const isVisible = item => {
        if (!item) return false;

        const rect = item.getBoundingClientRect();
        const style = getComputedStyle(item);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const getMainText = item => normalize(
        item.querySelector(
            'strong, ' +
            '.search-autocomplete__item__text-wrapper span:first-child, ' +
            '.search-autocomplete-mobile__item__text-wrapper span:first-child, ' +
            'div > div:first-child'
        )?.textContent || ''
    );

    const getScore = item => {
        const fullText = normalize(item.textContent || '');
        const mainText = getMainText(item);
        const compactText = compact(item.textContent || '');

        if (mainText === target) return 0;
        if (compactText === compact(`${targetText} Türkiye`)) return 1;
        if (compactText === compact(`${targetText}, Türkiye`)) return 1;
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

    if (!selected) {
        return JSON.stringify({
            clicked: false,
            reason: 'öneri bulunamadı',
            itemCount: items.length
        });
    }

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
            target.dispatchEvent(new PointerEvent(type, {
                ...eventOptions,
                pointerId: 1,
                pointerType: 'mouse',
                isPrimary: true,
                buttons
            }));
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

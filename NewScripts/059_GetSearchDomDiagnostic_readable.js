// Proposed replacement for:
// JavaScriptCopies/059_BAService_GetSearchDomDiagnosticAsync_line150.js
//
// Purpose:
// Return diagnostic JSON before search automation starts.
//
// Important:
// The JSON field names are intentionally kept the same:
// url, title, inputCount, pickupById, inputs, possibleLocationElements

(() => {
    const normalizeText = value => (value || '').replace(/\s+/g, ' ').trim();

    const isVisible = element => {
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const inputs = Array
        .from(document.querySelectorAll('input, textarea'))
        .slice(0, 20)
        .map((input, index) => ({
            index,
            id: input.id || '',
            name: input.getAttribute('name') || '',
            type: input.getAttribute('type') || '',
            placeholder: input.getAttribute('placeholder') || '',
            value: input.value || '',
            ariaLabel: input.getAttribute('aria-label') || '',
            visible: isVisible(input)
        }));

    const locationCandidateSelector = [
        '[id*="location" i]',
        '[placeholder*="alış" i]',
        '[placeholder*="teslim" i]',
        '[class*="location" i]',
        '[class*="autocomplete" i]'
    ].join(',');

    const possibleLocationElements = Array
        .from(document.querySelectorAll(locationCandidateSelector))
        .slice(0, 20)
        .map((element, index) => ({
            index,
            tag: element.tagName,
            id: element.id || '',
            className: element.className || '',
            placeholder: element.getAttribute('placeholder') || '',
            text: normalizeText(element.textContent).slice(0, 120),
            visible: isVisible(element)
        }));

    return JSON.stringify({
        url: location.href,
        title: document.title,
        inputCount: document.querySelectorAll('input, textarea').length,
        pickupById: !!document.querySelector('#inputPickUpLocation'),
        inputs,
        possibleLocationElements
    });
})();

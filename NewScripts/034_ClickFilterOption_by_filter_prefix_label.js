// Source copy: JavaScriptCopies/034_BAService.Results_ClickFilterOptionAsync_line90.js
// Purpose: Click a result filter option by filter prefix and visible label text.
//
// Based on captured results.html:
// - Transmission labels use name prefix "filter-transmission."
// - Fuel labels use name prefix "filter-fuel."
// - Label text includes values like "Otomatik (13)", "Benzin (8)", "Dizel (1)"
//
// Controlled simplification:
// - Removed broad fallback that scans every label/checkbox/radio on the page.
// - Kept text normalization and partial matching because labels contain counts.
// - Kept visibility checks.
// - Kept input click/change after label click to preserve Vue/Nuxt state updates.
//
// Test before moving this into the working C# automation code.

(() => {
    const targets = {{targetTextsJson}};
    const prefix = {{filterPrefixJson}};

    const normalize = value => (value || '')
        .toLocaleLowerCase('tr-TR')
        .replace(/\s+/g, ' ')
        .trim();

    const isVisible = el => {
        if (!el) return false;

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const normalizedTargets = targets.map(normalize);

    const labels = Array.from(document.querySelectorAll(`label[name^="${prefix}."]`))
        .filter(isVisible);

    const matchesTarget = text =>
        normalizedTargets.some(target =>
            text === target ||
            text.startsWith(target + ' ') ||
            text.includes(target)
        );

    const match = labels.find(label => matchesTarget(normalize(label.textContent || '')));
    if (!match) return false;

    match.scrollIntoView({ block: 'center', inline: 'nearest' });
    match.click();

    const input = match.querySelector('input[type="checkbox"], input[type="radio"]');
    if (input && !input.checked) {
        input.click();
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    return true;
})();

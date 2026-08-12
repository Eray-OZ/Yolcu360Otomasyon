// Source copy: JavaScriptCopies/046_BAService.SearchForm_SelectTimeAsync_line496.js
// Purpose: Open pickup/dropoff time picker.
//
// Based on captured time.html:
// - There are two groups with modaltitlecmskey="pickup_and_dropoff_date"
// - group[0] contains "Alış Saati"
// - group[1] contains "Bırakış Saati"
// - The clickable time box is the closest .cursor-pointer wrapper around that label.
//
// This removes the broad fallback that scans every visible "HH:mm" element.
//
// Test before moving this into the working C# automation code.

(() => {
    const index = {{indexJson}};
    const expectedLabel = index === 0 ? 'alış saati' : 'bırakış saati';

    const normalize = value => (value || '')
        .toLocaleLowerCase('tr-TR')
        .replace(/\s+/g, ' ')
        .trim();

    const isClickable = el => {
        if (!el) return false;

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden' &&
            !el.disabled &&
            el.getAttribute('aria-disabled') !== 'true';
    };

    const groups = Array.from(document.querySelectorAll('[modaltitlecmskey="pickup_and_dropoff_date"]'));
    const group = groups[index];
    if (!group) return false;

    const label = Array.from(group.querySelectorAll('span'))
        .find(span => normalize(span.textContent) === expectedLabel);

    const timeBox = label?.closest('.cursor-pointer');
    if (!isClickable(timeBox)) return false;

    timeBox.scrollIntoView({ block: 'center', inline: 'nearest' });
    timeBox.click();

    return true;
})();

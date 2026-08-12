// Source copy: JavaScriptCopies/047_BAService.SearchForm_SelectTimeAsync_line536.js
// Purpose: Select a time option from the open pickup/dropoff time dropdown.
//
// Based on captured time.html:
// - There are two groups with modaltitlecmskey="pickup_and_dropoff_date"
// - group[0] contains "Alış Saati"
// - group[1] contains "Bırakış Saati"
// - Open dropdown is an absolute panel under .relative.inline-block
// - Time options are li elements with text like "10:00"
//
// This removes broad page-wide option scans.
// It selects the visible li inside the currently opened time dropdown.
//
// Test before moving this into the working C# automation code.

(() => {
    const target = {{timeJson}};
    const index = {{indexJson}};
    const expectedLabel = index === 0 ? 'alış saati' : 'bırakış saati';

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

    const groups = Array.from(document.querySelectorAll('[modaltitlecmskey="pickup_and_dropoff_date"]'));
    const group = groups[index];
    if (!group) return false;

    const label = Array.from(group.querySelectorAll('span'))
        .find(span => normalize(span.textContent) === expectedLabel);

    const wrapper = label?.parentElement?.querySelector('.relative.inline-block') ||
        group.querySelector('.relative.inline-block');
    if (!wrapper) return false;

    const dropdown = Array.from(wrapper.querySelectorAll('.absolute'))
        .find(isVisible);
    if (!dropdown) return false;

    const option = Array.from(dropdown.querySelectorAll('li'))
        .find(li => isVisible(li) && normalize(li.textContent) === normalize(target));
    if (!option) return false;

    option.scrollIntoView({ block: 'nearest', inline: 'nearest' });

    const rect = option.getBoundingClientRect();
    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    const opts = { bubbles: true, cancelable: true, view: window, clientX: x, clientY: y };

    option.dispatchEvent(new MouseEvent('mousedown', { ...opts, buttons: 1 }));
    option.dispatchEvent(new MouseEvent('mouseup', opts));
    option.click();

    return true;
})();

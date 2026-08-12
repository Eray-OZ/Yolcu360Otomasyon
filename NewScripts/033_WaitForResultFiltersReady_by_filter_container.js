// Source copy: JavaScriptCopies/033_BAService.Results_WaitForResultFiltersReadyAsync_line61.js
// Purpose: Wait until result filter controls are available.
//
// Based on captured results.html:
// - Filter wrapper has class "filter-container"
// - Transmission filters use name/id prefix "filter-transmission."
// - Fuel filters use name/id prefix "filter-fuel."
//
// This removes broad body text checks like "vites", "yakıt", "filtre".
// Text checks can return true before actual filter controls are clickable.
//
// Test before moving this into the working C# automation code.

(() => {
    const isVisible = el => {
        if (!el) return false;

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const filterContainer = document.querySelector('.filter-container');
    if (!isVisible(filterContainer)) return false;

    const filterControl = filterContainer.querySelector(
        'label[name^="filter-transmission."], ' +
        'label[name^="filter-fuel."], ' +
        'input[id^="filter-transmission."], ' +
        'input[id^="filter-fuel."]'
    );

    return isVisible(filterControl);
})();

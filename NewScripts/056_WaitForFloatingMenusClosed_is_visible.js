// Proposed replacement for:
// JavaScriptCopies/056_BAService.SearchForm_WaitForFloatingMenusClosedAsync_line916.js
//
// Purpose:
// Return true when no date picker or autocomplete floating menu is visible.
//
// Note:
// Later, this local isVisible function can be replaced with window.__ba.isVisible.

(() => {
    const isVisible = element => {
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const floatingMenuSelector = [
        '.dp__menu',
        '.dp__outer_menu_wrap',
        '.search-autocomplete'
    ].join(',');

    return Array
        .from(document.querySelectorAll(floatingMenuSelector))
        .filter(isVisible)
        .length === 0;
})();

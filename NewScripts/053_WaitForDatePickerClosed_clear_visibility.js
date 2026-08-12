// Proposed replacement for:
// JavaScriptCopies/053_BAService.SearchForm_WaitForDatePickerClosedAsync_line852.js
//
// Purpose:
// Return true when no visible date picker menu remains on the page.

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

    const visibleDatePickerMenus = Array
        .from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
        .filter(isVisible);

    return visibleDatePickerMenus.length === 0;
})();

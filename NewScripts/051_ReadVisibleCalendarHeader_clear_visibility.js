// Proposed replacement for:
// JavaScriptCopies/051_BAService.SearchForm_WaitForCalendarHeaderChangedOrTargetVisibleAsync_line741.js
//
// Purpose:
// Read the visible date picker header text after clicking calendar navigation.
// C# compares this text with the previous header and target month/year.

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

    const visibleMenu = Array
        .from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
        .find(isVisible);

    if (!visibleMenu) {
        return '';
    }

    const headerSelectors = [
        '.dp__month_year_select',
        '.dp__calendar_header_item',
        '.dp__month_year_wrap',
        '.dp__calendar_header'
    ].join(',');

    return Array
        .from(visibleMenu.querySelectorAll(headerSelectors))
        .map(header => (header.textContent || '').trim())
        .filter(Boolean)
        .join(' ');
})();

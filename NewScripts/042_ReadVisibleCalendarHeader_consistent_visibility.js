// Source copy: JavaScriptCopies/042_BAService.SearchForm_NavigateToMonthAsync_line278.js
// Purpose: Read the visible datepicker month/year header text.
//
// Controlled cleanup:
// - Uses both width and height in visibility check.
// - Uses clearer variable names.
// - Filters empty header texts before joining.
//
// Test before moving this into the working C# automation code.

(() => {
    const menu = Array.from(document.querySelectorAll('.dp__menu, .dp__outer_menu_wrap'))
        .find(menu => {
            const rect = menu.getBoundingClientRect();
            const style = getComputedStyle(menu);

            return rect.width > 0 &&
                rect.height > 0 &&
                style.display !== 'none' &&
                style.visibility !== 'hidden';
        });

    if (!menu) return '';

    const headers = Array.from(menu.querySelectorAll(
        '.dp__month_year_select, ' +
        '.dp__calendar_header_item, ' +
        '.dp__month_year_wrap, ' +
        '.dp__calendar_header'
    ));

    return headers
        .map(header => (header.textContent || '').trim())
        .filter(Boolean)
        .join(' ');
})();

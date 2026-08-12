// Proposed replacement for:
// JavaScriptCopies/054_BAService.SearchForm_WaitForTimeOptionVisibleAsync_line871.js
//
// Purpose:
// Return true when the requested time option is visible in the opened time
// dropdown.
//
// Based on time.html:
// Time options are rendered as li elements inside the open dropdown area.

(() => {
    const target = {{timeJson}};

    const isVisible = element => {
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    return Array
        .from(document.querySelectorAll('.relative.inline-block li, li'))
        .filter(isVisible)
        .some(option => {
            const text = (option.textContent || '').trim();
            return text === target || text.startsWith(target);
        });
})();

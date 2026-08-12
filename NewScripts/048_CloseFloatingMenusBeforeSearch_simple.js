// Proposed replacement for:
// JavaScriptCopies/048_BAService.SearchForm_ClickSearchButtonAsync_line601.js
//
// Purpose:
// Before clicking the search button, remove focus from the active field and hide
// only the floating menus that can sit on top of the search button.
//
// Note:
// This is still slightly intrusive because it changes inline display styles.
// It is kept because date/time/autocomplete popups may block the real search
// button click if they are still open.

(() => {
    const active = document.activeElement;
    if (active && typeof active.blur === 'function') {
        active.blur();
    }

    document
        .querySelectorAll('.dp__menu, .search-autocomplete')
        .forEach(menu => {
            menu.style.display = 'none';
        });

    return true;
})();

// Proposed replacement for:
// JavaScriptCopies/058_BAService_CloseInitialPopupAsync_line120.js
//
// Purpose:
// Close the initial discount popup if its close button is visible.
//
// Note:
// Later, this local isVisible function can be replaced with window.__ba.isVisible.

(() => {
    const closeButton = document.querySelector('.gs_trigger_discount_popup_close_container');

    const isVisible = element => {
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    if (!isVisible(closeButton)) {
        return false;
    }

    closeButton.click();
    return true;
})();

// Proposed replacement for:
// JavaScriptCopies/050_BAService.SearchForm_IsPickupLocationSelectionAppliedAsync_line711.js
//
// Purpose:
// After clicking an autocomplete suggestion, confirm that the pickup location is
// now selected enough for the form to continue.
//
// Rule:
// 1. The pickup location input must exist.
// 2. The input must contain text.
// 3. No visible autocomplete suggestions should remain open.

(() => {
    const pickupInput = document.querySelector({{pickupLocationInputSelectorJson}});

    const isVisible = element => {
        if (!element) return false;

        const rect = element.getBoundingClientRect();
        const style = getComputedStyle(element);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const openSuggestions = Array
        .from(document.querySelectorAll({{locationSuggestionSelectorJson}}))
        .filter(isVisible);

    const hasPickupText = !!pickupInput &&
        pickupInput.value.trim().length > 0;

    return hasPickupText && openSuggestions.length === 0;
})();

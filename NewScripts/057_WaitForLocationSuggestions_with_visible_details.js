// Proposed replacement for:
// JavaScriptCopies/057_BAService.SearchForm_WaitForLocationSuggestionsAsync_line941.js
//
// Purpose:
// Return diagnostic JSON about autocomplete suggestions.
//
// Important:
// The C# side logs this JSON, so the shape must stay compatible:
// { total, visible, text }

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

    const suggestions = Array
        .from(document.querySelectorAll({{selectorJson}}));

    const visibleSuggestions = suggestions.filter(isVisible);

    return JSON.stringify({
        total: suggestions.length,
        visible: visibleSuggestions.length,
        text: visibleSuggestions
            .slice(0, 3)
            .map(suggestion => (suggestion.textContent || '').replace(/\s+/g, ' ').trim())
    });
})();

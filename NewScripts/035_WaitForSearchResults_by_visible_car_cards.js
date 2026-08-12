// Source copy: JavaScriptCopies/035_BAService.Results_WaitForSearchResultsAsync_line162.js
// Purpose: Wait until visible search result cards are rendered.
//
// Based on captured results.html:
// - Result list has id="car_card_list"
// - Each result item has class "car-card"
//
// This removes broad body text checks like "hemen kirala" and "günlük fiyat".
// A visible card inside #car_card_list is a stronger signal.
//
// Test before moving this into the working C# automation code.

(() => {
    const cards = Array.from(document.querySelectorAll('#car_card_list .car-card'));

    return cards.some(card => {
        const rect = card.getBoundingClientRect();
        const style = getComputedStyle(card);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    });
})();

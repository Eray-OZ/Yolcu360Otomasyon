// Source copy: JavaScriptCopies/036_BAService.Results_ReadSearchResultsAsync_line187.js
// Purpose: Read visible result cards into SearchResultItem JSON.
//
// Based on captured results.html:
// - Result list has id="car_card_list"
// - Each result item has class "car-card"
// - Total price uses id="car_total_price"
// - Daily price uses data-cms-key="text_daily_price2"
// - Some fields are duplicated for desktop/mobile, so this reads the visible one.
//
// Important:
// The page duplicates some ids between desktop and mobile layouts.
// firstVisibleText(...) avoids reading hidden mobile/desktop duplicates.
//
// Test before moving this into the working C# automation code.

(() => {
    const normalize = value => (value || '').replace(/\s+/g, ' ').trim();

    const isVisible = el => {
        if (!el) return false;

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    const firstVisibleText = (root, selector) => {
        const element = Array.from(root.querySelectorAll(selector)).find(isVisible);
        return normalize(element?.textContent);
    };

    const cards = Array.from(document.querySelectorAll('#car_card_list .car-card'))
        .filter(isVisible);

    const items = cards.map(card => {
        const specs = Array.from(card.querySelectorAll('.icon-gear-type, .icon-gas-type'))
            .map(icon => normalize(icon.parentElement?.textContent))
            .filter(Boolean);

        const title = firstVisibleText(card, '.text-dark-gray.text-lg.font-bold');
        const subtitle = firstVisibleText(card, '[data-cms-key="or_similar"]');
        const price = firstVisibleText(card, '#car_total_price');
        const dailyPrice = firstVisibleText(card, '[data-cms-key="text_daily_price2"]');
        const transmission = specs.find(text => /manuel|otomatik/i.test(text)) || '';
        const fuelType = specs.find(text => /benzin|dizel|hibrit|hybrid|elektrik|electric/i.test(text)) || '';
        const supplier = normalize(card.querySelector('figure img[alt]')?.getAttribute('alt'));
        const pickupInfo = normalize(card.querySelector('.icon-filled')?.parentElement?.textContent);
        const actionText = firstVisibleText(card, '[data-cms-key="button_rent_now"]');
        const url = normalize(card.querySelector('a[href]')?.getAttribute('href'));

        return {
            title,
            subtitle,
            price,
            dailyPrice,
            transmission,
            fuelType,
            supplier,
            pickupInfo,
            actionText,
            url
        };
    }).filter(item => item.title || item.price);

    return JSON.stringify(items);
})();

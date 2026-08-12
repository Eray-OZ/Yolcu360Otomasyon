// Proposed replacement for:
// JavaScriptCopies/049_BAService.SearchForm_ClickSearchButtonAsync_line616.js
//
// HTML source:
// <button id="search" data-cms-key="search">ARAÇ ARA</button>
//
// Because the search button has a stable id, the script does not need generic
// fallbacks such as button[type="submit"] or text includes("Ara").

(() => {
    const btn = document.querySelector('#search');

    if (!btn) {
        return JSON.stringify({
            success: false,
            reason: 'Search button #search not found'
        });
    }

    btn.scrollIntoView({ block: 'center', inline: 'center' });

    const rect = btn.getBoundingClientRect();
    const style = getComputedStyle(btn);
    const isClickable =
        rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        style.pointerEvents !== 'none' &&
        !btn.disabled &&
        btn.getAttribute('aria-disabled') !== 'true';

    if (!isClickable) {
        return JSON.stringify({
            success: false,
            reason: 'Search button exists but is not clickable',
            text: (btn.textContent || '').trim()
        });
    }

    const x = rect.left + rect.width / 2;
    const y = rect.top + rect.height / 2;
    const eventOptions = {
        bubbles: true,
        cancelable: true,
        view: window,
        clientX: x,
        clientY: y
    };

    // Keep the real mouse event sequence because the site may listen to click
    // through Vue/Nuxt event handlers. The broad selector fallbacks were removed,
    // but the click behavior is intentionally still close to the working version.
    btn.dispatchEvent(new MouseEvent('mousedown', { ...eventOptions, buttons: 1 }));
    btn.dispatchEvent(new MouseEvent('mouseup', eventOptions));
    btn.click();

    return JSON.stringify({
        success: true,
        text: (btn.textContent || '').trim()
    });
})();

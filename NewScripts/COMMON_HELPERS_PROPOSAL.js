// Proposed shared JavaScript helpers.
//
// These helpers are NOT used by the app yet.
// They show how repeated inline helper code can be centralized later.
//
// Important:
// If this is moved into the working C# automation, it must be injected before
// any script that calls window.__ba.isVisible(...).

(() => {
    window.__ba = window.__ba || {};

    window.__ba.isVisible = el => {
        if (!el) return false;

        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden';
    };

    return true;
})();

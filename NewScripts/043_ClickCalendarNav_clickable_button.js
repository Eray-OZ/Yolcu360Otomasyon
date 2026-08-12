// Source copy: JavaScriptCopies/043_BAService.SearchForm_ClickCalendarNavAsync_line314.js
// Purpose: Click the datepicker previous/next month navigation button.
//
// Controlled cleanup:
// - Keeps the original selector strategy.
// - Adds visible/enabled checks before clicking.
// - Uses .dp__nav_btn fallback only after filtering clickable buttons.
//
// Test before moving this into the working C# automation code.

(() => {
    const forward = {{forwardJson}};

    const isClickable = button => {
        if (!button) return false;

        const rect = button.getBoundingClientRect();
        const style = getComputedStyle(button);

        return rect.width > 0 &&
            rect.height > 0 &&
            style.display !== 'none' &&
            style.visibility !== 'hidden' &&
            !button.disabled &&
            button.getAttribute('aria-disabled') !== 'true';
    };

    const next = document.querySelector("[data-dp-element='action-next'], .dp__next_btn, button[aria-label*='Next']");
    const prev = document.querySelector("[data-dp-element='action-prev'], .dp__prev_btn, button[aria-label*='Prev']");
    const navButtons = Array.from(document.querySelectorAll('.dp__nav_btn')).filter(isClickable);

    const button = forward
        ? (isClickable(next) ? next : navButtons.at(-1))
        : (isClickable(prev) ? prev : navButtons[0]);

    if (!isClickable(button)) return false;

    button.click();
    return true;
})();

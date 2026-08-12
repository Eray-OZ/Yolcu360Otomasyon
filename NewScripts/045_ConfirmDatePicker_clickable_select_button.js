// Source copy: JavaScriptCopies/045_BAService.SearchForm_ConfirmDatePickerAsync_line475.js
// Purpose: Confirm the selected date range in the datepicker.
//
// Controlled cleanup:
// - Removes duplicate selector "button.dp__action_select" because ".dp__action_select" already matches it.
// - Adds visible/enabled checks before clicking.
//
// Later shared helper candidate:
// Replace local isClickable(...) with window.__ba.isClickable(...).
//
// Test before moving this into the working C# automation code.

(() => {
    const button = document.querySelector('.dp__action_select, .dp__select');
    if (!button) return false;

    const rect = button.getBoundingClientRect();
    const style = getComputedStyle(button);

    const isClickable = rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        !button.disabled &&
        button.getAttribute('aria-disabled') !== 'true';

    if (!isClickable) return false;

    button.click();
    return true;
})();

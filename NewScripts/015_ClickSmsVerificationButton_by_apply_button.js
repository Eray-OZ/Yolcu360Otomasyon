// Source copy: JavaScriptCopies/015_BAService.Auth_FillSmsVerificationCodeAsync_line521.js
// Purpose: Click the SMS verification button after filling the SMS code.
//
// Based on captured Yolcu360 SMS verification HTML:
// - Verify button has data-cms-key="button_apply"
//
// This removes the broad fallback that scans buttons by text.
// Test before moving this into the working C# automation code.

(() => {
    const button = document.querySelector('button[data-cms-key="button_apply"]');
    if (!button) return false;

    const rect = button.getBoundingClientRect();
    const style = getComputedStyle(button);

    const isVisible = rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden';

    if (!isVisible) return false;

    button.disabled = false;
    button.removeAttribute('disabled');
    button.classList.remove('disabled');

    button.click();

    return true;
})();

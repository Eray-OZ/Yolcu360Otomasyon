// Source copy: JavaScriptCopies/013_BAService.Auth_WaitForSmsVerificationButtonReadyAsync_line381.js
// Purpose: Wait until the SMS verification button is visible and clickable.
//
// Based on captured Yolcu360 SMS verification HTML:
// - Verify button has data-cms-key="button_apply"
//
// This removes the broad fallback that scans every button by text.
// The fallback can produce false positives if another button contains
// "devam", "gönder", "giriş", etc.
//
// Test before moving this into the working C# automation code.

(() => {
    const button = document.querySelector('button[data-cms-key="button_apply"]');
    if (!button) return false;

    const rect = button.getBoundingClientRect();
    const style = getComputedStyle(button);

    return rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        !button.disabled &&
        button.getAttribute('aria-disabled') !== 'true';
})();

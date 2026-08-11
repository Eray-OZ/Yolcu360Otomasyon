// Source copy: JavaScriptCopies/012_BAService.Auth_WaitForSmsCodeInputReadyAsync_line351.js
// Purpose: Wait until the SMS code input is visible and writable.
//
// Based on captured Yolcu360 SMS verification HTML:
// - SMS code input has id="sms_input"
//
// This replaces the broad "scan every input and guess OTP fields" logic
// with the exact selector used by the page.
//
// Test before moving this into the working C# automation code.

(() => {
    const input = document.querySelector('#sms_input');
    if (!input) return false;

    const rect = input.getBoundingClientRect();
    const style = getComputedStyle(input);

    return rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        !input.disabled &&
        input.getAttribute('readonly') === null;
})();

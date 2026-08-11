// Source copy: JavaScriptCopies/009_BAService.Auth_WaitForPhoneInputReadyAsync_line257.js
// Purpose: Wait until the phone input is present, visible, and writable.
//
// Selector note:
// Use one combined selector instead of repeating two querySelector calls.
//
// Old:
// document.querySelector('#phn-input') || document.querySelector('input[type="tel"]')
//
// New:
// document.querySelector('#phn-input, input[type="tel"]')
//
// Proposed shared selector name for C#:
// PhoneInputSelector = "#phn-input, input[type=\"tel\"]"
//
// Test before moving this into the working C# automation code.

(() => {
    const input = document.querySelector('#phn-input, input[type="tel"]');
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

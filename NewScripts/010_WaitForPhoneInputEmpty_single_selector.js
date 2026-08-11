// Source copy: JavaScriptCopies/010_BAService.Auth_WaitForPhoneInputEmptyAsync_line277.js
// Purpose: Confirm that the phone input was cleared before typing the phone number.
//
// Uses shared selector decision from SELECTOR_NOTES.md:
// document.querySelector('#phn-input, input[type="tel"]')
//
// Test before moving this into the working C# automation code.

(() => {
    const input = document.querySelector('#phn-input, input[type="tel"]');
    return !!input && (input.value || '').trim().length === 0;
})();

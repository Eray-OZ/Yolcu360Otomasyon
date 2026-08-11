// Source copy: JavaScriptCopies/008_BAService.Auth_LoginWithPhoneAsync_line230.js
// Purpose: Detect whether the SMS verification screen is visible.
//
// Based on captured Yolcu360 SMS verification HTML:
// - SMS code input has id="sms_input"
// - Verify button has data-cms-key="button_apply"
//
// Why this is safer than the original:
// The phone number screen also contains "SMS" and "doğrulama" text.
// Text-only checks can therefore produce false positives.
// This version waits for the actual SMS code input instead.
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
        style.visibility !== 'hidden';
})();

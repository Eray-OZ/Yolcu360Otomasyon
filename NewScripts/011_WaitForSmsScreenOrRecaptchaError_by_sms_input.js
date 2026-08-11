// Source copy: JavaScriptCopies/011_BAService.Auth_WaitForSmsScreenOrRecaptchaErrorAsync_line294.js
// Purpose: Detect whether login flow reached SMS verification, recaptcha error, or still waiting.
//
// Based on captured Yolcu360 SMS verification HTML:
// - SMS code input has id="sms_input"
//
// Why this is safer than the original:
// The phone number screen also contains "SMS" and "doğrulama" text.
// Text-only checks can therefore return "sms" too early.
// This version returns "sms" only when the actual SMS code input is visible.
//
// Return values:
// - "recaptcha": recaptcha/score error detected
// - "sms": #sms_input is visible
// - "waiting": neither condition is ready yet
//
// Test before moving this into the working C# automation code.

(() => {
    if (window.__hasRecaptchaScoreError && window.__hasRecaptchaScoreError()) {
        return 'recaptcha';
    }

    const smsInput = document.querySelector('#sms_input');
    if (!smsInput) return 'waiting';

    const rect = smsInput.getBoundingClientRect();
    const style = getComputedStyle(smsInput);

    const isVisible = rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden';

    return isVisible ? 'sms' : 'waiting';
})();

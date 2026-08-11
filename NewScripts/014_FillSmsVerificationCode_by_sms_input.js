// Source copy: JavaScriptCopies/014_BAService.Auth_FillSmsVerificationCodeAsync_line420.js
// Purpose: Fill the SMS verification code into Yolcu360 SMS input.
//
// Based on captured Yolcu360 SMS verification HTML:
// - SMS code input has id="sms_input"
//
// This replaces the broad OTP/input guessing logic with the exact input selector.
// It keeps the native HTMLInputElement value setter and input/change events
// so Vue/Nuxt can detect the value update.
//
// Test before moving this into the working C# automation code.

(() => {
    const code = {{codeJson}};
    const input = document.querySelector('#sms_input');

    if (!input) {
        return JSON.stringify({
            success: false,
            reason: 'SMS input bulunamadı'
        });
    }

    const rect = input.getBoundingClientRect();
    const style = getComputedStyle(input);

    const isReady = rect.width > 0 &&
        rect.height > 0 &&
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        !input.disabled &&
        input.getAttribute('readonly') === null;

    if (!isReady) {
        return JSON.stringify({
            success: false,
            reason: 'SMS input hazır değil'
        });
    }

    input.focus();
    input.click();

    const descriptor = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
    if (descriptor?.set) {
        descriptor.set.call(input, code);
    } else {
        input.value = code;
    }

    input.dispatchEvent(new InputEvent('input', {
        bubbles: true,
        inputType: 'insertText',
        data: code
    }));

    input.dispatchEvent(new Event('change', { bubbles: true }));

    return JSON.stringify({
        success: true,
        type: 'sms_input',
        id: input.id
    });
})();

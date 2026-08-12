// Source copy: JavaScriptCopies/031_BAService.Payment_WaitForPaymentButtonReadyAsync_line129.js
// Purpose: Wait until the iyzico payment button is visible and clickable.
//
// Based on captured iyzico sandbox payment HTML:
// - Payment submit button has id="iyz-payment-button"
//
// This removes the fallback that scans buttons by visible "ödeme" text.
// The exact id is safer and easier to explain.
//
// Test before moving this into the working C# automation code.

(() => {
    const button = document.querySelector('#iyz-payment-button');
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

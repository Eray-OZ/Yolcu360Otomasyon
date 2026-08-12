// Source copy: JavaScriptCopies/028_BAService.Payment_CompleteIyzicoSandboxPaymentAsync_line54.js
// Purpose: Click the iyzico payment button.
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

    button.scrollIntoView({ block: 'center', inline: 'nearest' });
    button.click();

    return true;
})();
